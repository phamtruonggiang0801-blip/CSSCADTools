using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CSSCADTools.Models;

namespace CSSCADTools.Utilities {
    /// <summary>
    /// Đọc TOÀN BỘ BomItem, validate + ép kiểu dữ liệu (Number/String), nạp vào 1 List DUY
    /// NHẤT (List&lt;ExportedDataRow&gt;) — tách biệt hoàn toàn bước xử lý dữ liệu khỏi bước
    /// ghi Excel. Sau khi List này đầy đủ, BomExcelExporter mới ghi ĐỒNG LOẠT (bulk write)
    /// 1 lần xuống file, thay vì ghi rải rác từng dòng trong lúc duyệt dữ liệu.
    /// </summary>
    public static class ExportedDataBuilder {
        public static List<ExportedDataRow> Build(List<BomItem> items) {
            var rows = new List<ExportedDataRow>();

            foreach (var item in items.OrderBy(i => i.SourceFile).ThenBy(i => i.Item)) {
                rows.Add(new ExportedDataRow {
                    Item = NormalizeText(item.Item),
                    Qty = TryParseDouble(item.Qty),
                    Drw = NormalizeText(item.Drw),
                    Code = NormalizeText(item.Code),
                    Description = NormalizeText(item.Description),
                    Type = NormalizeText(item.Type),
                    Size = NormalizeText(item.Size),
                    Material = NormalizeText(item.Material),
                    Weight = TryParseDouble(item.Weight),
                    Delivery = NormalizeText(item.Delivery),
                    SourceFile = NormalizeText(item.SourceFile),
                    MaterialColumnKey = BomIdentity.MaterialColumnKey(item) ?? "",
                    DrawingNo = BomIdentity.ExtractDrawingNumber(item.SourceFile),
                    Source = item
                });
            }

            return rows;
        }

        /// <summary>Chuẩn hóa text: null -> chuỗi rỗng, trim khoảng trắng thừa — không bao giờ throw</summary>
        private static string NormalizeText(string s) => (s ?? "").Trim();

        /// <summary>Parse an toàn: null/rỗng/không phải số -> null (KHÔNG throw) — ghi ô trống thay vì crash</summary>
        private static double? TryParseDouble(string s) {
            if (string.IsNullOrWhiteSpace(s)) return null;
            return double.TryParse(s.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : (double?)null;
        }
    }
}
