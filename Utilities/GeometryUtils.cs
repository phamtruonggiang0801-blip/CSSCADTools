using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace CSSCADTools.Utilities
{
    public static class GeometryUtils
    {
        /// <summary>
        /// Tính điểm trung bình cộng của tâm các đối tượng được chọn (Chuyển đổi từ VBA)
        /// </summary>
        public static void CalculateAverageMidpoint()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            // Cho phép chọn mọi loại đối tượng có GeometricExtents
            PromptSelectionResult selRes = ed.GetSelection();
            if (selRes.Status != PromptStatus.OK) return;

            double sumX = 0, sumY = 0, sumZ = 0;
            int count = 0;

            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                foreach (SelectedObject selObj in selRes.Value)
                {
                    Entity ent = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Entity;
                    if (ent != null)
                    {
                        try
                        {
                            // Lấy tâm hình học từ Extents của đối tượng
                            Extents3d extent = ent.GeometricExtents;
                            sumX += (extent.MinPoint.X + extent.MaxPoint.X) / 2;
                            sumY += (extent.MinPoint.Y + extent.MaxPoint.Y) / 2;
                            sumZ += (extent.MinPoint.Z + extent.MaxPoint.Z) / 2;
                            count++;
                        }
                        catch (Autodesk.AutoCAD.Runtime.Exception)
                        {
                            // Bỏ qua nếu đối tượng không có Extents (như một số loại Layer rác)
                        }
                    }
                }
                tr.Commit();
            }

            if (count > 0)
            {
                Point3d avgPoint = new Point3d(sumX / count, sumY / count, sumZ / count);
                string msg = $"--- GEOMETRY ANALYSIS ---\n\n" +
                             $"Objects analyzed: {count}\n" +
                             $"Average X: {Math.Round(avgPoint.X, 3)}\n" +
                             $"Average Y: {Math.Round(avgPoint.Y, 3)}\n" +
                             $"Average Z: {Math.Round(avgPoint.Z, 3)}";
                
                Application.ShowAlertDialog(msg);
            }
        }
    }
}