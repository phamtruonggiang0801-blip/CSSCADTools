using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.DatabaseServices;
using CSSCADTools.Models;

namespace CSSCADTools.Utilities {
    /// <summary>
    /// Tái dựng bảng BOM từ các entity MTEXT nằm trong block SW_TABLEANNOTATION_1.
    /// Ô trống không có entity nên bắt buộc định vị theo tọa độ (X, Y), không dựa vào thứ tự entity.
    /// </summary>
    public static class BomTableParser {
        private const double ROW_TOLERANCE = 2.0;

        // Từ khóa header (đã UPPER) -> tên cột BomItem tương ứng.
        // Thứ tự kiểm tra quan trọng: "SIZE" phải match trước để bắt "SIZE / DIMENSIONS".
        private static readonly (string Keyword, string Column)[] HeaderKeywords = new[] {
            ("ITEM", "Item"),
            ("QTY", "Qty"),
            ("DRW", "Drw"),
            ("CODE", "Code"),
            ("DESCRIPTION", "Description"),
            ("TYPE", "Type"),
            ("SIZE", "Size"),
            ("MATERIAL", "Material"),
            ("WEIGHT", "Weight"),
            ("MASS", "Weight"),
            ("DELIVERY", "Delivery"),
        };

        private const int MIN_HEADER_MATCHES = 4;

        // Một số ô (thường là MATERIAL) chứa 2 vật liệu tương đương viết trên 2 dòng
        // (vd "Q355D," / "SM490B"), nhưng được lưu thành 2 MTEXT RIÊNG BIỆT ở 2 Y hơi
        // lệch nhau quanh baseline của hàng — KHÔNG gộp thành 1 entity duy nhất.
        // Lệch này (~vài đơn vị) vượt ROW_TOLERANCE nên bị GroupIntoRows tách thành
        // "hàng" riêng chỉ có 1 ô — coi là mảnh vỡ cần gộp lại, không phải item thật.
        private const int MIN_CELLS_FOR_REAL_ROW = 3;

        private class Cell {
            public double X;
            public double Y;
            public string Text;
        }

        /// <summary>Đọc toàn bộ MTEXT trong block, tái dựng bảng, trả về danh sách BomItem (SourceFile chưa gán)</summary>
        public static List<BomItem> Parse(BlockTableRecord btr, Transaction tr, out string warning) {
            warning = null;
            var cells = new List<Cell>();

            foreach (ObjectId entId in btr) {
                Entity ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                if (ent is MText mText) {
                    string clean = CleanMTextFormatting(mText.Contents);
                    if (!string.IsNullOrWhiteSpace(clean)) {
                        cells.Add(new Cell { X = mText.Location.X, Y = mText.Location.Y, Text = clean.Trim() });
                    }
                }
            }

            if (cells.Count == 0) {
                warning = "Không tìm thấy MTEXT nào trong block SW_TABLEANNOTATION_1";
                return new List<BomItem>();
            }

            // Gom nhóm theo Y (dung sai ROW_TOLERANCE) -> mỗi nhóm là 1 hàng
            var rows = GroupIntoRows(cells);

            // Tìm hàng header
            List<Cell> headerRow = null;
            var columnMap = new List<(double X, string Column)>();

            foreach (var row in rows) {
                var matched = new List<(double X, string Column)>();
                var usedColumns = new HashSet<string>();

                foreach (var cell in row) {
                    string upper = cell.Text.ToUpperInvariant();
                    foreach (var kw in HeaderKeywords) {
                        if (usedColumns.Contains(kw.Column)) continue;
                        if (upper.Contains(kw.Keyword)) {
                            matched.Add((cell.X, kw.Column));
                            usedColumns.Add(kw.Column);
                            break;
                        }
                    }
                }

                if (matched.Count >= MIN_HEADER_MATCHES) {
                    headerRow = row;
                    columnMap = matched;
                    break;
                }
            }

            if (headerRow == null) {
                warning = "Không xác định được hàng header (ITEM/QTY/...) trong block SW_TABLEANNOTATION_1";
                return new List<BomItem>();
            }

            columnMap = columnMap.OrderBy(c => c.X).ToList();

            var dataRows = rows.Where(r => r != headerRow).ToList();
            MergeFragmentRows(dataRows);

            var items = new List<BomItem>();
            foreach (var row in dataRows) {
                var item = new BomItem();
                bool hasData = false;

                // Sắp theo Y giảm dần: nếu 1 cột bị gộp nhiều mảnh (multi-line),
                // dòng trên được nối trước, dòng dưới nối sau — đúng thứ tự đọc.
                foreach (var cell in row.OrderByDescending(c => c.Y)) {
                    string column = NearestColumn(cell.X, columnMap);
                    if (column == null) continue;
                    AssignColumn(item, column, cell.Text);
                    hasData = true;
                }

                if (hasData) items.Add(item);
            }

            return items;
        }

