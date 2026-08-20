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

namespace PurgeFamily
{
    /// <summary>
    /// Runs Revit's built-in "Purge Unused" performance-adviser rule against the active family
    /// document, repeatedly deleting unused elements up to three passes (deleting one round of
    /// unused elements can make previously-in-use elements become unused, so it re-runs until
    /// nothing more is purgable or the attempt cap is reached). Only runs on family documents.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class PurgeFamily : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            Autodesk.Revit.DB.Document doc = uiApp.ActiveUIDocument.Document;
            Autodesk.Revit.ApplicationServices.Application app = doc.Application;
            UIApplication uiapp = new UIApplication(app);
            UIDocument uidoc = new UIDocument(doc);

            if (doc.IsFamilyDocument)
            {
                //delete all unused families
                // Run up to 3 purge passes; each pass only continues to the next if it found (and deleted) anything.
                bool continueDUE = false;
                continueDUE = deleteUnused(1, doc);
                if (continueDUE)
                {
                    continueDUE = false;
                    continueDUE = deleteUnused(2, doc);
                    if (continueDUE)
                    {
                        deleteUnused(3, doc);
                    }
                }

                //var commandId = RevitCommandId.LookupPostableCommandId(PostableCommand.PurgeUnused);
                //uiapp.PostCommand(commandId);

                MessageBox.Show("Purge Family Complete", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Error attempting to purge family. Cancelling process.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }


            return Result.Succeeded;
        }

        /// <summary>
        /// Runs Revit's "Purge Unused" PerformanceAdviser rule (by its well-known GUID) against
        /// <paramref name="doc"/>, deletes every element it reports as unused in a single
        /// transaction, and returns whether anything was deleted (i.e. whether another pass might
        /// find more). <paramref name="attempt"/> is only used for the transaction name/tracking.
        /// </summary>
        public bool deleteUnused(int attempt, Document doc)
        {
            List<ElementId> purgableElementIds = new List<ElementId>();
            IList<PerformanceAdviserRuleId> ruleIds = new List<PerformanceAdviserRuleId>();
            // GUID of Revit's built-in "Purge Unused" performance-adviser rule.
            string PurgeGuid = "e8c63650-70b7-435a-9010-ec97660c1bda";
            foreach (PerformanceAdviserRuleId rule in PerformanceAdviser.GetPerformanceAdviser().GetAllRuleIds())
            {
                if (rule.Guid == Guid.Parse(PurgeGuid))
                {
                    ruleIds.Add(rule);
                    break;
                }
            }

            // Executing the rule reports each unused/purgable element as a "failing" element.
            IList<FailureMessage> failureMessages = PerformanceAdviser.GetPerformanceAdviser().ExecuteRules(doc, ruleIds);

            if (failureMessages.Count > 0)
            {
                foreach (FailureMessage fm in failureMessages)
                {
                    ICollection<ElementId> elList = fm.GetFailingElements();
                    foreach (ElementId elid in elList)
                    {
                        if (!purgableElementIds.Contains(elid))
                        {
                            purgableElementIds.Add(elid);
                        }
                    }
                }
            }

            if (purgableElementIds.Any())
            {
                // Delete all reported unused elements in one transaction.
                using (Transaction t = new Transaction(doc, "Delete Unused Items"))
                {
                    t.Start();
                    doc.Delete(purgableElementIds);
                    t.Commit();
                }

                return true;
            }
            else
            {
                return false;
            }
        }
    }
}