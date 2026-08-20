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
using System.Data.OleDb;
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
using System.Security.AccessControl;

namespace EmbedImages
{
    /// <summary>
    /// Finds every generic-model family instance whose family name starts with "ce_em" or "ce_st"
    /// (embed/stamp families), opens each distinct family for editing, and exports its active view
    /// as a PNG image into the project's "images\Temp" folder (resolved via
    /// <c>FindProjectFolder.FindFolder</c>).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CreateEmbedImages : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            Autodesk.Revit.DB.Document doc = uiApp.ActiveUIDocument.Document;
            Autodesk.Revit.ApplicationServices.Application app = doc.Application;
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            //bool error = false;
            Dictionary<string, ElementId> embeds = new Dictionary<string, ElementId>();

            // Collect one representative element id per distinct "ce_em"/"ce_st" family in the model.
            List<Element> genericModels = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel).ToList();
            foreach (Element el in genericModels)
            {
                string familyName = el.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM).AsValueString();
                if (familyName.ToLower().StartsWith("ce_em") || familyName.ToLower().StartsWith("ce_st"))
                {
                    if (!embeds.ContainsKey(familyName))
                    {
                        embeds.Add(familyName, el.Id);
                    }
                }
            }

            // Resolve the project's shared "images" folder on the file share (creates it if missing).
            string imagesDirectory = FindProjectFolder.FindProjectFolder.FindFolder(doc, "images");

            foreach (KeyValuePair<string, ElementId> kvp in embeds)
            {
                // Open the family for editing so its active view can be exported as an image.
                Family family = doc.GetElement(kvp.Value) as Family;
                Document familyDoc = doc.EditFamily(family);
                Autodesk.Revit.DB.View currentView = familyDoc.ActiveView;
                List<ElementId> views = new List<ElementId>();
                views.Add(currentView.Id);

                // Sanitize the family name for use as a filename, and export into images\Temp.
                string kvpKey = kvp.Key.ReplaceSpecialCharacters();
                string path = System.IO.Path.Combine(imagesDirectory, kvpKey + ".png");
                string temp = System.IO.Path.Combine(imagesDirectory, "Temp");
                string tempPath = System.IO.Path.Combine(temp, kvpKey + ".png");
                ThinLinesOptions.AreThinLinesEnabled = false;
                ImageExportOptions ieo = new ImageExportOptions
                {
                    FilePath = tempPath,
                    HLRandWFViewsFileType = ImageFileType.PNG,
                    ImageResolution = ImageResolution.DPI_600,
                    ShouldCreateWebSite = false,
                    ZoomType = ZoomFitType.Zoom,
                    Zoom = 100,
                    ExportRange = ExportRange.SetOfViews
                };
                ieo.SetViewsAndSheets(views);
                doc.ExportImage(ieo);


                familyDoc.Close();
            }
            return Result.Succeeded;
        }
    }
    
    public static class MethodExtension
    {
        /// <summary>Replaces filesystem-unsafe/special characters in a string with underscores, for use as a filename.</summary>
        public static string ReplaceSpecialCharacters(this string str)
        {
            List<char> badChars = new List<char>
            {
              '<', '>', ':', '\"', '/', '\\', '|', '?', '*', '.', '@', '#', '$', '%', '^', '(', ')', '+', '=', '`', '~', ';', ','
            };
            StringBuilder sb = new StringBuilder();
            foreach (char c in str)
            {
                if (!badChars.Contains(c))
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
}