using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB.Visual;
using View = Autodesk.Revit.DB.View;
using System.Diagnostics;
using System.Threading;

namespace BuildBunkForm
{
    /// <summary>
    /// Modeless "Build/Bunk Tool" dialog for batch-editing curtain wall panel (unit) status fields —
    /// Build #, Bunk #, Assembled/Glazed/Bunked/QC/Shipped/Truck/Structural-Tested/Weather-Tested
    /// dates and Weight — across a set of selected panels shown in a grid. Because it's modeless while
    /// Revit's API is single-threaded, edits are pushed through an <see cref="ExternalEvent"/>
    /// (<see cref="RequestHandler"/>) rather than applied directly from UI event handlers. Opened via
    /// <see cref="LaunchForm"/> (wired to the ribbon) which enforces a single running instance.
    /// </summary>
    public partial class BuildBunkForm : System.Windows.Forms.Form
    {
        public static System.Windows.Forms.Form thisForm = null;
        private RequestHandler m_Handler;
        private ExternalEvent m_ExEvent;

        public BuildBunkForm(ExternalEvent exEvent, RequestHandler handler)
        {
            InitializeComponent();
            m_Handler = handler;
            m_ExEvent = exEvent;
        }

        /// <summary>Applies the shared theme, marks the tool as running (blocks a second instance via
        /// <see cref="LaunchForm.running"/>), restores the saved size/location, and sets Apply as the
        /// form's default (Enter-key) button.</summary>
        private void BuildBunkForm_Load(object sender, EventArgs e)
        {
            GMSRevitAddin.DarkTheme.MarkPrimary(buttonApply);
            GMSRevitAddin.DarkTheme.Apply(this);
            LaunchForm.running = true;
            thisForm = this;
            updateSizeLocation();
            this.AcceptButton = buttonApply;
        }

        /// <summary>Restores the form's size/location from <c>Properties.Settings</c> if previously saved
        /// (sentinel value (-100,-100) means "never saved" — leave the designer default).</summary>
        public void updateSizeLocation()
        {
            if (GMSRevitAddin.Properties.Settings.Default.BBFormSize != new System.Drawing.Size(-100, -100))
            {
                this.Size = GMSRevitAddin.Properties.Settings.Default.BBFormSize;
            }
            if (GMSRevitAddin.Properties.Settings.Default.BBFormLocation != new System.Drawing.Point(-100, -100))
            {
                this.Location = GMSRevitAddin.Properties.Settings.Default.BBFormLocation;
            }
        }

        /// <summary>Persists the form's location whenever it moves.</summary>
        public void OnLocationChange(object sender, EventArgs e)
        {
            GMSRevitAddin.Properties.Settings.Default.BBFormLocation = this.Location;
            GMSRevitAddin.Properties.Settings.Default.Save();

        }
        /// <summary>Persists the form's size whenever it's resized.</summary>
        public void OnSizeChange(object sender, EventArgs e)
        {
            GMSRevitAddin.Properties.Settings.Default.BBFormSize = this.Size;
            GMSRevitAddin.Properties.Settings.Default.Save();
        }

        /// <summary>Releases the <see cref="ExternalEvent"/>/handler when the form closes — required
        /// cleanup since Revit keeps the event registered until explicitly disposed.</summary>
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            m_ExEvent.Dispose();
            m_ExEvent = null;
            m_Handler = null;

            base.OnFormClosed(e);
        }

        private void EnableCommands(bool status)
        {
            foreach (System.Windows.Forms.Control ctrl in this.Controls)
            {
                ctrl.Enabled = status;
            }
        }

        /// <summary>Queues a request on the shared <see cref="RequestHandler"/> and raises the
        /// <see cref="ExternalEvent"/> so Revit executes it on its own thread, then disables the
        /// form's controls until <see cref="WakeUp"/> is called back (avoids re-entrant clicks while
        /// the transaction is in flight).</summary>
        private void MakeRequest(RequestId request)
        {
            m_Handler.Request.Make(request);
            m_ExEvent.Raise();
            DozeOff();
        }

