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
using System.Xml.Serialization;


//MUST be updated any time button location on GMS tab changes (commandpath and command id change)

namespace UpdateKeyboardShortcuts
{
    /// <summary>
    /// Ensures every GMS ribbon command has its intended default keyboard shortcut registered in
    /// Revit's per-user KeyboardShortcuts.xml. Called once from GMS_tools.OnStartup (via
    /// <see cref="updateXML"/>) on every Revit launch, since the shortcuts file is user/machine-local
    /// and won't already contain GMS entries on a fresh profile. Must be kept in sync by hand any
    /// time a button's ribbon location (panel/tab path) or command id changes — see the file header
    /// comment above.
    /// </summary>
    public class UpdateKeyboardShortcuts
    {
        /// <summary>
        /// Appends a new <c>&lt;ShortcutItem&gt;</c> element to the loaded KeyboardShortcuts.xml and
        /// saves it back to disk. Used both to add a brand-new shortcut entry and, after an existing
        /// mismatched entry is removed, to re-insert it with the correct shortcut/path.
        /// </summary>
        public static void addToXML(string pathToFile, string commandName, string commandID, string shortcut, string path)
        {
            try
            {
                XDocument xdoc = XDocument.Load(pathToFile);
                XElement root = new XElement("ShortcutItem");
                root.Add(new XAttribute("CommandName", commandName));
                root.Add(new XAttribute("CommandId", commandID));
                root.Add(new XAttribute("Shortcuts", shortcut));
                root.Add(new XAttribute("Paths", path));
                xdoc.Element("Shortcuts").Add(root);
                xdoc.Save(pathToFile);
                //MessageBox.Show(pathToFile + System.Environment.NewLine + commandName + System.Environment.NewLine + commandID + System.Environment.NewLine + shortcut + System.Environment.NewLine + path);
            }
            catch (Exception ex)
            {
                GMSRevitAddin.GmsUi.ShowError("Error attempting to Update KeyboardShortcuts.xml for command name: " + commandName + "." + System.Environment.NewLine + ex.Message, "UKS1 Error", ex);
            }
        }

        /// <summary>Plain-old-data projection of a &lt;ShortcutItem&gt; element's three attributes,
        /// used to inspect an existing entry without holding onto the live XElement.</summary>
        public class shortCut
        {
            [XmlAttributeAttribute("CommandName")]
            public string cName { get; set; }

            [XmlAttributeAttribute("Shortcuts")]
            public string sKeys { get; set; }

            [XmlAttributeAttribute("Paths")]
            public string pKeys { get; set; }

        }

        /// <summary>
        /// Loads the current user's KeyboardShortcuts.xml (or, if it doesn't exist yet, copies the
        /// GMS-provided default from <see cref="GMSRevitAddin.GmsPaths.GmsRevitRoot"/>) and makes sure
        /// every GMS command below has its default shortcut assigned at its current ribbon path.
        /// Call from GMS_tools.OnStartup.
        ///
        /// The pattern repeated below for each command is:
        ///  1. Query &lt;ShortcutItem&gt; elements whose CommandName contains the command's keywords
        ///     and whose CommandId contains "GMS" (a loose match, since Revit's CommandName text and
        ///     CommandId format aren't identical to what we register).
        ///  2. If a matching entry exists:
        ///     - and its Paths attribute matches the command's current ribbon location: add the
        ///       Shortcuts attribute if missing, or overwrite it if it's set to something other than
        ///       our default — either way this "claims"/resets the shortcut to the GMS default.
        ///     - otherwise (the ribbon path moved, e.g. after a panel reorganization): remove the
        ///       stale entry and re-add it fresh via <see cref="addToXML"/> with the current path.
        ///  3. If no matching entry exists at all (e.g. first run on a new machine), add it fresh.
        /// Each block saves the XDocument to disk immediately after mutating it (XDocument doesn't
        /// auto-persist), and the "break" inside the mismatch-fix branches stops iterating a
        /// (should-be) single-element sequence right after the fix is applied.
        /// </summary>
        public static void updateXML()
        {
            //search for keyboard shortcut file
            string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string pathToShortcutsFile = Path.Combine(path, @"Autodesk\Revit\Autodesk Revit " + GMSRevitAddin.GmsVersion.Number, "KeyboardShortcuts.xml");

            if (File.Exists(pathToShortcutsFile))
            {
                XDocument xdoc = XDocument.Load(pathToShortcutsFile);
                try
                {
                    //ExportPieces
                    IEnumerable<shortCut> ExportPiecesShortcut = from rec in xdoc.Descendants("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Export") && s.Attribute("CommandName").Value.Contains("Pieces") && s.Attribute("CommandId").Value.Contains("GMS"))
                                                             select new shortCut()
                                                             {
                                                                 cName = (string)rec.Attribute("CommandName"),
                                                                 sKeys = (string)rec.Attribute("Shortcuts"),
                                                                 pKeys = (string)rec.Attribute("Paths")
                                                             };
                    if (ExportPiecesShortcut.Any())
                    {
                        foreach (shortCut sc in ExportPiecesShortcut)
                        {
                            if (sc.pKeys == "GMS>Piece Extraction")
                            {
                                if (null == sc.sKeys)
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Export") && s.Attribute("CommandName").Value.Contains("Pieces") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.Add(new XAttribute("Shortcuts", "EP"));
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                                else if (sc.sKeys != "EP")
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Export") && s.Attribute("CommandName").Value.Contains("Pieces") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.SetAttributeValue("Shortcuts", "EP");
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                            }
                            else
                            {
                                xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Export") && s.Attribute("CommandName").Value.Contains("Pieces") && s.Attribute("CommandId").Value.Contains("GMS")).Remove();
                                xdoc.Save(pathToShortcutsFile);
                                addToXML(pathToShortcutsFile, "Tag Units", "CustomCtrl_%CustomCtrl_%GMS%Piece Extraction%Export Pieces", "EP", "GMS>Piece Extraction");
                            }
                        }
                    }
                    else
                    {
                        addToXML(pathToShortcutsFile, "Tag Units", "CustomCtrl_%CustomCtrl_%GMS%Piece Extraction%Export Pieces", "EP", "GMS>Piece Extraction");
                    }

                    // <TagUnits>
                    //TagUnits
                    IEnumerable<shortCut> TagUnitsShortcut = from rec in xdoc.Descendants("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Tag") && s.Attribute("CommandName").Value.Contains("Units") && s.Attribute("CommandId").Value.Contains("GMS"))
                                                             select new shortCut()
                                                             {
                                                                 cName = (string)rec.Attribute("CommandName"),
                                                                 sKeys = (string)rec.Attribute("Shortcuts"),
                                                                 pKeys = (string)rec.Attribute("Paths")
                                                             };
                    if (TagUnitsShortcut.Any())
                    {
                        foreach (shortCut sc in TagUnitsShortcut)
                        {
                            if(sc.pKeys == "GMS>Tagging Tools")
                            {
                                if (null == sc.sKeys)
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Tag") && s.Attribute("CommandName").Value.Contains("Units") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.Add(new XAttribute("Shortcuts", "TU"));
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                                else if (sc.sKeys != "TU")
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Tag") && s.Attribute("CommandName").Value.Contains("Units") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.SetAttributeValue("Shortcuts", "TU");
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                            }
                            else
                            {
                                xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Tag") && s.Attribute("CommandName").Value.Contains("Units") && s.Attribute("CommandId").Value.Contains("GMS")).Remove();
                                xdoc.Save(pathToShortcutsFile);
                                addToXML(pathToShortcutsFile, "Tag Units", "CustomCtrl_%CustomCtrl_%GMS%Tagging Tools%Tag Units", "TU", "GMS>Tagging Tools");
                            }
                        }
                    }
                    else
                    {
                        addToXML(pathToShortcutsFile, "Tag Units", "CustomCtrl_%CustomCtrl_%GMS%Tagging Tools%Tag Units", "TU", "GMS>Tagging Tools");
                    }

                    // <Halftone&Dashed>
                    //Halftone&Dashed
                    IEnumerable<shortCut> HalftoneDashedShortcut = from rec in xdoc.Descendants("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Halftone") && s.Attribute("CommandName").Value.Contains("Dashed") && s.Attribute("CommandId").Value.Contains("GMS"))
                                                                   select new shortCut()
                                                                   {
                                                                       cName = (string)rec.Attribute("CommandName"),
                                                                       sKeys = (string)rec.Attribute("Shortcuts"),
                                                                       pKeys = (string)rec.Attribute("Paths")
                                                                   };
                    if (HalftoneDashedShortcut.Any())
                    {
                        foreach (shortCut sc in HalftoneDashedShortcut)
                        {
                            if (sc.pKeys == "GMS>Graphical Overrides")
                            {
                                if (null == sc.sKeys)
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Halftone") && s.Attribute("CommandName").Value.Contains("Dashed") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.Add(new XAttribute("Shortcuts", "MD"));
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                                else if (sc.sKeys != "MD")
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Halftone") && s.Attribute("CommandName").Value.Contains("Dashed") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.SetAttributeValue("Shortcuts", "MD");
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                            }
                            else
                            {
                                xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Halftone") && s.Attribute("CommandName").Value.Contains("Dashed") && s.Attribute("CommandId").Value.Contains("GMS")).Remove();
                                xdoc.Save(pathToShortcutsFile);
                                addToXML(pathToShortcutsFile, "Halftone & Dashed", "CustomCtrl_%CustomCtrl_%GMS%Graphical Overrides%Halftone & Dashed", "MD", "GMS>Graphical Overrides");
                            }
                        }
                    }
                    else
                    {
                        addToXML(pathToShortcutsFile, "Halftone & Dashed", "CustomCtrl_%CustomCtrl_%GMS%Graphical Overrides%Halftone & Dashed", "MD", "GMS>Graphical Overrides");
                    }

