using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CSSCADTools.Models;

namespace CSSCADTools.Utilities {
    /// <summary>
    /// Dựng 2 sheet mô phỏng layout file mẫu "1112470WD_Yard fitting_material quality quantity_FINAL.xlsm":
    ///
    /// - "Material List": các bản vẽ KHÔNG bắt đầu bằng "1" (bản vẽ chi tiết/vật tư thô như
    ///   4050, 5800...) — mỗi item hiện khối lượng vào đúng cột Grade+Thickness (nếu tự phân
    ///   loại được qua SteelSpecClassifier) VÀ ở cột TOTAL/KG cùng dòng — cả 2 đều là CÔNG THỨC
    ///   tham chiếu TRỰC TIẾP về đúng dòng của item đó trong sheet ExportedData (one true
    ///   source), không copy giá trị tĩnh. Dòng TOTAL/KG cuối sheet dùng SUMIFS/SUMPRODUCT
    ///   tham chiếu ExportedData theo cột helper "MATERIAL COLUMN KEY"/"DRAWING NO".
    ///
    /// - "Assembly List": các bản vẽ bắt đầu bằng "1" (bản vẽ tổng lắp/fitting chính, thường
    ///   KHÔNG tự có Material — mà tham chiếu sang 1 bản vẽ chi tiết khác qua cột Drw). Khối
    ///   lượng theo từng cột Grade+Thickness là CÔNG THỨC SUMIFS tra cứu CHÉO trực tiếp trong
    ///   ExportedData (lọc theo DRAWING NO = bản vẽ được tham chiếu + MATERIAL COLUMN KEY),
    ///   nhân với Qty của chính item đó (ô trong cùng hàng). Mỗi bản vẽ 1* có 1 ô Qty (cột E,
    ///   tô vàng, mặc định 1) đại diện số lượng bản vẽ đó cần dùng trong dự án — người dùng tự
    ///   sửa. Dòng TOTAL/KG DUY NHẤT ở cuối sheet dùng công thức thật, tự tính lại khi sửa Qty.
    ///
    /// Toàn bộ cột Grade+Thickness (G trở đi) dùng CHUNG 1 danh sách cột giữa 2 sheet để cùng
    /// 1 cột luôn mang cùng 1 ý nghĩa (vd cột G luôn là "AH36 / 15.0" ở cả 2 sheet). Việc PHÂN
    /// LOẠI (Size/Material -> cột nào) vẫn do code quyết định (SteelSpecClassifier) — chỉ có
    /// GIÁ TRỊ hiển thị là công thức sống, vì bản thân thuật toán phân loại không thể diễn đạt
    /// gọn bằng công thức Excel thường.
    /// </summary>
    public static class BomTemplateExporter {
        private const int FIXED_COLS = 6; // A=STT/Drawing, B=Name, C=ITEM, D=Size, E=Q'ty, F=Delivery

        public static List<MinimalXlsxWriter.SheetData> Build(BomScanResult scan, ExportedDataLayout layout, AssemblyRollupContext rollup) {
            var byDrawing = GroupByDrawing(scan.Items);
            var knownDrawings = new HashSet<string>(byDrawing.Select(d => d.Drawing), StringComparer.OrdinalIgnoreCase);

            var specForItem = new Dictionary<BomItem, SteelSpecClassifier.Spec>();
            var orderedColumns = CollectColumns(scan.Items, specForItem);
            var columnIndex = BuildColumnIndex(orderedColumns);

            var onePrefixed = byDrawing.Where(d => d.Drawing.StartsWith("1", StringComparison.Ordinal)).ToList();
            var others = byDrawing.Where(d => !d.Drawing.StartsWith("1", StringComparison.Ordinal)).ToList();

            // NVL GỐC trực tiếp (không qua tham chiếu Drw) của MỖI bản vẽ theo từng cột
            // Grade+Thickness — tính cho TOÀN BỘ bản vẽ (kể cả 1*-prefixed, dù hiếm) để làm nền
            // (base case) cho closure đệ quy bên dưới. Bất kỳ bản vẽ nào cũng có thể vừa có NVL
            // gốc vừa tham chiếu bản vẽ khác — không còn phụ thuộc tiền tố "1" như trước.
            var drawingColumnSums = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var section in byDrawing) {
                drawingColumnSums[section.Drawing] = ComputeOwnColumnSums(section, specForItem, columnIndex, orderedColumns.Count);
            }

            // Đồ thị tham chiếu Drw NHIỀU CẤP: cạnh (bản vẽ chứa item -> Drw được tham chiếu,
            // hệ số Qty của chính dòng đó). Dùng để cộng dồn NVL của bản vẽ LÁ (vd 4205) lên
            // bản vẽ tham chiếu qua NHIỀU trung gian (vd 1215 <- 4202 <- 4205) thay vì chỉ dò
            // được đúng 1 cấp như phiên bản trước.
            var referencesOf = BuildReferenceGraph(byDrawing, knownDrawings);

