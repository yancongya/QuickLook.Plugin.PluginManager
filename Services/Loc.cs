using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Xml;

namespace QuickLook.Plugin.PluginManager.Services
{
    public static class Loc
    {
        private static readonly object Sync = new object();
        private static Dictionary<string, string> _zh;
        private static Dictionary<string, string> _en;

        public static string TranslationsPath
        {
            get
            {
                var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
                return Path.Combine(dir, "Translations.config");
            }
        }

        public static string Get(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return "";
            EnsureLoaded();
            var isChinese = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh";
            var table = isChinese ? _zh : _en;
            string value;
            if (table != null && table.TryGetValue(key, out value) && !string.IsNullOrEmpty(value))
                return value;
            if (_zh != null && _zh.TryGetValue(key, out value) && !string.IsNullOrEmpty(value))
                return value;
            return key;
        }

        public static string Format(string key, params object[] args)
        {
            var text = Get(key);
            try
            {
                return string.Format(CultureInfo.CurrentCulture, text, args);
            }
            catch (FormatException)
            {
                return text;
            }
        }

        public static void Reload()
        {
            lock (Sync)
            {
                _zh = null;
                _en = null;
            }
        }

        private static void EnsureLoaded()
        {
            if (_zh != null && _en != null) return;
            lock (Sync)
            {
                if (_zh != null && _en != null) return;
                var zh = new Dictionary<string, string>(StringComparer.Ordinal);
                var en = new Dictionary<string, string>(StringComparer.Ordinal);
                try
                {
                    var path = TranslationsPath;
                    if (File.Exists(path))
                    {
                        var doc = new XmlDocument();
                        doc.Load(path);
                        var nodes = doc.SelectNodes("/Translations/Entry");
                        if (nodes != null)
                        {
                            foreach (XmlNode node in nodes)
                            {
                                if (node.Attributes == null) continue;
                                var key = node.Attributes["Key"] != null ? node.Attributes["Key"].Value : null;
                                if (string.IsNullOrWhiteSpace(key)) continue;
                                var value = node.Attributes["Value"] != null ? node.Attributes["Value"].Value : "";
                                var valueEn = node.Attributes["ValueEn"] != null ? node.Attributes["ValueEn"].Value : "";
                                zh[key] = value;
                                en[key] = string.IsNullOrEmpty(valueEn) ? value : valueEn;
                            }
                        }
                    }
                }
                catch
                {
                    // Fall back to keys when the translation file is missing or invalid.
                }
                _zh = zh;
                _en = en;
            }
        }
    }
}
