using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace QuickLook.Plugin.PluginManager.Services
{
    public sealed class ConfigLogService
    {
        public string DataRoot
        {
            get { return DataRootResolver.DataRoot; }
        }

        public string LogPath
        {
            get
            {
                var root = DataRoot;
                return string.IsNullOrWhiteSpace(root) ? null : Path.Combine(root, "QuickLook.Exception.log");
            }
        }

        public IReadOnlyList<string> GetConfigFiles()
        {
            var result = new List<string>();
            var root = DataRoot;
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return result;
            try
            {
                foreach (var file in Directory.GetFiles(root, "QuickLook*.config", SearchOption.TopDirectoryOnly))
                {
                    var name = Path.GetFileName(file);
                    if (string.Equals(name, "QuickLook.config", StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith("QuickLook.Plugin.", StringComparison.OrdinalIgnoreCase))
                        result.Add(file);
                }
            }
            catch
            {
            }
            return result.OrderBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        public string ReadText(string path, int maxBytes = 512 * 1024)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return "";
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                           FileShare.ReadWrite | FileShare.Delete))
                {
                    if (stream.Length > maxBytes)
                        stream.Seek(stream.Length - maxBytes, SeekOrigin.Begin);
                    using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
            catch (Exception ex)
            {
                return "[" + ex.Message + "]";
            }
        }

        public bool TryClearLog(out string error)
        {
            error = null;
            var path = LogPath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                error = "Log file does not exist.";
                return false;
            }
            try
            {
                using (new FileStream(path, FileMode.Truncate, FileAccess.Write, FileShare.ReadWrite))
                {
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