            // Hệ số nhân dồn TỪ MỖI bản vẽ TOP (1*-prefixed) xuống MỌI bản vẽ nó reach được (kể
            // cả chính nó, hệ số 1) — dùng cho sheet Summary để tính TOTAL QTY/WEIGHT toàn dự án
            // (khác với closure ở trên: cái này KHÔNG lọc theo cột Grade+Thickness, vì hệ số
            // "bản vẽ này được build bao nhiêu lần" không phụ thuộc vật liệu bên trong nó).
            foreach (var top in onePrefixed) {
                var reach = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                ExpandReach(top.Drawing, 1.0, reach, new List<string>(), referencesOf, scan.Warnings);
                foreach (var kv in reach) {
                    if (kv.Value == 0) continue;
                    if (!rollup.ContributionsOf.TryGetValue(kv.Key, out var list)) {
                        list = new List<(string TopDrawing, double Multiplier)>();
                        rollup.ContributionsOf[kv.Key] = list;
                    }
                    list.Add((top.Drawing, kv.Value));
                }
            }

            var sheets = new List<MinimalXlsxWriter.SheetData>();

            if (others.Count > 0) {
                sheets.Add(BuildMaterialListSheet(others, orderedColumns, columnIndex, specForItem, knownDrawings, layout));
            }

            if (onePrefixed.Count > 0) {
                sheets.Add(BuildAssemblyListSheet(onePrefixed, orderedColumns, columnIndex, specForItem, drawingColumnSums, referencesOf, knownDrawings, scan.Warnings, rollup, layout));
            }

            // Nhiều nhánh đệ quy (ComputeClosure + ExpandReach) có thể cùng phát hiện 1 vòng lặp
            // tham chiếu và ghi trùng cảnh báo — loại bỏ trùng lặp để sheet Warnings sạch.
            var dedupedWarnings = scan.Warnings.Distinct().ToList();
            scan.Warnings.Clear();
            scan.Warnings.AddRange(dedupedWarnings);

            return sheets;
        }

        /// <summary>
        /// Đệ quy: từ "drawing" với hệ số nhân "multiplier" (khởi đầu 1.0 tại bản vẽ TOP), cộng
        /// dồn vào "result" MỌI bản vẽ reach được (kể cả chính "drawing") — KHÔNG lọc theo cột
        /// Grade+Thickness (khác ComputeClosure ở trên), vì mục đích ở đây là biết "bản vẽ X
        /// được build bao nhiêu lần", áp dụng cho MỌI dòng BOM của X bất kể vật liệu gì. Có
        /// cycle-guard giống ComputeClosure.
        /// </summary>
        private static void ExpandReach(
            string drawing, double multiplier,
            Dictionary<string, double> result,
            List<string> path,
            Dictionary<string, List<(string Target, double Qty)>> referencesOf,
            List<string> warnings) {

            if (path.Contains(drawing, StringComparer.OrdinalIgnoreCase)) {
                warnings.Add($"Phát hiện THAM CHIẾU VÒNG LẶP giữa các bản vẽ: {FormatCyclePath(path, drawing)} — bỏ qua nhánh này khi tính hệ số Qty toàn dự án.");
                return;
            }
            path.Add(drawing);

            result.TryGetValue(drawing, out double existing);
            result[drawing] = existing + multiplier;

            if (referencesOf.TryGetValue(drawing, out var edges)) {
                foreach (var edge in edges) {
                    if (edge.Qty == 0) continue;
                    ExpandReach(edge.Target, multiplier * edge.Qty, result, path, referencesOf, warnings);
                }
            }

            path.RemoveAt(path.Count - 1);
        }

        /// <summary>Định dạng chuỗi bản vẽ gây vòng lặp, vd "4010 -> 4202 -> 4010", để user tự tìm đúng dòng BOM gây lỗi</summary>
        private static string FormatCyclePath(List<string> path, string repeatedDrawing) {
            return string.Join(" -> ", path) + " -> " + repeatedDrawing;
        }

        private static double[] ComputeOwnColumnSums(
            DrawingSection section,
            Dictionary<BomItem, SteelSpecClassifier.Spec> specForItem,
            Dictionary<string, int> columnIndex,
            int dynCount) {

            var sums = new double[dynCount];
            foreach (var item in section.Items) {
                if (specForItem.TryGetValue(item, out var spec)) {
                    int idx = columnIndex[ColumnKey(spec.Material, spec.Thickness)];
                    sums[idx] += ParseDoubleOrDefault(item.Weight, 0);
                }
            }
            return sums;
        }

