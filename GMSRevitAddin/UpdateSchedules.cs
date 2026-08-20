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
using System.Threading;
using System.Windows.Forms;
//using System.Data.OleDb;
using System.Data.Odbc;
using System.IO;
using System.Diagnostics;
using Microsoft.VisualBasic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Shapes;
using PieceDescriptions;
using Line = Autodesk.Revit.DB.Line;
using DuplicateViewOptions;
//using System.Web.UI.WebControls;
using Parameter = Autodesk.Revit.DB.Parameter;
using System.Linq.Expressions;
using System.Security.Policy;
using System.Windows.Media.Converters;
using System.Drawing.Drawing2D;
using System.Windows.Media.Media3D;
using System.Windows.Media;

namespace UpdateSchedules
{
    /// <summary>
    /// Rebuilds the Extrusion schedule's preview images and weight/material data. Collects every
    /// "Extrusion*" family instance in the model whose family name ends in a numeric die number,
    /// looks each die up in the GMS Extrusion Access DB ("Extrusion Data" table) for Weight/Alloy/
    /// Temper, writes those into the matching detail-component type's "Schedule - Weight / Ft" and
    /// "Schedule - Material" parameters, and (via <see cref="createImages"/>) regenerates that type's
    /// preview image. Called directly by its own ribbon button and as a step of <see cref="UpdateAll"/>.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class UpdateExtrusions : IExternalCommand
    {
        public static List<FamilySymbol> extrusionList = new List<FamilySymbol>();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            Autodesk.Revit.DB.Document doc = uiApp.ActiveUIDocument.Document;
            Autodesk.Revit.ApplicationServices.Application app = doc.Application;
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            bool error = false;
            Dictionary<string, ElementId> foundDetailItems = new Dictionary<string, ElementId>();

            extrusionList.Clear();

            FilteredElementCollector fec = new FilteredElementCollector(doc);
            fec = fec.OfClass(typeof(FamilyInstance));
            List<Element> families = fec.ToList();
            List<string> names = new List<string>();

            foreach (Element fam in families)
            {
                FamilyInstance fi = fam as FamilyInstance;
                FamilySymbol fs = fi.Symbol;
                string familyName = fi.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM).AsValueString();

                if (!string.IsNullOrWhiteSpace(familyName))
                {
                    if (familyName.ToLower().StartsWith("extrusion") && !familyName.ToLower().Contains("mod") && !familyName.ToLower().Contains("new") && !extrusionList.Contains(fs) && !names.Contains(familyName.ToLower()))
                    {
                        string[] parse = familyName.Split('-');
                        if (parse.Length == 2 || (parse.Length == 3 && familyName.ToLower().Contains("gmd")))
                        {
                            // The last segment is expected to be a numeric die number; non-numeric names
                            // (e.g. "Extrusion - ZVA001", "Tube Die") simply aren't extrusions to collect.
                            // TryParse avoids the per-item FormatException that Convert.ToInt64 threw for
                            // every non-numeric family (log spam, no functional effect).
                            if (long.TryParse(parse[parse.Length - 1].Trim(), out long test) && test > 0)
                            {
                                names.Add(familyName.ToLower());
                                extrusionList.Add(fs);
                            }
                        }
                    }
                }
            }

