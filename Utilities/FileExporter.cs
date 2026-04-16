using System;
using System.Collections.Generic;
using System.IO;
using CSSCADTools.Models;

namespace CSSCADTools.Utilities
{
    public static class FileExporter
    {
        /// <summary>
        /// Xuất 2 file CSV: Detail Check + Section Mark Check.
        /// basePath là đường dẫn gốc do user chọn (VD: Report_20260416_1100.csv)
        /// </summary>
        public static void ExportReport(ScanResult scan, string basePath)
        {
            string dir = Path.GetDirectoryName(basePath);
            string name = Path.GetFileNameWithoutExtension(basePath);

            // File 1: DETAIL CHECK
            string detailPath = Path.Combine(dir, name + "_DETAIL.csv");
            using (StreamWriter sw = new StreamWriter(detailPath, false, new System.Text.UTF8Encoding(true)))
            {
                sw.WriteLine("Detail ID,Source Type,Source File,Details File,Status");
                foreach (var item in scan.SourceDetails)
                {
                    string target = scan.DetailDefinitions.TryGetValue(item.DetailId, out string foundInFile) ? foundInFile : "---";
                    string status = target == "---" ? "MISSING" : "OK";
                    sw.WriteLine($"DET.{item.DetailId},{item.SourceType},{item.SourceFile},{target},{status}");
                }
            }

            // File 2: REVERSE CHECK — Detail Sheet → có được tham chiếu không?
            string reversePath = Path.Combine(dir, name + "_REVERSE.csv");
            using (StreamWriter sw = new StreamWriter(reversePath, false, new System.Text.UTF8Encoding(true)))
            {
                sw.WriteLine("Detail ID,Defined In File,Referenced By,Source Type,Status");

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

                    if (refLookup.TryGetValue(detailId, out var refs))
                    {
                        foreach (var r in refs)
                        {
                            sw.WriteLine($"DET.{detailId},{definedIn},{r.File},{r.Type},OK");
                        }
                    }
                    else
                    {
                        sw.WriteLine($"DET.{detailId},{definedIn},---,---,UNREFERENCED");
                    }
                }
            }

            // File 3: SECTION MARK CHECK
            string sectionPath = Path.Combine(dir, name + "_SECTION.csv");
            using (StreamWriter sw = new StreamWriter(sectionPath, false, new System.Text.UTF8Encoding(true)))
            {
                sw.WriteLine("Section Mark Value,Source File,Check File,Check Status");
                foreach (var item in scan.SectionMarks)
                {
                    sw.WriteLine($"{item.MarkValue},{item.SourceFile},{item.CheckFile},{item.CheckStatus}");
                }
            }
        }

        /// <summary>
        /// Xuất file Datalog CSV.
        /// </summary>
        public static void ExportDataLog(ScanResult scan, string basePath)
        {
            string dir = Path.GetDirectoryName(basePath);
            string name = Path.GetFileNameWithoutExtension(basePath);
            string logPath = Path.Combine(dir, name + "_DATALOG.csv");

            using (StreamWriter sw = new StreamWriter(logPath, false, new System.Text.UTF8Encoding(true)))
            {
                sw.WriteLine("File Name,Entity Type,Raw Content,Extracted Result");
                foreach (var entry in scan.DataLog)
                {
                    string safeRaw = EscapeCsvField(entry.RawContent);
                    string safeResult = EscapeCsvField(entry.ExtractedResult);
                    sw.WriteLine($"{entry.FileName},{entry.EntityType},{safeRaw},{safeResult}");
                }
            }
        }

        private static string EscapeCsvField(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r") || value.Contains("\\"))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }
            return value;
        }
    }
}
