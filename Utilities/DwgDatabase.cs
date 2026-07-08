using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using CSSCADTools.Models;

namespace CSSCADTools.Utilities
{
    public static class DwgDatabase
    {
        private static readonly string[] SourceTypes = {
            "TOP PLATE",
            "BOTTOM PLATE",
            "LONGITUDINAL BEAM",
            "TRANSVERSAL PLATE"
        };

        /// <summary>
        /// Quét toàn bộ file DWG trong 1 pass.
        /// Tự động lọc revision — chỉ giữ bản mới nhất khi tên file chỉ khác chữ cuối.
        /// </summary>
        public static ScanResult ScanFiles(string[] files)
        {
            // Bước 0: Lọc revision — chỉ giữ file revision mới nhất
            string[] filteredFiles = FilterLatestRevisions(files);

            var result = new ScanResult();

            // Dữ liệu trung gian cho Section Mark
            var sectionMarkSources = new Dictionary<string, (string Original, string File)>(StringComparer.OrdinalIgnoreCase);
            var allTextByFile = new Dictionary<string, List<string>>();

            // Dữ liệu trung gian cho Detail classification — CHỈ thu thập trong lúc quét, KHÔNG
            // quyết định định nghĩa/tham chiếu ngay tại đây (xem ClassifyDetails bên dưới để biết
            // lý do: phải quét XONG toàn bộ file mới quyết định được, tránh phụ thuộc thứ tự file).
            var perFileDetails = new List<(string FileName, string SourceType, List<string> Distinct, Dictionary<string, int> Occurrence)>();

            foreach (string file in filteredFiles)
            {
                using (Database sideDb = new Database(false, true))
                {
                    try
                    {
                        sideDb.ReadDwgFile(file, FileOpenMode.OpenForReadAndAllShare, true, "");
                        string fileName = Path.GetFileName(file);

                        using (Transaction tr = sideDb.TransactionManager.StartTransaction())
                        {
                            BlockTable bt = (BlockTable)tr.GetObject(sideDb.BlockTableId, OpenMode.ForRead);
                            BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                            string detectedSourceType = null;
                            List<string> foundDetailsInFile = new List<string>();
                            List<string> textsInFile = new List<string>();

                            foreach (ObjectId entId in ms)
                            {
                                Entity ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                                string rawText = "";
                                string entityType = "";

                                if (ent is DBText dbText)
                                {
                                    rawText = dbText.TextString;
                                    entityType = "DBText";
                                }
                                else if (ent is MText mText)
                                {
                                    rawText = mText.Contents;
                                    entityType = "MText";
                                }
                                else if (ent is BlockReference blkRef)
                                {
                                    string blkName = GetEffectiveName(blkRef, tr);

                                    if (blkName.Equals("_MCG_TITLE_NEW", StringComparison.OrdinalIgnoreCase))
                                    {
                                        detectedSourceType = ClassifyTitleBlock(blkRef, tr);
                                    }

                                    if (SectionScanner.IsSectionMarkBlock(blkName))
                                    {
                                        SectionScanner.CollectAttributes(blkRef, tr, fileName, sectionMarkSources);

                                        // Log section mark attributes
                                        foreach (ObjectId attId in blkRef.AttributeCollection)
                                        {
                                            AttributeReference attRef = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                                            if (attRef != null && !string.IsNullOrWhiteSpace(attRef.TextString))
                                            {
                                                result.DataLog.Add(new DataLogEntry {
                                                    FileName = fileName,
                                                    EntityType = blkName + " [Attr]",
                                                    RawContent = attRef.TextString.Trim(),
                                                    ExtractedResult = attRef.TextString.Trim()
                                                });
                                            }
                                        }
                                    }

                                    continue;
                                }

                                if (!string.IsNullOrEmpty(rawText))
                                {
                                    var extracted = TextParser.ParseDetailNumbers(rawText);
                                    if (extracted.Count > 0)
                                    {
                                        foundDetailsInFile.AddRange(extracted);
                                    }

                                    textsInFile.Add(rawText);

                                    // Log text entity
                                    result.DataLog.Add(new DataLogEntry {
                                        FileName = fileName,
                                        EntityType = entityType,
                                        RawContent = rawText,
                                        ExtractedResult = extracted.Count > 0 ? string.Join(" | ", extracted) : ""
                                    });
                                }
                            }

                            if (textsInFile.Count > 0)
                            {
                                allTextByFile[fileName] = textsInFile;
                            }

                            // Đếm số lần MỖI Detail ID xuất hiện trong CHÍNH FILE NÀY (mọi entity,
                            // DBText lẫn MText, kể cả trùng lặp) — CHỈ thu thập, chưa quyết định
                            // định nghĩa/tham chiếu (xem ClassifyDetails, gọi sau khi quét hết file).
                            var occurrenceCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                            foreach (var det in foundDetailsInFile)
                            {
                                occurrenceCount.TryGetValue(det, out int c);
                                occurrenceCount[det] = c + 1;
                            }

                            perFileDetails.Add((fileName, detectedSourceType, foundDetailsInFile.Distinct().ToList(), occurrenceCount));

                            tr.Commit();
                        }
                    }
                    catch
                    {
                    }
                }
            }

            // Detail classification — chạy SAU khi đã quét XONG toàn bộ file (không phải trong
            // lúc quét từng file) để KHÔNG phụ thuộc thứ tự file được xử lý trước/sau.
            ClassifyDetails(perFileDetails, result);

            // Section Mark cross-reference (delegate sang SectionScanner)
            result.SectionMarks = SectionScanner.CrossReference(sectionMarkSources, allTextByFile);

            return result;
        }

