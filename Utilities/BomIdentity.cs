using System.Text.RegularExpressions;
using CSSCADTools.Models;

namespace CSSCADTools.Utilities {
    /// <summary>
    /// Các hàm định danh dùng CHUNG giữa nhiều exporter (ExportedData, Summary, Material List,
    /// Assembly List) — tránh trùng lặp/lệch logic trích số hiệu bản vẽ và khóa cột vật liệu,
    /// vì các cột helper trong sheet ExportedData phải khớp CHÍNH XÁC với logic mà các sheet
    /// khác dùng để dựng công thức SUMIFS tham chiếu ngược lại.
    /// </summary>
    public static class BomIdentity {
        private static readonly Regex DrawingNoRegex = new Regex(@"-(\d+)_", RegexOptions.Compiled);

        public static string ExtractDrawingNumber(string sourceFile) {
            var match = DrawingNoRegex.Match(sourceFile ?? "");
            return match.Success ? match.Groups[1].Value : (sourceFile ?? "UNKNOWN");
        }

        /// <summary>Khóa cột vật liệu "Material|Thickness" nếu item phân loại được, ngược lại null</summary>
        public static string MaterialColumnKey(BomItem item) {
            var spec = SteelSpecClassifier.Classify(item);
            return spec == null ? null : $"{spec.Material}|{spec.Thickness}";
        }
    }
}