        private static Dictionary<string, List<(string Target, double Qty)>> BuildReferenceGraph(
            List<DrawingSection> byDrawing, HashSet<string> knownDrawings) {

            var referencesOf = new Dictionary<string, List<(string Target, double Qty)>>(StringComparer.OrdinalIgnoreCase);
            foreach (var section in byDrawing) {
                var edges = new List<(string Target, double Qty)>();
                foreach (var item in section.Items) {
                    string drw = (item.Drw ?? "").Trim();
                    // Bỏ qua item TỰ tham chiếu chính bản vẽ chứa nó (Drw == chính section.Drawing)
                    // — đây là quy ước thường gặp trong BOM table SolidWorks: dòng đại diện cho
                    // CHÍNH assembly đó tự điền số bản vẽ của mình vào cột DRW (không phải ý
                    // nghĩa "được lắp từ 1 sub-assembly khác"). Coi đây là cạnh không mang ý
                    // nghĩa tham chiếu thật — nếu không loại, sẽ luôn tạo vòng lặp giả (bản vẽ ->
                    // chính nó) ở MỌI nơi bản vẽ này được dùng, dù dữ liệu hoàn toàn hợp lệ.
                    if (drw.Length > 0 && !drw.Equals(section.Drawing, StringComparison.OrdinalIgnoreCase) && knownDrawings.Contains(drw)) {
                        edges.Add((drw, ParseDoubleOrDefault(item.Qty, 0)));
                    }
                }
                referencesOf[section.Drawing] = edges;
            }
            return referencesOf;
        }

        /// <summary>
        /// Đệ quy có nhớ (memoized): trả về, cho MỖI cột Grade+Thickness, danh sách các bản vẽ
        /// LÁ mang NVL gốc kèm hệ số nhân dồn (tích các Qty dọc đường tham chiếu từ "drawing"
        /// tới bản vẽ lá đó) — cho phép cộng dồn NVL qua NHIỀU CẤP tham chiếu Drw (không chỉ 1
        /// cấp). Có cycle-guard theo nhánh đệ quy (visiting): nếu phát hiện vòng lặp tham chiếu
        /// (lỗi dữ liệu CAD), dừng nhánh đó và ghi cảnh báo thay vì treo chương trình.
        /// </summary>
        private static Dictionary<string, Dictionary<string, double>> ComputeClosure(
            string drawing,
            Dictionary<string, double[]> drawingColumnSums,
            Dictionary<string, List<(string Target, double Qty)>> referencesOf,
            List<SteelSpecClassifier.Spec> orderedColumns,
            Dictionary<string, Dictionary<string, Dictionary<string, double>>> cache,
            List<string> path,
            List<string> warnings) {

            if (cache.TryGetValue(drawing, out var cached)) return cached;

            if (path.Contains(drawing, StringComparer.OrdinalIgnoreCase)) {
                warnings.Add($"Phát hiện THAM CHIẾU VÒNG LẶP giữa các bản vẽ: {FormatCyclePath(path, drawing)} — bỏ qua nhánh này để tránh tính sai/treo chương trình.");
                return new Dictionary<string, Dictionary<string, double>>();
            }
            path.Add(drawing);

            var result = new Dictionary<string, Dictionary<string, double>>();

            // (a) NVL gốc trực tiếp của chính bản vẽ này (base case của đệ quy)
            if (drawingColumnSums.TryGetValue(drawing, out var own)) {
                for (int i = 0; i < own.Length && i < orderedColumns.Count; i++) {
                    if (own[i] == 0) continue;
                    string key = ColumnKey(orderedColumns[i].Material, orderedColumns[i].Thickness);
                    AddLeaf(result, key, drawing, 1.0);
                }
            }

            // (b) đệ quy qua các bản vẽ được tham chiếu, nhân dồn theo Qty của từng cấp
            if (referencesOf.TryGetValue(drawing, out var edges)) {
                foreach (var edge in edges) {
                    if (edge.Qty == 0) continue;
                    var subClosure = ComputeClosure(edge.Target, drawingColumnSums, referencesOf, orderedColumns, cache, path, warnings);
                    foreach (var kv in subClosure) {
                        foreach (var leaf in kv.Value) {
                            AddLeaf(result, kv.Key, leaf.Key, leaf.Value * edge.Qty);
                        }
                    }
                }
            }

            path.RemoveAt(path.Count - 1);
            cache[drawing] = result;
            return result;
        }

        private static void AddLeaf(Dictionary<string, Dictionary<string, double>> result, string columnKey, string leafDrawing, double multiplier) {
            if (!result.TryGetValue(columnKey, out var leaves)) {
                leaves = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                result[columnKey] = leaves;
            }
            leaves.TryGetValue(leafDrawing, out double existing);
            leaves[leafDrawing] = existing + multiplier;
        }

        private static double GetOwnColumnValue(Dictionary<string, double[]> drawingColumnSums, string drawing, int columnIdx) {
            if (drawingColumnSums.TryGetValue(drawing, out var sums) && columnIdx < sums.Length) return sums[columnIdx];
            return 0;
        }

