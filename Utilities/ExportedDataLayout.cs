using System.Collections.Generic;
using CSSCADTools.Models;

namespace CSSCADTools.Utilities {
    /// <summary>
    /// Mô tả cấu trúc cột/hàng của sheet "ExportedData" (nguồn dữ liệu duy nhất — "one true
    /// source") — dùng để các sheet khác (Summary, Material List, Assembly List) dựng công
    /// thức Excel (tham chiếu trực tiếp / SUMIFS / COUNTIFS / TEXTJOIN) trỏ ngược lại đây,
    /// thay vì copy giá trị tĩnh. Khai báo tập trung 1 chỗ để tránh lệch cột giữa các exporter.
    /// </summary>
    public class ExportedDataLayout {
        public const string SheetName = "ExportedData";

        public const string ColItem = "A";
        public const string ColQty = "B";
        public const string ColDrw = "C";
        public const string ColCode = "D";
        public const string ColDescription = "E";
        public const string ColType = "F";
        public const string ColSize = "G";
        public const string ColMaterial = "H";
        public const string ColWeight = "I";
        public const string ColDelivery = "J";
        public const string ColSourceFile = "K";
        public const string ColMaterialKey = "L";
        public const string ColDrawingNo = "M";

        public const int FirstDataRow = 2;

        /// <summary>Hàng cuối cùng có dữ liệu (= FirstDataRow + số item - 1)</summary>
        public int LastDataRow;

        /// <summary>Item -> hàng Excel tương ứng trong ExportedData (dùng cho tham chiếu trực tiếp 1-1)</summary>
        public Dictionary<BomItem, int> RowOf { get; } = new Dictionary<BomItem, int>();

        /// <summary>Tham chiếu 1 ô cụ thể, vd Ref(ColWeight, 15) -> "ExportedData!I15"</summary>
        public string Ref(string col, int row) => $"{SheetName}!{col}{row}";

        /// <summary>Vùng dữ liệu cố định (không đổi khi copy công thức), vd "ExportedData!$I$2:$I$137"</summary>
        public string Range(string col) => $"{SheetName}!${col}${FirstDataRow}:${col}${LastDataRow}";
    }
}
