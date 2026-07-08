using System.Collections.Generic;
using System.Linq;
using CSSCADTools.Models;

namespace CSSCADTools.Utilities {
    /// <summary>
    /// Xuất BomScanResult ra 1 file .xlsx. Sheet "ExportedData" là NGUỒN DỮ LIỆU DUY NHẤT
    /// (one true source) — các sheet khác (Summary, Material List, Assembly List) dùng CÔNG
    /// THỨC Excel (tham chiếu trực tiếp / SUMIFS / COUNTIFS / TEXTJOIN) trỏ ngược lại đây,
    /// KHÔNG copy giá trị tĩnh — sửa 1 dòng trong ExportedData thì mọi sheet khác tự cập nhật.
    /// 2 cột cuối (MATERIAL COLUMN KEY, DRAWING NO) là cột helper phục vụ công thức tra cứu,
    /// giá trị do code tính sẵn (SteelSpecClassifier / trích số hiệu bản vẽ) vì bản thân việc
    /// phân loại/parse Size là thuật toán, không thể diễn đạt gọn bằng công thức Excel thường.
    /// </summary>
    public static class BomExcelExporter {
        private static readonly string[] Headers = {
            "ITEM", "QTY", "DRW", "CODE", "DESCRIPTION", "TYPE",
            "SIZE / DIMENSIONS", "MATERIAL", "WEIGHT", "DELIVERY", "SOURCE FILE",
            "MATERIAL COLUMN KEY", "DRAWING NO"
        };

        private static readonly double[] ColumnWidths = {
            8, 8, 10, 10, 35, 15, 20, 12, 10, 12, 30, 20, 12
        };

        public static void Export(BomScanResult scan, string outputPath) {
            var sheets = new List<MinimalXlsxWriter.SheetData>();
            var layout = new ExportedDataLayout();

            // Bước 1 — ĐỌC TOÀN BỘ + validate + ép kiểu, nạp vào 1 List DUY NHẤT trước khi
            // đụng tới Excel. QTY/WEIGHT là double? THẬT SỰ trong ExportedDataRow nên khi bỏ
            // vào object[] bên dưới, MinimalXlsxWriter tự nhận diện ra ô Số — không cần khai
            // báo trước cột nào là số nữa.
            var exportedRows = ExportedDataBuilder.Build(scan.Items);

            var exportedDataSheet = new MinimalXlsxWriter.SheetData {
                Name = ExportedDataLayout.SheetName,
                Headers = Headers,
                ColumnWidths = ColumnWidths,
                FreezeHeaderRow = true,
                AutoFilter = true,
                // Ẩn — user chỉ cần xem Summary/Assembly List; ExportedData vẫn là "one true
                // source" đầy đủ công thức, chỉ giấu tab đi, không xóa/không ảnh hưởng tính toán.
                Hidden = true
            };

            // Bước 2 — GHI ĐỒNG LOẠT (bulk write): chỉ duyệt List đã chuẩn hóa để dựng object[],
            // không đọc/parse lại BomItem ở đây.
            int excelRow = ExportedDataLayout.FirstDataRow;
            foreach (var row in exportedRows) {
                exportedDataSheet.Rows.Add(new object[] {
                    row.Item, row.Qty, row.Drw, row.Code, row.Description, row.Type,
                    row.Size, row.Material, row.Weight, row.Delivery, row.SourceFile,
                    row.MaterialColumnKey, row.DrawingNo
                });

                layout.RowOf[row.Source] = excelRow;
                excelRow++;
            }

            layout.LastDataRow = excelRow - 1;

            sheets.Add(exportedDataSheet);

            if (scan.Items.Count > 0) {
                // BomTemplateExporter.Build PHẢI chạy TRƯỚC BomSummaryExporter.Build — nó dựng
                // layout của sheet Assembly List (biết chính xác Excel row của từng ô Qty
                // header) và ghi vào "rollup", để Summary tham chiếu CHÉO SHEET tới đúng ô đó
                // khi tính TOTAL QTY/WEIGHT toàn dự án. Thứ tự TAB hiển thị (Summary trước
                // Material List/Assembly List) không đổi — chỉ đổi thứ tự TÍNH TOÁN.
                var rollup = new AssemblyRollupContext();
                var templateSheets = BomTemplateExporter.Build(scan, layout, rollup);

                // Chỉ để lộ 2 sheet: Summary và Assembly List — Material List là sheet trung
                // gian (Assembly List đã tra cứu chéo NVL từ đó), user không cần thao tác trực
                // tiếp trên nó, nên ẩn đi giống ExportedData.
                foreach (var s in templateSheets) {
                    if (s.Name == "Material List") s.Hidden = true;
                }

                sheets.Add(BomSummaryExporter.Build(scan, layout, rollup));
                sheets.AddRange(templateSheets);
            }

            if (scan.Warnings.Count > 0) {
                var warnSheet = new MinimalXlsxWriter.SheetData {
                    Name = "Warnings",
                    Headers = new[] { "WARNING" },
                    ColumnWidths = new double[] { 80 },
                    // Vẫn ẩn — nhưng KHÔNG xóa: nếu có cảnh báo thật (vd vòng lặp tham chiếu,
                    // thiếu bản vẽ), user cần chủ động unhide để xem khi cần chẩn đoán.
                    Hidden = true
                };

                foreach (var w in scan.Warnings) {
                    warnSheet.Rows.Add(new[] { w });
                }

                sheets.Add(warnSheet);
            }

            MinimalXlsxWriter.Write(outputPath, sheets);
        }
    }
}