        // ================== SHEET: Material List (bản vẽ KHÔNG bắt đầu bằng "1") ==================
        private static MinimalXlsxWriter.SheetData BuildMaterialListSheet(
            List<DrawingSection> sections,
            List<SteelSpecClassifier.Spec> orderedColumns,
            Dictionary<string, int> columnIndex,
            Dictionary<BomItem, SteelSpecClassifier.Spec> specForItem,
            HashSet<string> knownDrawings,
            ExportedDataLayout layout) {

            int dynCount = orderedColumns.Count;
            int totalColIdx = FIXED_COLS + dynCount + 1;

            var rows = new List<MinimalXlsxWriter.GridRow>();
            void AddRow(Dictionary<int, string> cells, bool bold = false, bool highlight = false, bool headerStyle = false) {
                rows.Add(new MinimalXlsxWriter.GridRow { Cells = cells, Bold = bold, Highlight = highlight, HeaderStyle = headerStyle });
            }
            void AddBlankRow() => AddRow(new Dictionary<int, string>());

            AddTitleBlock(AddRow, AddBlankRow);
            int headerRowExcel = rows.Count + 1;
            AddHeaderRows(AddRow, AddBlankRow, orderedColumns, totalColIdx);

            var sumPerColumn = new double[dynCount];
            double grandTotal = 0;

            foreach (var section in sections.OrderBy(d => d.Drawing, StringComparer.OrdinalIgnoreCase)) {
                AddRow(new Dictionary<int, string> { { 1, section.Drawing }, { totalColIdx, "1 PANEL" } }, true);

                int seq = 1;
                foreach (var item in section.Items.OrderBy(i => ParseIntOrDefault(i.Item, int.MaxValue))) {
                    double weight = ParseDoubleOrDefault(item.Weight, 0);

                    var cells = new Dictionary<int, string> {
                        { 1, seq.ToString(CultureInfo.InvariantCulture) },
                        { 2, item.Description },
                        { 3, item.Drw },
                        { 4, item.Size },
                        { 5, item.Qty },
                        { 6, item.Delivery }
                    };

                    var itemRow = new MinimalXlsxWriter.GridRow { Cells = cells };

                    // Weight (cả ở cột phân loại lẫn cột TOTAL/KG cùng dòng) là công thức tham
                    // chiếu TRỰC TIẾP về đúng dòng của item này trong ExportedData — one true source.
                    if (layout.RowOf.TryGetValue(item, out int srcRow)) {
                        string weightRef = layout.Ref(ExportedDataLayout.ColWeight, srcRow);
                        itemRow.Formulas[totalColIdx] = (weightRef, weight);

                        bool classified = specForItem.TryGetValue(item, out var spec);
                        if (classified) {
                            int colIdx = columnIndex[ColumnKey(spec.Material, spec.Thickness)];
                            itemRow.Formulas[FIXED_COLS + 1 + colIdx] = (weightRef, weight);
                            sumPerColumn[colIdx] += weight;
                        }

                        bool hasOwnDrawing = !string.IsNullOrWhiteSpace(item.Drw) && knownDrawings.Contains(item.Drw.Trim());
                        itemRow.Highlight = !hasOwnDrawing && !classified;
                    }

                    grandTotal += weight;
                    rows.Add(itemRow);
                    seq++;
                }

                AddBlankRow();
            }

            // Dòng tổng cuối sheet — CÔNG THỨC sống tham chiếu ExportedData theo cột helper
            // "MATERIAL COLUMN KEY" (từng cột Grade+Thickness) và điều kiện "không phải bản vẽ 1*"
            // (SUMPRODUCT theo DRAWING NO) cho tổng toàn bộ — không phụ thuộc số sumPerColumn/
            // grandTotal tính sẵn (chỉ dùng làm giá trị cache).
            string weightRange = layout.Range(ExportedDataLayout.ColWeight);
            string materialKeyRange = layout.Range(ExportedDataLayout.ColMaterialKey);
            string drawingNoRange = layout.Range(ExportedDataLayout.ColDrawingNo);

            var totalRow = new MinimalXlsxWriter.GridRow { Bold = true };
            totalRow.Cells[2] = "TOTAL/KG";
            for (int i = 0; i < dynCount; i++) {
                if (sumPerColumn[i] == 0) continue;
                string key = EscapeFormulaText(ColumnKey(orderedColumns[i].Material, orderedColumns[i].Thickness));
                totalRow.Formulas[FIXED_COLS + 1 + i] = ($"SUMIFS({weightRange},{materialKeyRange},\"{key}\")", sumPerColumn[i]);
            }
            totalRow.Formulas[totalColIdx] = ($"SUMPRODUCT((LEFT({drawingNoRange},1)<>\"1\")*{weightRange})", grandTotal);
            rows.Add(totalRow);

            return new MinimalXlsxWriter.SheetData {
                Name = "Material List",
                GridRows = rows,
                ColumnWidths = BuildColumnWidths(dynCount),
                FullBorderStartRow = headerRowExcel,
                FullBorderColumnCount = totalColIdx
            };
        }

