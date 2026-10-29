using System.Collections.Generic;

namespace QuickLook.Plugin.PluginManager.Models
{
    public sealed class WikiCatalog
    {
        public int SchemaVersion { get; set; }
        public string SourceUrl { get; set; }
        public string ETag { get; set; }
        public string UpdatedAt { get; set; }
        public List<CatalogEntry> Plugins { get; set; }

        public WikiCatalog()
        {
            SchemaVersion = 1;
            Plugins = new List<CatalogEntry>();
        }
    }
}
