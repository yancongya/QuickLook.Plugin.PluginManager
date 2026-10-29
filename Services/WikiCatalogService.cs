using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using QuickLook.Plugin.PluginManager.Models;

namespace QuickLook.Plugin.PluginManager.Services
{
    public sealed class CatalogLoadResult
    {
        public WikiCatalog Catalog { get; set; }
        public bool FromCache { get; set; }
        public string Error { get; set; }
    }

    public sealed class WikiCatalogService
    {
        public const string WikiUrl = "https://raw.githubusercontent.com/wiki/QL-Win/QuickLook/Available-Plugins.md";

        // raw.githubusercontent.com 在部分地区不稳定；主源失败后依次尝试公共镜像。
        private static readonly string[] SourceUrls =
        {
            WikiUrl,
            "https://ghfast.top/https://raw.githubusercontent.com/wiki/QL-Win/QuickLook/Available-Plugins.md",
            "https://gh-proxy.com/https://raw.githubusercontent.com/wiki/QL-Win/QuickLook/Available-Plugins.md"
        };

        private static readonly HttpClient Http = HttpHelper.CreateClient(TimeSpan.FromSeconds(25));

        public string CachePath
        {
            get
            {
                var root = DataRootResolver.DataRoot;
                if (string.IsNullOrWhiteSpace(root))
                    root = Path.Combine(Path.GetTempPath(), "QuickLook.PluginManager");
                return Path.Combine(root, "PluginManager", "catalog.json");
            }
        }

        public WikiCatalog LoadCache()
        {
            try
            {
                if (!File.Exists(CachePath)) return null;
                var json = File.ReadAllText(CachePath, Encoding.UTF8);
                var catalog = JsonConvert.DeserializeObject<WikiCatalog>(json);
                if (catalog == null || catalog.Plugins == null) return null;
                return catalog;
            }
            catch
            {
                return null;
            }
        }

        public async Task<CatalogLoadResult> RefreshAsync(CancellationToken cancellationToken)
        {
            var cached = LoadCache();
            var errors = new List<string>();

            foreach (var url in SourceUrls)
            {
                var host = HostOf(url);
                for (var attempt = 1; attempt <= 2; attempt++)
                {
                    try
                    {
                        using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                        {
                            if (cached != null && !string.IsNullOrWhiteSpace(cached.ETag))
                                request.Headers.TryAddWithoutValidation("If-None-Match", cached.ETag);

                            using (var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
                                       cancellationToken).ConfigureAwait(false))
                            {
                                if (response.StatusCode == HttpStatusCode.NotModified && cached != null)
                                    return new CatalogLoadResult { Catalog = cached, FromCache = true };

                                response.EnsureSuccessStatusCode();
                                var markdown = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                                var catalog = ParseMarkdown(markdown);
                                catalog.SourceUrl = WikiUrl;
                                catalog.UpdatedAt = DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture);
                                if (response.Headers.ETag != null)
                                    catalog.ETag = response.Headers.ETag.Tag;
                                SaveCache(catalog);
                                return new CatalogLoadResult { Catalog = catalog, FromCache = false };
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        var detail = HttpHelper.Describe(ex);
                        if (attempt == 1)
                        {
                            // Transient local blocks (WSAEACCES etc.) often clear on retry.
                            try
                            {
                                await Task.Delay(400, cancellationToken).ConfigureAwait(false);
                            }
                            catch (OperationCanceledException)
                            {
                                throw;
                            }
                            continue;
                        }
                        AddError(errors, host + ": " + detail);
                    }
                }
            }

            var error = errors.Count == 0
                ? "No catalog source was tried."
                : string.Join(" | ", errors);
            if (cached != null)
                return new CatalogLoadResult { Catalog = cached, FromCache = true, Error = error };
            return new CatalogLoadResult { Error = error };
        }

        private static void AddError(List<string> errors, string value)
        {
            if (errors.Contains(value)) return;
            errors.Add(value);
        }

        private static string HostOf(string url)
        {
            try
            {
                return new Uri(url).Host;
            }
            catch
            {
                return url;
            }
        }

        public void SaveCache(WikiCatalog catalog)
        {
            if (catalog == null) return;
            try
            {
                var path = CachePath;
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
                var json = JsonConvert.SerializeObject(catalog, Formatting.Indented);
                File.WriteAllText(path, json, new UTF8Encoding(false));
            }
            catch
            {
            }
        }