                    //Halftone
                    IEnumerable<shortCut> HalftoneShortcut = from rec in xdoc.Descendants("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Halftone") && !s.Attribute("CommandName").Value.Contains("Dashed") && s.Attribute("CommandId").Value.Contains("GMS"))
                                                             select new shortCut()
                                                             {
                                                                 cName = (string)rec.Attribute("CommandName"),
                                                                 sKeys = (string)rec.Attribute("Shortcuts"),
                                                                 pKeys = (string)rec.Attribute("Paths")
                                                             };
                    if (HalftoneShortcut.Any())
                    {
                        foreach (shortCut sc in HalftoneShortcut)
                        {
                            if (sc.pKeys == "GMS>Graphical Overrides")
                            {
                                if (null == sc.sKeys)
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Halftone") && !s.Attribute("CommandName").Value.Contains("Dashed") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.Add(new XAttribute("Shortcuts", "MH"));
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                                else if (sc.sKeys != "MH")
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Halftone") && !s.Attribute("CommandName").Value.Contains("Dashed") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.SetAttributeValue("Shortcuts", "MH");
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                            }
                            else
                            {
                                xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Halftone") && !s.Attribute("CommandName").Value.Contains("Dashed") && s.Attribute("CommandId").Value.Contains("GMS")).Remove();
                                xdoc.Save(pathToShortcutsFile);
                                addToXML(pathToShortcutsFile, "Halftone", "CustomCtrl_%CustomCtrl_%GMS%Graphical Overrides%Halftone", "MH", "GMS>Graphical Overrides");
                            }
                        }
                    }
                    else
                    {
                        addToXML(pathToShortcutsFile, "Halftone", "CustomCtrl_%CustomCtrl_%GMS%Graphical Overrides%Halftone", "MH", "GMS>Graphical Overrides");
                    }

