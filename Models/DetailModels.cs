using System.Collections.Generic;

namespace CSSCADTools.Models {
    public class DetailInfo {
        public string DetailId { get; set; }
        public string SourceFile { get; set; }
        public string SourceType { get; set; }
        public string TargetFile { get; set; }
        public string Status => string.IsNullOrEmpty(TargetFile) || TargetFile == "---" ? "MISSING" : "OK";
    }

    public class SectionMarkInfo {
        public string MarkValue { get; set; }
        public string SourceFile { get; set; }
        /// <summary>File tìm thấy mark value (có thể là chính SourceFile hoặc file khác, hoặc "---")</summary>
        public string CheckFile { get; set; }
        public string CheckStatus { get; set; }
    }

    /// <summary>
    /// Một dòng trong Datalog — ghi lại nội dung thô và kết quả rút gọn
    /// </summary>
    public class DataLogEntry {
        public string FileName { get; set; }
        public string EntityType { get; set; }
        public string RawContent { get; set; }
        public string ExtractedResult { get; set; }
    }

    /// <summary>
    /// Kết quả quét toàn bộ — gom Detail check + Section Mark check + Datalog
    /// </summary>
    public class ScanResult {
        public List<DetailInfo> SourceDetails { get; set; } = new List<DetailInfo>();
        public Dictionary<string, string> DetailDefinitions { get; set; } = new Dictionary<string, string>();

        /// <summary>Detail ID "tự đủ" (xuất hiện ≥2 lần trong CHÍNH file định nghĩa nó — vd detail
        /// tiêu chuẩn/điển hình được vẽ lại độc lập trên nhiều bản vẽ) — dùng để loại khỏi kiểm tra
        /// "UNREFERENCED" ở sheet REVERSE, vì bản chất đã tự dùng ngay trong file của chính nó,
        /// không cần chờ 1 bản vẽ khác tham chiếu chéo tới mới coi là "có dùng".</summary>
        public HashSet<string> SelfDefinedDetails { get; set; } = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        public List<SectionMarkInfo> SectionMarks { get; set; } = new List<SectionMarkInfo>();
        public List<DataLogEntry> DataLog { get; set; } = new List<DataLogEntry>();
    }
}