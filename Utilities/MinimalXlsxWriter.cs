using System.Collections.Generic;
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
    /// Chỉ hỗ trợ đúng nhu cầu: nhiều sheet, header in đậm, độ rộng cột cố định,
    /// freeze hàng đầu + autofilter cho sheet đầu tiên. Cell dùng inlineStr nên không cần sharedStrings.xml.
    /// </summary>
    public static class MinimalXlsxWriter {
        public class SheetData {
            public string Name;
            public string[] Headers;
            public List<string[]> Rows = new List<string[]>();
            public double[] ColumnWidths;
            public bool FreezeHeaderRow;
            public bool AutoFilter;

            // Chế độ grid tự do (dùng cho layout kiểu template — title block, section header,
            // ma trận cột động...). Khi GridRows != null, BuildSheet bỏ qua Headers/Rows ở trên.
            public List<GridRow> GridRows;
        }

        /// <summary>1 hàng trong chế độ grid tự do: cột (1-based) -> giá trị. Cột không có key = ô trống.</summary>
        public class GridRow {
            public Dictionary<int, string> Cells = new Dictionary<int, string>();
            public bool Bold;
            public bool Highlight; // tô vàng — dùng để đánh dấu dòng cần người dùng chú ý thủ công
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
            sb.Append("<sheets>");
            for (int i = 0; i < sheets.Count; i++) {
                sb.Append($"<sheet name=\"{EscapeXml(sheets[i].Name)}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>");
            }
            sb.Append("</sheets>");
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

        private static string BuildStyles() {
            // fontId 0 = thường, fontId 1 = đậm.
            // cellXfs 0 = mặc định, 1 = đậm (header), 2 = tô vàng (dòng cần chú ý thủ công).
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">"
                + "<fonts count=\"2\">"
                + "<font><sz val=\"11\"/><name val=\"Calibri\"/></font>"
                + "<font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font>"
                + "</fonts>"
                + "<fills count=\"3\">"
                + "<fill><patternFill patternType=\"none\"/></fill>"
                + "<fill><patternFill patternType=\"gray125\"/></fill>"
                + "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFFFFF00\"/><bgColor indexed=\"64\"/></patternFill></fill>"
                + "</fills>"
                + "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>"
                + "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>"
                + "<cellXfs count=\"3\">"
                + "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>"
                + "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>"
                + "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"2\" borderId=\"0\" xfId=\"0\" applyFill=\"1\"/>"
                + "</cellXfs>"
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

            if (sheet.FreezeHeaderRow) {
                sb.Append("<sheetViews><sheetView workbookViewId=\"0\">");
                sb.Append("<pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/>");
                sb.Append("<selection pane=\"bottomLeft\" activeCell=\"A2\" sqref=\"A2\"/>");
                sb.Append("</sheetView></sheetViews>");
            }

            if (sheet.ColumnWidths != null) {
                sb.Append("<cols>");
                for (int i = 0; i < sheet.ColumnWidths.Length; i++) {
                    sb.Append($"<col min=\"{i + 1}\" max=\"{i + 1}\" width=\"{sheet.ColumnWidths[i].ToString(System.Globalization.CultureInfo.InvariantCulture)}\" customWidth=\"1\"/>");
                }
                sb.Append("</cols>");
            }

            sb.Append("<sheetData>");

            // Header row
            sb.Append("<row r=\"1\">");
            for (int c = 0; c < sheet.Headers.Length; c++) {
                string cellRef = ColumnLetter(c + 1) + "1";
                sb.Append($"<c r=\"{cellRef}\" t=\"inlineStr\" s=\"1\"><is><t xml:space=\"preserve\">{EscapeXml(sheet.Headers[c])}</t></is></c>");
            }
            sb.Append("</row>");

            // Data rows
            for (int r = 0; r < sheet.Rows.Count; r++) {
                int excelRow = r + 2;
                sb.Append($"<row r=\"{excelRow}\">");
                var values = sheet.Rows[r];
                for (int c = 0; c < values.Length; c++) {
                    if (string.IsNullOrEmpty(values[c])) continue;
                    string cellRef = ColumnLetter(c + 1) + excelRow;
                    sb.Append($"<c r=\"{cellRef}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{EscapeXml(values[c])}</t></is></c>");
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

            if (sheet.ColumnWidths != null) {
                sb.Append("<cols>");
                for (int i = 0; i < sheet.ColumnWidths.Length; i++) {
                    sb.Append($"<col min=\"{i + 1}\" max=\"{i + 1}\" width=\"{sheet.ColumnWidths[i].ToString(System.Globalization.CultureInfo.InvariantCulture)}\" customWidth=\"1\"/>");
                }
                sb.Append("</cols>");
            }

            sb.Append("<sheetData>");

            for (int r = 0; r < sheet.GridRows.Count; r++) {
                int excelRow = r + 1;
                var gridRow = sheet.GridRows[r];

                if (gridRow.Cells.Count == 0) {
                    sb.Append($"<row r=\"{excelRow}\"/>");
                    continue;
                }

                sb.Append($"<row r=\"{excelRow}\">");
                foreach (var kvp in gridRow.Cells.OrderBy(c => c.Key)) {
                    if (string.IsNullOrEmpty(kvp.Value)) continue;
                    string cellRef = ColumnLetter(kvp.Key) + excelRow;
                    string styleAttr = gridRow.Bold ? " s=\"1\"" : gridRow.Highlight ? " s=\"2\"" : "";
                    sb.Append($"<c r=\"{cellRef}\" t=\"inlineStr\"{styleAttr}><is><t xml:space=\"preserve\">{EscapeXml(kvp.Value)}</t></is></c>");
                }
                sb.Append("</row>");
            }

            sb.Append("</sheetData>");
            sb.Append("</worksheet>");
            return sb.ToString();
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