        // ================== SHEET: Assembly List (bản vẽ bắt đầu bằng "1") ==================
        private static MinimalXlsxWriter.SheetData BuildAssemblyListSheet(
            List<DrawingSection> sections,
            List<SteelSpecClassifier.Spec> orderedColumns,
            Dictionary<string, int> columnIndex,
            Dictionary<BomItem, SteelSpecClassifier.Spec> specForItem,
            Dictionary<string, double[]> drawingColumnSums,
            Dictionary<string, List<(string Target, double Qty)>> referencesOf,
            HashSet<string> knownDrawings,
            List<string> warnings,
            AssemblyRollupContext rollup,
            ExportedDataLayout layout) {

            int dynCount = orderedColumns.Count;
            // MATERIAL (N/A): cột "vật liệu chưa phân loại" BỔ SUNG, coi như 1 cột vật liệu
            // giống các cột Grade+Thickness khác (tham gia CÙNG cơ chế SUM/tổng cột) — chứa
            // KHỐI LƯỢNG SỐ khi đã biết weight riêng của item (dù không biết đúng Grade/Thickness
            // nào), CHỈ ghi chữ "N/A" khi hoàn toàn không biết cả weight lẫn material (cần user
            // tự điền tay). Vì vậy dùng chung mảng termsPerColumn/cachedPerColumn với các cột
            // Grade+Thickness, chỉ số [dynCount] là slot của cột N/A này. ĐÂY LÀ CỘT CUỐI CÙNG —
            // Assembly List đã bỏ cột TOTAL/KG riêng (theo yêu cầu user), mỗi cột vật liệu tự
            // đứng độc lập, không cộng gộp lại thành 1 số duy nhất (khác Material List).
            int naColIdx = FIXED_COLS + dynCount + 1;

            var rows = new List<MinimalXlsxWriter.GridRow>();
            void AddRow(Dictionary<int, string> cells, bool bold = false, bool highlight = false, bool headerStyle = false) {
                rows.Add(new MinimalXlsxWriter.GridRow { Cells = cells, Bold = bold, Highlight = highlight, HeaderStyle = headerStyle });
            }
            void AddBlankRow() => AddRow(new Dictionary<int, string>());

            AddTitleBlock(AddRow, AddBlankRow);
            int columnHeaderRowExcel = rows.Count + 1;
            AddHeaderRows(AddRow, AddBlankRow, orderedColumns, totalColIdx: null, naColIdx: naColIdx);

            // Không tổng theo từng bản vẽ (section) — chỉ gom số hạng công thức của MỖI section
            // lại, để cộng thành 1 dòng TOTAL/KG DUY NHẤT cho toàn bộ vật tư ở cuối sheet
            // (giống đúng file mẫu .xlsm — chỉ có 1 dòng TOTAL/KG ở cuối, không có tổng phụ
            // theo từng bản vẽ). Kích thước dynCount+1 để gồm cả cột N/A (slot cuối, index dynCount).
            var termsPerColumn = new List<string>[dynCount + 1];
            var cachedPerColumn = new double[dynCount + 1];
            for (int i = 0; i <= dynCount; i++) termsPerColumn[i] = new List<string>();

            string weightRange = layout.Range(ExportedDataLayout.ColWeight);
            string materialKeyRange = layout.Range(ExportedDataLayout.ColMaterialKey);
            string drawingNoRange = layout.Range(ExportedDataLayout.ColDrawingNo);

            // Nhớ kết quả closure theo từng bản vẽ để không tính lại khi nhiều dòng khác nhau
            // (ở nhiều bản vẽ 1* khác nhau) cùng tham chiếu 1 bản vẽ trung gian (vd nhiều mục
            // cùng tham chiếu "4202").
            var closureCache = new Dictionary<string, Dictionary<string, Dictionary<string, double>>>(StringComparer.OrdinalIgnoreCase);

            foreach (var section in sections.OrderBy(d => d.Drawing, StringComparer.OrdinalIgnoreCase)) {
                // Hàng đầu section: Drawing No (cột A) + Qty=1 tô vàng RIÊNG ô (cột E) —
                // đại diện số lượng bản vẽ này cần dùng, người dùng tự điền số thực tế.
                int headerRowExcel = rows.Count + 1;
                var headerRow = new MinimalXlsxWriter.GridRow {
                    Bold = true,
                    HighlightedCols = new HashSet<int> { 5 }
                };
                headerRow.Cells[1] = section.Drawing;
                headerRow.NumberCells[5] = 1;
                rows.Add(headerRow);

                // Ghi lại Excel row của ô Qty header này để sheet Summary tham chiếu CHÉO SHEET
                // tới đây khi tính TOTAL QTY/WEIGHT toàn dự án.
                rollup.AssemblyHeaderRowOf[section.Drawing] = headerRowExcel;

                // Ô Qty của CHÍNH bản vẽ này (vd "cần build 2 cái bản vẽ 1235") — phải nhân vào
                // MỌI công thức tra cứu chéo của các item bên trong section, nếu không sửa ô
                // này sẽ không có tác dụng gì (dòng TOTAL/KG không đổi dù user đổi Qty ở đây).
                string sectionQtyRef = $"$E${headerRowExcel}";

                int firstItemRowExcel = rows.Count + 1;

                int seq = 1;
                foreach (var item in section.Items.OrderBy(i => ParseIntOrDefault(i.Item, int.MaxValue))) {
                    int itemRowExcel = rows.Count + 1;

                    var itemRow = new MinimalXlsxWriter.GridRow();
                    itemRow.Cells[1] = seq.ToString(CultureInfo.InvariantCulture);
                    itemRow.Cells[2] = item.Description;
                    itemRow.Cells[3] = item.Drw;
                    itemRow.Cells[4] = item.Size;
                    itemRow.Cells[6] = item.Delivery;

                    double itemQty = ParseDoubleOrDefault(item.Qty, 0);
                    // Qty của chính item này PHẢI là số thực (không phải text) để công thức
                    // tra cứu chéo bên dưới nhân được trực tiếp với ô này.
                    itemRow.NumberCells[5] = itemQty;

                    // Tra cứu chéo NHIỀU CẤP: bản vẽ được Drw tham chiếu có thể CHÍNH NÓ tham
                    // chiếu tiếp bản vẽ khác (vd 1215 -> 4202 -> 4205 -> ...) — dùng closure đệ
                    // quy để gom NVL của MỌI bản vẽ LÁ trong toàn bộ chuỗi tham chiếu, mỗi lá 1
                    // số hạng SUMIFS (nhân hệ số dồn nếu ≠ 1), cộng lại rồi nhân với Qty của
                    // chính item này (cột E cùng hàng) VÀ Qty của CHÍNH bản vẽ section (ô đầu
                    // section, sectionQtyRef) — cả 2 đều LIVE, người dùng sửa được.
                    bool crossReferenced = false;
                    string targetDrawing = (item.Drw ?? "").Trim();
                    // Item có thể TỰ phân loại được Material/Size của CHÍNH dòng này (vd NAME
                    // PLATE: Drw="K101791" chỉ là MÃ CATALOG, không phải số hiệu bản vẽ thật —
                    // SteelSpecClassifier đã nhận diện qua tín hiệu cấu trúc "Drw không bắt đầu
                    // bằng chữ số"). Cần biết điều này TRƯỚC khi quyết định có cảnh báo "thiếu
                    // bản vẽ" hay không — item tự phân loại được thì không thật sự "thiếu dữ liệu".
                    bool classified = specForItem.TryGetValue(item, out var spec);

                    if (targetDrawing.Length > 0) {
                        if (knownDrawings.Contains(targetDrawing)) {
                            var closure = ComputeClosure(targetDrawing, drawingColumnSums, referencesOf, orderedColumns, closureCache, new List<string>(), warnings);
                            string qtyRef = $"E{itemRowExcel}";

                            for (int i = 0; i < dynCount; i++) {
                                string key = ColumnKey(orderedColumns[i].Material, orderedColumns[i].Thickness);
                                if (!closure.TryGetValue(key, out var leaves) || leaves.Count == 0) continue;

                                var terms = new List<string>();
                                double cachedSum = 0;

                                foreach (var leaf in leaves.OrderBy(l => l.Key, StringComparer.OrdinalIgnoreCase)) {
                                    string leafEsc = EscapeFormulaText(leaf.Key);
                                    string keyEsc = EscapeFormulaText(key);
                                    string term = $"SUMIFS({weightRange},{drawingNoRange},\"{leafEsc}\",{materialKeyRange},\"{keyEsc}\")";
                                    if (Math.Abs(leaf.Value - 1.0) > 1e-9) {
                                        term += $"*{leaf.Value.ToString("0.####", CultureInfo.InvariantCulture)}";
                                    }
                                    terms.Add(term);
                                    cachedSum += GetOwnColumnValue(drawingColumnSums, leaf.Key, i) * leaf.Value;
                                }

                                // sectionQtyRef (Qty của bản vẽ section, mặc định 1 lúc export) —
                                // trước đây ô này KHÔNG được công thức nào tham chiếu, nên user
                                // đổi Qty=1 -> 2 mà TOTAL không đổi. Nay nhân trực tiếp vào đây.
                                string formula = $"({string.Join("+", terms)})*{qtyRef}*{sectionQtyRef}";
                                itemRow.Formulas[FIXED_COLS + 1 + i] = (formula, cachedSum * itemQty * 1.0);
                                crossReferenced = true;
                            }
                        } else if (!classified) {
                            warnings.Add($"Mục '{item.Description}' (bản vẽ {section.Drawing}) tham chiếu bản vẽ '{targetDrawing}' nhưng bản vẽ này không có trong danh sách file đã quét — không tính được khối lượng tra cứu chéo.");
                        }
                    }

                    if (!crossReferenced) {
                        if (classified) {
                            // Tự phân loại được — ghi thẳng vào ĐÚNG cột Grade+Thickness (tham
                            // chiếu trực tiếp ExportedData × hệ số Qty bản vẽ section, giống hệt
                            // cơ chế Material List), KHÔNG qua cột N/A.
                            if (layout.RowOf.TryGetValue(item, out int srcRowClassified)) {
                                double weight = ParseDoubleOrDefault(item.Weight, 0);
                                string weightRef = layout.Ref(ExportedDataLayout.ColWeight, srcRowClassified);
                                int colIdx = columnIndex[ColumnKey(spec.Material, spec.Thickness)];
                                itemRow.Formulas[FIXED_COLS + 1 + colIdx] = ($"{weightRef}*{sectionQtyRef}", weight);
                            }
                        } else {
                            // KHÔNG tra cứu chéo được, KHÔNG tự phân loại được — cột N/A đóng vai
                            // trò "cột vật liệu chưa rõ": nếu ĐÃ biết khối lượng riêng của dòng
                            // này (item.Weight khác 0, vd VENTILATOR=80.6) thì ghi CÔNG THỨC số
                            // vào đây (tham chiếu trực tiếp ExportedData × hệ số Qty bản vẽ
                            // section) — KHÔNG ghi chữ "N/A" nữa, vì khối lượng vẫn cần được TÍNH
                            // VÀO tổng. Chỉ ghi chữ "N/A" (text, không tính vào SUM) khi hoàn
                            // toàn không biết khối lượng, để user tự điền tay.
                            double ownWeight = layout.RowOf.TryGetValue(item, out int srcRow)
                                ? ParseDoubleOrDefault(item.Weight, 0)
                                : 0;

                            if (ownWeight != 0) {
                                string weightRef = layout.Ref(ExportedDataLayout.ColWeight, srcRow);
                                itemRow.Formulas[naColIdx] = ($"{weightRef}*{sectionQtyRef}", ownWeight);
                            } else {
                                itemRow.Cells[naColIdx] = "N/A";
                            }
                        }
                    }

                    rows.Add(itemRow);
                    seq++;
                }

                int lastItemRowExcel = rows.Count;

                // KHÔNG thêm dòng tổng ở đây — chỉ gom số hạng "SUM(range)" của section này vào
                // danh sách số hạng chung của từng cột (kể cả cột N/A, slot cuối), để cộng thành
                // 1 dòng TOTAL/KG DUY NHẤT ở cuối toàn bộ sheet. Không cần nhân sectionQtyRef ở
                // đây nữa — mỗi ô item bên trong SUM(...) đã tự nhân sectionQtyRef rồi.
                if (lastItemRowExcel >= firstItemRowExcel) {
                    for (int i = 0; i <= dynCount; i++) {
                        int col = FIXED_COLS + 1 + i;
                        double colSumUnscaled = 0;
                        bool used = false;

                        for (int r = firstItemRowExcel; r <= lastItemRowExcel; r++) {
                            if (rows[r - 1].Formulas.TryGetValue(col, out var f)) { used = true; colSumUnscaled += f.CachedValue; }
                        }

                        if (!used) continue;

                        string colLetter = ColumnLetter(col);
                        termsPerColumn[i].Add($"SUM({colLetter}{firstItemRowExcel}:{colLetter}{lastItemRowExcel})");
                        cachedPerColumn[i] += colSumUnscaled;
                    }
                }

                AddBlankRow();
            }

            // Dòng TỔNG DUY NHẤT cho toàn bộ vật tư — mỗi cột vật liệu (kể cả N/A) TỰ đứng độc
            // lập, cộng dồn số hạng của từng bản vẽ (section) đã gom ở trên. KHÔNG còn gộp lại
            // thành 1 con số TOTAL/KG duy nhất (đã bỏ theo yêu cầu — cộng các cột KHÁC LOẠI vật
            // liệu lại với nhau không có ý nghĩa thực tế cho việc đặt mua). Tô nổi bật bằng
            // FooterStyle để phân biệt rõ đây là dòng tổng cuối sheet.
            var grandTotalRow = new MinimalXlsxWriter.GridRow { FooterStyle = true };
            grandTotalRow.Cells[2] = "TOTAL/KG";

            for (int i = 0; i <= dynCount; i++) {
                if (termsPerColumn[i].Count == 0) continue;

                int col = FIXED_COLS + 1 + i;
                string formula = string.Join("+", termsPerColumn[i]);
                grandTotalRow.Formulas[col] = (formula, cachedPerColumn[i]);
            }

            rows.Add(grandTotalRow);

            return new MinimalXlsxWriter.SheetData {
                Name = "Assembly List",
                GridRows = rows,
                ColumnWidths = BuildColumnWidths(dynCount, includeNaColumn: true, includeTotalColumn: false),
                FullBorderStartRow = columnHeaderRowExcel,
                FullBorderColumnCount = naColIdx
            };
        }

