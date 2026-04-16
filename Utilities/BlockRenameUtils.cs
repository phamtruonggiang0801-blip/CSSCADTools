using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace CSSCADTools.Utilities
{
    public static class BlockRenameUtils
    {
        /// <summary>
        /// Quét chọn nhiều Block Reference và đổi tên hàng loạt.
        /// Tự động đệ quy vào các Nested Block bên trong để đổi tên luôn.
        /// </summary>
        public static void RandomRenameBlock()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            // Filter: chỉ lấy INSERT (Block Reference)
            TypedValue[] filter = new TypedValue[] { new TypedValue((int)DxfCode.Start, "INSERT") };
            SelectionFilter selFilter = new SelectionFilter(filter);

            PromptSelectionOptions selOpt = new PromptSelectionOptions();
            selOpt.MessageForAdding = "\nQuét chọn các Block cần đổi tên (bao gồm cả Nested):";

            PromptSelectionResult selRes = ed.GetSelection(selOpt, selFilter);
            if (selRes.Status != PromptStatus.OK) return;

            using (DocumentLock docLock = doc.LockDocument())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // Tập BTR cần đổi tên — HashSet để dedupe và tránh cycle đệ quy vô tận
                HashSet<ObjectId> targetBtrIds = new HashSet<ObjectId>();

                // Bước 1: Từ các BlockReference được chọn, thu thập BTR + đệ quy vào nested
                foreach (SelectedObject selObj in selRes.Value)
                {
                    BlockReference br = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as BlockReference;
                    if (br == null) continue;

                    ObjectId rootBtrId = br.IsDynamicBlock ? br.DynamicBlockTableRecord : br.BlockTableRecord;
                    CollectNestedBlocks(rootBtrId, tr, targetBtrIds);
                }

                // Bước 2: Đổi tên tất cả BTR đã thu thập
                int renamed = 0;
                int skipped = 0;

                foreach (ObjectId btrId in targetBtrIds)
                {
                    BlockTableRecord btr = tr.GetObject(btrId, OpenMode.ForWrite) as BlockTableRecord;
                    if (btr == null) continue;

                    if (btr.IsAnonymous || btr.IsLayout || btr.IsFromExternalReference)
                    {
                        skipped++;
                        continue;
                    }

                    string oldName = btr.Name;
                    string newName = "CSS_" + Guid.NewGuid().ToString().Substring(0, 8).ToUpper();

                    try
                    {
                        btr.Name = newName;
                        ed.WriteMessage($"\n  [{renamed + 1}] '{oldName}' → '{newName}'");
                        renamed++;
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
                    {
                        ed.WriteMessage($"\n  [!] Không đổi được '{oldName}': {ex.Message}");
                        skipped++;
                    }
                }

                tr.Commit();

                ed.WriteMessage($"\n=== Hoàn tất: {renamed} block đã đổi tên (gồm nested), {skipped} bỏ qua ===");
            }
        }

        /// <summary>
        /// Đệ quy duyệt vào BlockTableRecord, thu thập BTR của nó và mọi nested BlockReference bên trong.
        /// HashSet ngăn cycle — nếu btrId đã có trong set thì dừng nhánh đệ quy.
        /// </summary>
        private static void CollectNestedBlocks(ObjectId btrId, Transaction tr, HashSet<ObjectId> collected)
        {
            if (btrId.IsNull || collected.Contains(btrId)) return;
            collected.Add(btrId);

            BlockTableRecord btr = tr.GetObject(btrId, OpenMode.ForRead) as BlockTableRecord;
            if (btr == null) return;

            // Không đệ quy vào XRef hoặc Layout (không phải nested thật sự)
            if (btr.IsFromExternalReference || btr.IsLayout) return;

            foreach (ObjectId entId in btr)
            {
                BlockReference nestedBr = tr.GetObject(entId, OpenMode.ForRead) as BlockReference;
                if (nestedBr == null) continue;

                ObjectId nestedBtrId = nestedBr.IsDynamicBlock
                    ? nestedBr.DynamicBlockTableRecord
                    : nestedBr.BlockTableRecord;

                CollectNestedBlocks(nestedBtrId, tr, collected);
            }
        }
    }
}