                    //ResetOverrides
                    IEnumerable<shortCut> ResetOverridesShortcut = from rec in xdoc.Descendants("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Reset") && s.Attribute("CommandName").Value.Contains("Overrides") && s.Attribute("CommandId").Value.Contains("GMS"))
                                                                   select new shortCut()
                                                                   {
                                                                       cName = (string)rec.Attribute("CommandName"),
                                                                       sKeys = (string)rec.Attribute("Shortcuts"),
                                                                       pKeys = (string)rec.Attribute("Paths")
                                                                   };
                    if (ResetOverridesShortcut.Any())
                    {
                        foreach (shortCut sc in ResetOverridesShortcut)
                        {
                            if (sc.pKeys == "GMS>Graphical Overrides")
                            {
                                if (null == sc.sKeys)
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Reset") && s.Attribute("CommandName").Value.Contains("Overrides") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.Add(new XAttribute("Shortcuts", "RR"));
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                                else if (sc.sKeys != "RR")
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Reset") && s.Attribute("CommandName").Value.Contains("Overrides") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.SetAttributeValue("Shortcuts", "RR");
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                            }
                            else
                            {
                                xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Reset") && s.Attribute("CommandName").Value.Contains("Overrides") && s.Attribute("CommandId").Value.Contains("GMS")).Remove();
                                xdoc.Save(pathToShortcutsFile);
                                addToXML(pathToShortcutsFile, "Reset Overrides", "CustomCtrl_%CustomCtrl_%GMS%Graphical Overrides%Reset Graphical Overrides", "RR", "GMS>Graphical Overrides");
                            }
                        }
                    }
                    else
                    {
                        addToXML(pathToShortcutsFile, "Reset Overrides", "CustomCtrl_%CustomCtrl_%GMS%Graphical Overrides%Reset Graphical Overrides", "RR", "GMS>Graphical Overrides");
                    }

                    ////ShowTags
                    //IEnumerable<shortcut> ShowTagsShortcut = from rec in xdoc.Descendants("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Show") && s.Attribute("CommandName").Value.Contains("Tags") && s.Attribute("CommandId").Value.Contains("GMS"))
                    //                                         select new shortcut()
                    //                                         {
                    //                                             cName = (string)rec.Attribute("CommandName"),
                    //                                             sKeys = (string)rec.Attribute("Shortcuts"),
                    //                                             pKeys = (string)rec.Attribute("Paths")
                    //                                         };
                    //if (ShowTagsShortcut.Any())
                    //{
                    //    foreach (shortcut sc in ShowTagsShortcut)
                    //    {
                    //        if (sc.pKeys == "GMS>Tagging Tools")
                    //        {
                    //            if (null == sc.sKeys)
                    //            {
                    //                var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Show") && s.Attribute("CommandName").Value.Contains("Tags") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                    //                elementAttribute.Add(new XAttribute("Shortcuts", "TV"));
                    //                xdoc.Save(pathToShortcutsFile);
                    //                break;
                    //            }
                    //            else if (sc.sKeys != "TV")
                    //            {
                    //                var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Show") && s.Attribute("CommandName").Value.Contains("Tags") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                    //                elementAttribute.SetAttributeValue("Shortcuts", "TV");
                    //                xdoc.Save(pathToShortcutsFile);
                    //                break;
                    //            }
                    //        }
                    //        else
                    //        {
                    //            xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Show") && s.Attribute("CommandName").Value.Contains("Tags") && s.Attribute("CommandId").Value.Contains("GMS")).Remove();
                    //            xdoc.Save(pathToShortcutsFile);
                    //            addToXML(pathToShortcutsFile, "Show All Tags", "CustomCtrl_%CustomCtrl_%GMS%Tagging Tools%Show/Hide All Tags", "TV", "GMS>Tagging Tools");
                    //        }
                    //    }
                    //}
                    //else
                    //{
                    //    addToXML(pathToShortcutsFile, "Show All Tags", "CustomCtrl_%CustomCtrl_%GMS%Tagging Tools%Show/Hide All Tags", "TV", "GMS>Tagging Tools");
                    //}

                    //NewUnitSheet
                    IEnumerable<shortCut> NewUnitSheetShortcut = from rec in xdoc.Descendants("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("New") && s.Attribute("CommandName").Value.Contains("Unit") && s.Attribute("CommandId").Value.Contains("GMS"))
                                                                 select new shortCut()
                                                                 {
                                                                     cName = (string)rec.Attribute("CommandName"),
                                                                     sKeys = (string)rec.Attribute("Shortcuts"),
                                                                     pKeys = (string)rec.Attribute("Paths")
                                                                 };

                    if (NewUnitSheetShortcut.Any())
                    {
                        foreach (shortCut sc in NewUnitSheetShortcut)
                        {
                            if (sc.pKeys == "GMS>Unit Drawing Tools")
                            {
                                if (null == sc.sKeys)
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("New") && s.Attribute("CommandName").Value.Contains("Unit") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.Add(new XAttribute("Shortcuts", "NU"));
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                                else if (sc.sKeys != "NU")
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("New") && s.Attribute("CommandName").Value.Contains("Unit") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.SetAttributeValue("Shortcuts", "NU");
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                            }
                            else
                            {
                                xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("New") && s.Attribute("CommandName").Value.Contains("Unit") && s.Attribute("CommandId").Value.Contains("GMS")).Remove();
                                xdoc.Save(pathToShortcutsFile);
                                addToXML(pathToShortcutsFile, "New Unit Sheet", "CustomCtrl_%CustomCtrl_%GMS%Unit Drawing Tools%New Unit Sheet", "NU", "GMS>Unit Drawing Tools");
                            }
                        }
                    }
                    else
                    {
                        addToXML(pathToShortcutsFile, "New Unit Sheet", "CustomCtrl_%CustomCtrl_%GMS%Unit Drawing Tools%New Unit Sheet", "NU", "GMS>Unit Drawing Tools");
                    }

                    //DimensionNote
                    IEnumerable<shortCut> DimensionNoteShortcut = from rec in xdoc.Descendants("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Dimension") && s.Attribute("CommandName").Value.Contains("Note") && s.Attribute("CommandId").Value.Contains("GMS"))
                                                                  select new shortCut()
                                                                  {
                                                                      cName = (string)rec.Attribute("CommandName"),
                                                                      sKeys = (string)rec.Attribute("Shortcuts"),
                                                                      pKeys = (string)rec.Attribute("Paths")
                                                                  };
                    if (DimensionNoteShortcut.Any())
                    {
                        foreach (shortCut sc in DimensionNoteShortcut)
                        {
                            if (sc.pKeys == "GMS>GMS Tools")
                            {
                                if (null == sc.sKeys)
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Dimension") && s.Attribute("CommandName").Value.Contains("Note") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.Add(new XAttribute("Shortcuts", "DN"));
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                                else if (sc.sKeys != "DN")
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Dimension") && s.Attribute("CommandName").Value.Contains("Note") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.SetAttributeValue("Shortcuts", "DN");
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                            }
                            else
                            {
                                xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Dimension") && s.Attribute("CommandName").Value.Contains("Note") && s.Attribute("CommandId").Value.Contains("GMS")).Remove();
                                xdoc.Save(pathToShortcutsFile);
                                addToXML(pathToShortcutsFile, "Dimension Note", "CustomCtrl_%CustomCtrl_%GMS%GMS Tools%Dimension Note", "DN", "GMS>GMS Tools");
                            }
                        }
                    }
                    else
                    {
                        addToXML(pathToShortcutsFile, "Dimension Note", "CustomCtrl_%CustomCtrl_%GMS%GMS Tools%Dimension Note", "DN", "GMS>GMS Tools");
                    }

                    //BuildTool
                    IEnumerable<shortCut> BuildToolShortcut = from rec in xdoc.Descendants("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Build") && s.Attribute("CommandName").Value.Contains("Number") && s.Attribute("CommandId").Value.Contains("GMS"))
                                                              select new shortCut()
                                                              {
                                                                  cName = (string)rec.Attribute("CommandName"),
                                                                  sKeys = (string)rec.Attribute("Shortcuts"),
                                                                  pKeys = (string)rec.Attribute("Paths")
                                                              };
                    if (BuildToolShortcut.Any())
                    {
                        foreach (shortCut sc in BuildToolShortcut)
                        {
                            if (sc.pKeys == "GMS>Tagging Tools")
                            {
                                if (null == sc.sKeys)
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Build") && s.Attribute("CommandName").Value.Contains("Number") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.Add(new XAttribute("Shortcuts", "BD"));
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                                else if (sc.sKeys != "BD")
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Build") && s.Attribute("CommandName").Value.Contains("Number") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.SetAttributeValue("Shortcuts", "BD");
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                            }
                            else
                            {
                                xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Build") && s.Attribute("CommandName").Value.Contains("Number") && s.Attribute("CommandId").Value.Contains("GMS")).Remove();
                                xdoc.Save(pathToShortcutsFile);
                                addToXML(pathToShortcutsFile, "Build Number", "CustomCtrl_%CustomCtrl_%GMS%Tagging Tools%BuildNumber", "BD", "GMS>Tagging Tools");
                            }
                        }
                    }
                    else
                    {
                        addToXML(pathToShortcutsFile, "Build Number", "CustomCtrl_%CustomCtrl_%GMS%Tagging Tools%BuildNumber", "BD", "GMS>Tagging Tools");
                    }

                    //BunkTool
                    IEnumerable<shortCut> BunkToolShortcut = from rec in xdoc.Descendants("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Bunk") && s.Attribute("CommandName").Value.Contains("Number") && s.Attribute("CommandId").Value.Contains("GMS"))
                                                             select new shortCut()
                                                             {
                                                                 cName = (string)rec.Attribute("CommandName"),
                                                                 sKeys = (string)rec.Attribute("Shortcuts"),
                                                                 pKeys = (string)rec.Attribute("Paths")
                                                             };
                    if (BunkToolShortcut.Any())
                    {
                        foreach (shortCut sc in BunkToolShortcut)
                        {
                            if (sc.pKeys == "GMS>Tagging Tools")
                            {
                                if (null == sc.sKeys)
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Bunk") && s.Attribute("CommandName").Value.Contains("Number") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.Add(new XAttribute("Shortcuts", "BK"));
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                                else if (sc.sKeys != "BK")
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Bunk") && s.Attribute("CommandName").Value.Contains("Number") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.SetAttributeValue("Shortcuts", "BK");
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                            }
                            else
                            {
                                xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Bunk") && s.Attribute("CommandName").Value.Contains("Number") && s.Attribute("CommandId").Value.Contains("GMS")).Remove();
                                xdoc.Save(pathToShortcutsFile);
                                addToXML(pathToShortcutsFile, "Bunk Number", "CustomCtrl_%CustomCtrl_%GMS%Tagging Tools%BunkNumber", "BK", "GMS>Tagging Tools");
                            }
                        }
                    }
                    else
                    {
                        addToXML(pathToShortcutsFile, "Bunk Number", "CustomCtrl_%CustomCtrl_%GMS%Tagging Tools%BunkNumber", "BK", "GMS>Tagging Tools");
                    }

                    //UnitReleaseDate
                    IEnumerable<shortCut> UnitReleaseDateShortcut = from rec in xdoc.Descendants("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Unit") && s.Attribute("CommandName").Value.Contains("Date") && s.Attribute("CommandId").Value.Contains("GMS"))
                                                                 select new shortCut()
                                                                 {
                                                                     cName = (string)rec.Attribute("CommandName"),
                                                                     sKeys = (string)rec.Attribute("Shortcuts"),
                                                                     pKeys = (string)rec.Attribute("Paths")
                                                                 };

                    if (UnitReleaseDateShortcut.Any())
                    {
                        foreach (shortCut sc in UnitReleaseDateShortcut)
                        {
                            if (sc.pKeys == "GMS>Unit Drawing Tools")
                            {
                                if (null == sc.sKeys)
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Unit") && s.Attribute("CommandName").Value.Contains("Date") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.Add(new XAttribute("Shortcuts", "UR"));
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                                else if (sc.sKeys != "UR")
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Unit") && s.Attribute("CommandName").Value.Contains("Date") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.SetAttributeValue("Shortcuts", "UR");
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                            }
                            else
                            {
                                xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Unit") && s.Attribute("CommandName").Value.Contains("Date") && s.Attribute("CommandId").Value.Contains("GMS")).Remove();
                                xdoc.Save(pathToShortcutsFile);
                                addToXML(pathToShortcutsFile, "Unit Release Date", "CustomCtrl_%CustomCtrl_%GMS%Unit Drawing Tools%Unit Release Date", "UR", "GMS>Unit Drawing Tools");
                            }
                        }
                    }
                    else
                    {
                        addToXML(pathToShortcutsFile, "Unit Release Date", "CustomCtrl_%CustomCtrl_%GMS%Unit Drawing Tools%Unit Release Date", "UR", "GMS>Unit Drawing Tools");
                    }

                    // HelpMenu shortcut removed 2026-06-30 along with the hidden Help ribbon button.


                    //LevelTool
                    IEnumerable<shortCut> LevelToolShortcut = from rec in xdoc.Descendants("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Level") && s.Attribute("CommandName").Value.Contains("Number") && s.Attribute("CommandId").Value.Contains("GMS"))
                                                              select new shortCut()
                                                              {
                                                                  cName = (string)rec.Attribute("CommandName"),
                                                                  sKeys = (string)rec.Attribute("Shortcuts"),
                                                                  pKeys = (string)rec.Attribute("Paths")
                                                              };
                    if (LevelToolShortcut.Any())
                    {
                        foreach (shortCut sc in LevelToolShortcut)
                        {
                            if (sc.pKeys == "GMS>Tagging Tools")
                            {
                                if (null == sc.sKeys)
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Level") && s.Attribute("CommandName").Value.Contains("Number") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.Add(new XAttribute("Shortcuts", "LV"));
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                                else if (sc.sKeys != "LV")
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Level") && s.Attribute("CommandName").Value.Contains("Number") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.SetAttributeValue("Shortcuts", "LV");
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                            }
                            else
                            {
                                xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Level") && s.Attribute("CommandName").Value.Contains("Number") && s.Attribute("CommandId").Value.Contains("GMS")).Remove();
                                xdoc.Save(pathToShortcutsFile);
                                addToXML(pathToShortcutsFile, "Bunk Number", "CustomCtrl_%CustomCtrl_%GMS%Tagging Tools%LevelNumber", "LV", "GMS>Tagging Tools");
                            }
                        }
                    }
                    else
                    {
                        addToXML(pathToShortcutsFile, "Level Number", "CustomCtrl_%CustomCtrl_%GMS%Tagging Tools%LevelNumber", "LV", "GMS>Tagging Tools");
                    }


                    //Collect Detail Items
                    IEnumerable<shortCut> CollectDetailItemsShortcut = from rec in xdoc.Descendants("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Collect") && s.Attribute("CommandName").Value.Contains("Detail") && s.Attribute("CommandId").Value.Contains("GMS"))
                                                              select new shortCut()
                                                              {
                                                                  cName = (string)rec.Attribute("CommandName"),
                                                                  sKeys = (string)rec.Attribute("Shortcuts"),
                                                                  pKeys = (string)rec.Attribute("Paths")
                                                              };
                    if (CollectDetailItemsShortcut.Any())
                    {
                        foreach (shortCut sc in CollectDetailItemsShortcut)
                        {
                            if (sc.pKeys == "GMS>Detail Items")
                            {
                                if (null == sc.sKeys)
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Collect") && s.Attribute("CommandName").Value.Contains("Detail") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.Add(new XAttribute("Shortcuts", "CD"));
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                                else if (sc.sKeys != "CD")
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Collect") && s.Attribute("CommandName").Value.Contains("Detail") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.SetAttributeValue("Shortcuts", "CD");
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                            }
                            else
                            {
                                xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Collect") && s.Attribute("CommandName").Value.Contains("Detail") && s.Attribute("CommandId").Value.Contains("GMS")).Remove();
                                xdoc.Save(pathToShortcutsFile);
                                addToXML(pathToShortcutsFile, "Collect Detail Items", "CustomCtrl_%CustomCtrl_%GMS%GMS Tools%Collect Detail Items", "CD", "GMS>Detail Items");
                            }
                        }
                    }
                    else
                    {
                        addToXML(pathToShortcutsFile, "Collect Detail Items", "CustomCtrl_%CustomCtrl_%GMS%GMS Tools%Collect Detail Items", "CD", "GMS>Detail Items");
                    }


                    //Detail Item Palette
                    IEnumerable<shortCut> DetailItemPaletteShortcut = from rec in xdoc.Descendants("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Detail") && s.Attribute("CommandName").Value.Contains("Palette") && s.Attribute("CommandId").Value.Contains("GMS"))
                                                              select new shortCut()
                                                              {
                                                                  cName = (string)rec.Attribute("CommandName"),
                                                                  sKeys = (string)rec.Attribute("Shortcuts"),
                                                                  pKeys = (string)rec.Attribute("Paths")
                                                              };
                    if (DetailItemPaletteShortcut.Any())
                    {
                        foreach (shortCut sc in DetailItemPaletteShortcut)
                        {
                            if (sc.pKeys == "GMS>Detail Items")
                            {
                                if (null == sc.sKeys)
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Detail") && s.Attribute("CommandName").Value.Contains("Palette") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.Add(new XAttribute("Shortcuts", "DP"));
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                                else if (sc.sKeys != "DP")
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Detail") && s.Attribute("CommandName").Value.Contains("Palette") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.SetAttributeValue("Shortcuts", "DP");
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                            }
                            else
                            {
                                xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Detail") && s.Attribute("CommandName").Value.Contains("Palette") && s.Attribute("CommandId").Value.Contains("GMS")).Remove();
                                xdoc.Save(pathToShortcutsFile);
                                addToXML(pathToShortcutsFile, "Detail Item Palette", "CustomCtrl_%CustomCtrl_%GMS%Detail Items%ShowDetailItemPalette", "DP", "GMS>Detail Items");
                            }
                        }
                    }
                    else
                    {
                        addToXML(pathToShortcutsFile, "Detail Item Palette", "CustomCtrl_%CustomCtrl_%GMS%Detail Items%ShowDetailItemPalette", "DP", "GMS>Detail Items");
                    }


                    //Cycle Worksets
                    IEnumerable<shortCut> CycleWorksetsShortcut = from rec in xdoc.Descendants("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Cycle") && s.Attribute("CommandName").Value.Contains("Worksets") && s.Attribute("CommandId").Value.Contains("GMS"))
                                                                       select new shortCut()
                                                                       {
                                                                           cName = (string)rec.Attribute("CommandName"),
                                                                           sKeys = (string)rec.Attribute("Shortcuts"),
                                                                           pKeys = (string)rec.Attribute("Paths")
                                                                       };
                    if (CycleWorksetsShortcut.Any())
                    {
                        foreach (shortCut sc in CycleWorksetsShortcut)
                        {
                            if (sc.pKeys == "GMS>GMS Tools")
                            {
                                if (null == sc.sKeys)
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Cycle") && s.Attribute("CommandName").Value.Contains("Worksets") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.Add(new XAttribute("Shortcuts", "CY"));
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                                else if (sc.sKeys != "CY")
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Cycle") && s.Attribute("CommandName").Value.Contains("Worksets") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.SetAttributeValue("Shortcuts", "CY");
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                            }
                            else
                            {
                                xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Cycle") && s.Attribute("CommandName").Value.Contains("Worksets") && s.Attribute("CommandId").Value.Contains("GMS")).Remove();
                                xdoc.Save(pathToShortcutsFile);
                                addToXML(pathToShortcutsFile, "Cycle Worksets", "CustomCtrl_%CustomCtrl_%GMS%GMS Tools%Cycle Worksets", "CY", "GMS>GMS Tools");
                            }
                        }
                    }
                    else
                    {
                        addToXML(pathToShortcutsFile, "Cycle Worksets", "CustomCtrl_%CustomCtrl_%GMS%GMS Tools%Cycle Worksets", "CY", "GMS>GMS Tools");
                    }


                    //ShowTagsPerView
                    IEnumerable<shortCut> ShowTagsPerViewShortcut = from rec in xdoc.Descendants("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Show") && s.Attribute("CommandName").Value.Contains("View") && s.Attribute("CommandId").Value.Contains("GMS"))
                                                                    select new shortCut()
                                                             {
                                                                 cName = (string)rec.Attribute("CommandName"),
                                                                 sKeys = (string)rec.Attribute("Shortcuts"),
                                                                 pKeys = (string)rec.Attribute("Paths")
                                                             };
                    if (ShowTagsPerViewShortcut.Any())
                    {
                        foreach (shortCut sc in ShowTagsPerViewShortcut)
                        {
                            if (sc.pKeys == "GMS>Tagging Tools")
                            {
                                if (null == sc.sKeys)
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Show") && s.Attribute("CommandName").Value.Contains("View") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.Add(new XAttribute("Shortcuts", "VS"));
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                                else if (sc.sKeys != "VS")
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Show") && s.Attribute("CommandName").Value.Contains("View") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.SetAttributeValue("Shortcuts", "VS");
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                            }
                            else
                            {
                                xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Show") && s.Attribute("CommandName").Value.Contains("View") && s.Attribute("CommandId").Value.Contains("GMS")).Remove();
                                xdoc.Save(pathToShortcutsFile);
                                addToXML(pathToShortcutsFile, "Show Tags Per View", "CustomCtrl_%CustomCtrl_%GMS%Tagging Tools%Show Tags per Sheet", "VS", "GMS>Tagging Tools");
                            }
                        }
                    }
                    else
                    {
                        addToXML(pathToShortcutsFile, "Show Tags Per View", "CustomCtrl_%CustomCtrl_%GMS%Tagging Tools%Show Tags per Sheet", "VS", "GMS>Tagging Tools");
                    }


                    //HideTagsPerView
                    IEnumerable<shortCut> HideTagsPerViewShortcut = from rec in xdoc.Descendants("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Hide") && s.Attribute("CommandName").Value.Contains("View") && s.Attribute("CommandId").Value.Contains("GMS"))
                                                                    select new shortCut()
                                                                    {
                                                                        cName = (string)rec.Attribute("CommandName"),
                                                                        sKeys = (string)rec.Attribute("Shortcuts"),
                                                                        pKeys = (string)rec.Attribute("Paths")
                                                                    };
                    if (HideTagsPerViewShortcut.Any())
                    {
                        foreach (shortCut sc in HideTagsPerViewShortcut)
                        {
                            if (sc.pKeys == "GMS>Tagging Tools")
                            {
                                if (null == sc.sKeys)
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Hide") && s.Attribute("CommandName").Value.Contains("View") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.Add(new XAttribute("Shortcuts", "VH"));
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                                else if (sc.sKeys != "VH")
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Hide") && s.Attribute("CommandName").Value.Contains("View") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.SetAttributeValue("Shortcuts", "VH");
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                            }
                            else
                            {
                                xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Hide") && s.Attribute("CommandName").Value.Contains("View") && s.Attribute("CommandId").Value.Contains("GMS")).Remove();
                                xdoc.Save(pathToShortcutsFile);
                                addToXML(pathToShortcutsFile, "Hide Tags Per View", "CustomCtrl_%CustomCtrl_%GMS%Tagging Tools%Hide Tags per Sheet", "VH", "GMS>Tagging Tools");
                            }
                        }
                    }
                    else
                    {
                        addToXML(pathToShortcutsFile, "Hide Tags Per View", "CustomCtrl_%CustomCtrl_%GMS%Tagging Tools%Hide Tags per Sheet", "VH", "GMS>Tagging Tools");
                    }


                    //ShowTagsBySet
                    IEnumerable<shortCut> ShowTagsBySetShortcut = from rec in xdoc.Descendants("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Show") && s.Attribute("CommandName").Value.Contains("Set") && s.Attribute("CommandId").Value.Contains("GMS"))
                                                                    select new shortCut()
                                                                    {
                                                                        cName = (string)rec.Attribute("CommandName"),
                                                                        sKeys = (string)rec.Attribute("Shortcuts"),
                                                                        pKeys = (string)rec.Attribute("Paths")
                                                                    };
                    if (ShowTagsBySetShortcut.Any())
                    {
                        foreach (shortCut sc in ShowTagsBySetShortcut)
                        {
                            if (sc.pKeys == "GMS>Tagging Tools")
                            {
                                if (null == sc.sKeys)
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Show") && s.Attribute("CommandName").Value.Contains("Set") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.Add(new XAttribute("Shortcuts", "TS"));
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                                else if (sc.sKeys != "TS")
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Show") && s.Attribute("CommandName").Value.Contains("Set") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.SetAttributeValue("Shortcuts", "TS");
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                            }
                            else
                            {
                                xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Show") && s.Attribute("CommandName").Value.Contains("Set") && s.Attribute("CommandId").Value.Contains("GMS")).Remove();
                                xdoc.Save(pathToShortcutsFile);
                                addToXML(pathToShortcutsFile, "Tag Visibilty By Set:Show Tags", "CustomCtrl_%CustomCtrl_%CustomCtrl_%GMS%Tagging Tools%ShowHideTagsByType%ShowAllTags", "TS", "GMS>Tagging Tools");
                            }
                        }
                    }
                    else
                    {
                        addToXML(pathToShortcutsFile, "Tag Visibilty By Set:Show Tags", "CustomCtrl_%CustomCtrl_%CustomCtrl_%GMS%Tagging Tools%ShowHideTagsByType%ShowAllTags", "TS", "GMS>Tagging Tools");
                    }


                    //HideTagsBySet
                    IEnumerable<shortCut> HideTagsBySetShortcut = from rec in xdoc.Descendants("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Hide") && s.Attribute("CommandName").Value.Contains("Set") && s.Attribute("CommandId").Value.Contains("GMS"))
                                                                  select new shortCut()
                                                                  {
                                                                      cName = (string)rec.Attribute("CommandName"),
                                                                      sKeys = (string)rec.Attribute("Shortcuts"),
                                                                      pKeys = (string)rec.Attribute("Paths")
                                                                  };
                    if (HideTagsBySetShortcut.Any())
                    {
                        foreach (shortCut sc in HideTagsBySetShortcut)
                        {
                            if (sc.pKeys == "GMS>Tagging Tools")
                            {
                                if (null == sc.sKeys)
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Hide") && s.Attribute("CommandName").Value.Contains("Set") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.Add(new XAttribute("Shortcuts", "TH"));
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                                else if (sc.sKeys != "TH")
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Hide") && s.Attribute("CommandName").Value.Contains("Set") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.SetAttributeValue("Shortcuts", "TH");
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                            }
                            else
                            {
                                xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Hide") && s.Attribute("CommandName").Value.Contains("Set") && s.Attribute("CommandId").Value.Contains("GMS")).Remove();
                                xdoc.Save(pathToShortcutsFile);
                                addToXML(pathToShortcutsFile, "Tag Visibilty By Set:Hide Tags", "CustomCtrl_%CustomCtrl_%CustomCtrl_%GMS%Tagging Tools%ShowHideTagsByType%HideAllTags", "TH", "GMS>Tagging Tools");
                            }
                        }
                    }
                    else
                    {
                        addToXML(pathToShortcutsFile, "Tag Visibilty By Set:Hide Tags", "CustomCtrl_%CustomCtrl_%CustomCtrl_%GMS%Tagging Tools%ShowHideTagsByType%HideAllTags", "TH", "GMS>Tagging Tools");
                    }

                    //PurgeFamily
                    IEnumerable<shortCut> PurgeFamilyShortcut = from rec in xdoc.Descendants("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Purge") && s.Attribute("CommandName").Value.Contains("Family") && s.Attribute("CommandId").Value.Contains("GMS"))
                                                                  select new shortCut()
                                                                  {
                                                                      cName = (string)rec.Attribute("CommandName"),
                                                                      sKeys = (string)rec.Attribute("Shortcuts"),
                                                                      pKeys = (string)rec.Attribute("Paths")
                                                                  };
                    if (PurgeFamilyShortcut.Any())
                    {
                        foreach (shortCut sc in PurgeFamilyShortcut)
                        {
                            if (sc.pKeys == "GMS>GMS Tools")
                            {
                                if (null == sc.sKeys)
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Purge") && s.Attribute("CommandName").Value.Contains("Family") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.Add(new XAttribute("Shortcuts", "PA"));
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                                else if (sc.sKeys != "PA")
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Purge") && s.Attribute("CommandName").Value.Contains("Family") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.SetAttributeValue("Shortcuts", "PA");
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                            }
                            else
                            {
                                xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Purge") && s.Attribute("CommandName").Value.Contains("Family") && s.Attribute("CommandId").Value.Contains("GMS")).Remove();
                                xdoc.Save(pathToShortcutsFile);
                                addToXML(pathToShortcutsFile, "Purge Family", "CustomCtrl_%CustomCtrl_%GMS%GMS Tools%Purge Family", "PA", "GMS>GMS Tools");
                            }
                        }
                    }
                    else
                    {
                        addToXML(pathToShortcutsFile, "Purge Family", "CustomCtrl_%CustomCtrl_%GMS%GMS Tools%Purge Family", "PA", "GMS>GMS Tools");
                    }

                    //Update Schedule - Extrusion
                    IEnumerable<shortCut> UpdateScheduleEShortcut = from rec in xdoc.Descendants("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Update") && s.Attribute("CommandName").Value.Contains("Extrusion") && s.Attribute("CommandId").Value.Contains("GMS"))
                                                                    select new shortCut()
                                                                    {
                                                                        cName = (string)rec.Attribute("CommandName"),
                                                                        sKeys = (string)rec.Attribute("Shortcuts"),
                                                                        pKeys = (string)rec.Attribute("Paths")
                                                                    };
                    if (UpdateScheduleEShortcut.Any())
                    {
                        foreach (shortCut sc in UpdateScheduleEShortcut)
                        {
                            if (sc.pKeys == "GMS>Detail Items")
                            {
                                if (null == sc.sKeys)
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Update") && s.Attribute("CommandName").Value.Contains("Extrusion") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.Add(new XAttribute("Shortcuts", "USE"));
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                                else if (sc.sKeys != "USE")
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Update") && s.Attribute("CommandName").Value.Contains("Extrusion") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.SetAttributeValue("Shortcuts", "USE");
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                            }
                            else
                            {
                                xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Update") && s.Attribute("CommandName").Value.Contains("Extrusion") && s.Attribute("CommandId").Value.Contains("GMS")).Remove();
                                xdoc.Save(pathToShortcutsFile);
                                addToXML(pathToShortcutsFile, "Update Schedules:Extrusions", "CustomCtrl_%CustomCtrl_%CustomCtrl_%GMS%Detail Items%Update Schedules%Extrusions Updater", "USE", "GMS>Detail Items");
                            }
                        }
                    }
                    else
                    {
                        addToXML(pathToShortcutsFile, "Update Schedules:Extrusions", "CustomCtrl_%CustomCtrl_%CustomCtrl_%GMS%Detail Items%Update Schedules%Extrusions Updater", "USE", "GMS>Detail Items");
                    }

                    //Update Schedule - Fastener
                    IEnumerable<shortCut> UpdateScheduleFShortcut = from rec in xdoc.Descendants("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Update") && s.Attribute("CommandName").Value.Contains("Fastener") && s.Attribute("CommandId").Value.Contains("GMS"))
                                                                    select new shortCut()
                                                                    {
                                                                        cName = (string)rec.Attribute("CommandName"),
                                                                        sKeys = (string)rec.Attribute("Shortcuts"),
                                                                        pKeys = (string)rec.Attribute("Paths")
                                                                    };
                    if (UpdateScheduleFShortcut.Any())
                    {
                        foreach (shortCut sc in UpdateScheduleFShortcut)
                        {
                            if (sc.pKeys == "GMS>Detail Items")
                            {
                                if (null == sc.sKeys)
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Update") && s.Attribute("CommandName").Value.Contains("Fastener") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.Add(new XAttribute("Shortcuts", "USF"));
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                                else if (sc.sKeys != "USF")
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Update") && s.Attribute("CommandName").Value.Contains("Fastener") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.SetAttributeValue("Shortcuts", "USF");
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                            }
                            else
                            {
                                xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Update") && s.Attribute("CommandName").Value.Contains("Fastener") && s.Attribute("CommandId").Value.Contains("GMS")).Remove();
                                xdoc.Save(pathToShortcutsFile);
                                addToXML(pathToShortcutsFile, "Update Schedules:Fasteners", "CustomCtrl_%CustomCtrl_%CustomCtrl_%GMS%Detail Items%Update Schedules%Fasteners Updater", "USF", "GMS>Detail Items");
                            }
                        }
                    }
                    else
                    {
                        addToXML(pathToShortcutsFile, "Update Schedules:Fasteners", "CustomCtrl_%CustomCtrl_%CustomCtrl_%GMS%Detail Items%Update Schedules%Fasteners Updater", "USF", "GMS>Detail Items");
                    }

                    //Update Schedule - Component
                    IEnumerable<shortCut> UpdateScheduleCShortcut = from rec in xdoc.Descendants("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Update") && s.Attribute("CommandName").Value.Contains("Component") && s.Attribute("CommandId").Value.Contains("GMS"))
                                                                    select new shortCut()
                                                                    {
                                                                        cName = (string)rec.Attribute("CommandName"),
                                                                        sKeys = (string)rec.Attribute("Shortcuts"),
                                                                        pKeys = (string)rec.Attribute("Paths")
                                                                    };
                    if (UpdateScheduleCShortcut.Any())
                    {
                        foreach (shortCut sc in UpdateScheduleCShortcut)
                        {
                            if (sc.pKeys == "GMS>Detail Items")
                            {
                                if (null == sc.sKeys)
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Update") && s.Attribute("CommandName").Value.Contains("Component") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.Add(new XAttribute("Shortcuts", "USC"));
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                                else if (sc.sKeys != "USC")
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Update") && s.Attribute("CommandName").Value.Contains("Component") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.SetAttributeValue("Shortcuts", "USC");
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                            }
                            else
                            {
                                xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Update") && s.Attribute("CommandName").Value.Contains("Component") && s.Attribute("CommandId").Value.Contains("GMS")).Remove();
                                xdoc.Save(pathToShortcutsFile);
                                addToXML(pathToShortcutsFile, "Update Schedules:Components", "CustomCtrl_%CustomCtrl_%CustomCtrl_%GMS%Detail Items%Update Schedules%Components Updater", "USC", "GMS>Detail Items");
                            }
                        }
                    }
                    else
                    {
                        addToXML(pathToShortcutsFile, "Update Schedules:Components", "CustomCtrl_%CustomCtrl_%CustomCtrl_%GMS%Detail Items%Update Schedules%Components Updater", "USC", "GMS>Detail Items");
                    }

                    //Update Schedule - All
                    IEnumerable<shortCut> UpdateScheduleAShortcut = from rec in xdoc.Descendants("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Update") && s.Attribute("CommandName").Value.Contains("Schedule") && s.Attribute("CommandName").Value.Contains("Component") && s.Attribute("CommandId").Value.Contains("GMS"))
                                                                    select new shortCut()
                                                                    {
                                                                        cName = (string)rec.Attribute("CommandName"),
                                                                        sKeys = (string)rec.Attribute("Shortcuts"),
                                                                        pKeys = (string)rec.Attribute("Paths")
                                                                    };
                    if (UpdateScheduleAShortcut.Any())
                    {
                        foreach (shortCut sc in UpdateScheduleAShortcut)
                        {
                            if (sc.pKeys == "GMS>Detail Items")
                            {
                                if (null == sc.sKeys)
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Update") && s.Attribute("CommandName").Value.Contains("Schedule") && s.Attribute("CommandName").Value.Contains("All") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.Add(new XAttribute("Shortcuts", "USA"));
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                                else if (sc.sKeys != "USA")
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Update") && s.Attribute("CommandName").Value.Contains("Schedule") && s.Attribute("CommandName").Value.Contains("All") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.SetAttributeValue("Shortcuts", "USA");
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                            }
                            else
                            {
                                xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Update") && s.Attribute("CommandName").Value.Contains("Schedule") && s.Attribute("CommandName").Value.Contains("All") && s.Attribute("CommandId").Value.Contains("GMS")).Remove();
                                xdoc.Save(pathToShortcutsFile);
                                addToXML(pathToShortcutsFile, "Update Schedules:All", "CustomCtrl_%CustomCtrl_%CustomCtrl_%GMS%Detail Items%Update Schedules%All Updater", "USA", "GMS>Detail Items");
                            }
                        }
                    }
                    else
                    {
                        addToXML(pathToShortcutsFile, "Update Schedules:All", "CustomCtrl_%CustomCtrl_%CustomCtrl_%GMS%Detail Items%Update Schedules%All Updater", "USA", "GMS>Detail Items");
                    }



                    //Update Tags - Current
                    IEnumerable<shortCut> UpdateTagsCurrentShortcut = from rec in xdoc.Descendants("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Update") && s.Attribute("CommandName").Value.Contains("Tags") && s.Attribute("CommandName").Value.Contains("Current") && s.Attribute("CommandId").Value.Contains("GMS"))
                                                                      select new shortCut()
                                                                    {
                                                                        cName = (string)rec.Attribute("CommandName"),
                                                                        sKeys = (string)rec.Attribute("Shortcuts"),
                                                                        pKeys = (string)rec.Attribute("Paths")
                                                                    };
                    if (UpdateTagsCurrentShortcut.Any())
                    {
                        foreach (shortCut sc in UpdateTagsCurrentShortcut)
                        {
                            if (sc.pKeys == "GMS>Tagging Tools")
                            {
                                if (null == sc.sKeys)
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Update") && s.Attribute("CommandName").Value.Contains("Tags") && s.Attribute("CommandName").Value.Contains("Current") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.Add(new XAttribute("Shortcuts", "UTC"));
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                                else if (sc.sKeys != "UTC")
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Update") && s.Attribute("CommandName").Value.Contains("Tags") && s.Attribute("CommandName").Value.Contains("Current") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.SetAttributeValue("Shortcuts", "UTC");
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                            }
                            else
                            {
                                xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Update") && s.Attribute("CommandName").Value.Contains("Tags") && s.Attribute("CommandName").Value.Contains("Current") && s.Attribute("CommandId").Value.Contains("GMS")).Remove();
                                xdoc.Save(pathToShortcutsFile);
                                addToXML(pathToShortcutsFile, "Update Tags:Current Sheet", "CustomCtrl_%CustomCtrl_%CustomCtrl_%GMS%Tagging Tools%Update Tags%Current Sheet", "UTC", "GMS>Tagging Tools");
                            }
                        }
                    }
                    else
                    {
                        addToXML(pathToShortcutsFile, "Update Tags:Current Sheet", "CustomCtrl_%CustomCtrl_%CustomCtrl_%GMS%Tagging Tools%Update Tags%Current Sheet", "UTC", "GMS>Tagging Tools");
                    }



                    //Update Tags - By Set
                    IEnumerable<shortCut> UpdateTagsBySetShortcut = from rec in xdoc.Descendants("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Update") && s.Attribute("CommandName").Value.Contains("Tags") && s.Attribute("CommandName").Value.Contains("By Set") && s.Attribute("CommandId").Value.Contains("GMS"))
                                                                    select new shortCut()
                                                                    {
                                                                        cName = (string)rec.Attribute("CommandName"),
                                                                        sKeys = (string)rec.Attribute("Shortcuts"),
                                                                        pKeys = (string)rec.Attribute("Paths")
                                                                    };
                    if (UpdateTagsBySetShortcut.Any())
                    {
                        foreach (shortCut sc in UpdateTagsBySetShortcut)
                        {
                            if (sc.pKeys == "GMS>Tagging Tools")
                            {
                                if (null == sc.sKeys)
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Update") && s.Attribute("CommandName").Value.Contains("Tags") && s.Attribute("CommandName").Value.Contains("By Set") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.Add(new XAttribute("Shortcuts", "UTS"));
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                                else if (sc.sKeys != "UTS")
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Update") && s.Attribute("CommandName").Value.Contains("Tags") && s.Attribute("CommandName").Value.Contains("By Set") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.SetAttributeValue("Shortcuts", "UTS");
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                            }
                            else
                            {
                                xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Update") && s.Attribute("CommandName").Value.Contains("Tags") && s.Attribute("CommandName").Value.Contains("By Set") && s.Attribute("CommandId").Value.Contains("GMS")).Remove();
                                xdoc.Save(pathToShortcutsFile);
                                addToXML(pathToShortcutsFile, "Update Tags:By Set", "CustomCtrl_%CustomCtrl_%CustomCtrl_%GMS%Tagging Tools%Update Tags%By Set", "UTS", "GMS>Tagging Tools");
                            }
                        }
                    }
                    else
                    {
                        addToXML(pathToShortcutsFile, "Update Tags:By Set", "CustomCtrl_%CustomCtrl_%CustomCtrl_%GMS%Tagging Tools%Update Tags%By Set", "UTS", "GMS>Tagging Tools");
                    }

                    //Update Tags - All
                    IEnumerable<shortCut> UpdateTagsAllShortcut = from rec in xdoc.Descendants("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Update") && s.Attribute("CommandName").Value.Contains("Tags") && s.Attribute("CommandName").Value.Contains("All") && s.Attribute("CommandId").Value.Contains("GMS"))
                                                                    select new shortCut()
                                                                    {
                                                                        cName = (string)rec.Attribute("CommandName"),
                                                                        sKeys = (string)rec.Attribute("Shortcuts"),
                                                                        pKeys = (string)rec.Attribute("Paths")
                                                                    };
                    if (UpdateTagsAllShortcut.Any())
                    {
                        foreach (shortCut sc in UpdateTagsAllShortcut)
                        {
                            if (sc.pKeys == "GMS>Tagging Tools")
                            {
                                if (null == sc.sKeys)
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Update") && s.Attribute("CommandName").Value.Contains("Tags") && s.Attribute("CommandName").Value.Contains("All") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.Add(new XAttribute("Shortcuts", "UTA"));
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                                else if (sc.sKeys != "UTA")
                                {
                                    var elementAttribute = xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Update") && s.Attribute("CommandName").Value.Contains("Tags") && s.Attribute("CommandName").Value.Contains("All") && s.Attribute("CommandId").Value.Contains("GMS")).Single();
                                    elementAttribute.SetAttributeValue("Shortcuts", "UTA");
                                    xdoc.Save(pathToShortcutsFile);
                                    break;
                                }
                            }
                            else
                            {
                                xdoc.Root.Elements("ShortcutItem").Where(s => s.Attribute("CommandName").Value.Contains("Update") && s.Attribute("CommandName").Value.Contains("Tags") && s.Attribute("CommandName").Value.Contains("All") && s.Attribute("CommandId").Value.Contains("GMS")).Remove();
                                xdoc.Save(pathToShortcutsFile);
                                addToXML(pathToShortcutsFile, "Update Tags:All", "CustomCtrl_%CustomCtrl_%CustomCtrl_%GMS%Tagging Tools%Update Tags%All", "UTA", "GMS>Tagging Tools");
                            }
                        }
                    }
                    else
                    {
                        addToXML(pathToShortcutsFile, "Update Tags:All", "CustomCtrl_%CustomCtrl_%CustomCtrl_%GMS%Tagging Tools%Update Tags%All", "UTA", "GMS>Tagging Tools");
                    }




                }
                catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("UpdateKeyboardShortcuts", __ex); }
                //catch (Exception e)
                //{
                //    MessageBox.Show("Error updating keyboardshortcuts.xml" + System.Environment.NewLine + e.Message + System.Environment.NewLine + e.InnerException.Message);
                //}
            }
            else
            {
                //try to copy from C:\GMS\Revit\<version>\ folder to %appdata%\Autodesk\Revit\Autodesk Revit <version>\ folder
                try
                {
                    string filename = "KeyboardShortcuts.xml";
                    string sourcePath = GMSRevitAddin.GmsPaths.GmsRevitRoot;
                    string targetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Autodesk\Revit\Autodesk Revit " + GMSRevitAddin.GmsVersion.Number);

                    string sourceFile = System.IO.Path.Combine(sourcePath, filename);
                    string destFile = System.IO.Path.Combine(targetPath, filename);

                    System.IO.File.Copy(sourceFile, destFile, true);
                }
                catch (Exception e)
                {
                    GMSRevitAddin.GmsUi.ShowError("Error copying keyboardshortcuts.xml" + System.Environment.NewLine + e.Message, "UKS2 Error", e);
                }
            }
        }
    }
}