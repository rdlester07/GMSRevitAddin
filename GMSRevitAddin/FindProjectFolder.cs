using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace FindProjectFolder
{
    /// <summary>
    /// Resolves the on-disk project folder for the given document — either by matching the
    /// project's number against a subfolder of the shared projects root (for cloud/BIM 360 models),
    /// or from the document's worksharing central model path (for file-based workshared models) —
    /// then locates/creates the project's "GMS config" and "GMS config\Images" (+ "Temp") subfolders.
    /// </summary>
    public class FindProjectFolder
    {
        /// <summary>
        /// Given a project directory on the share, locates the Revit-model folder by structure rather
        /// than a fixed subpath: the first subfolder whose name contains "Main" (case-insensitive),
        /// then the first subfolder inside it whose name contains "RevitModel". This tolerates varying
        /// on-share layouts (e.g. "3 - Main\RevitModel" vs "5 - Main\2 - RM - RevitModel"). Returns
        /// the resolved folder path, or null if either level can't be found. When more than one
        /// candidate matches at a level it uses the first and warns (does not fail).
        /// </summary>
        public static string FindRevitModelFolder(string projectDirectory)
        {
            if (string.IsNullOrWhiteSpace(projectDirectory) || !Directory.Exists(projectDirectory))
            {
                return null;
            }

            string mainFolder = PickFolderContaining(projectDirectory, "Main");
            if (mainFolder == null)
            {
                return null;
            }

            return PickFolderContaining(mainFolder, "RevitModel");
        }

        /// <summary>
        /// Returns the first immediate subfolder of <paramref name="parent"/> whose leaf name contains
        /// <paramref name="keyword"/> (case-insensitive), or null if none. Warns (listing candidates)
        /// when more than one matches, then uses the first.
        /// </summary>
        private static string PickFolderContaining(string parent, string keyword)
        {
            string[] matches = Directory.GetDirectories(parent)
                .Where(d => System.IO.Path.GetFileName(d).IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();

            if (matches.Length == 0)
            {
                return null;
            }

            if (matches.Length > 1)
            {
                string candidates = string.Join("\n", matches.Select(System.IO.Path.GetFileName));
                GMSRevitAddin.GmsUi.ShowWarning(
                    "More than one folder containing \"" + keyword + "\" was found under:\n" + parent +
                    "\n\nUsing the first:\n" + candidates,
                    "Ambiguous project folder");
            }

            return matches[0];
        }

        /// <summary>
        /// Returns the requested project subfolder path: "model" (the Revit model folder), "config"
        /// ("GMS config"), "images" ("GMS config\Images"), or the raw resolved model path for any
        /// other value. Returns null if the folder can't be resolved or created.
        /// </summary>
        public static string FindFolder(Document doc, string folder)
        {
            string Path = null;
            bool error = false;

            // Cloud-hosted models don't expose a local worksharing path — instead, match the
            // project's Number against the GMS shared "Projects" root to find the project's folder.
            if (doc.IsModelInCloud)
            {
                string projectsFolder = GMSRevitAddin.GmsPaths.ProjectsFolder;
                ProjectInfo projectInfo = doc.ProjectInformation;
                string projectNumber = projectInfo.Number.Replace("#", "");
                if (!string.IsNullOrWhiteSpace(projectNumber))
                {
                    string[] projectDirectories = System.IO.Directory.GetDirectories(projectsFolder, projectNumber + "*", SearchOption.TopDirectoryOnly);
                    if (projectDirectories.Any())
                    {
                        if (projectDirectories.Count() > 1)
                        {
                            error = true;
                            TaskDialog td = new TaskDialog("Error");
                            td.MainInstruction = "More than one project directory found beginning with " + projectNumber + ".";
                            td.CommonButtons = TaskDialogCommonButtons.Close;
                            TaskDialogResult tdr = td.Show();

                            Path = null;
                        }
                        else
                        {
                            string revitFolder = FindRevitModelFolder(projectDirectories[0]);
                            if (!string.IsNullOrWhiteSpace(revitFolder))
                            {
                                Path = System.IO.Path.Combine(revitFolder, "blank.rvt");
                            }
                            else
                            {
                                error = true;
                                TaskDialog td = new TaskDialog("Error");
                                td.MainInstruction = "Could not locate the Revit model folder (a \"Main\" folder containing a \"RevitModel\" subfolder) under " + projectDirectories[0] + ".";
                                td.CommonButtons = TaskDialogCommonButtons.Close;
                                TaskDialogResult tdr = td.Show();

                                Path = null;
                            }
                        }
                    }
                    else
                    {
                        error= true;
                        TaskDialog td = new TaskDialog("Error");
                        td.MainInstruction = "Could not find project directory beginning with " + projectNumber + ".";
                        td.CommonButtons = TaskDialogCommonButtons.Close;
                        TaskDialogResult tdr = td.Show();

                        Path = null;
                    }
                }
                else
                {
                    error= true;
                    TaskDialog td = new TaskDialog("Error");
                    td.MainInstruction = "Project Number is empty. Please set the Project Number under Manage > Project Information.";
                    td.CommonButtons = TaskDialogCommonButtons.Close;
                    TaskDialogResult tdr = td.Show();

                    Path = null;
                }
            }
            else
            {
                // File-based workshared model — derive the path from its worksharing central model path.
                Path = ModelPathUtils.ConvertModelPathToUserVisiblePath(doc.GetWorksharingCentralModelPath());
            }

            // Strip the model's filename to get the containing project/Revit folder.
            string[] parsePath = Path.Split('\\');
            string projectRevitFolder = Path.Replace(parsePath[parsePath.Length - 1], "");
            string configDirectory = System.IO.Path.Combine(projectRevitFolder, "GMS config");
            string imagesDirectory = System.IO.Path.Combine(configDirectory, "Images");
            string tempImageDirectory = System.IO.Path.Combine(imagesDirectory, "Temp");


            // Ensure the config/images/temp-images folders exist, creating any that are missing.
            if (configDirectory != null && !Directory.Exists(configDirectory))
            {
                try
                {
                    Directory.CreateDirectory(configDirectory);
                }
                catch
                {
                    error= true;
                    MessageBox.Show("Unable to create Config folder.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }


            if (imagesDirectory != null && !Directory.Exists(imagesDirectory))
            {
                try
                {
                    Directory.CreateDirectory(imagesDirectory);
                }
                catch
                {
                    error= true;
                    MessageBox.Show("Unable to create Image folder.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            if (tempImageDirectory != null && !Directory.Exists(tempImageDirectory))
            {
                try
                {
                    Directory.CreateDirectory(tempImageDirectory);
                }
                catch
                {
                    error = true;
                    MessageBox.Show("Unable to create Temp image folder.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            if (error)
            {
                return null;
            }
            else
            {
                switch (folder)
                {
                    case "model":
                        return projectRevitFolder;
                    case "config":
                        return configDirectory;
                    case "images":
                        return imagesDirectory;
                    default:
                        return Path;
                }
            }
        }
    }
}