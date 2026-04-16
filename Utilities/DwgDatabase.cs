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

                            // Detail classification
                            foundDetailsInFile = foundDetailsInFile.Distinct().ToList();

                            if (detectedSourceType != null)
                            {
                                foreach (var det in foundDetailsInFile)
                                {
                                    result.SourceDetails.Add(new DetailInfo {
                                        DetailId = det,
                                        SourceFile = fileName,
                                        SourceType = detectedSourceType
                                    });
                                }
                            }
                            else
                            {
                                foreach (var det in foundDetailsInFile)
                                {
                                    if (!result.DetailDefinitions.ContainsKey(det))
                                    {
                                        result.DetailDefinitions.Add(det, fileName);
                                    }
                                }
                            }

                            tr.Commit();
                        }
                    }
                    catch
                    {
                    }
                }
            }

            // Section Mark cross-reference (delegate sang SectionScanner)
            result.SectionMarks = SectionScanner.CrossReference(sectionMarkSources, allTextByFile);

            return result;
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
