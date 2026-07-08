using System;
using System.Collections.Generic;

namespace CSSCADTools.Utilities {
    /// <summary>
    /// Cầu nối dữ liệu giữa BomTemplateExporter (sinh sheet "Assembly List", biết chính xác Excel
    /// row của từng ô Qty header) và BomSummaryExporter (cần tham chiếu CHÉO SHEET tới đúng ô đó
    /// để tính TOTAL QTY/TOTAL WEIGHT toàn dự án — có nhân hệ số Qty của Assembly List, không chỉ
    /// tính theo 1 lần build của mỗi bản vẽ như trước).
    /// </summary>
    public class AssemblyRollupContext {
        /// <summary>Bản vẽ 1*-prefixed (top) -> Excel row của ô Qty header (cột E) trong sheet "Assembly List"</summary>
        public Dictionary<string, int> AssemblyHeaderRowOf { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        /// <summary>MỌI bản vẽ (kể cả chính bản vẽ top) -> danh sách (bản vẽ TOP reach tới nó, hệ số nhân dồn qua Qty dọc đường tham chiếu)</summary>
        public Dictionary<string, List<(string TopDrawing, double Multiplier)>> ContributionsOf { get; } = new Dictionary<string, List<(string TopDrawing, double Multiplier)>>(StringComparer.OrdinalIgnoreCase);
    }
}
