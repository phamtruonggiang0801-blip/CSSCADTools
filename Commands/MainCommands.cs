using System;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using CSSCADTools.Views;

[assembly: CommandClass(typeof(CSSCADTools.Commands.MainCommands))]

namespace CSSCADTools.Commands
{
    public class MainCommands
    {
        private static PaletteSet _ps = null;

        [CommandMethod("CSSTools")]
        public void ShowPalette()
        {
            try
            {
                if (_ps == null)
                {
                    // Sử dụng một GUID cố định để AutoCAD ghi nhớ vị trí Palette
                    _ps = new PaletteSet("CSS CAD TOOLS", new Guid("D6B5D7A1-4F2B-4A8B-9E1C-6C5D4E3F2A1B"));
                    _ps.Size = new System.Drawing.Size(250, 500);
                    _ps.DockEnabled = (DockSides)((int)DockSides.Left + (int)DockSides.Right);
                    
                    // Khởi tạo View
                    MainPaletteControl control = new MainPaletteControl();
                    _ps.AddVisual("Tools", control);
                }
                
                _ps.Visible = true;
            }
            catch (System.Exception ex)
            {
                Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\nError: " + ex.Message);
            }
        }
    }
}