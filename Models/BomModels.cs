using System.Collections.Generic;

namespace CSSCADTools.Models {
    /// <summary>Một dòng BOM đọc được từ block SW_TABLEANNOTATION_1</summary>
    public class BomItem {
        public string Item { get; set; }
        public string Qty { get; set; }
        public string Drw { get; set; }
        public string Code { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }
        public string Size { get; set; }
        public string Material { get; set; }
        public string Weight { get; set; }
        public string Delivery { get; set; }
        public string SourceFile { get; set; }
    }

    /// <summary>Kết quả quét toàn bộ file DXF — danh sách BOM + cảnh báo file lỗi/thiếu block</summary>
    public class BomScanResult {
        public List<BomItem> Items { get; set; } = new List<BomItem>();
        public List<string> Warnings { get; set; } = new List<string>();
    }
}
