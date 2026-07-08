using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CSSCADTools.Models;

namespace CSSCADTools.Utilities {
    /// <summary>
    /// Dựng sheet "Summary" — bản gộp (aggregate) của sheet ExportedData: thay vì mỗi dòng là
    /// 1 lần xuất hiện của item trong 1 bản vẽ, mỗi dòng là 1 ITEM DUY NHẤT.
    ///
    /// "Cùng 1 item" được xác định bằng tổ hợp Description + Size + Material + Type
    /// (không phân biệt hoa/thường, bỏ khoảng trắng thừa) — vì không có mã định danh
    /// duy nhất xuyên suốt các bản vẽ (Drw/Code chỉ là mã tham chiếu, thường trống).
    /// Nhãn nhóm (Description/Type/Size/Material/Delivery/USED IN) do code xác định và ghi
    /// tĩnh, nhưng TOTAL QTY / TOTAL WEIGHT / OCCURRENCES là CÔNG THỨC Excel thật (SUMIFS/
    /// COUNTIFS — hàm cổ điển từ Excel 2007, an toàn) tham chiếu sang ExportedData — "one
    /// true source": sửa 1 dòng trong ExportedData thì các con số này tự cập nhật.
    ///
    /// TOTAL QTY / TOTAL WEIGHT còn nhân thêm HỆ SỐ Qty của sheet Assembly List: mỗi bản vẽ
    /// (kể cả bản vẽ trung gian nhiều cấp) có thể được 1 hay nhiều bản vẽ TOP (1*-prefixed)
    /// "cần build N lần" — hệ số này tham chiếu CHÉO SHEET tới đúng ô Qty header trong Assembly
    /// List (xem AssemblyRollupContext), nên user sửa Qty ở Assembly List thì Summary tự cập
    /// nhật theo, không chỉ tính theo 1 lần build của mỗi bản vẽ gốc như trước. Bản vẽ KHÔNG
    /// được bản vẽ TOP nào tham chiếu tới (đứng độc lập) giữ nguyên hệ số = 1 (không đổi).
    ///
    /// USED IN CỐ TÌNH giữ tĩnh (không dùng TEXTJOIN/UNIQUE): các hàm này khi Excel lưu vào
    /// OOXML cần tiền tố đặc biệt (_xlfn., với hàm mảng động như UNIQUE là _xlfn._xlws.) —
    /// viết tay thiếu đúng tiền tố sẽ ra lỗi #NAME? mà không cách nào kiểm chứng được nếu
    /// không có Excel thật để test trực tiếp, nên chọn phương án an toàn hơn.
    ///
    /// OCCURRENCES CỐ TÌNH KHÔNG nhân hệ số này — đây là con số CẤU TRÚC (item xuất hiện
    /// trong bao nhiêu dòng BOM đã quét), không liên quan tới việc build assembly bao nhiêu lần.
    /// </summary>
    public static class BomSummaryExporter {
        private static readonly string[] Headers = {
            "DESCRIPTION", "TYPE", "SIZE", "MATERIAL", "DELIVERY", "TOTAL QTY", "TOTAL WEIGHT", "USED IN", "OCCURRENCES"
        };

        private static readonly double[] ColumnWidths = { 35, 20, 22, 15, 10, 12, 12, 30, 12 };

        private class Group {
            public string Description;
            public string Type;
            public string Size;
            public string Material;
            public Dictionary<string, (double Qty, double Weight)> PerDrawing = new Dictionary<string, (double Qty, double Weight)>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> DeliveryValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public int Occurrences;
        }

