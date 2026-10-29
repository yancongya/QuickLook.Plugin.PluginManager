using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnblockZoneIdentifier;

namespace QuickLook.Plugin.PluginManager.Services
{
    public sealed class CleanupResult
    {
        public int RemovedFiles { get; set; }
        public int RemovedDirectories { get; set; }
        public List<string> Errors { get; set; }

        public CleanupResult()
        {
            Errors = new List<string>();
        }
    }

    public sealed class UnblockResult
    {
        public int UnblockedFiles { get; set; }
        public List<string> Errors { get; set; }

        public UnblockResult()
        {
            Errors = new List<string>();
        }
    }

    public sealed class NetworkProbeLine
    {
        public string Label { get; set; }
        public bool DnsOk { get; set; }
        public bool TcpOk { get; set; }
        public bool HttpOk { get; set; }
        public long ElapsedMs { get; set; }
        public string Detail { get; set; }
    }

    public sealed class NetworkProbeResult
    {
        public List<NetworkProbeLine> Lines { get; set; }

        public NetworkProbeResult()
        {
            Lines = new List<NetworkProbeLine>();
        }
    }

    public static class RepairService
    {
        public static void OpenFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "\"" + path + "\"",
                    UseShellExecute = true
                });
            }
            catch
            {
            }
        }

        public static void RevealFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                if (File.Exists(path))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = "/select,\"" + path + "\"",
                        UseShellExecute = true
                    });
                }
                else
                {
                    OpenFolder(Path.GetDirectoryName(path));
                }
            }
            catch
            {
            }
        }

        public static void OpenUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                && !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                return;
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch
            {
            }
        }

        public static bool RestartQuickLook(out string error)
        {
            error = null;
            try
            {
                var process = Process.GetCurrentProcess();
                var fileName = process.MainModule != null ? process.MainModule.FileName : null;
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    error = "Could not locate the QuickLook executable.";
                    return false;
                }
                Process.Start(new ProcessStartInfo
                {
                    FileName = fileName,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(fileName) ?? ""
                });
                process.Kill();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static CleanupResult CleanupLeftovers()
        {
            var result = new CleanupResult();
            var userPluginPath = DataRootResolver.UserPluginPath;
            if (string.IsNullOrWhiteSpace(userPluginPath) || !Directory.Exists(userPluginPath))
                return result;

            try
            {
                foreach (var file in Directory.GetFiles(userPluginPath, "*.to_be_deleted", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.Delete(file);
                        result.RemovedFiles++;
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add(Path.GetFileName(file) + ": " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add(ex.Message);
            }

            try
            {
                foreach (var dir in Directory.GetDirectories(userPluginPath, "*", SearchOption.AllDirectories)
                    .OrderByDescending(d => d.Length))
                {
                    try
                    {
                        if (!Directory.EnumerateFileSystemEntries(dir).Any())
                        {
                            Directory.Delete(dir, false);
                            result.RemovedDirectories++;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add(Path.GetFileName(dir) + ": " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add(ex.Message);
            }

            return result;
        }

        public static UnblockResult UnblockPluginFiles()
        {
            var result = new UnblockResult();
            var userPluginPath = DataRootResolver.UserPluginPath;
            if (string.IsNullOrWhiteSpace(userPluginPath) || !Directory.Exists(userPluginPath))
                return result;

            IEnumerable<string> files;
            try
            {
                files = Directory.GetFiles(userPluginPath, "*", SearchOption.AllDirectories);
            }
            catch (Exception ex)
            {
                result.Errors.Add(ex.Message);
                return result;
            }

            foreach (var file in files)
            {
                try
                {
                    if (!ZoneIdentifierManager.IsZoneBlocked(file)) continue;
                    ZoneIdentifierManager.UnblockZone(file);
                    result.UnblockedFiles++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add(Path.GetFileName(file) + ": " + ex.Message);
                }
            }
            return result;
        }

        /// <summary>
        /// Step-by-step probe: DNS -> TCP:443 -> HTTPS GET, for each catalog/GitHub host.
        /// Pinpoints where a connection is being refused or blocked locally.
        /// </summary>
        public static async Task<NetworkProbeResult> ProbeNetworkAsync(CancellationToken cancellationToken)
        {
            var result = new NetworkProbeResult();
            var targets = new[]
            {
                new { Label = "raw.githubusercontent.com", Host = "raw.githubusercontent.com", Url = WikiCatalogService.WikiUrl },
                new { Label = "ghfast.top", Host = "ghfast.top", Url = "https://ghfast.top/https://raw.githubusercontent.com/wiki/QL-Win/QuickLook/Available-Plugins.md" },
                new { Label = "gh-proxy.com", Host = "gh-proxy.com", Url = "https://gh-proxy.com/https://raw.githubusercontent.com/wiki/QL-Win/QuickLook/Available-Plugins.md" },
                new { Label = "api.github.com", Host = "api.github.com", Url = "https://api.github.com/repos/QL-Win/QuickLook/releases/latest" }
            };

            foreach (var t in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var line = new NetworkProbeLine { Label = t.Label };
                var sw = Stopwatch.StartNew();
                try
                {
                    IPAddress[] addresses;
                    try
                    {
                        addresses = await Dns.GetHostAddressesAsync(t.Host).ConfigureAwait(false);
                        line.DnsOk = addresses != null && addresses.Length > 0;
                    }
                    catch (Exception ex)
                    {
                        line.Detail = "DNS: " + HttpHelper.Describe(ex);
                        result.Lines.Add(line);
                        continue;
                    }

                    using (var tcp = new TcpClient())
                    {
                        using (cancellationToken.Register(() => { try { tcp.Close(); } catch { } }))
                        {
                            try
                            {
                                await tcp.ConnectAsync(t.Host, 443).ConfigureAwait(false);
                                line.TcpOk = tcp.Connected;
                            }
                            catch (Exception ex)
                            {
                                line.Detail = "TCP: " + HttpHelper.Describe(ex);
                                result.Lines.Add(line);
                                continue;
                            }
                        }
                    }

                    using (var http = new HttpClient(new HttpClientHandler())
                    {
                        Timeout = TimeSpan.FromSeconds(10)
                    })
                    {
                        http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", HttpHelper.UserAgent);
                        using (var response = await http.GetAsync(t.Url, HttpCompletionOption.ResponseHeadersRead,
                                   cancellationToken).ConfigureAwait(false))
                        {
                            line.HttpOk = response.IsSuccessStatusCode;
                            if (!line.HttpOk)
                                line.Detail = "HTTP " + (int)response.StatusCode;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    line.Detail = HttpHelper.Describe(ex);
                }
                finally
                {
                    sw.Stop();
                    line.ElapsedMs = sw.ElapsedMilliseconds;
                    result.Lines.Add(line);
                }
            }

            return result;
        }

        public static string FormatProbeResult(NetworkProbeResult result)
        {
            if (result == null || result.Lines.Count == 0) return "";
            var sb = new StringBuilder();
            string last = null;
            foreach (var line in result.Lines)
            {
                string text;
                if (line.HttpOk)
                {
                    text = line.Label + " OK " + line.ElapsedMs + "ms";
                }
                else
                {
                    var stage = line.DnsOk ? (line.TcpOk ? "HTTP" : "TCP") : "DNS";
                    text = line.Label + " FAIL@" + stage;
                    text += !string.IsNullOrWhiteSpace(line.Detail)
                        ? " " + line.Detail
                        : " " + line.ElapsedMs + "ms";
                }

                // 同一目标多轮探测结果相同时只显示一次
                if (text == last) continue;
                if (sb.Length > 0) sb.AppendLine();
                sb.Append(text);
                last = text;
            }
            return sb.ToString();
        }
    }
}