        /// <summary>
        /// Gộp các "hàng" quá ít ô (mảnh vỡ do 1 cell bị word-wrap thành nhiều MTEXT
        /// ở Y hơi lệch nhau) vào hàng thật gần nhất theo khoảng cách Y.
        /// Sửa trực tiếp trên danh sách dataRows (xóa mảnh vỡ, gộp cell vào hàng thật).
        /// </summary>
        private static void MergeFragmentRows(List<List<Cell>> dataRows) {
            var realRows = dataRows.Where(r => r.Count >= MIN_CELLS_FOR_REAL_ROW).ToList();
            if (realRows.Count == 0) return; // không có hàng thật nào để gộp vào — giữ nguyên

            var fragmentRows = dataRows.Where(r => r.Count < MIN_CELLS_FOR_REAL_ROW).ToList();

            foreach (var fragment in fragmentRows) {
                var nearest = realRows.OrderBy(r => Math.Abs(r[0].Y - fragment[0].Y)).First();
                nearest.AddRange(fragment);
                dataRows.Remove(fragment);
            }
        }

        private static List<List<Cell>> GroupIntoRows(List<Cell> cells) {
            var sorted = cells.OrderByDescending(c => c.Y).ToList();
            var rows = new List<List<Cell>>();

            foreach (var cell in sorted) {
                var row = rows.FirstOrDefault(r => Math.Abs(r[0].Y - cell.Y) <= ROW_TOLERANCE);
                if (row == null) {
                    row = new List<Cell>();
                    rows.Add(row);
                }
                row.Add(cell);
            }

            return rows;
        }

        private static string NearestColumn(double x, List<(double X, string Column)> columnMap) {
            if (columnMap.Count == 0) return null;

            string best = columnMap[0].Column;
            double bestDist = Math.Abs(x - columnMap[0].X);

            for (int i = 1; i < columnMap.Count; i++) {
                double dist = Math.Abs(x - columnMap[i].X);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = columnMap[i].Column;
                }
            }

            return best;
        }

        private static void AssignColumn(BomItem item, string column, string text) {
            switch (column) {
                case "Item": item.Item = Append(item.Item, text); break;
                case "Qty": item.Qty = Append(item.Qty, text); break;
                case "Drw": item.Drw = Append(item.Drw, text); break;
                case "Code": item.Code = Append(item.Code, text); break;
                case "Description": item.Description = Append(item.Description, text); break;
                case "Type": item.Type = Append(item.Type, text); break;
                case "Size": item.Size = Append(item.Size, text); break;
                case "Material": item.Material = Append(item.Material, text); break;
                case "Weight": item.Weight = Append(item.Weight, text); break;
                case "Delivery": item.Delivery = Append(item.Delivery, text); break;
            }
        }

        /// <summary>Nối thêm text vào giá trị cột đã có (trường hợp 1 cột bị gộp nhiều mảnh multi-line)</summary>
        private static string Append(string existing, string add) {
            if (string.IsNullOrEmpty(existing)) return add;
            return existing.TrimEnd() + " " + add.TrimStart();
        }

        /// <summary>Bỏ các format code của MTEXT (\P, \A1;, {...}, \C1;...) chỉ giữ lại text thuần</summary>
        private static string CleanMTextFormatting(string raw) {
            if (string.IsNullOrEmpty(raw)) return raw;

            string text = raw;
            text = text.Replace("\\P", " / ").Replace("\\p", " / ");
            text = text.Replace("\\~", " ");
            text = Regex.Replace(text, @"\\[A-Za-z][^;\\{}]*;", "");
            text = text.Replace("{", "").Replace("}", "");
            text = text.Replace("\\\\", "\\");
            return Regex.Replace(text, @"\s+", " ").Trim();
        }
    }
}