        public static WikiCatalog ParseMarkdown(string markdown)
        {
            var catalog = new WikiCatalog { SourceUrl = WikiUrl };
            if (string.IsNullOrWhiteSpace(markdown)) return catalog;

            var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var activeStart = FindHeading(lines, "List of plugins");
            var legacyStart = FindHeading(lines, "List of legacy plugins");

            if (activeStart >= 0)
                ParseSection(lines, activeStart + 1, FindSectionEnd(lines, activeStart + 1), false, catalog);
            if (legacyStart >= 0)
                ParseSection(lines, legacyStart + 1, FindSectionEnd(lines, legacyStart + 1), true, catalog);

            return catalog;
        }

        public static void MergeInstalled(WikiCatalog catalog, IEnumerable<PluginInfo> installed)
        {
            if (catalog == null || catalog.Plugins == null) return;
            var map = new Dictionary<string, PluginInfo>(StringComparer.OrdinalIgnoreCase);
            if (installed != null)
            {
                foreach (var plugin in installed)
                {
                    AddKey(map, plugin.Namespace, plugin);
                    AddKey(map, plugin.Name, plugin);
                    if (!string.IsNullOrWhiteSpace(plugin.FolderPath))
                        AddKey(map, Path.GetFileName(plugin.FolderPath), plugin);
                    if (!string.IsNullOrWhiteSpace(plugin.DllPath))
                        AddKey(map, Path.GetFileNameWithoutExtension(plugin.DllPath), plugin);
                }
            }

            foreach (var entry in catalog.Plugins)
            {
                PluginInfo match;
                if (map.TryGetValue(CatalogEntry.NormalizeName(entry.Name), out match)
                    || (TryGetRepoName(entry.RepoUrl, out var repoName)
                        && map.TryGetValue(CatalogEntry.NormalizeName(repoName), out match)))
                {
                    entry.IsInstalled = true;
                    entry.InstalledVersion = string.IsNullOrWhiteSpace(match.Version) ? "" : match.Version;
                }
                else
                {
                    entry.IsInstalled = false;
                    entry.InstalledVersion = "";
                }
            }

            ReverseMergeRepoUrls(catalog, installed);
        }

        /// <summary>
        /// Write catalog repo urls back to installed plugins so update checks can call GitHub.
        /// </summary>
        private static void ReverseMergeRepoUrls(WikiCatalog catalog, IEnumerable<PluginInfo> installed)
        {
            if (catalog == null || catalog.Plugins == null || installed == null) return;
            var byName = new Dictionary<string, CatalogEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in catalog.Plugins)
            {
                if (string.IsNullOrWhiteSpace(entry.RepoUrl)) continue;
                AddEntryKey(byName, CatalogEntry.NormalizeName(entry.Name), entry);
                if (TryGetRepoName(entry.RepoUrl, out var repoName))
                    AddEntryKey(byName, CatalogEntry.NormalizeName(repoName), entry);
            }

            foreach (var plugin in installed)
            {
                plugin.CatalogRepoUrl = null;
                if (TryGetValue(byName, CatalogEntry.NormalizeName(plugin.Namespace), out var entry)
                    || TryGetValue(byName, CatalogEntry.NormalizeName(plugin.Name), out entry)
                    || (!string.IsNullOrWhiteSpace(plugin.FolderPath)
                        && TryGetValue(byName, CatalogEntry.NormalizeName(Path.GetFileName(plugin.FolderPath)), out entry))
                    || (!string.IsNullOrWhiteSpace(plugin.DllPath)
                        && TryGetValue(byName, CatalogEntry.NormalizeName(Path.GetFileNameWithoutExtension(plugin.DllPath)), out entry)))
                {
                    plugin.CatalogRepoUrl = entry.RepoUrl;
                }
            }
        }

        private static void AddEntryKey(Dictionary<string, CatalogEntry> map, string key, CatalogEntry entry)
        {
            if (!string.IsNullOrEmpty(key) && !map.ContainsKey(key)) map[key] = entry;
        }

