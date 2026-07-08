using System;
using System.Collections.Generic;
using CSSCADTools.Models;

namespace CSSCADTools.Utilities
{
    public static class FileExporter
    {
        /// <summary>
        /// Xuất 1 file Excel (.xlsx) DUY NHẤT gồm 4 sheet — DETAIL, REVERSE, SECTION, DATALOG —
        /// gộp từ 4 báo cáo CSV riêng lẻ trước đây (Detail Check, Reverse Check, Section Mark
        /// Check, Datalog) để user chỉ cần mở 1 file thay vì 4 file rời rạc. Dùng
        /// MinimalXlsxWriter (viết tay OOXML) — KHÔNG dùng ClosedXML (xem lý do trong
        /// MinimalXlsxWriter.cs: xung đột SixLabors.Fonts trong tiến trình AutoCAD).
        /// </summary>
        public static void ExportReportExcel(ScanResult scan, string outputPath)
        {
            var sheets = new List<MinimalXlsxWriter.SheetData> {
                BuildDetailSheet(scan),
                BuildReverseSheet(scan),
                BuildSectionSheet(scan),
                BuildDataLogSheet(scan)
            };

            MinimalXlsxWriter.Write(outputPath, sheets);
        }

        private static MinimalXlsxWriter.SheetData BuildDetailSheet(ScanResult scan)
        {
            var sheet = new MinimalXlsxWriter.SheetData
            {
                Name = "DETAIL",
                Headers = new[] { "Detail ID", "Source Type", "Source File", "Details File", "Status" },
                ColumnWidths = new double[] { 14, 14, 30, 30, 12 },
                FreezeHeaderRow = true,
                AutoFilter = true
            };

            foreach (var item in scan.SourceDetails)
            {
                string target = scan.DetailDefinitions.TryGetValue(item.DetailId, out string foundInFile) ? foundInFile : "---";
                string status = target == "---" ? "MISSING" : "OK";
                sheet.Rows.Add(new object[] { $"DET.{item.DetailId}", item.SourceType, item.SourceFile, target, status });
            }

            return sheet;
        }

        private static MinimalXlsxWriter.SheetData BuildReverseSheet(ScanResult scan)
        {
            var sheet = new MinimalXlsxWriter.SheetData
            {
                Name = "REVERSE",
                Headers = new[] { "Detail ID", "Defined In File", "Referenced By", "Source Type", "Status" },
                ColumnWidths = new double[] { 14, 30, 30, 14, 14 },
                FreezeHeaderRow = true,
                AutoFilter = true
            };

            // Tạo lookup nhanh: detailId → list of (sourceFile, sourceType)
            var refLookup = new Dictionary<string, List<(string File, string Type)>>(StringComparer.OrdinalIgnoreCase);
            foreach (var src in scan.SourceDetails)
            {
                if (!refLookup.ContainsKey(src.DetailId))
                    refLookup[src.DetailId] = new List<(string, string)>();
                // Tránh trùng lặp cùng file
                bool exists = false;
                foreach (var r in refLookup[src.DetailId])
                    if (string.Equals(r.File, src.SourceFile, StringComparison.OrdinalIgnoreCase)) { exists = true; break; }
                if (!exists)
                    refLookup[src.DetailId].Add((src.SourceFile, src.SourceType));
            }

            foreach (var kvp in scan.DetailDefinitions)
            {
                string detailId = kvp.Key;
                string definedIn = kvp.Value;

                // Detail "tự đủ" (xuất hiện ≥2 lần ngay trong file định nghĩa nó, vd detail tiêu
                // chuẩn/điển hình) đã coi như tự dùng tại chỗ — bỏ qua khỏi kiểm tra REVERSE, vì
                // không tham chiếu chéo sang file khác không có nghĩa là "không ai dùng".
                if (scan.SelfDefinedDetails.Contains(detailId)) continue;

                if (refLookup.TryGetValue(detailId, out var refs))
                {
                    foreach (var r in refs)
                    {
                        sheet.Rows.Add(new object[] { $"DET.{detailId}", definedIn, r.File, r.Type, "OK" });
                    }
                }
                else
                {
                    sheet.Rows.Add(new object[] { $"DET.{detailId}", definedIn, "---", "---", "UNREFERENCED" });
                }
            }

            return sheet;
        }

        private static MinimalXlsxWriter.SheetData BuildSectionSheet(ScanResult scan)
        {
            var sheet = new MinimalXlsxWriter.SheetData
            {
                Name = "SECTION",
                Headers = new[] { "Section Mark Value", "Source File", "Check File", "Check Status" },
                ColumnWidths = new double[] { 20, 30, 30, 14 },
                FreezeHeaderRow = true,
                AutoFilter = true
            };

            foreach (var item in scan.SectionMarks)
            {
                sheet.Rows.Add(new object[] { item.MarkValue, item.SourceFile, item.CheckFile, item.CheckStatus });
            }

            return sheet;
        }

        private static MinimalXlsxWriter.SheetData BuildDataLogSheet(ScanResult scan)
        {
            var sheet = new MinimalXlsxWriter.SheetData
            {
                Name = "DATALOG",
                Headers = new[] { "File Name", "Entity Type", "Raw Content", "Extracted Result" },
                ColumnWidths = new double[] { 30, 16, 40, 40 },
                FreezeHeaderRow = true,
                AutoFilter = true
            };

            foreach (var entry in scan.DataLog)
            {
                sheet.Rows.Add(new object[] { entry.FileName, entry.EntityType, entry.RawContent, entry.ExtractedResult });
            }

            return sheet;
        }
    }
}
