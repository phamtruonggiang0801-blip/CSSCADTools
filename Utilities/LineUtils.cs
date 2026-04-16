using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using System;

namespace CSSCADTools.Utilities
{
    public static class LineUtils
    {
        public static void CalculateTotalLength()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            // Step 1: Chọn đối tượng (tương đương sset trong VBA)
            TypedValue[] filter = new TypedValue[] { new TypedValue((int)DxfCode.Start, "LINE") };
            SelectionFilter selFilter = new SelectionFilter(filter);
            PromptSelectionResult selRes = ed.GetSelection(selFilter);

            if (selRes.Status != PromptStatus.OK) return;

            double totalLength = 0;
            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                foreach (SelectedObject selObj in selRes.Value)
                {
                    Line ln = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Line;
                    if (ln != null)
                    {
                        totalLength += ln.Length;
                    }
                }
                tr.Commit();
            }

            // Step 2: Hiển thị kết quả (tương đương MsgBox trong VBA)
            Application.ShowAlertDialog(
                $"--- SELECTION SUMMARY ---\n\n" +
                $"Total Lines Selected: {selRes.Value.Count}\n" +
                $"Total Length: {Math.Round(totalLength, 2)} units"
            );
        }
    }
}