        // ================== Helper dùng chung cho cả 2 sheet ==================

        private static void AddTitleBlock(Action<Dictionary<int, string>, bool, bool, bool> addRow, Action addBlankRow) {
            addRow(new Dictionary<int, string> { { 1, "MacGregor" } }, true, false, false);
            addBlankRow();
            addBlankRow();
            addRow(new Dictionary<int, string> { { 1, "YARD/SHIP:" } }, true, false, false);
            addRow(new Dictionary<int, string> { { 1, "CLASS:" } }, true, false, false);
            addRow(new Dictionary<int, string> { { 1, "DATE:" } }, true, false, false);
            addRow(new Dictionary<int, string> { { 1, "SIGN:" } }, true, false, false);
            addRow(new Dictionary<int, string> { { 1, "EDITION" } }, true, false, false);
            addBlankRow();
        }

        private static void AddHeaderRows(
            Action<Dictionary<int, string>, bool, bool, bool> addRow,
            Action addBlankRow,
            List<SteelSpecClassifier.Spec> orderedColumns,
            int? totalColIdx,
            int? naColIdx = null) {

            int dynCount = orderedColumns.Count;

            var header1 = new Dictionary<int, string> {
                { 1, "Drawing" }, { 2, "Name" }, { 3, "ITEM" }, { 4, "Size" }, { 5, "Q'ty" }, { 6, "Delivery" }
            };
            for (int i = 0; i < dynCount; i++) header1[FIXED_COLS + 1 + i] = orderedColumns[i].Material;
            if (naColIdx.HasValue) header1[naColIdx.Value] = "MATERIAL (N/A)";
            // TOTAL/KG là cột tùy chọn — Assembly List đã bỏ cột này (user yêu cầu), Material
            // List vẫn giữ nguyên.
            if (totalColIdx.HasValue) header1[totalColIdx.Value] = "TOTAL/KG";
            // Hàng tiêu đề cột thật sự — dùng HeaderStyle (nền xanh đậm, chữ trắng, căn giữa)
            // thay vì chỉ Bold, để phân biệt trực quan với title block/dòng TOTAL cũng Bold.
            addRow(header1, false, false, true);

            if (dynCount > 0) {
                var header2 = new Dictionary<int, string>();
                for (int i = 0; i < dynCount; i++) header2[FIXED_COLS + 1 + i] = orderedColumns[i].Thickness;
                addRow(header2, false, false, true);
            }

            addBlankRow();
        }

