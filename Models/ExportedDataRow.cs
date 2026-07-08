namespace CSSCADTools.Models {
    /// <summary>
    /// Dòng dữ liệu ĐÃ CHUẨN HÓA KIỂU (typed) cho sheet ExportedData — được nạp đầy đủ vào
    /// 1 List DUY NHẤT (xem ExportedDataBuilder) TRƯỚC khi ghi Excel, tách biệt hoàn toàn
    /// bước đọc/validate dữ liệu khỏi bước ghi file, rồi ghi ĐỒNG LOẠT 1 lần.
    ///
    /// Trường số (Qty/Weight) là double? THẬT SỰ (không phải string) để Excel ghi ra đúng
    /// ô kiểu Number — tránh lặp lại lỗi SUM/SUMIFS trả về 0 vì ô bị ghi dạng Text.
    /// </summary>
    public class ExportedDataRow {
        public string Item;
        public double? Qty;
        public string Drw;
        public string Code;
        public string Description;
        public string Type;
        public string Size;
        public string Material;
        public double? Weight;
        public string Delivery;
        public string SourceFile;
        public string MaterialColumnKey;
        public string DrawingNo;

        /// <summary>BomItem gốc — để các sheet khác (Summary/Material List/Assembly List) tra cứu ngược</summary>
        public BomItem Source;
    }
}
