using System;
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
    ///   1. Drw KHÔNG tham chiếu sang 1 bản vẽ THẬT KHÁC — 3 trường hợp được coi là "không
    ///      phải tham chiếu": (a) Drw rỗng; (b) Drw không có "hình dạng" số hiệu bản vẽ thật
    ///      (không bắt đầu bằng chữ số — số hiệu bản vẽ thật LUÔN bắt đầu bằng chữ số, vd
    ///      "4202", "2151", "1226-M"; Drw bắt đầu bằng CHỮ như "K101791" là MÃ HÀNG CATALOG,
    ///      không phải tham chiếu); (c) Drw TRÙNG với chính bản vẽ chứa dòng này (tự tham
    ///      chiếu — dòng "nhãn" của assembly tự điền số bản vẽ của mình vào cột DRW, xem
    ///      AddReferenceGraph ở BomTemplateExporter — CHÍNH dòng này mới là nơi chứa Material
    ///      thật của bản vẽ đó, không thể loại nó chỉ vì Drw "trông giống" 1 tham chiếu).
    ///   2. Size có "hình dạng" của kích thước vật liệu: hoặc là 1 số đơn thuần ("10.0"),
    ///      hoặc có chứa "x" nối các số ("15.0x160x250", "65x65x6.0, L=310") — khi đúng dạng
    ///      này, Thickness = số NHỎ NHẤT trong Size (cột "Material | Thickness" cụ thể).
    ///      Ngược lại (Size kiểu "TYPE LL 450/85", "LIFTING SOCKET" — mã kiểu loại/mô tả,
    ///      không phải kích thước phôi), VẪN phân loại nếu đã biết rõ Material, nhưng gộp
    ///      theo ĐÚNG TÊN VẬT LIỆU (Thickness để rỗng) — KHÔNG cố tách theo Size/Type, vì
    ///      lấy "số nhỏ nhất" trong các mã kiểu loại dạng này dễ ĐỤNG ĐỘ giữa nhiều loại khác
    ///      nhau (vd "450/85" và "600/85" cùng có số nhỏ nhất là "85" dù là 2 sản phẩm khác
    ///      hẳn nhau) — gộp theo Material tránh cộng nhầm nhiều loại khác nhau vào 1 cột sai.
    /// Tự thích ứng với bất kỳ tên vật tư mới nào (ANGLE, CHANNEL, TUBE...) mà không cần sửa code.
    /// </summary>
    public static class SteelSpecClassifier {
        private static readonly Regex NumberRegex = new Regex(@"\d+(?:\.\d+)?", RegexOptions.Compiled);
        private static readonly Regex BareNumberRegex = new Regex(@"^\s*\d+(?:\.\d+)?\s*$", RegexOptions.Compiled);
        private static readonly Regex DrawingReferenceRegex = new Regex(@"^\s*\d", RegexOptions.Compiled);

        public class Spec {
            public string Material;
            public string Thickness;
        }

        /// <summary>Trả về Spec nếu dòng đủ rõ để phân loại, ngược lại trả về null (để trống, không đoán)</summary>
        public static Spec Classify(BomItem item) {
            if (string.IsNullOrWhiteSpace(item.Material)) return null;
            if (string.IsNullOrWhiteSpace(item.Size)) return null;

            string drw = (item.Drw ?? "").Trim();
            string ownDrawing = BomIdentity.ExtractDrawingNumber(item.SourceFile);
            bool isSelfReference = drw.Length > 0 && drw.Equals(ownDrawing, StringComparison.OrdinalIgnoreCase);

            // Chỉ loại khi Drw THẬT SỰ trỏ sang 1 bản vẽ KHÁC — tự tham chiếu (Drw == chính
            // bản vẽ chứa dòng này) KHÔNG bị loại, vì đây chính là dòng chứa Material thật.
            if (LooksLikeDrawingReference(drw) && !isSelfReference) return null;

            string thickness = LooksLikeStockDimensions(item.Size) ? ExtractSmallestNumber(item.Size) : "";

            return new Spec {
                Material = item.Material.Trim(),
                Thickness = thickness ?? ""
            };
        }

        /// <summary>Drw có "hình dạng" số hiệu bản vẽ thật (bắt đầu bằng chữ số) hay không —
        /// phân biệt với mã hàng catalog/nhà cung cấp (bắt đầu bằng chữ, vd "K101791").</summary>
        private static bool LooksLikeDrawingReference(string drw) {
            return drw.Length > 0 && DrawingReferenceRegex.IsMatch(drw);
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
