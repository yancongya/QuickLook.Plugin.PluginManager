using System;

namespace QuickLook.Plugin.PluginManager.Services
{
    public static class VersionUtil
    {
        /// <summary>
        /// Compare two version-ish strings (e.g. "v1.2.3" vs "1.2"). Returns true when
        /// <paramref name="latest"/> is strictly newer than <paramref name="current"/>.
        /// Falls back to case-insensitive ordinal comparison when either side is unparseable.
        /// </summary>
        public static bool IsNewer(string latest, string current)
        {
            var a = Clean(latest);
            var b = Clean(current);
            if (string.IsNullOrEmpty(a)) return false;
            if (string.IsNullOrEmpty(b)) return true;

            int[] va, vb;
            if (TryParse(a, out va) && TryParse(b, out vb))
            {
                var len = Math.Max(va.Length, vb.Length);
                for (var i = 0; i < len; i++)
                {
                    var x = i < va.Length ? va[i] : 0;
                    var y = i < vb.Length ? vb[i] : 0;
                    if (x != y) return x > y;
                }
                return false;
            }

            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase) > 0;
        }

        private static bool TryParse(string text, out int[] parts)
        {
            var split = text.Split('.');
            var nums = new int[split.Length];
            for (var i = 0; i < split.Length; i++)
            {
                int value;
                if (!int.TryParse(split[i], out value))
                {
                    parts = null;
                    return false;
                }
                nums[i] = value;
            }
            parts = nums;
            return true;
        }

        private static string Clean(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var text = value.Trim();
            while (text.StartsWith("v", StringComparison.OrdinalIgnoreCase) && text.Length > 1
                   && char.IsDigit(text[1]))
            {
                text = text.Substring(1);
            }
            return text;
        }
    }
}
