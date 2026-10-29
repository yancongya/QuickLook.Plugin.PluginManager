using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using QuickLook.Plugin.PluginManager.Models;

namespace QuickLook.Plugin.PluginManager.Services
{
    public sealed class PackageValidationResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public PluginMetadata Metadata { get; set; }
        public long Size { get; set; }
    }

    public static class SafeArchive
    {
        public static bool LooksLikeZip(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
                using (var stream = File.OpenRead(path))
                {
                    return stream.ReadByte() == 'P' && stream.ReadByte() == 'K';
                }
            }
            catch
            {
                return false;
            }
        }

        public static PackageValidationResult ValidatePackage(string zipPath, string expectedNamespace,
            params string[] aliases)
        {
            var result = new PackageValidationResult();
            try
            {
                if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
                {
                    result.Error = "File does not exist.";
                    return result;
                }

                result.Size = new FileInfo(zipPath).Length;
                if (result.Size <= 0)
                {
                    result.Error = "File is empty.";
                    return result;
                }
                if (result.Size > 200L * 1024 * 1024)
                {
                    result.Error = "Package is larger than 200 MB.";
                    return result;
                }
                if (!LooksLikeZip(zipPath))
                {
                    result.Error = "File is not a zip package.";
                    return result;
                }

                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    var unsafeEntry = archive.Entries.FirstOrDefault(e => IsUnsafeEntry(e.FullName));
                    if (unsafeEntry != null)
                    {
                        result.Error = "Package contains an unsafe path: " + unsafeEntry.FullName;
                        return result;
                    }

                    var metadataEntry = archive.Entries.FirstOrDefault(e =>
                        string.Equals(MetadataReader.NormalizeEntry(e.FullName), MetadataReader.FileName,
                            StringComparison.OrdinalIgnoreCase));
                    if (metadataEntry == null)
                    {
                        result.Error = "Package is missing " + MetadataReader.FileName + ".";
                        return result;
                    }

                    string xml;
                    using (var reader = new StreamReader(metadataEntry.Open(), Encoding.UTF8, true))
                    {
                        xml = reader.ReadToEnd();
                    }

                    var metadata = MetadataReader.Parse(xml);
                    if (metadata == null)
                    {
                        result.Error = "Package metadata is invalid.";
                        return result;
                    }
                    if (string.IsNullOrWhiteSpace(metadata.Namespace)
                        || !metadata.Namespace.StartsWith("QuickLook.Plugin.", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Error = "Package namespace must start with QuickLook.Plugin.";
                        return result;
                    }
                    if (!string.IsNullOrWhiteSpace(expectedNamespace)
                        && !string.Equals(metadata.Namespace, expectedNamespace, StringComparison.OrdinalIgnoreCase))
                    {
                        var accepted = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        AddName(accepted, expectedNamespace);
                        if (aliases != null)
                        {
                            foreach (var alias in aliases) AddName(accepted, alias);
                        }

                        var actual = CatalogEntry.NormalizeName(metadata.Namespace);
                        if (!accepted.Contains(actual))
                        {
                            result.Error = string.Format("Package is for {0}, expected {1}.", metadata.Namespace, expectedNamespace);
                            return result;
                        }
                    }

                    result.Metadata = metadata;
                    result.Success = true;
                    return result;
                }
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                return result;
            }
        }

        public static bool VerifySha256(string path, string digest)
        {
            if (string.IsNullOrWhiteSpace(digest)) return true;
            var value = digest.Trim();
            if (value.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                value = value.Substring("sha256:".Length);
            if (value.Length != 64) return true;

            try
            {
                using (var sha = SHA256.Create())
                using (var stream = File.OpenRead(path))
                {
                    var hash = sha.ComputeHash(stream);
                    var actual = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    return string.Equals(actual, value.ToLowerInvariant(), StringComparison.Ordinal);
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool IsUnsafeEntry(string name)
        {
            var normalized = MetadataReader.NormalizeEntry(name);
            if (string.IsNullOrWhiteSpace(normalized)) return true;
            if (normalized.StartsWith("/", StringComparison.Ordinal)) return true;
            if (normalized.Length >= 2 && normalized[1] == ':') return true;
            var parts = normalized.Split('/');
            foreach (var part in parts)
            {
                if (part == "..") return true;
            }
            return false;
        }

        private static void AddName(System.Collections.Generic.HashSet<string> set, string value)
        {
            var key = CatalogEntry.NormalizeName(value);
            if (!string.IsNullOrEmpty(key)) set.Add(key);
        }
    }
}