        /// <summary>
        /// Quyết định MỖI Detail ID là "định nghĩa" hay "tham chiếu" — làm SAU KHI đã quét hết
        /// mọi file (không phải trong lúc quét từng file), qua 2 bước:
        ///
        /// 1. TỰ ĐỦ trong 1 file: Detail ID xuất hiện ≥2 lần trong CHÍNH 1 file (bất kể entity
        ///    gì) — đây là detail TIÊU CHUẨN/ĐIỂN HÌNH được vẽ lại đầy đủ độc lập trên từng bản vẽ
        ///    áp dụng, không cần tra cứu chéo. Luôn đăng ký "đã định nghĩa" tại chính file đó.
        ///
        /// 2. TỰ ĐỦ giữa NHIỀU file "không phân loại được" (title block không khớp 4 loại cấu
        ///    kiện đã biết — vd nhóm "General Details"): nếu 1 Detail ID xuất hiện (đúng 1 lần
        ///    mỗi file) ở ≥2 file khác nhau trong nhóm này, TẤT CẢ các file đó đang cùng nhắc tới
        ///    1 detail — chọn 1 file (theo thứ tự ALPHABET, KHÔNG phụ thuộc thứ tự quét, để kết
        ///    quả ổn định/lặp lại được) làm "Defined In File" hiển thị, các file còn lại đăng ký
        ///    là THAM CHIẾU CHÉO thật (SourceType = "GENERAL DETAILS"). Trước đây, do xử lý XEN
        ///    KẼ trong lúc quét, chỉ file được xử lý ĐẦU TIÊN mới "thắng" đăng ký định nghĩa, các
        ///    file còn lại (dù rõ ràng đang tham chiếu tới CÙNG 1 Detail ID) bị bỏ qua ÂM THẦM —
        ///    không tạo được quan hệ tham chiếu nào cả, khiến MỌI Detail ID trong nhóm này luôn
        ///    báo UNREFERENCED ở sheet REVERSE dù rõ ràng có nhiều bản vẽ đang dùng chung.
        ///
        /// Phần còn lại (file được phân loại 1 trong 4 Source Type đã biết, HOẶC file không phân
        /// loại được nhưng Detail ID chỉ xuất hiện đúng 1 nơi duy nhất trong toàn bộ batch quét)
        /// giữ nguyên hành vi cũ.
        /// </summary>
        private static void ClassifyDetails(
            List<(string FileName, string SourceType, List<string> Distinct, Dictionary<string, int> Occurrence)> perFileDetails,
            ScanResult result)
        {
            // Bước 1: tự đủ TRONG CÙNG 1 file.
            foreach (var pf in perFileDetails)
            {
                var selfDefined = pf.Distinct.Where(d => pf.Occurrence[d] >= 2).ToList();
                foreach (var det in selfDefined)
                {
                    if (!result.DetailDefinitions.ContainsKey(det))
                    {
                        result.DetailDefinitions.Add(det, pf.FileName);
                    }
                    result.SelfDefinedDetails.Add(det);
                }
            }

            // Bước 2: với các Detail ID CHƯA tự đủ (bước 1), đếm nó xuất hiện ở BAO NHIÊU FILE
            // KHÁC NHAU trên toàn bộ batch quét — dùng cho case "tự đủ giữa nhiều file" ở dưới.
            var filesContaining = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var pf in perFileDetails)
            {
                foreach (var det in pf.Distinct)
                {
                    if (result.SelfDefinedDetails.Contains(det)) continue;

                    if (!filesContaining.TryGetValue(det, out var fileSet))
                    {
                        fileSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        filesContaining[det] = fileSet;
                    }
                    fileSet.Add(pf.FileName);
                }
            }

