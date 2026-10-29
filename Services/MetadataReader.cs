using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using QuickLook.Plugin.PluginManager.Models;

namespace QuickLook.Plugin.PluginManager.Services
{
    public static class MetadataReader
    {
        public const string FileName = "QuickLook.Plugin.Metadata.config";

        public static PluginMetadata ReadFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
            try
            {
                var doc = LoadXml(File.ReadAllText(path));
                return FromDocument(doc);
            }
            catch
            {
                return null;
            }
        }

        public static PluginMetadata ReadFromZip(string zipPath)
        {
            if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath)) return null;
            try
            {
                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    var entry = archive.Entries.FirstOrDefault(e =>
                        string.Equals(NormalizeEntry(e.FullName), FileName, StringComparison.OrdinalIgnoreCase));
                    if (entry == null) return null;
                    using (var reader = new StreamReader(entry.Open()))
                    {
                        var doc = LoadXml(reader.ReadToEnd());
                        return FromDocument(doc);
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        public static PluginMetadata Parse(string xml)
        {
            try
            {
                return FromDocument(LoadXml(xml));
            }
            catch
            {
                return null;
            }
        }

        public static string NormalizeEntry(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            return name.Replace('\\', '/').TrimStart('/');
        }

        private static XmlDocument LoadXml(string text)
        {
            var doc = new XmlDocument { XmlResolver = null };
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };
            using (var stringReader = new StringReader(text))
            using (var reader = XmlReader.Create(stringReader, settings))
            {
                doc.Load(reader);
            }
            return doc;
        }

        private static PluginMetadata FromDocument(XmlDocument doc)
        {
            if (doc == null) return null;
            var ns = doc.SelectSingleNode("/Metadata/Namespace");
            var version = doc.SelectSingleNode("/Metadata/Version");
            var description = doc.SelectSingleNode("/Metadata/Description");
            var metadata = new PluginMetadata
            {
                Namespace = ns == null ? null : ns.InnerText.Trim(),
                Version = version == null ? null : version.InnerText.Trim(),
                Description = description == null ? null : description.InnerText.Trim()
            };
            if (string.IsNullOrWhiteSpace(metadata.Namespace)) return null;
            return metadata;
        }
    }
}