        public static MinimalXlsxWriter.SheetData Build(BomScanResult scan, ExportedDataLayout layout, AssemblyRollupContext rollup) {
            var groups = new Dictionary<string, Group>();

            foreach (var item in scan.Items) {
                string key = GroupKey(item);

                if (!groups.TryGetValue(key, out var group)) {
                    group = new Group {
                        Description = item.Description,
                        Type = item.Type,
                        Size = item.Size,
                        Material = item.Material
                    };
                    groups[key] = group;
                }

                string drawing = BomIdentity.ExtractDrawingNumber(item.SourceFile);
                group.PerDrawing.TryGetValue(drawing, out var acc);
                acc.Qty += ParseDoubleOrDefault(item.Qty, 0);
                acc.Weight += ParseDoubleOrDefault(item.Weight, 0);
                group.PerDrawing[drawing] = acc;

                group.Occurrences++;

                if (!string.IsNullOrWhiteSpace(item.Delivery)) group.DeliveryValues.Add(item.Delivery.Trim());
            }

            var rows = new List<MinimalXlsxWriter.GridRow>();

            var header = new MinimalXlsxWriter.GridRow { HeaderStyle = true };
            for (int i = 0; i < Headers.Length; i++) header.Cells[i + 1] = Headers[i];
            rows.Add(header);

            string descRange = layout.Range(ExportedDataLayout.ColDescription);
            string sizeRange = layout.Range(ExportedDataLayout.ColSize);
            string materialRange = layout.Range(ExportedDataLayout.ColMaterial);
            string typeRange = layout.Range(ExportedDataLayout.ColType);
            string qtyRange = layout.Range(ExportedDataLayout.ColQty);
            string weightRange = layout.Range(ExportedDataLayout.ColWeight);
            string drawingNoRange = layout.Range(ExportedDataLayout.ColDrawingNo);

            foreach (var group in groups.Values
                .OrderBy(g => g.Description, StringComparer.OrdinalIgnoreCase)
                .ThenBy(g => g.Material, StringComparer.OrdinalIgnoreCase)
                .ThenBy(g => g.Size, StringComparer.OrdinalIgnoreCase)) {

                string delivery = group.DeliveryValues.Count == 0 ? ""
                    : group.DeliveryValues.Count == 1 ? group.DeliveryValues.First()
                    : "Mixed";

                string descCrit = EscapeFormulaText(group.Description);
                string sizeCrit = EscapeFormulaText(group.Size);
                string materialCrit = EscapeFormulaText(group.Material);
                string typeCrit = EscapeFormulaText(group.Type);

                string baseCriteria = $"{descRange},\"{descCrit}\",{sizeRange},\"{sizeCrit}\",{materialRange},\"{materialCrit}\",{typeRange},\"{typeCrit}\"";

                var row = new MinimalXlsxWriter.GridRow();
                row.Cells[1] = group.Description;
                row.Cells[2] = group.Type;
                row.Cells[3] = group.Size;
                row.Cells[4] = group.Material;
                row.Cells[5] = delivery;

                // TOTAL QTY/WEIGHT toàn dự án: cộng theo TỪNG bản vẽ item này xuất hiện, mỗi
                // bản vẽ 1 số hạng SUMIFS (lọc thêm theo DRAWING NO) nhân với hệ số Qty của
                // (các) bản vẽ TOP tham chiếu tới nó — hệ số này tham chiếu CHÉO SHEET tới ô
                // Qty của Assembly List nên LIVE, sửa Qty ở đó thì 2 cột này tự cập nhật.
                var qtyTerms = new List<string>();
                var weightTerms = new List<string>();
                double cachedQty = 0, cachedWeight = 0;

                foreach (var kv in group.PerDrawing.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)) {
                    string drawing = kv.Key;
                    string drawingCrit = $"{baseCriteria},{drawingNoRange},\"{EscapeFormulaText(drawing)}\"";
                    var (multFormula, multCached) = BuildMultiplierFormula(drawing, rollup);

                    string qtyBase = $"SUMIFS({qtyRange},{drawingCrit})";
                    string weightBase = $"SUMIFS({weightRange},{drawingCrit})";

                    qtyTerms.Add(multFormula == "1" ? qtyBase : $"{qtyBase}*{multFormula}");
                    weightTerms.Add(multFormula == "1" ? weightBase : $"{weightBase}*{multFormula}");

                    cachedQty += kv.Value.Qty * multCached;
                    cachedWeight += kv.Value.Weight * multCached;
                }

                row.Formulas[6] = (string.Join("+", qtyTerms), cachedQty);
                row.Formulas[7] = (string.Join("+", weightTerms), cachedWeight);

                row.Cells[8] = string.Join(", ", group.PerDrawing.Keys.OrderBy(d => d, StringComparer.OrdinalIgnoreCase));

                // OCCURRENCES CỐ TÌNH không nhân hệ số Assembly Qty — xem doc-comment đầu file.
                row.Formulas[9] = ($"COUNTIFS({baseCriteria})", group.Occurrences);

                rows.Add(row);
            }

            return new MinimalXlsxWriter.SheetData {
                Name = "Summary",
                GridRows = rows,
                ColumnWidths = ColumnWidths,
                FreezeHeaderRow = true,
                AutoFilter = true,
                FullBorderStartRow = 1,
                FullBorderColumnCount = Headers.Length
            };
        }

        /// <summary>
        /// Dựng công thức hệ số Qty toàn dự án cho 1 bản vẽ: nếu KHÔNG có bản vẽ TOP nào (1*-
        /// prefixed) tham chiếu tới nó (đứng độc lập) -> hệ số = hằng số "1" (không đổi so với
        /// trước). Nếu có 1 hay nhiều bản vẽ TOP tham chiếu tới (kể cả chính nó, nếu nó LÀ bản
        /// vẽ TOP) -> nối các ô Qty header tương ứng trong Assembly List (tham chiếu CHÉO SHEET,
        /// LIVE) bằng dấu "+", mỗi ô nhân thêm hệ số nhân dồn nếu khác 1.
        /// </summary>
        private static (string Formula, double Cached) BuildMultiplierFormula(string drawing, AssemblyRollupContext rollup) {
            if (!rollup.ContributionsOf.TryGetValue(drawing, out var contributions) || contributions.Count == 0) {
                return ("1", 1.0);
            }

            var terms = new List<string>();
            double cached = 0;

            foreach (var c in contributions.OrderBy(x => x.TopDrawing, StringComparer.OrdinalIgnoreCase)) {
                if (!rollup.AssemblyHeaderRowOf.TryGetValue(c.TopDrawing, out int headerRow)) continue;

                string cellRef = $"'Assembly List'!$E${headerRow}";
                string term = Math.Abs(c.Multiplier - 1.0) > 1e-9
                    ? $"{cellRef}*{c.Multiplier.ToString("0.####", CultureInfo.InvariantCulture)}"
                    : cellRef;
                terms.Add(term);
                cached += 1.0 * c.Multiplier; // giá trị mặc định của ô Qty header lúc export luôn = 1
            }

            if (terms.Count == 0) return ("1", 1.0);
            string formula = terms.Count == 1 ? terms[0] : "(" + string.Join("+", terms) + ")";
            return (formula, cached);
        }

        private static string GroupKey(BomItem item) {
            return string.Join("|",
                Normalize(item.Description),
                Normalize(item.Size),
                Normalize(item.Material),
                Normalize(item.Type));
        }

        private static string Normalize(string s) => (s ?? "").Trim().ToUpperInvariant();

        private static string EscapeFormulaText(string s) => (s ?? "").Replace("\"", "\"\"");

        private static double ParseDoubleOrDefault(string s, double fallback) {
            return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : fallback;
        }
    }
}
