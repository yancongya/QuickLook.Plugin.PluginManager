using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace QuickLook.Plugin.PluginManager.Services
{
    public sealed class DataRootInfo
    {
        public bool Success { get; set; }
        public string DataRoot { get; set; }
        public string UserPluginPath { get; set; }
        public string AppPath { get; set; }
        public string Source { get; set; }
        public string Error { get; set; }
    }

    public static class DataRootResolver
    {
        public const string SelfNamespace = "QuickLook.Plugin.PluginManager";
        private static readonly object Sync = new object();
        private static DataRootInfo _cached;

        public static DataRootInfo Resolve()
        {
            lock (Sync)
            {
                if (_cached != null) return _cached;
                _cached = ResolveCore();
                return _cached;
            }
        }

        public static void ResetForTests()
        {
            lock (Sync)
            {
                _cached = null;
            }
        }

        public static string DataRoot
        {
            get
            {
                var info = Resolve();
                return info.Success ? info.DataRoot : null;
            }
        }

        public static string UserPluginPath
        {
            get
            {
                var info = Resolve();
                return info.Success ? info.UserPluginPath : null;
            }
        }

        public static string AppPath
        {
            get
            {
                var info = Resolve();
                return info.Success ? info.AppPath : null;
            }
        }

        public static string SelfPluginDirectory
        {
            get { return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ""; }
        }

        public static bool IsUnderDataRoot(string path)
        {
            var info = Resolve();
            if (!info.Success || string.IsNullOrWhiteSpace(path)) return false;
            var root = Normalize(info.DataRoot);
            var target = Normalize(path);
            if (root.Length == 0 || target.Length == 0) return false;
            if (string.Equals(target, root, StringComparison.OrdinalIgnoreCase)) return true;
            return target.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase)
                   || target.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsSelfPath(string path)
        {
            var self = Normalize(SelfPluginDirectory);
            var target = Normalize(path);
            if (self.Length == 0 || target.Length == 0) return false;
            return string.Equals(self, target, StringComparison.OrdinalIgnoreCase)
                   || self.StartsWith(target + "\\", StringComparison.OrdinalIgnoreCase)
                   || target.StartsWith(self + "\\", StringComparison.OrdinalIgnoreCase);
        }

        private static DataRootInfo ResolveCore()
        {
            var appPath = ReflectStaticString("QuickLook", "QuickLook.App", "AppPath");

            var fromApp = ReflectStaticString("QuickLook", "QuickLook.App", "UserPluginPath");
            if (!string.IsNullOrWhiteSpace(fromApp))
            {
                var root = Path.GetDirectoryName(Normalize(fromApp));
                if (LooksLikeDataRoot(root))
                    return Success(root, fromApp, appPath, "App.UserPluginPath");
            }

            var localData = ReflectStaticString("QuickLook.Common", "QuickLook.Common.Helpers.SettingHelper", "LocalDataPath");
            if (!string.IsNullOrWhiteSpace(localData))
            {
                var pluginDir = Path.Combine(localData, "QuickLook.Plugin");
                if (LooksLikeDataRoot(localData))
                    return Success(localData, pluginDir, appPath, "SettingHelper.LocalDataPath");
            }

            var fallbackRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "pooi.moe", "QuickLook");
            if (LooksLikeDataRoot(fallbackRoot))
                return Success(fallbackRoot, Path.Combine(fallbackRoot, "QuickLook.Plugin"), appPath, "fallback");

            return new DataRootInfo
            {
                Success = false,
                Error = "Could not resolve the QuickLook data folder.",
                AppPath = appPath,
                Source = "none"
            };
        }

        private static DataRootInfo Success(string root, string userPluginPath, string appPath, string source)
        {
            root = Normalize(root);
            userPluginPath = Normalize(userPluginPath);
            appPath = Normalize(appPath);
            if (!string.IsNullOrWhiteSpace(appPath) && string.Equals(root, appPath, StringComparison.OrdinalIgnoreCase))
            {
                return new DataRootInfo
                {
                    Success = false,
                    Error = "Resolved data folder is the application folder.",
                    AppPath = appPath,
                    Source = source
                };
            }

            return new DataRootInfo
            {
                Success = true,
                DataRoot = root,
                UserPluginPath = userPluginPath,
                AppPath = appPath,
                Source = source
            };
        }

        private static bool LooksLikeDataRoot(string root)
        {
            if (string.IsNullOrWhiteSpace(root)) return false;
            try
            {
                if (!Directory.Exists(root)) return false;
                if (Directory.Exists(Path.Combine(root, "QuickLook.Plugin"))) return true;
                if (File.Exists(Path.Combine(root, "QuickLook.config"))) return true;
                if (Directory.Exists(Path.Combine(root, "WebView2_Data"))) return true;
                var name = Path.GetFileName(root.TrimEnd('\\', '/'));
                if (string.Equals(name, "UserData", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(name, "QuickLook", StringComparison.OrdinalIgnoreCase))
                {
                    var parent = Path.GetFileName(Path.GetDirectoryName(root.TrimEnd('\\', '/')));
                    if (string.Equals(parent, "pooi.moe", StringComparison.OrdinalIgnoreCase)) return true;
                }
                var self = SelfPluginDirectory;
                if (!string.IsNullOrWhiteSpace(self)
                    && self.StartsWith(Normalize(root) + "\\", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch
            {
            }
            return false;
        }

        private static string ReflectStaticString(string assemblyName, string typeName, string fieldName)
        {
            try
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => string.Equals(a.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));
                if (assembly == null) return null;
                var type = assembly.GetType(typeName, false);
                if (type == null) return null;
                var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
                if (field == null || field.FieldType != typeof(string)) return null;
                return field.GetValue(null) as string;
            }
            catch
            {
                return null;
            }
        }

        private static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            try
            {
                var full = Path.GetFullPath(path);
                return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return path.Trim().TrimEnd('\\', '/');
            }
        }
    }
}
