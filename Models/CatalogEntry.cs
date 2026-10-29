using System;
using System.ComponentModel;
using System.Globalization;
using QuickLook.Plugin.PluginManager.Services;

namespace QuickLook.Plugin.PluginManager.Models
{
    public sealed class CatalogEntry : INotifyPropertyChanged
    {
        private bool _isInstalled;
        private string _installedVersion;

        public string Name { get; set; }
        public string VersionText { get; set; }
        public string Description { get; set; }
        public string RepoUrl { get; set; }
        public string ReleasesUrl { get; set; }
        public bool IsLegacy { get; set; }
        public string ListName { get; set; }

        public bool IsInstalled
        {
            get { return _isInstalled; }
            set
            {
                if (_isInstalled == value) return;
                _isInstalled = value;
                Raise("IsInstalled");
                Raise("ActionText");
                Raise("StatusText");
            }
        }

        public string InstalledVersion
        {
            get { return _installedVersion; }
            set
            {
                if (_installedVersion == value) return;
                _installedVersion = value;
                Raise("InstalledVersion");
                Raise("StatusText");
            }
        }

        public string GroupHeader
        {
            get { return IsLegacy ? Loc.Get("Available_Legacy") : Loc.Get("Available_Active"); }
        }

        public string DisplayVersion
        {
            get { return string.IsNullOrWhiteSpace(VersionText) ? Loc.Get("Msg_UnknownVersion") : VersionText; }
        }

        public string ActionText
        {
            get { return IsInstalled ? Loc.Get("Btn_Update") : Loc.Get("Btn_Install"); }
        }

        public string StatusText
        {
            get
            {
                if (IsInstalled)
                    return Loc.Get("Btn_Installed") + " " + (string.IsNullOrWhiteSpace(InstalledVersion) ? "" : InstalledVersion);
                return "";
            }
        }

        public bool HasRepo
        {
            get { return !string.IsNullOrWhiteSpace(RepoUrl); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Raise(string name)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(name));
        }

        public static string NormalizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            var text = value.Trim().ToLower(CultureInfo.InvariantCulture);
            if (text.StartsWith("quicklook.plugin.", StringComparison.Ordinal))
                text = text.Substring("quicklook.plugin.".Length);
            var chars = new char[text.Length];
            var count = 0;
            foreach (var c in text)
            {
                if (char.IsLetterOrDigit(c)) chars[count++] = c;
            }
            return new string(chars, 0, count);
        }
    }
}
