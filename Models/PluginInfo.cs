using System;
using System.ComponentModel;
using System.IO;
using QuickLook.Plugin.PluginManager.Services;

namespace QuickLook.Plugin.PluginManager.Models
{
    public sealed class PluginInfo : INotifyPropertyChanged
    {
        private string _latestVersion;
        private bool _hasUpdate;
        private bool _checkingUpdate;

        public string Name { get; set; }
        public string Namespace { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
        public string FolderPath { get; set; }
        public string DllPath { get; set; }
        public bool IsBuiltIn { get; set; }
        public bool IsLoose { get; set; }
        public bool IsSelf { get; set; }
        public bool HasMetadata { get; set; }
        public bool HasDll { get; set; }
        public int FileCount { get; set; }
        public DateTime? LastWriteTime { get; set; }

        /// <summary>GitHub repo url matched from the QuickLook Wiki catalog.</summary>
        public string CatalogRepoUrl { get; set; }

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Namespace))
                    return Namespace;
                if (!string.IsNullOrWhiteSpace(Name))
                    return Name;
                if (!string.IsNullOrWhiteSpace(FolderPath))
                    return Path.GetFileName(FolderPath);
                return "?";
            }
        }

        public string DisplayVersion
        {
            get { return string.IsNullOrWhiteSpace(Version) ? Loc.Get("Msg_UnknownVersion") : Version; }
        }

        public string UpdateVersionText
        {
            get
            {
                if (!HasUpdate) return "";
                return DisplayVersion + "  →  " + LatestVersion;
            }
        }

        public string TypeText
        {
            get
            {
                if (IsBuiltIn) return Loc.Get("Type_BuiltIn");
                if (IsLoose) return Loc.Get("Type_Loose");
                return Loc.Get("Type_User");
            }
        }

        public string StatusText
        {
            get
            {
                if (IsSelf) return Loc.Get("Status_Self");
                if (IsBuiltIn) return Loc.Get("Status_Ready");
                if (!HasMetadata) return Loc.Get("Status_NoMetadata");
                if (!HasDll) return Loc.Get("Status_NoDll");
                return Loc.Get("Status_Ready");
            }
        }

        public bool StatusIsWarning
        {
            get { return !IsSelf && !IsBuiltIn && (!HasMetadata || !HasDll); }
        }

        public bool CanUninstall
        {
            get { return !IsBuiltIn && !IsSelf && !IsLoose && !string.IsNullOrEmpty(FolderPath); }
        }

        public bool CanCheckUpdate
        {
            get { return !IsSelf && !string.IsNullOrWhiteSpace(CatalogRepoUrl); }
        }

        public bool CanUpdate
        {
            get { return HasUpdate && !IsSelf && !string.IsNullOrWhiteSpace(CatalogRepoUrl); }
        }

        public string LatestVersion
        {
            get { return _latestVersion; }
            set
            {
                if (_latestVersion == value) return;
                _latestVersion = value;
                Raise(nameof(LatestVersion));
                Raise(nameof(UpdateVersionText));
            }
        }

        public bool HasUpdate
        {
            get { return _hasUpdate; }
            set
            {
                if (_hasUpdate == value) return;
                _hasUpdate = value;
                Raise(nameof(HasUpdate));
                Raise(nameof(UpdateVersionText));
                Raise(nameof(CanUpdate));
            }
        }

        public bool CheckingUpdate
        {
            get { return _checkingUpdate; }
            set
            {
                if (_checkingUpdate == value) return;
                _checkingUpdate = value;
                Raise(nameof(CheckingUpdate));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Raise(string name)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(name));
        }
    }
}
