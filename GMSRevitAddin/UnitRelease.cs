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
using Autodesk.Revit.UI.Events;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB.Visual;
using Autodesk.Revit.DB.Events;
using System.Windows.Forms;
using View = Autodesk.Revit.DB.View;
using System.IO;
using System.Diagnostics;

namespace UnitRelease
{
    /// <summary>
    /// Collects all unit sheets (sheet titles containing "U-") in the project and shows the
    /// <c>UnitReleaseForm</c> dialog so the user can pick which units to mark as released.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class UnitRelease : IExternalCommand
    {
        public class UnitInfo
        {
            public string sheetName { get; set; }
            public string description { get; set; }
            public string releaseDate { get; set; }
            public string elementID { get; set; }
        }

        public static List<UnitInfo> unitSheets = new List<UnitInfo>();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            Autodesk.Revit.DB.Document doc = uiApp.ActiveUIDocument.Document;
            Autodesk.Revit.ApplicationServices.Application app = doc.Application;
            UIApplication uiapp = new UIApplication(app);
            UIDocument uidoc = new UIDocument(doc);

            unitSheets.Clear();
            unitSheets = collectUnitSheets(doc);

            if (unitSheets.Any())
            {
                System.Windows.Forms.Form unitForm = new UnitReleaseForm.UnitReleaseForm(unitSheets, commandData);
                unitForm.ShowDialog();
            }
            else
            {
                MessageBox.Show("No unit sheets found in project", "UR1 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return Result.Succeeded;
        }

        /// <summary>
        /// Returns basic info (sheet number, description, current issue date, element id) for every
        /// sheet whose title contains "U-" (i.e. a unit sheet).
        /// </summary>
        public static List<UnitInfo> collectUnitSheets(Autodesk.Revit.DB.Document doc)
        {
            List<UnitInfo> units = new List<UnitInfo>();
            units.Clear();
            FilteredElementCollector fec = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Sheets);
            fec.WhereElementIsNotElementType();
            List<Element> sheetList = (List<Element>)fec.ToElements().ToList();

            foreach (Element el in sheetList)
            {
                ViewSheet vs = el as ViewSheet;
                if (vs.Title.Contains("U-"))
                {
                    UnitInfo temp = new UnitInfo();
                    temp.sheetName = vs.SheetNumber;
                    temp.elementID = vs.Id.ToString();
                    ParameterSet ps = vs.Parameters;
                    int ticker = 0;
                    // Stop once both "Sheet Name" and "Sheet Issue Date" have been read.
                    foreach (Parameter p in ps)
                    {
                        if (ticker >= 2)
                        {
                            break;
                        }
                        else if (p.Definition.Name == "Sheet Name")
                        {
                            temp.description = p.AsString();
                            ticker++;
                        }
                        else if (p.Definition.Name == "Sheet Issue Date")
                        {
                            temp.releaseDate = p.AsString();
                            ticker++;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(temp.sheetName))
                    {
                        units.Add(temp);
                    }
                }
            }
            return units;
        }

    }

    /// <summary>
    /// Applies the release date chosen in <c>UnitReleaseForm</c> to the selected unit sheets'
    /// "Sheet Issue Date" and appends " - Released" to their "Grouping - Usage" sheet set, marks the
    /// matching curtain wall panels' "Unit - Released" parameter, and (optionally) batch-exports the
    /// released sheets to a combined PDF.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class UnitReleaseUpdate : IExternalCommand
    {
        // Populated by UnitReleaseForm before Execute runs: release date, chosen sheets
        // (element id string -> unit/sheet number), and whether to export a PDF afterward.
        public static string date { get; set; }
        public static Dictionary<string, string> units { get; set; }
        public static bool printPDF { get; set; }

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            Autodesk.Revit.DB.Document doc = uiApp.ActiveUIDocument.Document;
            Autodesk.Revit.ApplicationServices.Application app = doc.Application;
            UIApplication uiapp = new UIApplication(app);
            UIDocument uidoc = new UIDocument(doc);
            List<string> unitsToUpdate = new List<string>();

            using (Transaction tPS = new Transaction(doc, "Update Sheet Issue Date"))
            {
                tPS.Start();
                // For each selected sheet: set its issue date, and append " - Released" to its
                // "Grouping - Usage" sheet set (unless already marked released).
                foreach (string idNumber in units.Keys)
                {
                    try
                    {
                        long number = Convert.ToInt64(idNumber);
                        ElementId elid = new ElementId(number);
                        Element el = doc.GetElement(elid);
                        Parameter pr = el.get_Parameter(BuiltInParameter.SHEET_ISSUE_DATE);
                        if (pr != null)
                        {
                            pr.Set(date);
                            unitsToUpdate.Add(units[idNumber]);
                        }
                    }
                    catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("UnitRelease", __ex); }
                    try
                    {
                        long number = Convert.ToInt64(idNumber);
                        ElementId elid = new ElementId(number);
                        Element el = doc.GetElement(elid);
                        ParameterSet paras = el.Parameters;
                        Parameter SheetSet = null;
                        foreach (Parameter p in paras)
                        {
                            if (p.Definition.Name == "Grouping - Usage")
                            {
                                SheetSet = p;
                                break;
                            }
                        }
                        if (SheetSet != null)
                        {
                            string setType = SheetSet.AsString();
                            if(!setType.EndsWith(" - Released"))
                            {
                                SheetSet.Set(setType + " - Released");
                            }
                        }
                    }
                    catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("UnitRelease", __ex); }
                }

                // Flag each curtain wall panel belonging to a released unit ("Unit - Mark Number" matches
                // one of the selected unit numbers) by setting its "Unit - Released" parameter to 1.
                FilteredElementCollector cwPanels = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_CurtainWallPanels);

