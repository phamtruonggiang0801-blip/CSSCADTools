using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using CSSCADTools.Models;

namespace CSSCADTools.Utilities {
    /// <summary>
    /// Dựng sheet "Material List" mô phỏng layout file mẫu
    /// "1112470WD_Yard fitting_material quality quantity_FINAL.xlsm":
    /// title block để trống cho người dùng tự điền, section theo từng bản vẽ
    /// (Drawing No + hệ số "N PANEL" mặc định = 1), và ma trận cột Grade+Thickness
    /// tự sinh động theo dữ liệu — chỉ điền cột cụ thể cho dòng PLATE đủ rõ ràng
    /// (xem SteelSpecClassifier), các dòng khác vẫn có TOTAL/KG nhưng để trống cột chi tiết.
    /// Dòng nào vừa KHÔNG có bản vẽ riêng để tra cứu thêm (Drw trống hoặc không khớp
    /// bản vẽ nào trong lô đang xử lý) vừa KHÔNG tự phân loại được vật liệu sẽ được
    /// tô vàng để người dùng biết cần tự điền/tra cứu thủ công.
    /// </summary>
    public static class BomTemplateExporter {
        private static readonly Regex DrawingNoRegex = new Regex(@"-(\d+)_", RegexOptions.Compiled);

        private const int FIXED_COLS = 6; // A=STT/Drawing, B=Name, C=ITEM, D=Size, E=Q'ty, F=Delivery

        public static MinimalXlsxWriter.SheetData Build(BomScanResult scan) {
            var byDrawing = GroupByDrawing(scan.Items);

            var specForItem = new Dictionary<BomItem, SteelSpecClassifier.Spec>();
            var orderedColumns = CollectColumns(scan.Items, specForItem);

            int dynCount = orderedColumns.Count;
            int totalColIdx = FIXED_COLS + dynCount + 1;

            var columnIndex = new Dictionary<string, int>();
            for (int i = 0; i < dynCount; i++) {
                columnIndex[ColumnKey(orderedColumns[i].Material, orderedColumns[i].Thickness)] = i;
            }

            var knownDrawings = new HashSet<string>(byDrawing.Select(d => d.Drawing), StringComparer.OrdinalIgnoreCase);

            var rows = new List<MinimalXlsxWriter.GridRow>();
            void AddRow(Dictionary<int, string> cells, bool bold = false, bool highlight = false) {
                rows.Add(new MinimalXlsxWriter.GridRow { Cells = cells, Bold = bold, Highlight = highlight });
            }
            void AddBlankRow() => AddRow(new Dictionary<int, string>());

            // ----- Title block (để trống cho người dùng tự điền) -----
            AddRow(new Dictionary<int, string> { { 1, "MacGregor" } }, true);
            AddBlankRow(); // dòng tiêu đề dự án — để trống
            AddBlankRow();
            AddRow(new Dictionary<int, string> { { 1, "YARD/SHIP:" } }, true);
            AddRow(new Dictionary<int, string> { { 1, "CLASS:" } }, true);
            AddRow(new Dictionary<int, string> { { 1, "DATE:" } }, true);
            AddRow(new Dictionary<int, string> { { 1, "SIGN:" } }, true);
            AddRow(new Dictionary<int, string> { { 1, "EDITION" } }, true);
            AddBlankRow();

            // ----- Header 2 tầng: tên Grade (hàng 1) + độ dày (hàng 2) -----
            var header1 = new Dictionary<int, string> {
                { 1, "Drawing" }, { 2, "Name" }, { 3, "ITEM" }, { 4, "Size" }, { 5, "Q'ty" }, { 6, "Delivery" }
            };
            for (int i = 0; i < dynCount; i++) header1[FIXED_COLS + 1 + i] = orderedColumns[i].Material;
            header1[totalColIdx] = "TOTAL/KG";
            AddRow(header1, true);

            if (dynCount > 0) {
                var header2 = new Dictionary<int, string>();
                for (int i = 0; i < dynCount; i++) header2[FIXED_COLS + 1 + i] = orderedColumns[i].Thickness;
                AddRow(header2, true);
            }

            AddBlankRow();

            // ----- Section theo từng bản vẽ -----
            var sumPerColumn = new double[dynCount];
            double grandTotal = 0;

            foreach (var section in byDrawing.OrderBy(d => d.Drawing, StringComparer.OrdinalIgnoreCase)) {
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
                        { 6, item.Delivery },
                        { totalColIdx, item.Weight }
                    };

                    bool classified = specForItem.TryGetValue(item, out var spec);
                    if (classified) {
                        int colIdx = columnIndex[ColumnKey(spec.Material, spec.Thickness)];
                        cells[FIXED_COLS + 1 + colIdx] = item.Weight;
                        sumPerColumn[colIdx] += weight;
                    }

                    // Tô vàng dòng nào KHÔNG có bản vẽ riêng để tra cứu (Drw trống hoặc
                    // không khớp bản vẽ nào trong lô đang xử lý) VÀ cũng không tự phân loại
                    // được vật liệu — đây là dòng người dùng buộc phải tự tra/điền thủ công.
                    bool hasOwnDrawing = !string.IsNullOrWhiteSpace(item.Drw) && knownDrawings.Contains(item.Drw.Trim());
                    bool needsManualReview = !hasOwnDrawing && !classified;

                    grandTotal += weight;
                    AddRow(cells, highlight: needsManualReview);
                    seq++;
                }

                AddBlankRow();
            }

            // ----- Dòng tổng -----
            var totalRow = new Dictionary<int, string> { { 2, "TOTAL/KG" } };
            for (int i = 0; i < dynCount; i++) {
                if (sumPerColumn[i] != 0) totalRow[FIXED_COLS + 1 + i] = FormatNumber(sumPerColumn[i]);
            }
            totalRow[totalColIdx] = FormatNumber(grandTotal);
            AddRow(totalRow, true);

            var widths = new List<double> { 10, 32, 10, 22, 8, 10 };
            for (int i = 0; i < dynCount; i++) widths.Add(11);
            widths.Add(12);

            return new MinimalXlsxWriter.SheetData {
                Name = "Material List",
                GridRows = rows,
                ColumnWidths = widths.ToArray()
            };
        }

        private class DrawingSection {
            public string Drawing;
            public List<BomItem> Items = new List<BomItem>();
        }

        private static List<DrawingSection> GroupByDrawing(List<BomItem> items) {
            var sections = new List<DrawingSection>();
            var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in items) {
                string drawing = ExtractDrawingNumber(item.SourceFile);
                if (!index.TryGetValue(drawing, out int idx)) {
                    idx = sections.Count;
                    index[drawing] = idx;
                    sections.Add(new DrawingSection { Drawing = drawing });
                }
                sections[idx].Items.Add(item);
            }

            return sections;
        }

        private static string ExtractDrawingNumber(string sourceFile) {
            var match = DrawingNoRegex.Match(sourceFile ?? "");
            return match.Success ? match.Groups[1].Value : (sourceFile ?? "UNKNOWN");
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

        private static string ColumnKey(string material, string thickness) => $"{material}|{thickness}";

        private static int ParseIntOrDefault(string s, int fallback) {
            return int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out int v) ? v : fallback;
        }

        private static double ParseDoubleOrDefault(string s, double fallback) {
            return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : fallback;
        }

        private static string FormatNumber(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
    }
}
