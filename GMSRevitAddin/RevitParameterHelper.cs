using Autodesk.Revit.DB;

namespace GMSRevitAddin
{
    /// <summary>
    /// Helpers for the parameter read/write patterns that recur throughout the add-in.
    ///
    /// These centralize the "look up a parameter by name, null-check, then get/set" boilerplate
    /// and, importantly, log when a parameter is missing or a write fails instead of letting the
    /// failure pass silently. Migrate existing <c>LookupParameter(...)</c> call sites onto these
    /// incrementally (with in-Revit testing) to reduce duplication.
    /// </summary>
    public static class RevitParameterHelper
    {
        /// <summary>Read a parameter's value as a string, or return null if the element/parameter is absent.</summary>
        public static string GetString(Element element, string parameterName)
        {
            Parameter p = element == null ? null : element.LookupParameter(parameterName);
            return p == null ? null : p.AsString();
        }

        /// <summary>
        /// Set a string parameter by name. Returns true on success. Logs (rather than throwing or
        /// silently swallowing) when the parameter is missing, read-only, or the write fails -
        /// e.g. on a checked-out/owned element in a workshared model.
        /// </summary>
        public static bool TrySetString(Element element, string parameterName, string value)
        {
            if (element == null)
            {
                GmsLog.Warn("TrySetString: null element for parameter '" + parameterName + "'");
                return false;
            }

            Parameter p = element.LookupParameter(parameterName);
            if (p == null)
            {
                GmsLog.Warn("TrySetString: parameter '" + parameterName + "' not found on element " + element.Id);
                return false;
            }
            if (p.IsReadOnly)
            {
                GmsLog.Warn("TrySetString: parameter '" + parameterName + "' is read-only on element " + element.Id);
                return false;
            }

            try
            {
                p.Set(value ?? string.Empty);
                return true;
            }
            catch (System.Exception ex)
            {
                GmsLog.Error("TrySetString: failed to set '" + parameterName + "' on element " + element.Id, ex);
                return false;
            }
        }

        /// <summary>
        /// Set a parameter by name, regardless of storage type (String/Integer/Double) — unlike
        /// <see cref="TrySetString"/>, which only handles String storage. Text (String storage)
        /// parameters are set directly via <see cref="Parameter.Set(string)"/> (the same call
        /// <see cref="TrySetString"/> uses — proven correct across ~120 existing call sites); every
        /// other storage type goes through <see cref="Parameter.SetValueString(string)"/>, which
        /// parses <paramref name="value"/> as the document's UI-formatted text for that parameter
        /// (length/number/etc.). <c>SetValueString</c> is NOT used for String storage — it is meant
        /// for unit-aware numeric-ish text, and routing plain text parameters through it turned out
        /// to silently fail to write anything back (caught by the try/catch below, logged, but never
        /// visibly surfaced) — hence the branch. Returns true on success; logs (rather than throwing
        /// or silently swallowing) when the parameter is missing, read-only, or the write fails.
        /// </summary>
        public static bool TrySetValueString(Element element, string parameterName, string value)
        {
            if (element == null)
            {
                GmsLog.Warn("TrySetValueString: null element for parameter '" + parameterName + "'");
                return false;
            }

            Parameter p = element.LookupParameter(parameterName);
            if (p == null)
            {
                GmsLog.Warn("TrySetValueString: parameter '" + parameterName + "' not found on element " + element.Id);
                return false;
            }
            if (p.IsReadOnly)
            {
                GmsLog.Warn("TrySetValueString: parameter '" + parameterName + "' is read-only on element " + element.Id);
                return false;
            }

            try
            {
                if (p.StorageType == StorageType.String)
                    p.Set(value ?? string.Empty);
                else
                    p.SetValueString(value ?? string.Empty);
                return true;
            }
            catch (System.Exception ex)
            {
                GmsLog.Error("TrySetValueString: failed to set '" + parameterName + "' on element " + element.Id, ex);
                return false;
            }
        }
    }
}