                string currentUnitNumber = "";
                foreach (Element el in cwPanels)
                {
                    try
                    {
                        currentUnitNumber = "";
                        Parameter mn = el.LookupParameter("Unit - Mark Number");
                        if (mn != null && !string.IsNullOrWhiteSpace(mn.AsString()))
                        {
                            currentUnitNumber = mn.AsString();
                            if (units.ContainsValue(mn.AsString()))
                            {
                                Parameter ur = el.LookupParameter("Unit - Released");
                                if (ur != null)
                                {
                                    ur.Set(1);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        GMSRevitAddin.GmsUi.ShowError("Error setting " + currentUnitNumber + " to released." + System.Environment.NewLine + ex.Message, "UR2 Error", ex);
                    }
                }
                foreach (String unit in unitsToUpdate)
                {

                }
                doc.Regenerate();
                tPS.Commit();
            }

            if (printPDF)
            {
                bool error = false;
                // Each user gets their own PDF output subfolder (under GmsPaths.PdfOutputRoot); clear it out
                // before exporting so old PDFs don't linger.
                string user = Environment.UserName.ToLower();
                if (!Directory.Exists(GMSRevitAddin.GmsPaths.PdfOutputRoot + user))
                {
                    try
                    {
                        Directory.CreateDirectory(GMSRevitAddin.GmsPaths.PdfOutputRoot + user);
                    }
                    catch
                    {
                        error = true;
                        MessageBox.Show("Can not access your PDF_Output directory. Close the program and try again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else if (Directory.Exists(GMSRevitAddin.GmsPaths.PdfOutputRoot + user))
                {
                    try
                    {
                        DirectoryInfo di = new DirectoryInfo(GMSRevitAddin.GmsPaths.PdfOutputRoot + user);
                        foreach (FileInfo file in di.EnumerateFiles())
                        {
                            file.Delete();
                        }
                        foreach (DirectoryInfo dir in di.EnumerateDirectories())
                        {
                            dir.Delete(true);
                        }
                    }
                    catch
                    {
                        error = true;
                        MessageBox.Show("Can not delete existing files in " + GMSRevitAddin.GmsPaths.PdfOutputRoot + user + ". Most likely reason is there is a file in the folder that is open. Export to PDF is cancelled. Export the Units to PDF manually.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }

                if (!error)
                {
                    // Build the list of sheet views (the selected unit sheets) to include in the combined PDF.
                    IList<ElementId> views = new List<ElementId>();
                    foreach (string idNumber in units.Keys)
                    {
                        try
                        {
                            long number = Convert.ToInt64(idNumber);
                            ElementId elid = new ElementId(number);
                            views.Add(elid);
                        }
                        catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("UnitRelease", __ex); }
                    }

                    using (Transaction tPDF = new Transaction(doc, "PDF Units"))
                    {
                        tPDF.Start();

                        // PDF export settings: grayscale, 300 DPI, ANSI B landscape, combined into a single file.
                        PDFExportOptions options = new PDFExportOptions();
                        options.AlwaysUseRaster = true;
                        options.ColorDepth = ColorDepthType.GrayScale;
                        options.Combine = true;
                        options.ExportQuality = PDFExportQualityType.DPI300;
                        options.FileName = DateTime.Now.ToString("yyyyMMdd") + " - Released Units "; //filename if combine is true
                        options.HideCropBoundaries = true;
                        options.HideReferencePlane = true;
                        options.HideScopeBoxes = true;
                        options.HideUnreferencedViewTags = true;
                        options.MaskCoincidentLines = true;
                        options.PaperFormat = ExportPaperFormat.ANSI_B; //11x17
                        options.PaperOrientation = PageOrientationType.Landscape;
                        options.PaperPlacement = PaperPlacementType.Center;
                        options.RasterQuality = RasterQualityType.Medium;
                        options.ReplaceHalftoneWithThinLines = true;
                        options.StopOnError = true;
                        options.ViewLinksInBlue = true;
                        options.ZoomType = ZoomType.Zoom;
                        options.ZoomPercentage = 100;

                        // Combined-file naming rule uses the sheet number (unused when Combine == true, but required by the API).
                        var sn = TableCellCombinedParameterData.Create();
                        sn.ParamId = new ElementId(BuiltInParameter.SHEET_NUMBER);

                        var table = new List<TableCellCombinedParameterData>
                        {
                            sn
                        };
                        options.SetNamingRule(table);

                        doc.Export(GMSRevitAddin.GmsPaths.PdfOutputRoot + user, views, options);

                        tPDF.Commit();

                        // Open the output folder for the user once the export finishes.
                        try
                        {
                            Process.Start(GMSRevitAddin.GmsPaths.PdfOutputRoot + user);
                        }
                        catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("UnitRelease", __ex); }
                    }
                }
            }
            return Result.Succeeded;
        }
    }
}