            using (ProgressForm.ProgressForm pgfm = new ProgressForm.ProgressForm("Update Extrusion Schedule", null, extrusionList.Count() + 3, string.Empty))
            {
                pgfm.IncrementWithText("Collecting Extrusion Data");
                string DBPath = GMSRevitAddin.GmsPaths.ExtrusionDatabase;
                if (!File.Exists(DBPath))
                {
                    pgfm.Close();
                    MessageBox.Show("Could not locate the extrusion database.Operation cancelled.", "US1 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return Result.Cancelled;
                }
                else
                {
                    // Work off a copy of the DB in %TEMP%\GMS rather than the shared file directly, so
                    // this process's ODBC connection doesn't hold a lock on the live file on the share.
                    string winTempFolder = System.IO.Path.GetTempPath();
                    string GMSPath = System.IO.Path.Combine(winTempFolder, "GMS");
                    string DBTempPath = System.IO.Path.Combine(GMSPath, "GMS Extrusion Database.accdb");

                    if (!Directory.Exists(GMSPath))
                    {
                        try
                        {
                            Directory.CreateDirectory(GMSPath);
                        }
                        catch
                        {
                            error = true;
                            pgfm.Close();
                            MessageBox.Show("Error creating temporary directory.", "US2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    if (!error)
                    {
                        File.Copy(DBPath, DBTempPath, true);
                    }

                    Dictionary<string, List<string>> extrusionData = new Dictionary<string, List<string>>();
                    if (!error && extrusionList.Any())
                    {
                        extrusionData.Clear();
                        //OleDbCommand cmd = new OleDbCommand();
                        //OleDbConnection cn = new OleDbConnection();

                        OdbcCommand cmd = new OdbcCommand();
                        OdbcConnection cn = new OdbcConnection();

                        // ODBC connection string (NOT the OLEDB "Provider=..." form, which an OdbcConnection
                        // rejects with IM002 "no default driver specified"). Requires the Microsoft Access
                        // ODBC driver (ships with the Access Database Engine redistributable).
                        cn.ConnectionString = @"Driver={Microsoft Access Driver (*.mdb, *.accdb)};Dbq=" + DBTempPath + ";";
                        cmd.Connection = cn;

                        // Test connection: probe a known die so a bad driver/path/permission fails fast,
                        // before spending time looping over every extrusion family below.
                        string test = "";
                        try
                        {
                            cn.Open();
                            string comStringTest = "SELECT Weight, Alloy, Temper FROM [Extrusion Data] WHERE [Die Number] = 'GMD-0117'";
                            using (OdbcCommand commandTest = new OdbcCommand(comStringTest, cn))
                            using (OdbcDataReader readerTest = commandTest.ExecuteReader())
                            {
                                while (readerTest.Read())
                                {
                                    test = readerTest.GetValue(0).ToString();
                                }
                            }
                            cn.Close();
                            // ACE (the Access driver) keeps holding a file lock on the .accdb for a moment
                            // after Close() — Sleep + ReleaseObjectPool + GC.Collect deliberately forces the
                            // underlying connection pool/finalizers to let go so the temp copy can be
                            // deleted/re-copied later without an IOException.
                            System.Threading.Thread.Sleep(500);
                            //OleDbConnection.ReleaseObjectPool();
                            OdbcConnection.ReleaseObjectPool();
                            System.GC.Collect();
                        }
                        catch (Exception ex)
                        {
                            error = true;
                            pgfm.Close();
                            GMSRevitAddin.GmsUi.ShowError("Error connecting to extrusion database." + System.Environment.NewLine + ex.Message, "Update Schedules Error", ex);
                        }
                        if (string.IsNullOrWhiteSpace(test))
                        {
                            error = true;
                            pgfm.Close();
                            MessageBox.Show("Error connecting to extrusion database.");
                        }

                        if (!error)
                        {
                            foreach (FamilySymbol famSym in extrusionList)
                            {
                                pgfm.IncrementWithText("Collecting Extrusion Data - " + famSym.FamilyName);
                                try
                                {
                                    string dieNum = famSym.FamilyName.Split('-')[1].Trim();
                                    if (dieNum.ToUpper() == "GMD")
                                    {
                                        dieNum = "GMD-" + famSym.FamilyName.Split('-')[2].Trim();
                                    }
                                    cn.Open();
                                    // Note: die number is built from the family name, not user input, but it
                                    // is still string-concatenated into the SQL rather than parameterized here.
                                    string comString = "SELECT Weight, Alloy, Temper FROM [Extrusion Data] WHERE [Die Number] = '" + dieNum + "'";
                                    using (OdbcCommand command = new OdbcCommand(comString, cn))
                                    using (OdbcDataReader reader = command.ExecuteReader())
                                    {
                                        // list[0]=Weight, list[1]=Alloy, list[2]=Temper — order relied on below
                                        // when writing "Schedule - Weight / Ft" / "Schedule - Material".
                                        while (reader.Read())
                                        {
                                            if (!extrusionData.ContainsKey(dieNum))
                                            {
                                                List<string> list = new List<string>();
                                                list.Add(reader.GetValue(0).ToString());
                                                list.Add(reader.GetValue(1).ToString());
                                                list.Add(reader.GetValue(2).ToString());
                                                extrusionData.Add(dieNum, list);
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    GMSRevitAddin.GmsUi.ShowError(ex, "Update Schedules Error");
                                }
                                cn.Close();
                                // Same ACE-lock-release workaround as above, repeated per die query so the
                                // connection/file handle doesn't accumulate across the loop.
                                System.Threading.Thread.Sleep(500);
                                //OleDbConnection.ReleaseObjectPool();
                                OdbcConnection.ReleaseObjectPool();
                                System.GC.Collect();
                            }

                            FilteredElementCollector detailItems = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_DetailComponents).WhereElementIsElementType();
                            Dictionary<string, ElementId> collectedItems = new Dictionary<string, ElementId>();
                            foreach (Element el in detailItems)
                            {
                                try
                                {
                                    string famName = el.get_Parameter(BuiltInParameter.ALL_MODEL_FAMILY_NAME).AsString();
                                    if (famName.ToLower().Contains("extrusion") && !collectedItems.ContainsKey(famName))
                                    {
                                        collectedItems.Add(famName, el.Id);
                                    }
                                }
                                catch
                                {
                                    //MessageBox.Show("Extrusion already present in list." + System.Environment.NewLine + famName);
                                }
                            }

                            pgfm.IncrementWithText("Updating Extrusion Data");
                            if (!error)
                            {
                                using (Transaction tx = new Transaction(doc, "Update Extrusion Data"))
                                {
                                    tx.Start();
                                    foreach (KeyValuePair<string, List<string>> kvp in extrusionData)
                                    {
                                        foreach (KeyValuePair<string, ElementId> kv in collectedItems)
                                        {
                                            bool foundWeight = false;
                                            bool foundMaterial = false;

                                            if (kv.Key.Contains(kvp.Key))
                                            {
                                                ParameterSet ps = doc.GetElement(kv.Value).Parameters;
                                                foreach (Parameter p in ps)
                                                {
                                                    if (p.Definition.Name == "Schedule - Weight / Ft")
                                                    {
                                                        string weigth = kvp.Value[0].ToString();
                                                        if (!string.IsNullOrWhiteSpace(weigth))
                                                        {
                                                            try
                                                            {
                                                                p.Set(Convert.ToDouble(kvp.Value[0]));
                                                                foundWeight = true;
                                                            }
                                                            catch
                                                            {
                                                                pgfm.Close();
                                                                error = true;
                                                                MessageBox.Show("Error converting weight value to number.", "US3 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                                            }
                                                        }
                                                        else
                                                        {
                                                            try
                                                            {
                                                                p.Set(0);
                                                            }
                                                            catch
                                                            {
                                                                pgfm.Close();
                                                                error = true;
                                                                MessageBox.Show("Weight value not found and error setting paramenter to 0", "US4 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                                            }
                                                        }
                                                    }

                                                    if (p.Definition.Name == "Schedule - Material")
                                                    {
                                                        string finish = kvp.Value[1].ToString() + "-" + kvp.Value[2].ToString();
                                                        try
                                                        {
                                                            p.Set(finish);
                                                            foundMaterial = true;
                                                        }
                                                        catch
                                                        {
                                                            pgfm.Close();
                                                            error = true;
                                                            MessageBox.Show("Error setting Material parameter", "US5 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                                        }
                                                    }

                                                    if (foundWeight && foundMaterial)
                                                    {
                                                        string[] temp = kv.Key.Split('-');
                                                        if (temp.Length == 2 && !string.IsNullOrWhiteSpace(temp[1]))
                                                        {
                                                            try
                                                            {
                                                                double tempNum = Convert.ToDouble(temp[1].Trim());
                                                                if (temp[0].Trim().ToLower() == "extrusion")
                                                                {
                                                                    foundDetailItems.Add(kv.Key, kv.Value);
                                                                }
                                                            }
                                                            catch
                                                            {
                                                                //MessageBox.Show("Found non-standard item name." + System.Environment.NewLine + kv.Key);
                                                            }
                                                        }
                                                        else if (temp[1].ToLower().Trim() == "gmd" && temp.Length == 3 && !string.IsNullOrWhiteSpace(temp[2]))
                                                        {
                                                            try
                                                            {
                                                                double tempNum = Convert.ToDouble(temp[2].Trim());
                                                                foundDetailItems.Add(kv.Key, kv.Value);
                                                            }
                                                            catch
                                                            {
                                                                //MessageBox.Show("Found non-standard item name." + System.Environment.NewLine + kv.Key);
                                                            }

                                                        }
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    tx.Commit();
                                }
                            }
                        }
                    }
                    try
                    {
                        File.Delete(DBTempPath);
                    }
                    catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("UpdateSchedules", __ex); }
                }
                pgfm.IncrementWithText("Extrusion Data Updated");
            }

            if (!error && foundDetailItems.Any())
            {
                bool passed = createImages.create(doc, uidoc, foundDetailItems, "Extrusion");
                if (!passed)
                {
                    error = true;
                }
            }
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class UpdateFasteners : IExternalCommand
    {
        public static List<FamilySymbol> fastenerList = new List<FamilySymbol>();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            Autodesk.Revit.DB.Document doc = uiApp.ActiveUIDocument.Document;
            Autodesk.Revit.ApplicationServices.Application app = doc.Application;
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            bool error = false;
            string imagesDirectory = "";
            imagesDirectory = FindProjectFolder.FindProjectFolder.FindFolder(doc, "images");

            fastenerList.Clear();
            using (ProgressForm.ProgressForm pgfm = new ProgressForm.ProgressForm("Update Fastener Schedule", null, 30, string.Empty))
            {
                pgfm.IncrementWithText("Begin Update Fastener Schedule");

                FilteredElementCollector fec = new FilteredElementCollector(doc);
                fec = fec.OfClass(typeof(FamilyInstance));
                List<Element> families = fec.ToList();

                List<string> familyNames = new List<string>();

                foreach (Element fam in families)
                {
                    FamilyInstance fi = fam as FamilyInstance;
                    FamilySymbol fs = fi.Symbol;
                    string familyAndType = fi.get_Parameter(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM).AsValueString();

                    if (!string.IsNullOrWhiteSpace(familyAndType) && !familyNames.Contains(familyAndType))
                    {
                        if (familyAndType.ToLower().StartsWith("fastener") && !familyAndType.ToLower().Contains("plan"))
                        {
                            familyNames.Add(familyAndType);
                            fastenerList.Add(fs);
                        }
                    }
                }
            }

            if (!error && fastenerList.Any())
            {
                Dictionary<string, ElementId> fasteners = new Dictionary<string, ElementId>();

                using (ProgressForm.ProgressForm pgfm = new ProgressForm.ProgressForm("Update Fastener Schedule", null, 29, string.Empty))
                {
                    pgfm.IncrementWithText("Collecting Fasteners");

                    foreach (FamilySymbol famSym in fastenerList)
                    {
                        string name = famSym.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_NAME).AsValueString();
                        List<string> temp = name.Split('-').ToList();
                        if (temp.Count > 2 && !name.Contains(" "))
                        {
                            if (temp[0].ToLower() == "cu" && !fasteners.ContainsKey(name))
                            {
                                fasteners.Add("Fastener - " + name.Replace("\"", "in").Replace("/", "_").Replace("\\", "_"), famSym.Id);
                            }
                            else
                            {
                                try
                                {
                                    long test = Convert.ToInt64(temp[0]);
                                    if (test > 0 && !fasteners.ContainsKey(name))
                                    {
                                        fasteners.Add("Fastener - " + name.Replace("\"", "in").Replace("/", "_").Replace("\\", "_"), famSym.Id);
                                    }
                                }
                                catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("UpdateSchedules", __ex); }
                            }
                        }
                    }
                }

                //int pBarCount = (fasteners.Count() * 3) + 2;
                if (fasteners.Any())
                {
                    bool passed = createImages.create(doc, uidoc, fasteners, "Fastener");
                    if (!passed)
                    {
                        error = true;
                    }
                }
            }
                    return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class UpdateComponents : IExternalCommand
    {
        public static List<FamilySymbol> componentList = new List<FamilySymbol>();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            Autodesk.Revit.DB.Document doc = uiApp.ActiveUIDocument.Document;
            Autodesk.Revit.ApplicationServices.Application app = doc.Application;
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            bool error = false;
            string imagesDirectory = "";
            imagesDirectory = FindProjectFolder.FindProjectFolder.FindFolder(doc, "images");

            componentList.Clear();
            using (ProgressForm.ProgressForm pgfm = new ProgressForm.ProgressForm("Update Component Schedule", null, 30, string.Empty))
            {
                pgfm.IncrementWithText("Begin Update Component Schedule");

                FilteredElementCollector fec = new FilteredElementCollector(doc);
                fec = fec.OfClass(typeof(FamilyInstance));
                List<Element> families = fec.ToList();

                Dictionary<string, ElementId> familyNames = new Dictionary<string, ElementId>();

                foreach (Element fam in families)
                {
                    FamilyInstance fi = fam as FamilyInstance;
                    FamilySymbol fs = fi.Symbol;
                    string familyAndType = fi.get_Parameter(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM).AsValueString();

                    if (!string.IsNullOrWhiteSpace(familyAndType) && !familyNames.ContainsKey(familyAndType))
                    {
                        if (familyAndType.ToLower().StartsWith("component"))
                        {
                            familyNames.Add(familyAndType, fam.Id);
                            componentList.Add(fs);
                        }
                    }
                }
            }

            if (!error && componentList.Any())
            {
                Dictionary<string, ElementId> components = new Dictionary<string, ElementId>();

                using (ProgressForm.ProgressForm pgfm = new ProgressForm.ProgressForm("Update Component Schedule", null, 29, string.Empty))
                {
                    pgfm.IncrementWithText("Collecting Components");

                    foreach (FamilySymbol famSym in componentList)
                    {
                        string name = famSym.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_NAME).AsValueString();
                        string cleanName = "Component - " + name.Replace("\"", "in").Replace("/", "_").Replace("\'", "ft").Replace("\\", "_").Replace("{", "_").Replace("}", "_").Replace("*", "_").Replace("%", "_").Replace("@", "_").Replace(":", "_").Replace(";", "_");
                        if (!components.ContainsKey(cleanName))
                        {
                            components.Add(cleanName, famSym.Id);
                        }
                    }
                }

                //int pBarCount = (components.Count() * 2) + 2;
                if (components.Any())
                {
                    bool passed = createImages.create(doc, uidoc, components, "Component");
                    if (!passed)
                    {
                        error = true;
                    }
                }
            }
                    return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class UpdateAll : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            Autodesk.Revit.DB.Document doc = uiApp.ActiveUIDocument.Document;
            Autodesk.Revit.ApplicationServices.Application app = doc.Application;
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;

            var command = new UpdateExtrusions();
            command.Execute(commandData, ref message, elements);
            var command1 = new UpdateFasteners();
            command1.Execute(commandData, ref message, elements);
            var command2 = new UpdateComponents();
            command2.Execute(commandData, ref message, elements);
            // Run the new Cycle Worksets command from the GM Code folder (new implementation)
            try
            {
                var cycleCmd = new GMSRevitAddin.GMCode.CustomCommand.CycleWorkSets.CycleWorksetsCommand();
                cycleCmd.Execute(commandData, ref message, elements);
            }
            catch
            {
                // Swallow exceptions here to avoid breaking the overall UpdateAll flow
            }

            return Result.Succeeded;
        }
    }

    public static class MethodExtension
    {
        public static string ReplaceSpecialCharacters(this string str)
        {
            List<char> badChars = new List<char>
            {
              '<', '>', ':', '\"', '/', '\\', '|', '?', '*', '.', '@', '#', '$', '%', '^', '(', ')', '+', '=', '`', '~', ';', ','
            };
        StringBuilder sb = new StringBuilder();
            foreach (char c in str)
            {

                //if(c != '<' || c != '>' || c != ':' || c != '\"' || c != '/' || c != '\\' || c != '|' || c != '?' || c != '*' || c != '.' || c != '@' || c != '#' || c != '$' || c != '%' || c != '^' || c != '(' || c != ')' || c != '+' || c != '=' || c != '`' || c != '~' || c != ';' || c != ',')
                if(!badChars.Contains(c))
                {
                    sb.Append(c);
                }
                else
                {
                    sb.Append('_');
                }
            }
            return sb.ToString().Trim();
        }
    }

    public static class createImages
    {
        public static bool create (Document doc, UIDocument uidoc, Dictionary<string, ElementId> foundDetailItems, string schType)
        {
            bool error = false;
            string imagesDirectory = "";
            int pBarCount = foundDetailItems.Count() + 4;
            List<string> unableToEdit = new List<string>();

            using (ProgressForm.ProgressForm pgfm = new ProgressForm.ProgressForm("Update " + schType + " Schedule", null, pBarCount, string.Empty))
            {
                pgfm.IncrementWithText("Begin Create Images");
                unableToEdit.Clear();
                imagesDirectory = FindProjectFolder.FindProjectFolder.FindFolder(doc, "images");

                if (imagesDirectory != null && Directory.Exists(imagesDirectory))
                {
                    FilteredElementCollector col = new FilteredElementCollector(uidoc.Document);
                    List<Element> links = col.OfCategory(BuiltInCategory.OST_RasterImages).ToList();

                    pgfm.IncrementWithText("Deleting Existing Imported " + schType + " Images");
                    foreach (Element link in links)
                    {
                        try
                        {
                            if (link.Name.StartsWith(schType))
                            {
                                using (Transaction tx1 = new Transaction(doc, "Delete old images"))
                                {
                                    tx1.Start();
                                    doc.Delete(link.Id);
                                    tx1.Commit();
                                }
                            }
                        }
                        catch
                        {
                            MessageBox.Show("Unable to delete existing image " + link.Name, "US6 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }

                    string temp = System.IO.Path.Combine(imagesDirectory, "Temp");
                    List<string> tempFiles = Directory.GetFiles(temp).ToList();
                    if (tempFiles.Any())
                    {
                        foreach (string s in tempFiles)
                        {
                            try
                            {
                                File.Delete(s);
                            }
                            catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("UpdateSchedules", __ex); }
                        }
                    }

                    ViewDrafting draftView = null;
                    List<ElementId> views = new List<ElementId>();

                    using (Transaction tx2 = new Transaction(doc, "Create Drafting View"))
                    {
                        tx2.Start();
                        ViewFamilyType vd = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>().FirstOrDefault(q => q.ViewFamily == ViewFamily.Drafting);
                        draftView = ViewDrafting.Create(doc, vd.Id);
                        views.Clear();
                        views.Add(draftView.Id);
                        tx2.Commit();
                    }

                    pgfm.IncrementWithText("Creating " + schType + " Images");

                    foreach (KeyValuePair<string, ElementId> kvp in foundDetailItems)
                    {
                        string kvpKey = kvp.Key.ReplaceSpecialCharacters();
                        List<ElementId> toDelete = new List<ElementId>();

                        pgfm.IncrementWithText("Processing Image: " + kvpKey);
                        using (Transaction tx3 = new Transaction(doc, "Create " + schType + " Image"))
                        {
                            tx3.Start();
                            draftView.Name = "ExportImage - " + kvpKey;

                            FamilySymbol famSym = doc.GetElement(kvp.Value) as FamilySymbol;
                            FamilyInstance newFI = doc.Create.NewFamilyInstance(new XYZ(0, 0, 0), famSym, draftView);
                            toDelete.Add(newFI.Id);
                            if (schType == "Component")
                            {
                                bool regen = false;
                                Parameter param_ShowLines = newFI.LookupParameter("Show Lines");
                                if(param_ShowLines != null)
                                {
                                    try
                                    {
                                        param_ShowLines.Set(0);
                                        regen = true;
                                    }
                                    catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("UpdateSchedules", __ex); }
                                }
                                Parameter param_Length = newFI.LookupParameter("Length");
                                if (param_Length != null)
                                {
                                    try
                                    {
                                        param_Length.SetValueString("0.01");
                                        regen = true;
                                    }
                                    catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("UpdateSchedules", __ex); }
                                }
                                if (regen)
                                {
                                    doc.Regenerate();
                                }
                            }
                            BoundingBoxXYZ bb = newFI.get_BoundingBox(draftView);
                            XYZ start = new XYZ(bb.Min.X - 0.01, bb.Min.Y - 0.01, 0);
                            XYZ right = new XYZ(bb.Max.X + 0.01, bb.Min.Y - 0.01, 0);
                            XYZ left = new XYZ(bb.Min.X - 0.01, bb.Max.Y + 0.01, 0);
                            XYZ end = new XYZ(bb.Max.X + 0.01, bb.Max.Y + 0.01, 0);
                            Line line1 = Line.CreateBound(start, right);
                            Line line2 = Line.CreateBound(right, end);
                            Line line3 = Line.CreateBound(end, left);
                            Line line4 = Line.CreateBound(left, start);
                            DetailCurve dc1 = doc.Create.NewDetailCurve(draftView, line1);
                            DetailCurve dc2 = doc.Create.NewDetailCurve(draftView, line2);
                            DetailCurve dc3 = doc.Create.NewDetailCurve(draftView, line3);
                            DetailCurve dc4 = doc.Create.NewDetailCurve(draftView, line4);
                            GraphicsStyle gs = dc1.LineStyle as GraphicsStyle;
                            OverrideGraphicSettings ogs = new OverrideGraphicSettings();
                            ogs.SetProjectionLineColor(new Autodesk.Revit.DB.Color(255,255,255));
                            draftView.SetElementOverrides(dc1.Id, ogs);
                            draftView.SetElementOverrides(dc2.Id, ogs);
                            draftView.SetElementOverrides(dc3.Id, ogs);
                            draftView.SetElementOverrides(dc4.Id, ogs);

                            toDelete.Add(dc1.Id);
                            toDelete.Add(dc2.Id);
                            toDelete.Add(dc3.Id);
                            toDelete.Add(dc4.Id);

                            tx3.Commit();
                        }

                        string path = System.IO.Path.Combine(imagesDirectory, kvpKey + ".png");
                        string tempPath = System.IO.Path.Combine(temp, kvpKey + ".png");
                        ThinLinesOptions.AreThinLinesEnabled = false;
                        int zoomPercentage = 100;
                        if(schType == "Fastener")
                        {
                            if (!GlobalParametersManager.IsUniqueName(doc, "Fastener Image Scale"))
                            {
                                ElementId fsGpId = ElementId.InvalidElementId;
                                fsGpId = GlobalParametersManager.FindByName(doc, "Fastener Image Scale");
                                if (fsGpId != ElementId.InvalidElementId)
                                {
                                    try
                                    {
                                        GlobalParameter fsGp = doc.GetElement(fsGpId) as GlobalParameter;
                                        IntegerParameterValue integerParameterValue = fsGp.GetValue() as IntegerParameterValue;
                                        zoomPercentage = integerParameterValue.Value;
                                    }
                                    catch
                                    {
                                        MessageBox.Show("Error obtaining value of Global Parameter \"Fastener Image Scale\". Default of 10 will be used.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                        zoomPercentage = 10;
                                    }
                                }
                                else
                                {
                                    MessageBox.Show("Error locating Global Parameter \"Fastener Image Scale\". Default of 10 will be used.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                    zoomPercentage = 10;
                                }
                            }
                            else
                            {
                                MessageBox.Show("Error locating Global Parameter \"Fastener Image Scale\". Default of 10 will be used.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                zoomPercentage = 10;
                            }
                        }
                        ImageExportOptions ieo = new ImageExportOptions
                        {
                            FilePath = tempPath,
                            HLRandWFViewsFileType = ImageFileType.PNG,
                            ImageResolution = ImageResolution.DPI_600,
                            ShouldCreateWebSite = false,
                            ZoomType = ZoomFitType.Zoom,
                            Zoom = zoomPercentage,
                            ExportRange = ExportRange.SetOfViews
                        };
                        ieo.SetViewsAndSheets(views);
                        doc.ExportImage(ieo);

                        if (schType == "Extrusion" || schType == "Component")
                        {
                            string[] nFiles = Directory.GetFiles(temp);
                            string nFile = "";
                            foreach (string file in nFiles)
                            {
                                if (file.Contains(kvpKey))
                                {
                                    nFile = file;
                                    break;
                                }
                            }
                            if (nFile != null && nFile.Contains(kvpKey))
                            {
                                System.Drawing.Image imgBef = System.Drawing.Image.FromFile(nFile);
                                int newHeight = 512;
                                int newWidth = 512;
                                newHeight = imgBef.Height * 512 / imgBef.Width;
                                if (newHeight > 512)
                                {
                                    newHeight = 512;
                                    newWidth = imgBef.Width * 512 / imgBef.Height;
                                }
                                var res = new Bitmap(newWidth, newHeight);
                                using (var graphic = Graphics.FromImage(res))
                                {
                                    graphic.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                    graphic.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                                    graphic.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                                    graphic.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                                    graphic.DrawImage(imgBef, 0, 0, newWidth, newHeight);
                                }
                                var newRes = new Bitmap(512, 512);
                                using (var g = Graphics.FromImage(newRes))
                                {
                                    g.Clear(System.Drawing.Color.White);
                                    var x = (512 - res.Width) / 2;
                                    var y = (512 - res.Height) / 2;
                                    g.DrawImageUnscaled(res, x, y, res.Width, res.Height);
                                }
                                imgBef.Dispose();
                                try
                                {
                                    File.Delete(nFile);
                                }
                                catch
                                {
                                    wait(3000);
                                    try
                                    {
                                        File.Delete(nFile);
                                    }
                                    catch (Exception e)
                                    {
                                        pgfm.Close();
                                        error = true;
                                        GMSRevitAddin.GmsUi.ShowError("Unable to delete existing image file: " + nFile + System.Environment.NewLine + e.Message, "US7 Error", e);
                                    }
                                }
                                try
                                {
                                    newRes.Save(nFile, ImageFormat.Png);
                                }
                                catch
                                {
                                    wait(3000);
                                    try
                                    {
                                        newRes.Save(nFile, ImageFormat.Png);
                                    }
                                    catch (Exception e)
                                    {
                                        pgfm.Close();
                                        error = true;
                                        GMSRevitAddin.GmsUi.ShowError("Unable to save new image file: " + nFile + System.Environment.NewLine + e.Message, "US8 Error", e);
                                    }
                                }
                                res.Dispose();
                                newRes.Dispose();
                            }
                            else
                            {
                                error = true;
                                MessageBox.Show("Unable to locate new image file", "US9 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }

                        string[] newFiles = Directory.GetFiles(temp);
                        string newFile = "";
                        foreach (string file in newFiles)
                        {
                            if (file.Contains(kvpKey))
                            {
                                newFile = file;
                                break;
                            }
                        }
                        if (newFile != null && newFile.Contains(kvpKey))
                        {
                            File.Copy(newFile, path, true);
                            try
                            {
                                File.Delete(newFile);
                            }
                            catch
                            {
                                wait(3000);
                                try
                                {
                                    File.Delete(newFile);
                                }
                                catch (Exception e)
                                {
                                    pgfm.Close();
                                    error = true;
                                    GMSRevitAddin.GmsUi.ShowError("Unable to delete existing image file: " + tempPath + System.Environment.NewLine + e.Message, "US10 Error", e);
                                }
                            }
                        }
                        else
                        {
                            error = true;
                            MessageBox.Show("Unable to locate new image file", "US11 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }

                        if (!error)
                        {
                            ImageTypeOptions ito = new ImageTypeOptions(path, false, ImageTypeSource.Import);
                            using (Transaction tx4 = new Transaction(doc, "Import Image"))
                            {
                                tx4.Start();
                                ImageType.Create(doc, ito);
                                tx4.Commit();
                            }

                            Element loadedImage = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_RasterImages).Where(l => l.Name.Contains(kvpKey)).FirstOrDefault();

                            using (Transaction tx5 = new Transaction(doc, "Set Image Parameter"))
                            {
                                tx5.Start();
                                Parameter param = doc.GetElement(kvp.Value).LookupParameter("Schedule - Preview");
                                if (param != null)
                                {
                                    List<ElementId> elm = new List<ElementId> { kvp.Value };
                                    ICollection<ElementId> okToEdit = WorksharingUtils.CheckoutElements(doc, elm);
                                    if (okToEdit.Any())
                                    {
                                        try
                                        {
                                            param.Set(loadedImage.Id);
                                        }
                                        catch
                                        {
                                            unableToEdit.Add(kvp.Key);
                                        }
                                    }
                                    else
                                    {
                                        unableToEdit.Add(kvp.Key);
                                    }
                                }
                                else
                                {
                                    unableToEdit.Add(kvp.Key);
                                }

                                ParameterSet pset = doc.GetElement(kvp.Value).Parameters;
                                foreach (Parameter p in pset)
                                {
                                    if (p.Definition.Name == "Schedule - Preview")
                                    {
                                        p.Set(loadedImage.Id);
                                    }
                                }

                                if (toDelete.Any())
                                {
                                    doc.Delete(toDelete);
                                }
                                tx5.Commit();
                            }
                        }
                    }

                    using (Transaction tx6 = new Transaction(doc, "Delete temp drafting view."))
                    {
                        tx6.Start();
                        doc.Delete(draftView.Id);
                        doc.Regenerate();
                        tx6.Commit();
                    }

                    if (imagesDirectory != null)
                    {
                        List<string> files = System.IO.Directory.GetFiles(temp, "*Drafting View*").ToList();
                        foreach (string file in files)
                        {
                            try
                            {
                                File.Delete(file);
                            }
                            catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("UpdateSchedules", __ex); }
                        }
                    }
                    pgfm.IncrementWithText(schType + " Schedule Updated");
                }
                else
                {
                    pgfm.Close();
                    error = true;
                    MessageBox.Show("Unable to locate Images folder.", "US12 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            if (unableToEdit.Any())
            {
                string message = "The following elements have the Preview parameter checked out by another user. The images for these elements have been added to the project but you will need to manually set the Preview parameter value." + System.Environment.NewLine;
                foreach(string name in unableToEdit)
                {
                    message += name + System.Environment.NewLine;
                }
                MessageBox.Show(message, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            string tempFolder = System.IO.Path.Combine(imagesDirectory, "Temp");
            List<string> tmpFiles = Directory.GetFiles(tempFolder).ToList();
            if (tmpFiles.Any())
            {
                foreach (string s in tmpFiles)
                {
                    try
                    {
                        File.Delete(s);
                    }
                    catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("UpdateSchedules", __ex); }
                }
            }

            if (error)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public static void wait(int milliseconds)
        {
            var timer1 = new System.Windows.Forms.Timer();
            if (milliseconds <= 0) return;

            timer1.Interval = milliseconds;
            timer1.Enabled = true;
            timer1.Start();

            timer1.Tick += (s, e) =>
            {
                timer1.Enabled = false;
                timer1.Stop();
            };

            while (timer1.Enabled)
            {
                System.Windows.Forms.Application.DoEvents();
            }
            timer1.Dispose();
        }
    }
}