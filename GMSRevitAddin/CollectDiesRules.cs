using System;
using System.Collections.Generic;
using System.Linq;

namespace CollectDiesForm
{
    /// <summary>Which placement column a family+type belongs to in "Collect Detail Items".</summary>
    public enum FamilyBucket
    {
        Fastener,
        Component,
        Extrusion,
        DoNotSchedule,
        Other
    }

    /// <summary>
    /// One bucketing rule: a family+type whose name starts with <see cref="Prefix"/> — and, unless
    /// <see cref="Exclude"/> is blank, does NOT contain it — belongs to <see cref="Bucket"/>. Matching
    /// is case-insensitive (both fields are compared against the name lowercased by the caller).
    /// </summary>
    public class BucketRule
    {
        public string Prefix;
        public string Exclude;
        public FamilyBucket Bucket;

        public BucketRule(string prefix, string exclude, FamilyBucket bucket)
        {
            Prefix = prefix ?? "";
            Exclude = exclude ?? "";
            Bucket = bucket;
        }

        /// <summary><paramref name="lowerName"/> must already be lowercased by the caller.</summary>
        public bool Matches(string lowerName)
        {
            if (string.IsNullOrEmpty(Prefix) || !lowerName.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return false;
            }
            return string.IsNullOrEmpty(Exclude) || !lowerName.Contains(Exclude);
        }
    }

    /// <summary>
    /// The editable family-bucketing rule table used by "Collect Detail Items" to sort placed
    /// families into columns (fasteners / components / extrusions / do-not-schedule / other).
    /// Persisted to <see cref="GMSRevitAddin.Properties.Settings"/> so GMS staff can adjust it — via
    /// <see cref="CollectDiesRulesForm"/> — as family-naming conventions evolve, without a code
    /// change or rebuild. Rules are evaluated in order; the first match wins; a family+type matching
    /// no rule falls into <see cref="FamilyBucket.Other"/>.
    ///
    /// <see cref="Default"/> reproduces the original hardcoded fastener/component/extrusion/plan/mod
    /// logic exactly (a plain "Fastener…" family that isn't a "…Plan" variant → Fastener; a "…Plan"
    /// variant → DoNotSchedule; same shape for Extrusion/"…Mod"; "Component…" → Component), so a
    /// project that has never opened the rule editor behaves identically to before this feature.
    /// </summary>
    public static class BucketRuleSet
    {
        private const string RuleSeparator = "||";
        private const string FieldSeparator = "::";

        public static List<BucketRule> Default()
        {
            return new List<BucketRule>
            {
                new BucketRule("fastener", "plan", FamilyBucket.Fastener),
                new BucketRule("fastener", "", FamilyBucket.DoNotSchedule),
                new BucketRule("component", "", FamilyBucket.Component),
                new BucketRule("extrusion", "mod", FamilyBucket.Extrusion),
                new BucketRule("extrusion", "", FamilyBucket.DoNotSchedule),
            };
        }

        /// <summary>Loads the persisted rule table, or <see cref="Default"/> if nothing has been
        /// saved yet (or the saved value is malformed).</summary>
        public static List<BucketRule> Load()
        {
            string raw = GMSRevitAddin.Properties.Settings.Default.CollectDiesBucketRules;
            if (string.IsNullOrEmpty(raw))
            {
                return Default();
            }

            List<BucketRule> rules = new List<BucketRule>();
            foreach (string entry in raw.Split(new[] { RuleSeparator }, StringSplitOptions.None))
            {
                if (string.IsNullOrEmpty(entry))
                {
                    continue;
                }
                string[] fields = entry.Split(new[] { FieldSeparator }, StringSplitOptions.None);
                FamilyBucket bucket;
                if (fields.Length != 3 || !Enum.TryParse(fields[2], out bucket))
                {
                    continue; // skip a corrupted row rather than failing the whole load
                }
                rules.Add(new BucketRule(fields[0], fields[1], bucket));
            }
            return rules.Count > 0 ? rules : Default();
        }

        public static void Save(List<BucketRule> rules)
        {
            string raw = string.Join(RuleSeparator, rules.Select(r =>
                string.Join(FieldSeparator, r.Prefix ?? "", r.Exclude ?? "", r.Bucket.ToString())));
            GMSRevitAddin.Properties.Settings.Default.CollectDiesBucketRules = raw;
            GMSRevitAddin.Properties.Settings.Default.Save();
        }

        /// <summary>Buckets a family+type by the first matching rule, or <see cref="FamilyBucket.Other"/>
        /// if none match.</summary>
        public static FamilyBucket Classify(List<BucketRule> rules, string familyAndType)
        {
            string name = familyAndType.ToLowerInvariant();
            foreach (BucketRule rule in rules)
            {
                if (rule.Matches(name))
                {
                    return rule.Bucket;
                }
            }
            return FamilyBucket.Other;
        }
    }
}
