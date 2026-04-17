using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.DatabaseServices;
using CSSCADTools.Models;

namespace CSSCADTools.Utilities
{
    public static class SectionScanner
    {
        // Keyword nhận dạng Section Mark block (dùng Contains, không cần khớp chính xác).
        // Tiền tố/hậu tố tùy ý. VD: "MCG_SECTION_MARK_VERT", "SECTION_MARK_V2" đều match.
        private static readonly string[] SectionBlockKeywords = {
            "SECTION_MARK_VERT",
            "SECTION_MARK"
        };

        public static bool IsSectionMarkBlock(string blockName)
        {
            foreach (string keyword in SectionBlockKeywords)
            {
                if (blockName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        public static void CollectAttributes(
            BlockReference blkRef, Transaction tr, string fileName,
            Dictionary<string, (string Original, string File)> markSources)
        {
            foreach (ObjectId attId in blkRef.AttributeCollection)
            {
                AttributeReference attRef = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                if (attRef == null) continue;

                string val = attRef.TextString.Trim();
                if (string.IsNullOrEmpty(val)) continue;

                if (!markSources.ContainsKey(val))
                {
                    markSources[val] = (val, fileName);
                }
            }
        }

        public static List<SectionMarkInfo> CrossReference(
            Dictionary<string, (string Original, string File)> markSources,
            Dictionary<string, List<string>> allTextByFile)
        {
            var results = new List<SectionMarkInfo>();

            foreach (var kvp in markSources)
            {
                string markUpper = kvp.Key.ToUpper();
                string sourceFile = kvp.Value.File;
                string originalValue = kvp.Value.Original;

                string foundInFile = null;

                // Ưu tiên kiểm tra chính file nguồn trước (self check)
                if (allTextByFile.TryGetValue(sourceFile, out List<string> selfTexts))
                {
                    foreach (string rawText in selfTexts)
                    {
                        // Dọn MText formatting trước khi kiểm tra,
                        // tránh bị formatting code chen giữa giá trị (VD: SA61\fArial...;1 thay vì SA611)
                        string cleaned = CleanMTextFormatting(rawText).ToUpper();
                        if (cleaned.Contains(markUpper))
                        {
                            foundInFile = sourceFile;
                            break;
                        }
                    }
                }

                // Nếu không tìm thấy trong chính file → tìm ở các file khác
                if (foundInFile == null)
                {
                    foreach (var textKvp in allTextByFile)
                    {
                        if (string.Equals(textKvp.Key, sourceFile, StringComparison.OrdinalIgnoreCase))
                            continue;

                        foreach (string rawText in textKvp.Value)
                        {
                            string cleaned = CleanMTextFormatting(rawText).ToUpper();
                            if (cleaned.Contains(markUpper))
                            {
                                foundInFile = textKvp.Key;
                                break;
                            }
                        }
                        if (foundInFile != null) break;
                    }
                }

                results.Add(new SectionMarkInfo {
                    MarkValue = originalValue,
                    SourceFile = sourceFile,
                    CheckFile = foundInFile ?? "---",
                    CheckStatus = foundInFile != null ? "OK" : "MISSING"
                });
            }

            return results;
        }

        /// <summary>
        /// Dọn MText formatting codes để lấy text thuần.
        /// Dùng cùng logic với TextParser nhưng chỉ strip formatting, không parse Detail.
        /// VD: "SA61\fArial|b0|i0|c163|p34;1" → "SA611"
        /// </summary>
        private static string CleanMTextFormatting(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            // Khử Unicode
            text = Regex.Replace(text, @"\\U\+[A-Fa-f0-9]{4}", " ");
            // Khử mã định dạng phức tạp có dấu chấm phẩy (\fArial|b0;, \C1;, \H2.5;, \pxqc;...)
            text = Regex.Replace(text, @"\\[^;\\\n]*?;", "");
            // Ép phẳng \P và \N thành dấu cách
            text = text.Replace("\\P", " ").Replace("\\N", " ");
            // Khử mã định dạng đơn (\L, \O, \K, \X, \~)
            text = Regex.Replace(text, @"\\[LlOoKkxX~]", "");
            // Khử ngoặc nhọn
            text = Regex.Replace(text, @"[{}]", "");
            // Ép phẳng xuống dòng thật
            text = text.Replace("\n", " ").Replace("\r", " ");

            return text;
        }
    }
}