        private static bool TryGetValue(Dictionary<string, CatalogEntry> map, string key, out CatalogEntry entry)
        {
            if (string.IsNullOrEmpty(key))
            {
                entry = null;
                return false;
            }
            return map.TryGetValue(key, out entry);
        }

        private static void AddKey(Dictionary<string, PluginInfo> map, string value, PluginInfo plugin)
        {
            var key = CatalogEntry.NormalizeName(value);
            if (!string.IsNullOrEmpty(key) && !map.ContainsKey(key))
                map[key] = plugin;
        }

        private static bool TryGetRepoName(string repoUrl, out string name)
        {
            name = null;
            if (string.IsNullOrWhiteSpace(repoUrl)) return false;
            try
            {
                var uri = new Uri(repoUrl);
                var segment = uri.Segments.LastOrDefault();
                if (string.IsNullOrWhiteSpace(segment)) return false;
                name = Uri.UnescapeDataString(segment.Trim('/'));
                if (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                    name = name.Substring(0, name.Length - 4);
                return !string.IsNullOrWhiteSpace(name);
            }
            catch
            {
                return false;
            }
        }

        private static int FindHeading(string[] lines, string title)
        {
            var pattern = new Regex(@"^#{2,4}\s*" + Regex.Escape(title) + @"\s*$", RegexOptions.IgnoreCase);
            for (var i = 0; i < lines.Length; i++)
            {
                if (pattern.IsMatch(lines[i].Trim())) return i;
            }
            return -1;
        }

        private static int FindSectionEnd(string[] lines, int start)
        {
            for (var i = start; i < lines.Length; i++)
            {
                if (lines[i].TrimStart().StartsWith("#", StringComparison.Ordinal)) return i;
            }
            return lines.Length;
        }

        private static void ParseSection(string[] lines, int start, int end, bool isLegacy, WikiCatalog catalog)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = start; i < end; i++)
            {
                var line = lines[i].Trim();
                if (!line.Contains("|")) continue;
                if (!line.StartsWith("|", StringComparison.Ordinal) && line.Count(c => c == '|') < 3) continue;
                if (IsSeparator(line)) continue;

                var cells = SplitCells(line);
                if (cells.Length < 2) continue;
                if (string.Equals(cells[0], "Plugin", StringComparison.OrdinalIgnoreCase)) continue;

                var link = Regex.Match(cells[0], @"\[([^\]]+)\]\(([^)]+)\)");
                if (!link.Success) continue;

                var name = Clean(link.Groups[1].Value);
                var repoUrl = link.Groups[2].Value.Trim();
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(repoUrl)) continue;
                if (!repoUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)) continue;

                var afterLink = cells[0].Substring(link.Index + link.Length);
                var version = Clean(afterLink);
                var releasesUrl = repoUrl.TrimEnd('/') + "/releases";
                if (cells.Length > 2)
                {
                    var releaseLink = Regex.Match(cells[2], @"\[[^\]]*\]\(([^)]+)\)");
                    if (releaseLink.Success && releaseLink.Groups[1].Value.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        releasesUrl = releaseLink.Groups[1].Value.Trim();
                }

                var description = cells.Length > 3 ? Clean(cells[3]) : "";
                var key = repoUrl.ToLowerInvariant() + "|" + name.ToLowerInvariant();
                if (!seen.Add(key)) continue;

                catalog.Plugins.Add(new CatalogEntry
                {
                    Name = name,
                    VersionText = version,
                    Description = description,
                    RepoUrl = repoUrl,
                    ReleasesUrl = releasesUrl,
                    IsLegacy = isLegacy,
                    ListName = isLegacy ? "legacy" : "active"
                });
            }
        }

        private static string[] SplitCells(string line)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("|", StringComparison.Ordinal)) trimmed = trimmed.Substring(1);
            if (trimmed.EndsWith("|", StringComparison.Ordinal)) trimmed = trimmed.Substring(0, trimmed.Length - 1);
            return trimmed.Split('|').Select(c => c.Trim()).ToArray();
        }

        private static bool IsSeparator(string line)
        {
            var cells = SplitCells(line);
            if (cells.Length == 0) return false;
            return cells.All(c =>
            {
                var value = c.Replace(":", "").Replace("-", "").Trim();
                return value.Length == 0;
            });
        }

        private static string Clean(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("~~", "").Replace("**", "").Replace("`", "").Trim();
        }
    }
}