        private class DrawingSection {
            public string Drawing;
            public List<BomItem> Items = new List<BomItem>();
        }

        private static List<DrawingSection> GroupByDrawing(List<BomItem> items) {
            var sections = new List<DrawingSection>();
            var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in items) {
                string drawing = BomIdentity.ExtractDrawingNumber(item.SourceFile);
                if (!index.TryGetValue(drawing, out int idx)) {
                    idx = sections.Count;
                    index[drawing] = idx;
                    sections.Add(new DrawingSection { Drawing = drawing });
                }
                sections[idx].Items.Add(item);
            }

            return sections;
        }

        private static List<SteelSpecClassifier.Spec> CollectColumns(List<BomItem> items, Dictionary<BomItem, SteelSpecClassifier.Spec> specForItem) {
            var seen = new HashSet<string>();
            var columns = new List<SteelSpecClassifier.Spec>();

            foreach (var item in items) {
                var spec = SteelSpecClassifier.Classify(item);
                if (spec == null) continue;

                specForItem[item] = spec;
                string key = ColumnKey(spec.Material, spec.Thickness);
                if (seen.Add(key)) columns.Add(spec);
            }

            return columns
                .OrderBy(c => c.Material, StringComparer.OrdinalIgnoreCase)
                .ThenBy(c => ParseDoubleOrDefault(c.Thickness, 0))
                .ToList();
        }

        private static Dictionary<string, int> BuildColumnIndex(List<SteelSpecClassifier.Spec> orderedColumns) {
            var columnIndex = new Dictionary<string, int>();
            for (int i = 0; i < orderedColumns.Count; i++) {
                columnIndex[ColumnKey(orderedColumns[i].Material, orderedColumns[i].Thickness)] = i;
            }
            return columnIndex;
        }

        private static double[] BuildColumnWidths(int dynCount, bool includeNaColumn = false, bool includeTotalColumn = true) {
            var widths = new List<double> { 10, 32, 10, 22, 8, 10 };
            for (int i = 0; i < dynCount; i++) widths.Add(11);
            if (includeNaColumn) widths.Add(14);
            if (includeTotalColumn) widths.Add(12);
            return widths.ToArray();
        }

        private static string ColumnKey(string material, string thickness) => $"{material}|{thickness}";

        private static string EscapeFormulaText(string s) => (s ?? "").Replace("\"", "\"\"");

        private static int ParseIntOrDefault(string s, int fallback) {
            return int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out int v) ? v : fallback;
        }

        private static double ParseDoubleOrDefault(string s, double fallback) {
            return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : fallback;
        }

        /// <summary>Chuyển số cột (1-based) sang chữ cột Excel (A, B, ..., Z, AA...) — dùng để dựng công thức</summary>
        private static string ColumnLetter(int col) {
            string letters = "";
            while (col > 0) {
                int rem = (col - 1) % 26;
                letters = (char)('A' + rem) + letters;
                col = (col - 1) / 26;
            }
            return letters;
        }
    }
}
