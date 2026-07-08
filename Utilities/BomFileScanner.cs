using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.AutoCAD.DatabaseServices;
using CSSCADTools.Models;

namespace CSSCADTools.Utilities {
    /// <summary>
    /// Quét nhiều file DWG/DXF, tìm block "SW_TABLEANNOTATION_*" trong mỗi file
    /// và gom toàn bộ BOM item lại thành 1 kết quả duy nhất.
    /// SolidWorks đánh số thứ tự block table annotation theo số bảng có trong bản vẽ
    /// (_0, _1, _2...) nên bảng BOM thật KHÔNG cố định luôn ở "_1" — phải dò tất cả
    /// block cùng tiền tố và chỉ giữ lại block nào parse ra được header hợp lệ.
    /// </summary>
    public static class BomFileScanner {
        private const string BOM_BLOCK_PREFIX = "SW_TABLEANNOTATION_";

        // eProperClassSeparatorExpected (và 1 số lỗi DxfIn khác) đã quan sát thấy KHÔNG cố định
        // theo nội dung file — cùng 1 file lúc lỗi lúc không giữa các lần chạy khác nhau trong
        // cùng phiên AutoCAD. Đây là dấu hiệu lỗi mang tính trạng thái phiên làm việc (session-state)
        // của bộ nhập DXF trong AutoCAD, không phải file bị hỏng thật. Vì vậy thử lại với 1
        // Database hoàn toàn mới trước khi báo lỗi vĩnh viễn.
        private const int MAX_ATTEMPTS = 3;

        public static BomScanResult ScanFiles(string[] files) {
            var result = new BomScanResult();

            foreach (string file in files) {
                string fileName = Path.GetFileName(file);
                bool isDwg = string.Equals(Path.GetExtension(file), ".dwg", StringComparison.OrdinalIgnoreCase);
                string logPath = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(file) + "_dxfin.log");

                bool success = false;
                Exception lastException = null;

                for (int attempt = 1; attempt <= MAX_ATTEMPTS && !success; attempt++) {
                    using (Database db = new Database(false, true)) {
                        try {
                            if (isDwg) {
                                db.ReadDwgFile(file, FileOpenMode.OpenForReadAndAllShare, true, "");
                            } else {
                                db.DxfIn(file, logPath);
                            }

                            using (Transaction tr = db.TransactionManager.StartTransaction()) {
                                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

                                var candidateBlocks = new List<BlockTableRecord>();
                                foreach (ObjectId btrId in bt) {
                                    BlockTableRecord candidate = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                                    if (candidate.Name.StartsWith(BOM_BLOCK_PREFIX, StringComparison.OrdinalIgnoreCase)) {
                                        candidateBlocks.Add(candidate);
                                    }
                                }

                                if (candidateBlocks.Count == 0) {
                                    result.Warnings.Add($"{fileName}: không tìm thấy block nào có tiền tố {BOM_BLOCK_PREFIX}*");
                                    tr.Commit();
                                    success = true;
                                    continue;
                                }

                                var fileItems = new List<BomItem>();
                                var perBlockWarnings = new List<string>();
                                string materialNote = null;

                                foreach (var btr in candidateBlocks) {
                                    var items = BomTableParser.Parse(btr, tr, out string warning);

                                    if (items.Count > 0) {
                                        fileItems.AddRange(items);
                                    } else if (BomTableParser.TryParseMaterialNote(btr, tr, out string noteMaterial)) {
                                        // Block này KHÔNG phải bảng BOM lỗi — nó vốn dĩ chỉ là ghi
                                        // chú Material tự do (vd "Material: Q345D or equivalent")
                                        // tách riêng khỏi bảng BOM chính. KHÔNG ghi cảnh báo cho
                                        // block này, vì đây là hành vi bình thường, không phải lỗi.
                                        materialNote = noteMaterial;
                                    } else if (warning != null) {
                                        perBlockWarnings.Add($"{btr.Name}: {warning}");
                                    }
                                }

                                // Bản vẽ có đúng 1 item (đại diện cho chính bản vẽ đó) và item này
                                // đang thiếu Material, đồng thời tìm được ghi chú Material riêng ở
                                // 1 block khác trong CÙNG file -> điền vào. Chỉ áp dụng khi file có
                                // ĐÚNG 1 item để tránh áp nhầm cho item không liên quan khi 1 file
                                // có nhiều item.
                                if (materialNote != null && fileItems.Count == 1 && string.IsNullOrWhiteSpace(fileItems[0].Material)) {
                                    fileItems[0].Material = materialNote;
                                }

                                foreach (var item in fileItems) {
                                    item.SourceFile = fileName;
                                    result.Items.Add(item);
                                }

                                // Chỉ báo cảnh báo khi KHÔNG có block nào trong file cho ra dữ liệu —
                                // vì các block phụ (ghi chú, bảng khác) không parse được là bình thường,
                                // chỉ cần ít nhất 1 block chứa bảng BOM thật là đủ.
                                if (fileItems.Count == 0) {
                                    foreach (var w in perBlockWarnings) {
                                        result.Warnings.Add($"{fileName}: {w}");
                                    }
                                }

                                tr.Commit();
                                success = true;
                            }
                        }
                        catch (Exception ex) {
                            lastException = ex;
                        }
                    }
                }

                if (!success) {
                    // Log chi tiết chỉ tồn tại với DXF (DxfIn có tham số log riêng, ReadDwgFile thì không)
                    string logNote = isDwg ? "" : $" (xem log chi tiết: {logPath})";
                    result.Warnings.Add($"{fileName}: lỗi đọc file sau {MAX_ATTEMPTS} lần thử — {lastException?.Message}{logNote}");
                }
            }

            return result;
        }
    }
}
