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

            // File 2: SECTION MARK CHECK
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
