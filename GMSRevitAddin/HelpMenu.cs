using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI.Events;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB.Visual;
using System.Windows.Forms;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace HelpMenu
{
    /// <summary>
    /// Opens the add-in's compiled help file (<see cref="GMSRevitAddin.GmsPaths.HelpFile"/>,
    /// a .chm shipped alongside the DLL) in the OS default viewer.
    ///
    /// NOTE: per project history, the "Help" ribbon button and its KeyboardShortcuts.xml
    /// registration were removed (2026-06-30) — this command class was intentionally left in
    /// place rather than deleted, so it currently has no ribbon entry point and is effectively
    /// dead code (see the commented-out button block in GMS_tools.cs).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Help : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string path = GMSRevitAddin.GmsPaths.HelpFile;
            if (File.Exists(path))
            {
                try
                {
                    // Hand off to the OS's registered .chm viewer rather than parsing it ourselves.
                    System.Diagnostics.Process.Start(path);
                }
                catch
                {
                    MessageBox.Show("Unable to open help file.", "HM1 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                MessageBox.Show("Unable to locate help file.", "HM2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return Result.Succeeded;
        }
    }
}