        private void DozeOff()
        {
            EnableCommands(false);
        }

        /// <summary>Re-enables the form's controls; called by <see cref="RequestHandler.Execute"/>
        /// once the requested Revit-side work has completed.</summary>
        public void WakeUp()
        {
            EnableCommands(true);
        }

        /// <summary>Refreshes the grid and clears all input fields after a successful parameter update
        /// — re-reads the same unit set (<see cref="unitIDList"/>) so the grid reflects the new values.</summary>
        public static void postParamUpdate()
        {
            DataGridView dgv1 = thisForm.Controls["dataGridView1"] as DataGridView;
            dgv1.Rows.Clear();

            List<List<string>> rows = new List<List<string>>();
            rows.Clear();
            rows = LaunchForm.populateGrid(unitIDList);

            if (rows.Any())
            {
                foreach (List<string> rowdata in rows)
                {
                    dgv1.Rows.Add(rowdata.ToArray());
                }
            }
            thisForm.Controls["textBoxAssembled"].Text = "";
            thisForm.Controls["textBoxBuildNum"].Text = "";
            thisForm.Controls["textBoxBunked"].Text = "";
            thisForm.Controls["textBoxBunkNum"].Text = "";
            thisForm.Controls["textBoxGlazed"].Text = "";
            thisForm.Controls["textBoxQC"].Text = "";
            thisForm.Controls["textBoxShipped"].Text = "";
            thisForm.Controls["textBoxStruct"].Text = "";
            thisForm.Controls["textBoxTruck"].Text = "";
            thisForm.Controls["textBoxWeather"].Text = "";
            thisForm.Controls["textBoxWeight"].Text = "";
        }

        public static List<string> listToExecute = new List<string>();
        public static List<ElementId> unitIDList = new List<ElementId>();

