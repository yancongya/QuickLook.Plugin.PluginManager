using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using QuickLook.Plugin.PluginManager.Models;

namespace QuickLook.Plugin.PluginManager.Services
{
    public sealed class InstalledScanResult
    {
        public List<PluginInfo> Plugins { get; set; }
        public List<string> Errors { get; set; }

        public InstalledScanResult()
        {
            Plugins = new List<PluginInfo>();
            Errors = new List<string>();
        }
    }

    public sealed class UninstallResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public int RenamedCount { get; set; }
    }

    public sealed class InstalledPluginService
    {
        public InstalledScanResult Scan()
        {
            var result = new InstalledScanResult();
            var info = DataRootResolver.Resolve();
            if (!info.Success)
            {
                result.Errors.Add(info.Error);
                return result;
            }

            var seenFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenDlls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            ScanUserPlugins(info.UserPluginPath, result, seenFolders, seenDlls);
            ScanBuiltInPlugins(info.AppPath, info.UserPluginPath, result, seenFolders, seenDlls);

            result.Plugins = result.Plugins
                .OrderBy(p => p.IsBuiltIn ? 1 : 0)
                .ThenBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            return result;
        }

        public UninstallResult Uninstall(PluginInfo plugin)
        {
            var result = new UninstallResult();
            if (plugin == null)
            {
                result.Error = "No plugin selected.";
                return result;
            }
            if (!plugin.CanUninstall)
            {
                result.Error = "This plugin cannot be removed from the manager.";
                return result;
            }
            if (DataRootResolver.IsSelfPath(plugin.FolderPath))
            {
                result.Error = "Refusing to uninstall the running plugin.";
                return result;
            }

            var userPluginPath = DataRootResolver.UserPluginPath;
            if (string.IsNullOrWhiteSpace(userPluginPath)
                || !IsUnder(plugin.FolderPath, userPluginPath)
                || !DataRootResolver.IsUnderDataRoot(plugin.FolderPath))
            {
                result.Error = "Refusing to touch a folder outside the user plugin directory.";
                return result;
            }
            if (!Directory.Exists(plugin.FolderPath))
            {
                result.Error = "Plugin folder no longer exists.";
                return result;
            }

            var files = new List<string>();
            try
            {
                files.AddRange(Directory.GetFiles(plugin.FolderPath, "*", SearchOption.AllDirectories)
                    .Where(f => !f.EndsWith(".to_be_deleted", StringComparison.OrdinalIgnoreCase)));
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                return result;
            }

            if (files.Count == 0)
            {
                try
                {
                    Directory.Delete(plugin.FolderPath, false);
                    result.Success = true;
                }
                catch (Exception ex)
                {
                    result.Error = ex.Message;
                }
                return result;
            }

            var renamed = new List<KeyValuePair<string, string>>();
            var failures = new List<string>();
            foreach (var file in files)
            {
                var temp = Path.Combine(Path.GetDirectoryName(file) ?? plugin.FolderPath,
                    Guid.NewGuid().ToString("N") + ".to_be_deleted");
                try
                {
                    File.Move(file, temp);
                    renamed.Add(new KeyValuePair<string, string>(file, temp));
                }
                catch (Exception ex)
                {
                    failures.Add(Path.GetFileName(file) + ": " + ex.Message);
                    break;
                }
            }

            if (failures.Count > 0)
            {
                foreach (var pair in renamed)
                {
                    try
                    {
                        if (File.Exists(pair.Value) && !File.Exists(pair.Key))
                            File.Move(pair.Value, pair.Key);
                    }
                    catch
                    {
                    }
                }
                result.Error = string.Join("; ", failures);
                return result;
            }

            result.Success = true;
            result.RenamedCount = renamed.Count;
            RemoveEmptySubdirectories(plugin.FolderPath);
            return result;
        }

        private static void ScanUserPlugins(string userPluginPath, InstalledScanResult result,
            HashSet<string> seenFolders, HashSet<string> seenDlls)
        {
            if (string.IsNullOrWhiteSpace(userPluginPath) || !Directory.Exists(userPluginPath)) return;

            try
            {
                foreach (var dir in Directory.GetDirectories(userPluginPath))
                {
                    if (seenFolders.Contains(dir)) continue;
                    seenFolders.Add(dir);
                    var plugin = ReadDirectoryPlugin(dir, false, result.Errors);
                    if (plugin == null) continue;
                    result.Plugins.Add(plugin);
                    if (!string.IsNullOrWhiteSpace(plugin.DllPath)) seenDlls.Add(plugin.DllPath);
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add(ex.Message);
            }

            try
            {
                foreach (var dll in Directory.GetFiles(userPluginPath, "QuickLook.Plugin.*.dll", SearchOption.TopDirectoryOnly))
                {
                    if (seenDlls.Contains(dll)) continue;
                    seenDlls.Add(dll);
                    result.Plugins.Add(ReadLooseDll(dll, false));
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add(ex.Message);
            }
        }

        private static void ScanBuiltInPlugins(string appPath, string userPluginPath, InstalledScanResult result,
            HashSet<string> seenFolders, HashSet<string> seenDlls)
        {
            if (string.IsNullOrWhiteSpace(appPath)) return;
            var dir = Path.Combine(appPath, "QuickLook.Plugin");
            if (!Directory.Exists(dir)) return;
            if (!string.IsNullOrWhiteSpace(userPluginPath)
                && string.Equals(Normalize(dir), Normalize(userPluginPath), StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                foreach (var pluginDir in Directory.GetDirectories(dir))
                {
                    if (seenFolders.Contains(pluginDir)) continue;
                    seenFolders.Add(pluginDir);
                    var plugin = ReadDirectoryPlugin(pluginDir, true, result.Errors);
                    if (plugin == null) continue;
                    result.Plugins.Add(plugin);
                    if (!string.IsNullOrWhiteSpace(plugin.DllPath)) seenDlls.Add(plugin.DllPath);
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add(ex.Message);
            }

            try
            {
                foreach (var dll in Directory.GetFiles(dir, "QuickLook.Plugin.*.dll", SearchOption.TopDirectoryOnly))
                {
                    if (seenDlls.Contains(dll)) continue;
                    seenDlls.Add(dll);
                    result.Plugins.Add(ReadLooseDll(dll, true));
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add(ex.Message);
            }
        }

        private static PluginInfo ReadDirectoryPlugin(string dir, bool isBuiltIn, List<string> errors)
        {
            var metadataPath = Path.Combine(dir, MetadataReader.FileName);
            var metadata = MetadataReader.ReadFile(metadataPath);
            string dll = null;
            try
            {
                dll = Directory.GetFiles(dir, "QuickLook.Plugin.*.dll", SearchOption.TopDirectoryOnly).FirstOrDefault();
            }
            catch (Exception ex)
            {
                errors.Add(Path.GetFileName(dir) + ": " + ex.Message);
            }

            if (metadata == null && dll == null) return null;

            var info = new PluginInfo
            {
                Name = metadata != null ? metadata.Namespace : Path.GetFileNameWithoutExtension(dll),
                Namespace = metadata != null ? metadata.Namespace : Path.GetFileNameWithoutExtension(dll),
                // Metadata.config 的 <Version> 是元数据格式版本（恒为 1），插件真实版本在程序集 FileVersion 里
                Version = GetFileVersion(dll) ?? (metadata != null ? metadata.Version : null),
                Description = metadata != null ? metadata.Description : "",
                FolderPath = dir,
                DllPath = dll,
                IsBuiltIn = isBuiltIn,
                IsLoose = false,
                HasMetadata = metadata != null,
                HasDll = dll != null,
                LastWriteTime = SafeLastWrite(dir)
            };
            info.IsSelf = string.Equals(info.Namespace, DataRootResolver.SelfNamespace, StringComparison.OrdinalIgnoreCase)
                          || DataRootResolver.IsSelfPath(dir);
            info.FileCount = CountFiles(dir);
            return info;
        }

        private static PluginInfo ReadLooseDll(string dll, bool isBuiltIn)
        {
            var name = Path.GetFileNameWithoutExtension(dll);
            return new PluginInfo
            {
                Name = name,
                Namespace = name,
                Version = GetFileVersion(dll),
                Description = "",
                FolderPath = Path.GetDirectoryName(dll),
                DllPath = dll,
                IsBuiltIn = isBuiltIn,
                IsLoose = true,
                HasMetadata = false,
                HasDll = true,
                FileCount = 1,
                LastWriteTime = SafeLastWrite(dll),
                IsSelf = string.Equals(name, DataRootResolver.SelfNamespace, StringComparison.OrdinalIgnoreCase)
            };
        }

        private static string GetFileVersion(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try
            {
                var info = FileVersionInfo.GetVersionInfo(path);
                // 未写 AssemblyFileVersion 的程序集会得到 0.0.0.0，视为没有版本信息
                if (!string.IsNullOrWhiteSpace(info.FileVersion) && info.FileVersion != "0.0.0.0")
                    return info.FileVersion;

                // 退回 InformationalVersion（可能带 -g<hash> / +meta 尾巴，截掉）
                var pv = info.ProductVersion;
                if (!string.IsNullOrWhiteSpace(pv))
                {
                    var cut = pv.IndexOfAny(new[] { '-', '+' });
                    if (cut > 0) pv = pv.Substring(0, cut);
                    foreach (var c in pv)
                    {
                        if (char.IsDigit(c)) return pv;
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        private static int CountFiles(string dir)
        {
            try
            {
                return Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Length;
            }
            catch
            {
                return 0;
            }
        }

        private static DateTime? SafeLastWrite(string path)
        {
            try
            {
                return File.Exists(path) ? File.GetLastWriteTime(path) : Directory.GetLastWriteTime(path);
            }
            catch
            {
                return null;
            }
        }

        private static void RemoveEmptySubdirectories(string root)
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories)
                    .OrderByDescending(d => d.Length))
                {
                    try
                    {
                        if (!Directory.EnumerateFileSystemEntries(dir).Any())
                            Directory.Delete(dir, false);
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        private static bool IsUnder(string path, string root)
        {
            var p = Normalize(path);
            var r = Normalize(root);
            if (p.Length == 0 || r.Length == 0) return false;
            return string.Equals(p, r, StringComparison.OrdinalIgnoreCase)
                   || p.StartsWith(r + "\\", StringComparison.OrdinalIgnoreCase);
        }

        private static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            try
            {
                return Path.GetFullPath(path).TrimEnd('\\', '/');
            }
            catch
            {
                return path.Trim().TrimEnd('\\', '/');
            }
        }
    }
}
