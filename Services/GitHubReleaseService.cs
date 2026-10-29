using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using QuickLook.Plugin.PluginManager.Models;

namespace QuickLook.Plugin.PluginManager.Services
{
    public sealed class GitHubApiException : Exception
    {
        public bool IsRateLimited { get; set; }

        public GitHubApiException(string message, bool isRateLimited = false) : base(message)
        {
            IsRateLimited = isRateLimited;
        }
    }

    public sealed class GitHubReleaseService
    {
        private static readonly HttpClient Http = HttpHelper.CreateClient(TimeSpan.FromSeconds(30));

        public async Task<GitHubRelease> GetLatestAsync(string repoUrl, CancellationToken cancellationToken)
        {
            var slug = ParseRepoSlug(repoUrl);
            if (string.IsNullOrWhiteSpace(slug))
                throw new GitHubApiException("Not a GitHub repository URL.");

            var apiUrl = "https://api.github.com/repos/" + slug + "/releases/latest";
            using (var request = new HttpRequestMessage(HttpMethod.Get, apiUrl))
            {
                request.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
                request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
                using (var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
                           cancellationToken).ConfigureAwait(false))
                {
                    if (IsRateLimited(response))
                        throw new GitHubApiException("GitHub API rate limit reached.", true);
                    if (response.StatusCode == HttpStatusCode.NotFound)
                        throw new GitHubApiException("This repository has no published release.");
                    response.EnsureSuccessStatusCode();

                    var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var root = JObject.Parse(json);
                    var release = new GitHubRelease
                    {
                        TagName = (string)root["tag_name"],
                        HtmlUrl = (string)root["html_url"]
                    };

                    var assets = root["assets"] as JArray;
                    if (assets != null)
                    {
                        foreach (var item in assets.OfType<JObject>())
                        {
                            var name = (string)item["name"];
                            var url = (string)item["browser_download_url"];
                            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url)) continue;
                            release.Assets.Add(new ReleaseAsset
                            {
                                Name = name,
                                DownloadUrl = url,
                                Size = item["size"] != null ? (long)item["size"] : 0,
                                Digest = (string)item["digest"]
                            });
                        }
                    }
                    return release;
                }
            }
        }

        public static string ParseRepoSlug(string repoUrl)
        {
            if (string.IsNullOrWhiteSpace(repoUrl)) return null;
            try
            {
                var uri = new Uri(repoUrl);
                if (!string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)) return null;
                var segments = uri.AbsolutePath.Trim('/').Split('/');
                if (segments.Length < 2) return null;
                var owner = segments[0];
                var repo = segments[1];
                if (repo.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                    repo = repo.Substring(0, repo.Length - 4);
                if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo)) return null;
                return owner + "/" + repo;
            }
            catch
            {
                return null;
            }
        }

        public static ReleaseAsset SelectAsset(GitHubRelease release, string expectedNamespace, out string reason)
        {
            reason = null;
            if (release == null || release.Assets == null || release.Assets.Count == 0)
            {
                reason = "The release has no assets.";
                return null;
            }

            var candidates = release.Assets
                .Where(a => !string.IsNullOrWhiteSpace(a.Name)
                            && a.Name.EndsWith(".qlplugin", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (candidates.Count == 0)
            {
                reason = "The release has no .qlplugin asset.";
                return null;
            }
            if (candidates.Count == 1) return candidates[0];

            if (!string.IsNullOrWhiteSpace(expectedNamespace))
            {
                var exact = expectedNamespace + ".qlplugin";
                var match = candidates.FirstOrDefault(a =>
                    string.Equals(a.Name, exact, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;

                var expected = Models.CatalogEntry.NormalizeName(expectedNamespace);
                match = candidates.FirstOrDefault(a =>
                    Models.CatalogEntry.NormalizeName(Path.GetFileNameWithoutExtension(a.Name)) == expected);
                if (match != null) return match;

                match = candidates.FirstOrDefault(a =>
                    Models.CatalogEntry.NormalizeName(Path.GetFileNameWithoutExtension(a.Name)).Contains(expected));
                if (match != null) return match;
            }

            reason = "Multiple .qlplugin assets found: " + string.Join(", ", candidates.Select(a => a.Name));
            return null;
        }

        public async Task<string> DownloadAsync(ReleaseAsset asset, string destinationDirectory,
            IProgress<double> progress, CancellationToken cancellationToken)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            if (string.IsNullOrWhiteSpace(asset.DownloadUrl))
                throw new GitHubApiException("Asset has no download URL.");
            if (asset.Size > 200L * 1024 * 1024)
                throw new GitHubApiException("Asset is larger than 200 MB.");

            var fileName = Path.GetFileName(asset.Name);
            if (string.IsNullOrWhiteSpace(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new GitHubApiException("Asset has an invalid file name.");
            if (string.Equals(fileName, ".", StringComparison.Ordinal) || string.Equals(fileName, "..", StringComparison.Ordinal))
                throw new GitHubApiException("Asset has an invalid file name.");

            Directory.CreateDirectory(destinationDirectory);
            var target = Path.Combine(destinationDirectory, fileName);
            if (File.Exists(target))
            {
                var stem = Path.GetFileNameWithoutExtension(fileName);
                var ext = Path.GetExtension(fileName);
                var index = 1;
                while (File.Exists(target))
                {
                    target = Path.Combine(destinationDirectory, stem + "-" + index.ToString(CultureInfo.InvariantCulture) + ext);
                    index++;
                }
            }

            using (var request = new HttpRequestMessage(HttpMethod.Get, asset.DownloadUrl))
            using (var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
                       cancellationToken).ConfigureAwait(false))
            {
                if (IsRateLimited(response))
                    throw new GitHubApiException("GitHub download rate limit reached.", true);
                response.EnsureSuccessStatusCode();

                var total = response.Content.Headers.ContentLength ?? asset.Size;
                if (total > 200L * 1024 * 1024)
                    throw new GitHubApiException("Asset is larger than 200 MB.");

                try
                {
                    using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        var buffer = new byte[81920];
                        long readTotal = 0;
                        int read;
                        while ((read = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken)
                                   .ConfigureAwait(false)) > 0)
                        {
                            await output.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                            readTotal += read;
                            if (readTotal > 200L * 1024 * 1024)
                                throw new GitHubApiException("Asset is larger than 200 MB.");
                            if (total > 0 && progress != null)
                                progress.Report(Math.Min(100.0, readTotal * 100.0 / total));
                        }
                    }
                }
                catch
                {
                    TryDelete(target);
                    throw;
                }
            }

            if (!SafeArchive.VerifySha256(target, asset.Digest))
            {
                TryDelete(target);
                throw new GitHubApiException("SHA-256 digest does not match.");
            }

            if (progress != null) progress.Report(100.0);
            return target;
        }

        private static bool IsRateLimited(HttpResponseMessage response)
        {
            if (response.StatusCode != (HttpStatusCode)403 && response.StatusCode != (HttpStatusCode)429)
                return false;
            IEnumerable<string> values;
            if (response.Headers.TryGetValues("X-RateLimit-Remaining", out values))
            {
                var value = values.FirstOrDefault();
                if (string.Equals(value, "0", StringComparison.Ordinal)) return true;
            }
            return response.StatusCode == (HttpStatusCode)429;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
            }
        }
    }
}