        /// <summary>Replaces the current working set with a fresh Revit selection prompt (clears the
        /// grid first, unlike <see cref="buttonAddtoSet_Click"/> which appends).</summary>
        private void buttonNewSet_Click(object sender, EventArgs e)
        {
            dataGridView1.Rows.Clear();
            listToExecute.Clear();
            unitIDList.Clear();
            List<ElementId> elemIDs = new List<ElementId>();
            elemIDs.Clear();
            elemIDs = LaunchForm.getSelection();

            if (elemIDs.Any())
            {
                dataGridView1.Rows.Clear();

                List<List<string>> rows = new List<List<string>>();
                rows.Clear();
                rows = LaunchForm.populateGrid(elemIDs);

                if (rows.Any())
                {
                    foreach (List<string> rowdata in rows)
                    {
                        dataGridView1.Rows.Add(rowdata.ToArray());
                    }
                }
            }
            else
            {
                MessageBox.Show("No curtain wall panels were selected.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>Prompts for another Revit selection and appends the resulting panels to the
        /// existing grid rows (does not clear the grid first).</summary>
        private void buttonAddtoSet_Click(object sender, EventArgs e)
        {
            List<ElementId> elemIDs = new List<ElementId>();
            elemIDs.Clear();
            elemIDs = LaunchForm.getSelection();

            if (elemIDs.Any())
            {
                List<List<string>> rows = new List<List<string>>();
                rows.Clear();
                rows = LaunchForm.populateGrid(elemIDs);

                if (rows.Any())
                {
                    foreach (List<string> rowdata in rows)
                    {
                        dataGridView1.Rows.Add(rowdata.ToArray());
                    }
                }
            }
            else
            {
                MessageBox.Show("No curtain wall panels were selected.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

        }

        /// <summary>Removes the currently-selected grid rows from the working set (does not delete
        /// anything in the Revit model — only removes rows from the local grid).</summary>
        private void buttonRemoveFromSet_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow item in this.dataGridView1.SelectedRows)
            {
                dataGridView1.Rows.RemoveAt(item.Index);
            }
        }

        /// <summary>
        /// Collects every non-blank text field into <see cref="listToExecute"/> (fixed 11-slot order
        /// matching <see cref="RequestHandler.Execute"/>'s unpacking) and every grid row's ElementId
        /// into <see cref="unitIDList"/>, then raises the Apply request. Blank fields are left as "" so
        /// <see cref="RequestHandler.updateParams"/> skips them (a blank means "don't change this
        /// field" for that batch of units — see the check on each field below).
        /// </summary>
        private void buttonApply_Click(object sender, EventArgs e)
        {
            Int32 rowCount = dataGridView1.Rows.Count;
            if (rowCount < 1)
            {
                MessageBox.Show("No units in selection set.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                bool foundChange = false;
                string buildNumber = "";
                string bunkNumber = "";
                string assembled = "";
                string glazed = "";
                string bunked = "";
                string QCNumber = "";
                string weight = "";
                string shipped = "";
                string truck = "";
                string structural = "";
                string weather = "";

                if (!string.IsNullOrWhiteSpace(textBoxBuildNum.Text))
                {
                    buildNumber = textBoxBuildNum.Text.Trim();
                    foundChange = true;
                }
                if (!string.IsNullOrWhiteSpace(textBoxBunkNum.Text))
                {
                    bunkNumber = textBoxBunkNum.Text.Trim();
                    foundChange = true;
                }
                if (!string.IsNullOrWhiteSpace(textBoxAssembled.Text))
                {
                    assembled = textBoxAssembled.Text.Trim();
                    foundChange = true;
                }
                if (!string.IsNullOrWhiteSpace(textBoxGlazed.Text))
                {
                    glazed = textBoxGlazed.Text.Trim();
                    foundChange = true;
                }
                if (!string.IsNullOrWhiteSpace(textBoxBunked.Text))
                {
                    bunked = textBoxBunked.Text.Trim();
                    foundChange = true;
                }
                if (!string.IsNullOrWhiteSpace(textBoxQC.Text))
                {
                    QCNumber = textBoxQC.Text.Trim();
                    foundChange = true;
                }
                if (!string.IsNullOrWhiteSpace(textBoxWeight.Text))
                {
                    weight = textBoxWeight.Text.Trim();
                    foundChange = true;
                }
                if (!string.IsNullOrWhiteSpace(textBoxShipped.Text))
                {
                    shipped = textBoxShipped.Text.Trim();
                    foundChange = true;
                }
                if (!string.IsNullOrWhiteSpace(textBoxTruck.Text))
                {
                    truck = textBoxTruck.Text.Trim();
                    foundChange = true;
                }
                if (!string.IsNullOrWhiteSpace(textBoxStruct.Text))
                {
                    structural = textBoxStruct.Text.Trim();
                    foundChange = true;
                }
                if (!string.IsNullOrWhiteSpace(textBoxWeather.Text))
                {
                    weather = textBoxWeather.Text.Trim();
                    foundChange = true;
                }

                if (foundChange)
                {
                    List<string> unitsToUpdate = new List<string>();
                    unitsToUpdate.Clear();
                    unitIDList.Clear();
                    // The last column of each row holds the panel's ElementId (added by populateGrid).
                    foreach (DataGridViewRow row in dataGridView1.Rows)
                    {
                        unitsToUpdate.Add(row.Cells[row.Cells.Count - 1].Value.ToString());
                    }

                    foreach (string idNum in unitsToUpdate)
                    {
                        try
                        {
                            long idInt = Convert.ToInt64(idNum);
                            if (idInt > 0)
                            {
                                unitIDList.Add(new ElementId(idInt));
                            }
                        }
                        catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("BuildBunkForm", __ex); }
                    }

                    listToExecute.Clear();
                    listToExecute.Add(buildNumber);
                    listToExecute.Add(bunkNumber);
                    listToExecute.Add(assembled);
                    listToExecute.Add(glazed);
                    listToExecute.Add(bunked);
                    listToExecute.Add(QCNumber);
                    listToExecute.Add(weight);
                    listToExecute.Add(shipped);
                    listToExecute.Add(truck);
                    listToExecute.Add(structural);
                    listToExecute.Add(weather);

                    MakeRequest(RequestId.Apply);
                }
                else
                {
                    MessageBox.Show("No data found in fields. Enter information into the field to be updated before clicking Apply", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        /// <summary>Persists size/location and clears the running flag so the tool can be relaunched.</summary>
        private void BuildBunkForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            GMSRevitAddin.Properties.Settings.Default.BBFormLocation = this.Location;
            GMSRevitAddin.Properties.Settings.Default.BBFormSize = this.Size;
            GMSRevitAddin.Properties.Settings.Default.Save();
            LaunchForm.running = false;
        }
    }

    /// <summary>
    /// Entry point wired as the ribbon's Build/Bunk Tool command. Since the form is modeless and
    /// persists across command invocations, <see cref="running"/> guards against opening a second
    /// instance — a repeat click while the form is open is a no-op (<see cref="Result.Cancelled"/>).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class LaunchForm : IExternalCommand
    {
        public static System.Windows.Forms.Form thisForm = null;
        public static ExternalCommandData ECD = null;
        public static string mess = null;
        public static ElementSet ElSet = null;
        public static Document docu = null;
        public static UIDocument uidoc = null;
        public static Document doc = null;
        public static bool running = false;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            ECD = commandData;
            mess = message;
            ElSet = elements;
            uidoc = ECD.Application.ActiveUIDocument;
            doc = uidoc.Document;

            if (!running)
            {
                try
                {
                    Application app = new Application();
                    app.ShowForm(commandData.Application);
                    return Result.Succeeded;
                }
                catch (Exception ex)
                {

                    message = ex.Message;
                    return Result.Failed;
                }
            }
            else
            {
                return Result.Cancelled;
            }
        }

        /// <summary>Prompts the user to pick elements in the model and returns their ids; clears the
        /// active Revit selection afterward so the picked elements aren't left highlighted. Returns an
        /// empty list on Escape/cancel (caught silently) or any other picking error.</summary>
        public static List<ElementId> getSelection()
        {
            try
            {
                List<ElementId> eids = new List<ElementId>();
                eids.Clear();
                IList<Reference> pickedElements = uidoc.Selection.PickObjects(ObjectType.Element);
                if (pickedElements.Any())
                {
                    eids = (from Reference r in pickedElements select r.ElementId).ToList();
                    uidoc.Selection.SetElementIds(new List<ElementId>());
                    return eids;
                }
                else
                {
                    eids.Clear();
                    return eids;
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                List<ElementId> eids = new List<ElementId>();
                eids.Clear();
                return eids;
            }
        }

        /// <summary>
        /// Filters the given element ids down to curtain wall panels, reads each panel's Unit/Status
        /// parameters, and returns one row per panel (as string arrays, last entry = ElementId) sorted
        /// by "Unit - Address". Panels missing a "Unit - Mark Number" or "Unit - Address" are skipped
        /// (not a valid unit panel).
        /// </summary>
        public static List<List<string>> populateGrid(List<ElementId> elemList)
        {
            long BIcategoryID = (long)BuiltInCategory.OST_CurtainWallPanels;

            List<ElementId> eids = new List<ElementId>();
            eids.Clear();

            foreach (ElementId ei in elemList)
            {
                try
                {
                    Element el = uidoc.Document.GetElement(ei);
                    long categoryID = el.Category.Id.Value;
                    if (categoryID == BIcategoryID)
                    {
                        eids.Add(ei);
                    }
                }
                catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("BuildBunkForm", __ex); }
            }

            if (eids.Count > 0)
            {
                Dictionary<string, string[]> properties = new Dictionary<string, string[]>();
                properties.Clear();
                foreach (ElementId elid in eids)
                {
                    Element panel = doc.GetElement(elid);
                    ParameterSet parameters = panel.Parameters;
                    string unitNumber = "";
                    string address = "";
                    string origin = "";
                    string sheet = "";
                    string level = "";
                    string released = "";
                    string buildNumber = "";
                    string bunkNumber = "";
                    string assembled = "";
                    string glazed = "";
                    string bunked = "";
                    string QCNumber = "";
                    string weight = "";
                    string shipped = "";
                    string truck = "";
                    string structural = "";
                    string weather = "";

                    foreach (Parameter param in parameters)
                    {
                        if (param.Definition.Name == "Unit - Mark Number")
                        {
                            unitNumber = param.AsString();
                        }
                        if (param.Definition.Name == "Unit - Address")
                        {
                            address = param.AsString();
                        }
                        if (param.Definition.Name == "Origin")
                        {
                            origin = param.AsString();
                        }
                        if (param.Definition.Name == "Origin Sheet")
                        {
                            sheet = param.AsString();
                        }
                        if (param.Definition.Name == "Unit - Level")
                        {
                            level = param.AsString();
                        }
                        if (param.Definition.Name == "Unit - Released")
                        {
                            released = param.AsString();
                        }
                        if (param.Definition.Name == "Unit - Build")
                        {
                            buildNumber = param.AsString();
                        }
                        if (param.Definition.Name == "Unit - Bunk")
                        {
                            bunkNumber = param.AsString();
                        }
                        if (param.Definition.Name == "Status - Assembled")
                        {
                            assembled = param.AsString();
                        }
                        if (param.Definition.Name == "Status - Glazed")
                        {
                            glazed = param.AsString();
                        }
                        if (param.Definition.Name == "Status - Bunked")
                        {
                            bunked = param.AsString();
                        }
                        if (param.Definition.Name == "Status - QC Number")
                        {
                            QCNumber = param.AsString();
                        }
                        if (param.Definition.Name == "Status - Weight")
                        {
                            weight = param.AsString();
                        }
                        if (param.Definition.Name == "Status - Shipped")
                        {
                            shipped = param.AsString();
                        }
                        if (param.Definition.Name == "Status - Truck")
                        {
                            truck = param.AsString();
                        }
                        if (param.Definition.Name == "Status - Structural Tested")
                        {
                            structural = param.AsString();
                        }
                        if (param.Definition.Name == "Status - Weather Tested")
                        {
                            weather = param.AsString();
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(unitNumber))
                    {
                        string[] paramDefs = { unitNumber, address, origin, sheet, level, released, buildNumber, bunkNumber, assembled, glazed, bunked, QCNumber, weight, shipped, truck, structural, weather, elid.ToString() };
                        if (!string.IsNullOrWhiteSpace(address))
                        {
                            properties.Add(address, paramDefs);
                        }
                    }
                }

                if (properties.Any())
                {
                    List<string> keys = properties.Keys.ToList();
                    keys.Sort();

                    List<List<string>> rows = new List<List<string>>();
                    rows.Clear();

                    foreach (string key in keys)
                    {
                        rows.Add(properties[key].ToList());
                    }

                    return rows;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                MessageBox.Show("No curtain wall panels were found in selection.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
        }
    }

    /// <summary>
    /// A minimal <see cref="IExternalApplication"/> used only as a factory/owner for the modeless
    /// <see cref="BuildBunkForm"/> instance — creates the <see cref="ExternalEvent"/>/
    /// <see cref="RequestHandler"/> pair and shows the form on first launch, reusing the same
    /// instance on subsequent launches unless it was disposed.
    /// </summary>
    public class Application : IExternalApplication
    {
        internal static Application thisApp = null;
        private BuildBunkForm m_MyForm;

        public Result OnShutdown(UIControlledApplication application)
        {
            if (m_MyForm != null && m_MyForm.Visible)
            {
                m_MyForm.Close();
            }

            return Result.Succeeded;
        }

        public Result OnStartup(UIControlledApplication application)
        {
            m_MyForm = null;
            thisApp = this;

            return Result.Succeeded;
        }

        /// <summary>Creates the form (with its <see cref="ExternalEvent"/>/<see cref="RequestHandler"/>)
        /// on first call and shows it; a no-op if the form is already open.</summary>
        public void ShowForm(UIApplication uiapp)
        {
            thisApp = this;
            if (m_MyForm == null || m_MyForm.IsDisposed)
            {
                RequestHandler handler = new RequestHandler();

                ExternalEvent exEvent = ExternalEvent.Create(handler);

                m_MyForm = new BuildBunkForm(exEvent, handler);
                m_MyForm.Show();
            }
        }

        /// <summary>Re-enables the form's controls after an <see cref="ExternalEvent"/>-driven update
        /// completes; called from <see cref="RequestHandler.Execute"/>'s <c>finally</c> block.</summary>
        public void WakeFormUp()
        {
            if (m_MyForm != null)
            {
                m_MyForm.WakeUp();
            }
        }
    }

    /// <summary>
    /// <see cref="IExternalEventHandler"/> that performs the actual Revit-thread work for the
    /// Build/Bunk Tool — applying batched parameter edits to every selected panel inside one
    /// transaction. The form (running on the UI thread) queues a <see cref="Request"/> and raises the
    /// paired <see cref="ExternalEvent"/>; Revit then calls <see cref="Execute"/> on its own thread.
    /// </summary>
    public class RequestHandler : IExternalEventHandler
    {
        private Request m_request = new Request();

        public Request Request
        {
            get { return m_request; }
        }

        public string GetName()
        {
            return "GMSRevitAddin Build/Bunk Tool";
        }

        // The caller is responsible for the enclosing transaction so that all parameter
        // writes for the unit batch commit together (one transaction for the whole batch
        // instead of one transaction per parameter per unit).
        /// <summary>Sets a parameter's value, with "-" treated as a special "clear this field" sentinel.</summary>
        public void updateParams(Parameter p, string value)
        {
            if (value == "-")
            {
                p.Set("");
            }
            else
            {
                p.Set(value);
            }
        }

        /// <summary>
        /// Dispatches on the queued <see cref="RequestId"/>. For <see cref="RequestId.Apply"/>, unpacks
        /// <see cref="BuildBunkForm.listToExecute"/> (11 fixed-order fields) and, in a single
        /// transaction, applies each non-blank field to every unit in
        /// <see cref="BuildBunkForm.unitIDList"/>, then refreshes the grid. Always wakes the form back
        /// up in a <c>finally</c> block, even if nothing was queued or an error occurred.
        /// </summary>
            public void Execute(UIApplication app)
        {
            try
            {
                switch (Request.Take())
                {
                    case RequestId.None:
                        {
                            return;
                        }
                    case RequestId.Apply:
                        {
                            string buildNumber = BuildBunkForm.listToExecute[0];
                            string bunkNumber = BuildBunkForm.listToExecute[1];
                            string assembled = BuildBunkForm.listToExecute[2];
                            string glazed = BuildBunkForm.listToExecute[3];
                            string bunked = BuildBunkForm.listToExecute[4];
                            string QCNumber = BuildBunkForm.listToExecute[5];
                            string weight = BuildBunkForm.listToExecute[6];
                            string shipped = BuildBunkForm.listToExecute[7];
                            string truck = BuildBunkForm.listToExecute[8];
                            string structural = BuildBunkForm.listToExecute[9];
                            string weather = BuildBunkForm.listToExecute[10];

                            using (Transaction tr = new Transaction(LaunchForm.doc, "Update Unit Parameters"))
                            {
                            tr.Start();
                            foreach(ElementId elid in BuildBunkForm.unitIDList)
                            {
                                Element unit = LaunchForm.doc.GetElement(elid);
                                ParameterSet paras = unit.Parameters;

                                foreach (Parameter p in paras)
                                {
                                    if (p.Definition.Name == "Unit - Build" && !string.IsNullOrWhiteSpace(buildNumber))
                                    {
                                        updateParams(p, buildNumber);
                                        //p.Set(buildNumber);
                                    }
                                    if (p.Definition.Name == "Unit - Bunk" && !string.IsNullOrWhiteSpace(bunkNumber))
                                    {
                                        updateParams(p, bunkNumber);
                                        //p.Set(bunkNumber);
                                    }
                                    if (p.Definition.Name == "Status - Assembled" && !string.IsNullOrWhiteSpace(assembled))
                                    {
                                        updateParams(p, assembled);
                                        //p.Set(assembled);
                                    }
                                    if (p.Definition.Name == "Status - Glazed" && !string.IsNullOrWhiteSpace(glazed))
                                    {
                                        updateParams(p, glazed);

                                        //p.Set(glazed);
                                    }
                                    if (p.Definition.Name == "Status - Bunked" && !string.IsNullOrWhiteSpace(bunked))
                                    {
                                        updateParams(p, bunked);

                                        //p.Set(bunked);
                                    }
                                    if (p.Definition.Name == "Status - QC Number" && !string.IsNullOrWhiteSpace(QCNumber))
                                    {
                                        updateParams(p, QCNumber);

                                        //p.Set(QCNumber);
                                    }
                                    if (p.Definition.Name == "Status - Weight" && !string.IsNullOrWhiteSpace(weight))
                                    {
                                        updateParams(p, weight);

                                        //p.Set(weight);
                                    }
                                    if (p.Definition.Name == "Status - Shipped" && !string.IsNullOrWhiteSpace(shipped))
                                    {
                                        updateParams(p, shipped);

                                        //p.Set(shipped);
                                    }
                                    if (p.Definition.Name == "Status - Truck" && !string.IsNullOrWhiteSpace(truck))
                                    {
                                        updateParams(p, truck);

                                        //p.Set(truck);
                                    }
                                    if (p.Definition.Name == "Status - Structural Tested" && !string.IsNullOrWhiteSpace(structural))
                                    {
                                        updateParams(p, structural);

                                        //p.Set(structural);
                                    }
                                    if (p.Definition.Name == "Status - Weather Tested" && !string.IsNullOrWhiteSpace(weather))
                                    {
                                        updateParams(p, weather);

                                        //p.Set(weather);
                                    }

                                }
                            }
                            tr.Commit();
                            }

                            BuildBunkForm.postParamUpdate();
                            break;
                        }
                    default:
                        {
                            break;
                        }
                }
            }
            finally
            {
                Application.thisApp.WakeFormUp();
            }

            return;
        }
    }

    /// <summary>The set of Revit-thread operations <see cref="RequestHandler"/> can perform.</summary>
    public enum RequestId : int
    {
        None = 0,
        Apply = 1
    }

    /// <summary>Thread-safe single-slot request queue (UI thread writes via <see cref="Make"/>, the
    /// <see cref="ExternalEvent"/> callback reads/clears via <see cref="Take"/>) using
    /// <see cref="Interlocked.Exchange(ref int, int)"/> so no lock is needed.</summary>
    public class Request
    {
        private int m_request = (int)RequestId.None;

        /// <summary>Atomically reads and clears the pending request.</summary>
        public RequestId Take()
        {
            return (RequestId)Interlocked.Exchange(ref m_request, (int)RequestId.None);
        }

        /// <summary>Atomically sets the pending request (overwriting any unclaimed one).</summary>
        public void Make(RequestId request)
        {
            Interlocked.Exchange(ref m_request, (int)request);
        }
    }

    /// <summary>Minimal <see cref="IWin32Window"/> wrapper around a raw HWND — lets a WinForms dialog
    /// be parented to an arbitrary native window handle (e.g. Revit's main window).</summary>
    public class WindowHandle : IWin32Window
    {
        IntPtr _hwnd;

        public WindowHandle(IntPtr h)
        {
            Debug.Assert(IntPtr.Zero != h, "Expected non-null window hangle");

            _hwnd = h;
        }

        public IntPtr Handle
        {
            get
            {
                return _hwnd;
            }
        }
    }
}