            foreach (var pf in perFileDetails)
            {
                var stillNeedsCheck = pf.Distinct.Where(d => !result.SelfDefinedDetails.Contains(d)).ToList();

                foreach (var det in stillNeedsCheck)
                {
                    if (pf.SourceType != null)
                    {
                        // File phân loại được (1 trong 4 loại cấu kiện đã biết) — giữ nguyên hành
                        // vi cũ: luôn coi là THAM CHIẾU cần tra cứu chéo.
                        result.SourceDetails.Add(new DetailInfo {
                            DetailId = det,
                            SourceFile = pf.FileName,
                            SourceType = pf.SourceType
                        });
                    }
                    else if (filesContaining.TryGetValue(det, out var filesWithThisDetail) && filesWithThisDetail.Count >= 2)
                    {
                        // Tự đủ GIỮA NHIỀU FILE không phân loại được — xem doc-comment ở trên.
                        string definer = filesWithThisDetail.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).First();
                        if (!result.DetailDefinitions.ContainsKey(det))
                        {
                            result.DetailDefinitions.Add(det, definer);
                        }

                        if (!string.Equals(pf.FileName, definer, StringComparison.OrdinalIgnoreCase))
                        {
                            result.SourceDetails.Add(new DetailInfo {
                                DetailId = det,
                                SourceFile = pf.FileName,
                                SourceType = "GENERAL DETAILS"
                            });
                        }
                    }
                    else
                    {
                        // Chỉ 1 file duy nhất trong toàn bộ batch có Detail ID này — giữ nguyên
                        // hành vi cũ: coi là ĐỊNH NGHĨA (có thể là Detail Sheet thật chưa ai tham
                        // chiếu tới — REVERSE sẽ đúng đắn báo UNREFERENCED nếu thật sự không ai dùng).
                        if (!result.DetailDefinitions.ContainsKey(det))
                        {
                            result.DetailDefinitions.Add(det, pf.FileName);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Lọc revision: nhóm các file có tên giống nhau chỉ khác chữ cuối cùng,
        /// chỉ giữ bản có ký tự cuối cao nhất (revision mới nhất).
        /// VD: 1112470WD-2704_001_B.dwg và _C.dwg → chỉ giữ _C.dwg
        /// </summary>
        public static string[] FilterLatestRevisions(string[] files)
        {
            // key = baseName (tên file không có extension, bỏ ký tự cuối)
            // value = list of (fullPath, lastChar)
            var groups = new Dictionary<string, List<(string FullPath, char LastChar)>>(StringComparer.OrdinalIgnoreCase);

            foreach (string file in files)
            {
                string nameNoExt = Path.GetFileNameWithoutExtension(file);

                if (nameNoExt.Length < 2)
                {
                    // Tên quá ngắn, không có revision → giữ nguyên
                    groups[file] = new List<(string, char)> { (file, '\0') };
                    continue;
                }

                char lastChar = nameNoExt[nameNoExt.Length - 1];
                string baseName = nameNoExt.Substring(0, nameNoExt.Length - 1);

                if (!groups.ContainsKey(baseName))
                    groups[baseName] = new List<(string, char)>();

                groups[baseName].Add((file, lastChar));
            }

            var result = new List<string>();

            foreach (var kvp in groups)
            {
                if (kvp.Value.Count == 1)
                {
                    // Chỉ có 1 file trong nhóm → giữ
                    result.Add(kvp.Value[0].FullPath);
                }
                else
                {
                    // Nhiều file cùng base → chỉ giữ revision cao nhất
                    var latest = kvp.Value.OrderByDescending(x => x.LastChar).First();
                    result.Add(latest.FullPath);
                }
            }

            return result.ToArray();
        }

        private static string ClassifyTitleBlock(BlockReference blkRef, Transaction tr)
        {
            foreach (ObjectId attId in blkRef.AttributeCollection)
            {
                AttributeReference attRef = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                if (attRef == null) continue;

                string val = attRef.TextString.ToUpper();

                foreach (string sourceType in SourceTypes)
                {
                    if (val.Contains(sourceType)) return sourceType;
                }
            }
            return null;
        }

        private static string GetEffectiveName(BlockReference blkRef, Transaction tr)
        {
            if (blkRef.IsDynamicBlock)
            {
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(blkRef.DynamicBlockTableRecord, OpenMode.ForRead);
                return btr.Name;
            }
            return blkRef.Name;
        }
    }
}
