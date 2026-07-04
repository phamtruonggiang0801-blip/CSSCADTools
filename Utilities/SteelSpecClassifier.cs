using System.Globalization;
using System.Text.RegularExpressions;
using CSSCADTools.Models;

namespace CSSCADTools.Utilities {
    /// <summary>
    /// Phân loại dòng BOM "đủ rõ ràng" để tự động gán vào 1 cột Grade+Thickness cụ thể
    /// trong ma trận vật liệu (giống file mẫu .xlsm).
    ///
    /// Quy tắc đọc độ dày: LUÔN là số NHỎ NHẤT xuất hiện trong Size, bất kể vị trí
    /// (đầu/giữa/cuối) — vì thứ tự ghi WxLxT không nhất quán giữa các bản vẽ
    /// (vd "15.0x160x250" ghi dày trước, "630x190x30.0" ghi dày sau), nhưng độ dày
    /// luôn là kích thước nhỏ nhất trong 1 thanh/tấm vật liệu tiêu chuẩn.
    ///
    /// KHÔNG nhận diện "dòng vật tư thô" qua từ khóa tiếng Anh trong Description
    /// (PLATE/PIPE/BAR...) vì danh sách từ khóa sẽ luôn thiếu khi gặp tên vật tư mới.
    /// Thay vào đó dùng 2 tín hiệu CẤU TRÚC đã quan sát nhất quán trong dữ liệu thật:
    ///   1. Drw (mã tham chiếu sang bản vẽ khác) đang TRỐNG — dòng assembly luôn có Drw
    ///      trỏ sang bản vẽ chi tiết khác, dòng vật tư thô (cuối chuỗi tham chiếu) thì không.
    ///   2. Size có "hình dạng" của kích thước vật liệu: hoặc là 1 số đơn thuần ("10.0"),
    ///      hoặc có chứa "x" nối các số ("15.0x160x250", "65x65x6.0, L=310"). Ngược lại,
    ///      Size kiểu "L=550mm" (số kèm nhãn chữ, không có "x") không phải kích thước vật liệu.
    /// Tự thích ứng với bất kỳ tên vật tư mới nào (ANGLE, CHANNEL, TUBE...) mà không cần sửa code.
    /// </summary>
    public static class SteelSpecClassifier {
        private static readonly Regex NumberRegex = new Regex(@"\d+(?:\.\d+)?", RegexOptions.Compiled);
        private static readonly Regex BareNumberRegex = new Regex(@"^\s*\d+(?:\.\d+)?\s*$", RegexOptions.Compiled);

        public class Spec {
            public string Material;
            public string Thickness;
        }

        /// <summary>Trả về Spec nếu dòng đủ rõ để phân loại, ngược lại trả về null (để trống, không đoán)</summary>
        public static Spec Classify(BomItem item) {
            if (string.IsNullOrWhiteSpace(item.Material)) return null;
            if (string.IsNullOrWhiteSpace(item.Size)) return null;
            if (!string.IsNullOrWhiteSpace(item.Drw)) return null; // có tham chiếu bản vẽ khác -> không phải vật tư thô cuối chuỗi
            if (!LooksLikeStockDimensions(item.Size)) return null;

            string thickness = ExtractSmallestNumber(item.Size);
            if (thickness == null) return null;

            return new Spec {
                Material = item.Material.Trim(),
                Thickness = thickness
            };
        }

        private static bool LooksLikeStockDimensions(string size) {
            if (BareNumberRegex.IsMatch(size)) return true;
            return size.IndexOf('x') >= 0 || size.IndexOf('X') >= 0;
        }

        private static string ExtractSmallestNumber(string size) {
            var matches = NumberRegex.Matches(size);
            if (matches.Count == 0) return null;

            string smallestText = null;
            double smallestValue = double.MaxValue;

            foreach (Match m in matches) {
                double value = double.Parse(m.Value, CultureInfo.InvariantCulture);
                if (value < smallestValue) {
                    smallestValue = value;
                    smallestText = m.Value;
                }
            }

            return smallestText;
        }
    }
}
