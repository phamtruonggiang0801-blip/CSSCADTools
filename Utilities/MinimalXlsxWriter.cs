using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace CSSCADTools.Utilities {
    /// <summary>
    /// Tự tạo file .xlsx (OOXML) bằng System.IO.Compression, KHÔNG dùng thư viện ngoài (ClosedXML/EPPlus).
    /// Lý do: ClosedXML phụ thuộc SixLabors.Fonts để đo font, thư viện này ném
    /// TypeInitializationException khi chạy trong tiến trình AutoCAD (xung đột assembly
    /// System.Memory/System.Buffers giữa host và plugin, không thể fix bằng binding redirect
    /// vì AutoCAD không áp dụng app.config của plugin). Viết tay OOXML tránh hoàn toàn rủi ro này.
    /// Hỗ trợ: nhiều sheet, header in đậm, độ rộng cột cố định, freeze hàng đầu + autofilter,
    /// tô vàng theo hàng, all-border cho mọi ô có giá trị, ô số thực + ô công thức Excel (SUM/nhân)
    /// dùng cho các dòng tổng cần tự tính lại khi người dùng sửa số liệu.
    /// </summary>
    public static class MinimalXlsxWriter {
        public class SheetData {
            public string Name;
            public string[] Headers;

            // Mỗi hàng là 1 object[] — kiểu THẬT SỰ của từng ô (double/int/... -> ghi ra ô Số,
            // string -> ghi ra ô Text, null/"" -> ô trống) được BuildSheet tự nhận diện tại
            // runtime qua IsNumeric(), KHÔNG cần khai báo trước cột nào là số (đã bỏ
            // NumericColumns) — tránh lặp lại lỗi SUM/SUMIFS trả về 0 vì ô số bị ghi nhầm
            // thành text.
            public List<object[]> Rows = new List<object[]>();
            public double[] ColumnWidths;
            public bool FreezeHeaderRow;
            public bool AutoFilter;

            // Ẩn tab sheet này khi mở workbook (vẫn tồn tại đầy đủ, công thức các sheet khác
            // tham chiếu tới vẫn tính bình thường) — dùng để giấu các sheet dữ liệu nguồn/trung
            // gian (ExportedData, Material List, Warnings), chỉ để lộ sheet người dùng cần xem.
            public bool Hidden;

            // Chế độ grid tự do (dùng cho layout kiểu template — title block, section header,
            // ma trận cột động...). Khi GridRows != null, BuildSheet bỏ qua Headers/Rows ở trên.
            public List<GridRow> GridRows;

            // All-border cho TOÀN BỘ phạm vi bảng dữ liệu (kể cả ô trống), không chỉ ô có giá trị.
            // FullBorderStartRow = hàng bắt đầu (1-based, thường là hàng header cột); 0 = không dùng.
            // FullBorderColumnCount = số cột (từ cột 1) cần kẻ viền trên mỗi hàng trong phạm vi.
            public int FullBorderStartRow;
            public int FullBorderColumnCount;
        }

        /// <summary>1 hàng trong chế độ grid tự do: cột (1-based) -> giá trị. Cột không có key = ô trống.</summary>
        public class GridRow {
            public Dictionary<int, string> Cells = new Dictionary<int, string>(); // text (inlineStr)
            public Dictionary<int, double> NumberCells = new Dictionary<int, double>(); // số thực (để tham chiếu trong công thức)
            public Dictionary<int, (string Formula, double CachedValue)> Formulas = new Dictionary<int, (string, double)>(); // công thức Excel, kết quả SỐ
            public Dictionary<int, (string Formula, string CachedText)> TextFormulas = new Dictionary<int, (string, string)>(); // công thức Excel, kết quả VĂN BẢN (TEXTJOIN...)
            public bool Bold;
            public bool Highlight; // tô vàng — dùng để đánh dấu dòng cần người dùng chú ý thủ công
            public HashSet<int> HighlightedCols; // tô vàng RIÊNG 1 vài ô cụ thể, ghi đè Bold/Highlight của cả hàng

            // Hàng TIÊU ĐỀ CỘT thật sự (vd "DESCRIPTION/QTY/..." hay "Drawing/Name/ITEM/...") —
            // khác Bold (Bold còn dùng cho dòng TOTAL, tiêu đề section, title block...). Khi
            // true, MỌI ô trong hàng dùng style nổi bật riêng (nền xanh đậm, chữ trắng, căn giữa).
            public bool HeaderStyle;

            // Hàng TỔNG CUỐI SHEET (footer, vd dòng "TOTAL/KG" cuối Assembly List) — tô nền xám
            // đậm + chữ đậm để nổi bật, phân biệt với dòng TOTAL thường (Bold) và title
            // block/section header (cũng Bold nhưng không phải dòng tổng cuối cùng).
            public bool FooterStyle;
        }

        public static void Write(string outputPath, List<SheetData> sheets) {
            if (File.Exists(outputPath)) File.Delete(outputPath);

            using (var fs = new FileStream(outputPath, FileMode.CreateNew))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create)) {
                WriteEntry(zip, "[Content_Types].xml", BuildContentTypes(sheets.Count));
                WriteEntry(zip, "_rels/.rels", BuildPackageRels());
                WriteEntry(zip, "xl/workbook.xml", BuildWorkbook(sheets));
                WriteEntry(zip, "xl/_rels/workbook.xml.rels", BuildWorkbookRels(sheets.Count));
                WriteEntry(zip, "xl/styles.xml", BuildStyles());

                for (int i = 0; i < sheets.Count; i++) {
                    WriteEntry(zip, $"xl/worksheets/sheet{i + 1}.xml", BuildSheet(sheets[i]));
                }
            }
        }

        private static void WriteEntry(ZipArchive zip, string entryName, string xmlContent) {
            var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
            using (var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false))) {
                writer.Write(xmlContent);
            }
        }

        private static string BuildContentTypes(int sheetCount) {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
            sb.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
            sb.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
            sb.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
            sb.Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
            for (int i = 0; i < sheetCount; i++) {
                sb.Append($"<Override PartName=\"/xl/worksheets/sheet{i + 1}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
            }
            sb.Append("</Types>");
            return sb.ToString();
        }

        private static string BuildPackageRels() {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>"
                + "</Relationships>";
        }

        private static string BuildWorkbook(List<SheetData> sheets) {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");
            // bookViews PHẢI đứng TRƯỚC sheets theo thứ tự schema OOXML (CT_Workbook) — sai thứ
            // tự sẽ bị OpenXml validator báo lỗi "unexpected child element". Tab đang mở khi user
            // mở file lần đầu PHẢI trỏ tới 1 sheet KHÔNG bị ẩn — mặc định (không khai báo) Excel
            // chọn sheet đầu tiên trong danh sách, mà sheet đầu (ExportedData) giờ bị ẩn nên phải
            // chỉ định rõ tab đầu tiên KHÔNG ẩn.
            int activeTabIdx = sheets.FindIndex(s => !s.Hidden);
            if (activeTabIdx > 0) {
                sb.Append($"<bookViews><workbookView activeTab=\"{activeTabIdx}\"/></bookViews>");
            }

            sb.Append("<sheets>");
            for (int i = 0; i < sheets.Count; i++) {
                string hiddenAttr = sheets[i].Hidden ? " state=\"hidden\"" : "";
                sb.Append($"<sheet name=\"{EscapeXml(sheets[i].Name)}\" sheetId=\"{i + 1}\"{hiddenAttr} r:id=\"rId{i + 1}\"/>");
            }
            sb.Append("</sheets>");
            // fullCalcOnLoad: bắt Excel tính lại toàn bộ công thức khi mở file, vì file không có
            // calcChain.xml — nếu không ép tính lại, giá trị cache <v> có thể hiển thị sai sau khi
            // người dùng sửa các ô Qty tô vàng.
            sb.Append("<calcPr calcId=\"0\" fullCalcOnLoad=\"1\"/>");
            sb.Append("</workbook>");
            return sb.ToString();
        }

        private static string BuildWorkbookRels(int sheetCount) {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
            for (int i = 0; i < sheetCount; i++) {
                sb.Append($"<Relationship Id=\"rId{i + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i + 1}.xml\"/>");
            }
            sb.Append($"<Relationship Id=\"rId{sheetCount + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");
            sb.Append("</Relationships>");
            return sb.ToString();
        }

        // Chỉ số cellXfs dùng xuyên suốt code — đặt hằng số để tránh "magic number" rải rác.
        public const int STYLE_DEFAULT = 0;
        public const int STYLE_BOLD = 1;
        public const int STYLE_HIGHLIGHT = 2;
        public const int STYLE_HEADER = 3;      // nền xanh đậm, chữ trắng đậm, căn giữa — hàng tiêu đề cột thật sự
        public const int STYLE_CENTER = 4;      // như DEFAULT nhưng căn giữa — ô SỐ trong hàng thường
        public const int STYLE_BOLD_CENTER = 5; // như BOLD nhưng căn giữa — ô SỐ trong hàng đậm (TOTAL...)
        public const int STYLE_HIGHLIGHT_CENTER = 6; // như HIGHLIGHT nhưng căn giữa — ô SỐ trong hàng tô vàng
        public const int STYLE_FOOTER = 7;      // nền xám đậm, chữ đậm — dòng TỔNG CUỐI SHEET (footer)
        public const int STYLE_FOOTER_CENTER = 8; // như FOOTER nhưng căn giữa — ô SỐ trong dòng footer

        private static string BuildStyles() {
            // fontId 0 = thường, fontId 1 = đậm, fontId 2 = đậm + chữ trắng (dùng cho header nền màu).
            // borderId 1 = viền mảnh 4 cạnh (all border) — áp dụng cho MỌI cellXfs bên dưới.
            // fillId 3 = nền xanh đậm (header), fillId 4 = nền xám đậm (footer). Xem STYLE_* ở
            // trên để biết ý nghĩa từng cellXfs.
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">"
                + "<fonts count=\"3\">"
                + "<font><sz val=\"11\"/><name val=\"Calibri\"/></font>"
                + "<font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font>"
                + "<font><b/><sz val=\"11\"/><color rgb=\"FFFFFFFF\"/><name val=\"Calibri\"/></font>"
                + "</fonts>"
                + "<fills count=\"5\">"
                + "<fill><patternFill patternType=\"none\"/></fill>"
                + "<fill><patternFill patternType=\"gray125\"/></fill>"
                + "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFFFFF00\"/><bgColor indexed=\"64\"/></patternFill></fill>"
                + "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF1F4E78\"/><bgColor indexed=\"64\"/></patternFill></fill>"
                + "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFD9D9D9\"/><bgColor indexed=\"64\"/></patternFill></fill>"
                + "</fills>"
                // "hair" — nét mỏng nhất trong OOXML (mỏng hơn cả "thin") + màu đen tuyệt đối
                // (rgb, không dùng indexed) để không bị nhầm lẫn với gridline mặc định của Excel.
                + "<borders count=\"2\">"
                + "<border><left/><right/><top/><bottom/><diagonal/></border>"
                + "<border><left style=\"hair\"><color rgb=\"FF000000\"/></left><right style=\"hair\"><color rgb=\"FF000000\"/></right>"
                + "<top style=\"hair\"><color rgb=\"FF000000\"/></top><bottom style=\"hair\"><color rgb=\"FF000000\"/></bottom><diagonal/></border>"
                + "</borders>"
                // cellStyleXfs ("Normal" base style) khai báo SẴN borderId=1 — không dựa vào việc
                // Excel tôn trọng applyBorder override của cellXfs (đã quan sát thấy Excel thật
                // KHÔNG áp border cho các dòng thường dù cellXfs có applyBorder="1" borderId="1"
                // khi base style là borderId="0" — dù đúng theo OOXML spec, thực tế Excel không
                // nhất quán khi file thiếu <cellStyles>). Khai báo trùng khớp ngay từ base style
                // để loại bỏ hoàn toàn phụ thuộc vào cơ chế override.
                + "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\"/></cellStyleXfs>"
                + "<cellXfs count=\"9\">"
                + "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyBorder=\"1\"/>" // 0 DEFAULT
                + "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyBorder=\"1\"/>" // 1 BOLD
                + "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFill=\"1\" applyBorder=\"1\"/>" // 2 HIGHLIGHT
                + "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"3\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\"/></xf>" // 3 HEADER
                + "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\"/></xf>" // 4 CENTER
                + "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\"/></xf>" // 5 BOLD_CENTER
                + "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\"/></xf>" // 6 HIGHLIGHT_CENTER
                + "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"4\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"/>" // 7 FOOTER
                + "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"4\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\"/></xf>" // 8 FOOTER_CENTER
                + "</cellXfs>"
                + "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>"
                + "</styleSheet>";
        }

        private static string BuildSheet(SheetData sheet) {
            if (sheet.GridRows != null) return BuildGridSheet(sheet);

            int colCount = sheet.Headers.Length;
            int rowCount = sheet.Rows.Count + 1;
            string lastCol = ColumnLetter(colCount);

            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");

            // sheetView LUÔN ghi (kể cả không freeze) để tắt gridline mặc định của Excel — bảng
            // đã có viền vẽ sẵn (hair border) nên gridline xám mặc định chỉ gây rối mắt, không
            // còn khớp đúng ranh giới bảng thật (đặc biệt ở vùng ngoài phạm vi có dữ liệu).
            sb.Append("<sheetViews><sheetView workbookViewId=\"0\" showGridLines=\"0\">");
            if (sheet.FreezeHeaderRow) {
                sb.Append("<pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/>");
                sb.Append("<selection pane=\"bottomLeft\" activeCell=\"A2\" sqref=\"A2\"/>");
            }
            sb.Append("</sheetView></sheetViews>");

            if (sheet.ColumnWidths != null) {
                sb.Append("<cols>");
                for (int i = 0; i < sheet.ColumnWidths.Length; i++) {
                    sb.Append($"<col min=\"{i + 1}\" max=\"{i + 1}\" width=\"{sheet.ColumnWidths[i].ToString(CultureInfo.InvariantCulture)}\" customWidth=\"1\"/>");
                }
                sb.Append("</cols>");
            }

            sb.Append("<sheetData>");

            // Header row — style nổi bật (nền xanh đậm, chữ trắng, căn giữa)
            sb.Append("<row r=\"1\">");
            for (int c = 0; c < sheet.Headers.Length; c++) {
                string cellRef = ColumnLetter(c + 1) + "1";
                sb.Append($"<c r=\"{cellRef}\" t=\"inlineStr\" s=\"{STYLE_HEADER}\"><is><t xml:space=\"preserve\">{EscapeXml(sheet.Headers[c])}</t></is></c>");
            }
            sb.Append("</row>");

            // Data rows — luôn ghi đủ mọi ô trong phạm vi cột (kể cả ô trống) để all-border
            // phủ kín cả bảng, không chỉ những ô có giá trị. Ô SỐ căn giữa (STYLE_CENTER), ô
            // text giữ căn trái mặc định (STYLE_DEFAULT) — dễ đọc/so sánh số liệu hơn.
            for (int r = 0; r < sheet.Rows.Count; r++) {
                int excelRow = r + 2;
                sb.Append($"<row r=\"{excelRow}\">");
                var values = sheet.Rows[r];
                for (int c = 0; c < colCount; c++) {
                    string cellRef = ColumnLetter(c + 1) + excelRow;
                    object val = c < values.Length ? values[c] : null;

                    if (val == null || (val is string vs && vs.Length == 0)) {
                        sb.Append($"<c r=\"{cellRef}\" s=\"{STYLE_DEFAULT}\"/>");
                    } else if (IsNumeric(val)) {
                        double num = System.Convert.ToDouble(val, CultureInfo.InvariantCulture);
                        sb.Append($"<c r=\"{cellRef}\" s=\"{STYLE_CENTER}\"><v>{num.ToString("0.####", CultureInfo.InvariantCulture)}</v></c>");
                    } else {
                        sb.Append($"<c r=\"{cellRef}\" t=\"inlineStr\" s=\"{STYLE_DEFAULT}\"><is><t xml:space=\"preserve\">{EscapeXml(val.ToString())}</t></is></c>");
                    }
                }
                sb.Append("</row>");
            }

            sb.Append("</sheetData>");

            if (sheet.AutoFilter && sheet.Rows.Count > 0) {
                sb.Append($"<autoFilter ref=\"A1:{lastCol}{rowCount}\"/>");
            }

            sb.Append("</worksheet>");
            return sb.ToString();
        }

        private static string BuildGridSheet(SheetData sheet) {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");

            sb.Append("<sheetViews><sheetView workbookViewId=\"0\" showGridLines=\"0\">");
            if (sheet.FreezeHeaderRow) {
                sb.Append("<pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/>");
                sb.Append("<selection pane=\"bottomLeft\" activeCell=\"A2\" sqref=\"A2\"/>");
            }
            sb.Append("</sheetView></sheetViews>");

            if (sheet.ColumnWidths != null) {
                sb.Append("<cols>");
                for (int i = 0; i < sheet.ColumnWidths.Length; i++) {
                    sb.Append($"<col min=\"{i + 1}\" max=\"{i + 1}\" width=\"{sheet.ColumnWidths[i].ToString(CultureInfo.InvariantCulture)}\" customWidth=\"1\"/>");
                }
                sb.Append("</cols>");
            }

            sb.Append("<sheetData>");

            for (int r = 0; r < sheet.GridRows.Count; r++) {
                int excelRow = r + 1;
                var gridRow = sheet.GridRows[r];

                bool forceFullRow = sheet.FullBorderStartRow > 0 && excelRow >= sheet.FullBorderStartRow;
                bool empty = !forceFullRow && gridRow.Cells.Count == 0 && gridRow.NumberCells.Count == 0
                    && gridRow.Formulas.Count == 0 && gridRow.TextFormulas.Count == 0;
                if (empty) {
                    sb.Append($"<row r=\"{excelRow}\"/>");
                    continue;
                }

                // Style GỐC của cả hàng (chưa tính căn giữa) — HeaderStyle ưu tiên cao nhất
                // (hàng tiêu đề cột thật sự), sau đó FooterStyle (dòng tổng cuối sheet), Bold,
                // Highlight, cuối cùng Default.
                int rowBaseStyle = gridRow.HeaderStyle ? STYLE_HEADER
                    : gridRow.FooterStyle ? STYLE_FOOTER
                    : gridRow.Bold ? STYLE_BOLD
                    : gridRow.Highlight ? STYLE_HIGHLIGHT
                    : STYLE_DEFAULT;

                IEnumerable<int> allCols = gridRow.Cells.Keys
                    .Concat(gridRow.NumberCells.Keys)
                    .Concat(gridRow.Formulas.Keys)
                    .Concat(gridRow.TextFormulas.Keys);

                if (forceFullRow) {
                    allCols = allCols.Concat(Enumerable.Range(1, sheet.FullBorderColumnCount));
                }

                sb.Append($"<row r=\"{excelRow}\">");
                foreach (int col in allCols.Distinct().OrderBy(c => c)) {
                    string cellRef = ColumnLetter(col) + excelRow;
                    int baseStyle = (gridRow.HighlightedCols != null && gridRow.HighlightedCols.Contains(col))
                        ? STYLE_HIGHLIGHT
                        : rowBaseStyle;

                    if (gridRow.TextFormulas.TryGetValue(col, out var tf)) {
                        // Kết quả VĂN BẢN (TEXTJOIN...) — giữ căn trái, không center.
                        sb.Append($"<c r=\"{cellRef}\" t=\"str\" s=\"{baseStyle}\"><f>{EscapeXml(tf.Formula)}</f><v>{EscapeXml(tf.CachedText)}</v></c>");
                    } else if (gridRow.Formulas.TryGetValue(col, out var f)) {
                        int s = CenteredVariant(baseStyle);
                        sb.Append($"<c r=\"{cellRef}\" s=\"{s}\"><f>{EscapeXml(f.Formula)}</f><v>{f.CachedValue.ToString("0.####", CultureInfo.InvariantCulture)}</v></c>");
                    } else if (gridRow.NumberCells.TryGetValue(col, out double num)) {
                        int s = CenteredVariant(baseStyle);
                        sb.Append($"<c r=\"{cellRef}\" s=\"{s}\"><v>{num.ToString("0.####", CultureInfo.InvariantCulture)}</v></c>");
                    } else if (gridRow.Cells.TryGetValue(col, out string text) && !string.IsNullOrEmpty(text)) {
                        // Text — dùng nguyên baseStyle: HeaderStyle đã tự căn giữa (nhãn cột),
                        // Bold/Highlight/Default giữ căn trái mặc định (không center text).
                        sb.Append($"<c r=\"{cellRef}\" t=\"inlineStr\" s=\"{baseStyle}\"><is><t xml:space=\"preserve\">{EscapeXml(text)}</t></is></c>");
                    } else {
                        // Ô trống trong phạm vi full-border — vẫn ghi để có viền
                        sb.Append($"<c r=\"{cellRef}\" s=\"{baseStyle}\"/>");
                    }
                }
                sb.Append("</row>");
            }

            sb.Append("</sheetData>");

            if (sheet.AutoFilter && sheet.GridRows.Count > 0 && sheet.FullBorderColumnCount > 0) {
                string lastCol = ColumnLetter(sheet.FullBorderColumnCount);
                sb.Append($"<autoFilter ref=\"A1:{lastCol}{sheet.GridRows.Count}\"/>");
            }

            sb.Append("</worksheet>");
            return sb.ToString();
        }

        /// <summary>Nhận diện ô nào cần ghi ra Excel dạng Số (Number) dựa trên kiểu THẬT SỰ tại runtime,
        /// thay vì khai báo trước chỉ số cột — double?/int?... đã unbox thành double/int khi vào object[].</summary>
        private static bool IsNumeric(object val) {
            return val is double || val is int || val is float || val is long || val is decimal;
        }

        /// <summary>Map style GỐC của hàng (Default/Bold/Highlight/Header) sang biến thể CĂN GIỮA
        /// tương ứng — dùng cho ô SỐ (Formulas/NumberCells), giữ nguyên font/nền/viền của hàng.</summary>
        private static int CenteredVariant(int baseStyle) {
            switch (baseStyle) {
                case STYLE_BOLD: return STYLE_BOLD_CENTER;
                case STYLE_HIGHLIGHT: return STYLE_HIGHLIGHT_CENTER;
                case STYLE_FOOTER: return STYLE_FOOTER_CENTER;
                case STYLE_HEADER: return STYLE_HEADER; // đã center sẵn
                default: return STYLE_CENTER;
            }
        }

        private static string ColumnLetter(int col) {
            string letters = "";
            while (col > 0) {
                int rem = (col - 1) % 26;
                letters = (char)('A' + rem) + letters;
                col = (col - 1) / 26;
            }
            return letters;
        }

        private static string EscapeXml(string s) {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char ch in s) {
                switch (ch) {
                    case '&': sb.Append("&amp;"); break;
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    default:
                        // XML 1.0 không cho phép ký tự điều khiển (trừ tab/CR/LF)
                        if (ch < 0x20 && ch != '\t' && ch != '\n' && ch != '\r') continue;
                        sb.Append(ch);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
