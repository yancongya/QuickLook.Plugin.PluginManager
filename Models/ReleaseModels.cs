using System.Collections.Generic;

namespace QuickLook.Plugin.PluginManager.Models
{
    public sealed class GitHubRelease
    {
        public string TagName { get; set; }
        public string HtmlUrl { get; set; }
        public List<ReleaseAsset> Assets { get; set; }

        public GitHubRelease()
        {
            Assets = new List<ReleaseAsset>();
        }
    }

    public sealed class ReleaseAsset
    {
        public string Name { get; set; }
        public string DownloadUrl { get; set; }
        public long Size { get; set; }
        public string Digest { get; set; }
    }
}
