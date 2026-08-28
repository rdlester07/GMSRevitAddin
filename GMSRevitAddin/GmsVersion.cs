using System;
using System.Linq;
using System.Reflection;

namespace GMSRevitAddin
{
    /// <summary>
    /// Single source of truth for build-time facts about this assembly: the Revit version it
    /// targets and when it was compiled. Both are injected at build time as
    /// <see cref="AssemblyMetadataAttribute"/>s (see the <c>&lt;AssemblyMetadata&gt;</c> items in
    /// GMSRevitAddin.csproj) rather than hand-maintained, so they can never drift from what
    /// actually got built.
    /// </summary>
    public static class GmsVersion
    {
        private static readonly string _number = ResolveMetadata("RevitVersion", "2025");
        private static readonly string _buildTimestamp = ResolveMetadata("BuildTimestamp", "unknown");

        /// <summary>The Revit major version this build targets, e.g. "2025" or "2026". Read here
        /// (rather than hard-coded) so version-specific paths (<see cref="GmsPaths"/>,
        /// <see cref="GmsLog"/>, keyboard-shortcut and Addins folders) stay correct in every build
        /// and call context, including before <c>OnStartup</c> has run.</summary>
        public static string Number
        {
            get { return _number; }
        }

        /// <summary>
        /// When this DLL was compiled ("yyyy-MM-dd hh:mm tt", 12-hour clock, local time of the
        /// build machine), e.g. "2026-08-19 04:46 PM". There is no meaningful version number to
        /// show instead — the csproj
        /// never sets &lt;Version&gt;/&lt;AssemblyVersion&gt;, so every build's
        /// AssemblyFileVersion is the SDK's unchanging default 1.0.0.0. This is what
        /// <c>SettingsForm</c> shows so a deployed build can actually be identified.
        /// </summary>
        public static string BuildTimestamp
        {
            get { return _buildTimestamp; }
        }

        /// <summary>Reads a named <see cref="AssemblyMetadataAttribute"/> off this executing
        /// assembly, or <paramref name="fallback"/> if it's missing/blank. Never throws.</summary>
        private static string ResolveMetadata(string key, string fallback)
        {
            try
            {
                AssemblyMetadataAttribute meta = Assembly.GetExecutingAssembly()
                    .GetCustomAttributes<AssemblyMetadataAttribute>()
                    .FirstOrDefault(a => string.Equals(a.Key, key, StringComparison.OrdinalIgnoreCase));

                if (meta != null && !string.IsNullOrWhiteSpace(meta.Value))
                {
                    return meta.Value.Trim();
                }
            }
            catch
            {
                // Fall through to the default below; this must never throw.
            }

            return fallback;
        }
    }
}
