using System.Collections.Generic;
using System.Linq;
using CSSCADTools.Models;

namespace CSSCADTools.Utilities {
    /// <summary>Xuất BomScanResult ra 1 file .xlsx — bảng phẳng, gộp tất cả file DXF vào 1 sheet</summary>
    public static class BomExcelExporter {
        private static readonly string[] Headers = {
            "ITEM", "QTY", "DRW", "CODE", "DESCRIPTION", "TYPE",
            "SIZE / DIMENSIONS", "MATERIAL", "WEIGHT", "DELIVERY", "SOURCE FILE"
        };

        private static readonly double[] ColumnWidths = {
            8, 8, 10, 10, 35, 15, 20, 12, 10, 12, 30
        };

        public static void Export(BomScanResult scan, string outputPath) {
            var sheets = new List<MinimalXlsxWriter.SheetData>();

            var bomSheet = new MinimalXlsxWriter.SheetData {
                Name = "BOM",
                Headers = Headers,
                ColumnWidths = ColumnWidths,
                FreezeHeaderRow = true,
                AutoFilter = true
            };

            foreach (var item in scan.Items.OrderBy(i => i.SourceFile).ThenBy(i => i.Item)) {
                bomSheet.Rows.Add(new[] {
                    item.Item, item.Qty, item.Drw, item.Code, item.Description, item.Type,
                    item.Size, item.Material, item.Weight, item.Delivery, item.SourceFile
                });
            }

            sheets.Add(bomSheet);

            if (scan.Items.Count > 0) {
                sheets.Add(BomTemplateExporter.Build(scan));
            }

            if (scan.Warnings.Count > 0) {
                var warnSheet = new MinimalXlsxWriter.SheetData {
                    Name = "Warnings",
                    Headers = new[] { "WARNING" },
                    ColumnWidths = new double[] { 80 }
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
