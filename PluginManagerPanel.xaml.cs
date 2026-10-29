using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using QuickLook.Common.Helpers;
using QuickLook.Common.Plugin;
using QuickLook.Plugin.PluginManager.Models;
using QuickLook.Plugin.PluginManager.Services;

namespace QuickLook.Plugin.PluginManager
{
    public partial class PluginManagerPanel : UserControl, IDisposable
    {
        private sealed class ConfigItem
        {
            public string DisplayName { get; set; }
            public string Path { get; set; }
        }

        private readonly InstalledPluginService _installedService = new InstalledPluginService();
        private readonly WikiCatalogService _wikiService = new WikiCatalogService();
        private readonly GitHubReleaseService _githubService = new GitHubReleaseService();
        private readonly ConfigLogService _configService = new ConfigLogService();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private List<PluginInfo> _installed = new List<PluginInfo>();
        private WikiCatalog _catalog;
        private ListCollectionView _availableView;
        private TaskCompletionSource<bool> _confirm;
        private string _selectedConfigPath;
        private string _handoffPath;
        private ContextObject _context;
        private Themes _theme = Themes.Dark;
        private bool _busy;
        private bool _disposed;
        private bool _autoRefreshTried;

        public PluginManagerPanel()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        public void Initialize(string path, ContextObject context)
        {
            _context = context;
            if (_context != null)
                _context.PropertyChanged += OnContextPropertyChanged;

            _theme = ResolveTheme(_context?.Theme ?? Themes.None);
            ApplyTheme();

            var info = DataRootResolver.Resolve();
            if (info.Success)
            {
                DataRootText.Text = Loc.Get("Msg_DataRoot") + ": " + info.DataRoot;
            }
            else
            {
                DataRootText.Text = Loc.Get("Msg_NoDataRoot");
                SetStatus(Loc.Get("Status_NoDataRoot"));
            }

            LoadInstalled();
            LoadConfigFiles();
            LoadCatalogCache();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_context != null)
                _context.PropertyChanged -= OnContextPropertyChanged;
            try
            {
                _cts.Cancel();
            }
            catch
            {
            }
            _cts.Dispose();
            Loaded -= OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_autoRefreshTried) return;
            _autoRefreshTried = true;
            if (_catalog == null || _catalog.Plugins.Count == 0)
            {
                Dispatcher.BeginInvoke(new Action(async () => await RefreshCatalogAsync(false)),
                    DispatcherPriority.Background);
            }
        }

        private void OnContextPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ContextObject.Theme)) return;
            _theme = ResolveTheme(_context.Theme);
            ApplyTheme();
        }

        private static Themes ResolveTheme(Themes theme)
        {
            if (theme == Themes.Dark || theme == Themes.Light) return theme;
            try
            {
                return OSThemeHelper.AppsUseDarkTheme() ? Themes.Dark : Themes.Light;
            }
            catch
            {
                return Themes.Dark;
            }
        }

        private void ThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            var next = _theme == Themes.Dark ? Themes.Light : Themes.Dark;
            if (_context != null)
            {
                // Setting context.Theme makes QuickLook's window follow too.
                _context.Theme = next;
            }
            _theme = next;
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            var dark = _theme != Themes.Light;
            SetBrush("PanelBackground", dark ? "#202020" : "#F7F7F7");
            SetBrush("PanelForeground", dark ? "#EFEFEF" : "#1A1A1A");
            SetBrush("PanelSecondary", dark ? "#A8A8A8" : "#666666");
            SetBrush("PanelCard", dark ? "#2B2B2B" : "#FFFFFF");
            SetBrush("PanelBorder", dark ? "#3C3C3C" : "#D6D6D6");
            SetBrush("PanelButton", dark ? "#343434" : "#EFEFEF");
            SetBrush("PanelButtonHover", dark ? "#424242" : "#E2E2E2");
            SetBrush("PanelHover", dark ? "#2F2F2F" : "#ECECEC");
            SetBrush("PanelAccent", dark ? "#4C8BF5" : "#2F6FEB");
            SetBrush("PanelAccentText", "#FFFFFF");
            SetBrush("PanelDanger", dark ? "#E05A5A" : "#C42B1C");
            SetBrush("PanelWarn", dark ? "#E0B341" : "#8A6100");
            SetBrush("PanelWarnBg", dark ? "#3A3320" : "#FFF4D6");
            SetBrush("PanelSuccess", dark ? "#6BBF6E" : "#2E7D32");

            if (ThemeIcon != null)
            {
                // Show the theme you would switch TO.
                ThemeIcon.Text = dark ? "\uE706" : "\uE708";
                if (ThemeButton != null)
                    ThemeButton.ToolTip = Loc.Get(dark ? "Btn_Theme_Light" : "Btn_Theme_Dark");
            }
        }

        private void SetBrush(string key, string color)
        {
            try
            {
                Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            }
            catch
            {
            }
        }

        private void SetStatus(string text)
        {
            if (_disposed) return;
            StatusText.Text = text ?? "";
        }

        private void SetBusy(bool busy)
        {
            if (_disposed) return;
            _busy = busy;
            BusyBar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            Tabs.IsEnabled = !busy;
        }

        private void LoadInstalled()
        {
            if (_disposed) return;
            SetStatus(Loc.Get("Status_Scanning"));
            var scan = _installedService.Scan();
            _installed = scan.Plugins;
            InstalledList.ItemsSource = null;
            InstalledList.ItemsSource = _installed;

            var userCount = _installed.Count(p => !p.IsBuiltIn);
            var builtInCount = _installed.Count(p => p.IsBuiltIn);
            InstalledCount.Text = Loc.Format("Status_Scanned", userCount, builtInCount);

            if (scan.Errors.Count > 0)
                SetStatus(Loc.Format("Status_Error", scan.Errors[0]));
            else
                SetStatus(Loc.Get("Status_Ready"));

            if (_catalog != null)
            {
                WikiCatalogService.MergeInstalled(_catalog, _installed);
                BindAvailable();
            }
        }

        private void LoadCatalogCache()
        {
            _catalog = _wikiService.LoadCache();
            if (_catalog == null)
            {
                AvailableHint.Text = Loc.Get("Available_Empty");
                AvailableEmpty.Visibility = Visibility.Visible;
                return;
            }

            WikiCatalogService.MergeInstalled(_catalog, _installed);
            BindAvailable();
            AvailableHint.Text = Loc.Get("Available_Stale");
        }

        private void BindAvailable()
        {
            if (_catalog == null)
            {
                AvailableList.ItemsSource = null;
                AvailableEmpty.Visibility = Visibility.Visible;
                return;
            }

            _availableView = new ListCollectionView(_catalog.Plugins);
            _availableView.GroupDescriptions.Add(new PropertyGroupDescription("GroupHeader"));
            _availableView.Filter = FilterAvailableEntry;
            AvailableList.ItemsSource = _availableView;
            UpdateAvailableEmpty();
        }

        private bool FilterAvailableEntry(object obj)
        {
            var entry = obj as CatalogEntry;
            if (entry == null) return true;
            if (HideInstalledCheck != null && HideInstalledCheck.IsChecked == true && entry.IsInstalled)
                return false;
            return true;
        }

        private void UpdateAvailableEmpty()
        {
            var visible = 0;
            if (_availableView != null)
            {
                for (var i = 0; i < _availableView.Count; i++) visible++;
            }
            AvailableEmpty.Visibility = visible == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void HideInstalled_Changed(object sender, RoutedEventArgs e)
        {
            if (_availableView != null)
            {
                _availableView.Refresh();
                UpdateAvailableEmpty();
            }
        }

        private async Task RefreshCatalogAsync(bool userInitiated)
        {
            if (_busy || _disposed) return;
            if (!DataRootResolver.Resolve().Success)
            {
                SetStatus(Loc.Get("Status_NoDataRoot"));
                return;
            }

            SetBusy(true);
            SetStatus(Loc.Get("Available_Loading"));
            try
            {
                var result = await _wikiService.RefreshAsync(_cts.Token);
                if (_disposed) return;

                if (result.Catalog != null)
                {
                    _catalog = result.Catalog;
                    WikiCatalogService.MergeInstalled(_catalog, _installed);
                    BindAvailable();
                    AvailableHint.Text = result.FromCache ? Loc.Get("Available_Stale") : Loc.Get("Available_Fresh");
                    if (!string.IsNullOrWhiteSpace(result.Error))
                        SetStatus(Loc.Format("Status_CatalogFailed", result.Error));
                    else
                        SetStatus(Loc.Get("Status_Ready"));
                }
                else
                {
                    AvailableHint.Text = Loc.Format("Status_CatalogFailed", result.Error ?? "");
                    SetStatus(Loc.Format("Status_CatalogFailed", result.Error ?? ""));
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                SetStatus(Loc.Format("Status_CatalogFailed", HttpHelper.Describe(ex)));
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void RefreshInstalled_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;
            LoadInstalled();
            await Task.CompletedTask;
        }

        private async void RefreshCatalog_Click(object sender, RoutedEventArgs e)
        {
            await RefreshCatalogAsync(true);
        }

        private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
        {
            var path = DataRootResolver.UserPluginPath;
            if (string.IsNullOrWhiteSpace(path)) SetStatus(Loc.Get("Status_NoDataRoot"));
            else RepairService.OpenFolder(path);
        }

        private void OpenWiki_Click(object sender, RoutedEventArgs e)
        {
            RepairService.OpenUrl("https://github.com/QL-Win/QuickLook/wiki/Available-Plugins");
        }

        private void OpenRepo_Click(object sender, RoutedEventArgs e)
        {
            var entry = GetEntry(sender);
            if (entry != null) RepairService.OpenUrl(entry.RepoUrl);
        }

        private void Releases_Click(object sender, RoutedEventArgs e)
        {
            var entry = GetEntry(sender);
            if (entry != null) RepairService.OpenUrl(entry.ReleasesUrl);
        }

        private async void Install_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;
            var entry = GetEntry(sender);
            if (entry == null) return;

            await DownloadAndHandoffAsync(entry.RepoUrl, ExpectedNamespace(entry.Name), entry.Name,
                entry.Name, RepoName(entry.RepoUrl), null);
        }

        private async void UpdateInstalled_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;
            var plugin = GetPlugin(sender);
            if (plugin == null || string.IsNullOrWhiteSpace(plugin.CatalogRepoUrl)) return;

            await DownloadAndHandoffAsync(plugin.CatalogRepoUrl, plugin.Namespace, plugin.DisplayName,
                plugin.DisplayName, plugin.Namespace, RepoName(plugin.CatalogRepoUrl));
        }

        private async Task DownloadAndHandoffAsync(string repoUrl, string expectedNamespace,
            string displayName, params string[] aliases)
        {
            if (string.IsNullOrWhiteSpace(repoUrl)) return;
            SetBusy(true);
            SetStatus(Loc.Format("Status_Resolving", displayName));
            try
            {
                var release = await _githubService.GetLatestAsync(repoUrl, _cts.Token);
                string reason;
                var asset = GitHubReleaseService.SelectAsset(release, expectedNamespace, out reason);
                if (asset == null)
                {
                    SetStatus(Loc.Format("Status_NoRelease", reason ?? ""));
                    return;
                }

                var directory = Path.Combine(Path.GetTempPath(), "QuickLook.PluginManager",
                    Guid.NewGuid().ToString("N"));
                var progress = new Progress<double>(value =>
                {
                    SetStatus(Loc.Format("Status_Downloading", displayName) + " " +
                              ((int)value).ToString() + "%");
                });

                var downloaded = await _githubService.DownloadAsync(asset, directory, progress, _cts.Token);
                if (_disposed) return;

                var aliasList = (aliases ?? new string[0]).Where(a => !string.IsNullOrWhiteSpace(a)).ToArray();
                var validation = SafeArchive.ValidatePackage(downloaded, expectedNamespace, aliasList);
                if (!validation.Success)
                {
                    SetStatus(Loc.Format("Status_DownloadFailed", validation.Error));
                    return;
                }

                _handoffPath = downloaded;
                HandoffMessage.Text = Loc.Format("Handoff_Message", downloaded);
                HandoffOverlay.Visibility = Visibility.Visible;
                SetStatus(Loc.Get("Status_RestartRequired"));
            }
            catch (OperationCanceledException)
            {
            }
            catch (GitHubApiException ex)
            {
                SetStatus(ex.IsRateLimited ? Loc.Get("Status_RateLimited") : Loc.Format("Status_DownloadFailed", HttpHelper.Describe(ex)));
            }
            catch (Exception ex)
            {
                SetStatus(Loc.Format("Status_DownloadFailed", HttpHelper.Describe(ex)));
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;
            var plugin = GetPlugin(sender);
            if (plugin == null) return;
            await CheckUpdatesAsync(new List<PluginInfo> { plugin });
        }

        private async void CheckAllUpdates_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;
            var targets = _installed.Where(p => p.CanCheckUpdate).ToList();
            if (targets.Count == 0)
            {
                SetStatus(Loc.Get("Status_NoUpdates"));
                return;
            }
            await CheckUpdatesAsync(targets);
        }

        private async Task CheckUpdatesAsync(List<PluginInfo> targets)
        {
            if (targets == null || targets.Count == 0) return;

            if (_catalog == null || _catalog.Plugins.Count == 0)
            {
                LoadCatalogCache();
                if (_catalog == null || _catalog.Plugins.Count == 0)
                    await RefreshCatalogAsync(false);
                if (_catalog == null || _catalog.Plugins.Count == 0)
                {
                    SetStatus(Loc.Get("Status_CatalogFailed"));
                    return;
                }
                // Refresh installed so CatalogRepoUrl gets populated.
                LoadInstalled();
            }

            var withRepo = targets.Where(p => p.CanCheckUpdate).ToList();
            if (withRepo.Count == 0)
            {
                SetStatus(Loc.Format("Status_NoCatalogRepo", targets[0].DisplayName));
                return;
            }

            SetBusy(true);
            var updates = 0;
            try
            {
                foreach (var plugin in withRepo)
                {
                    SetStatus(Loc.Format("Status_CheckingUpdate", plugin.DisplayName));
                    try
                    {
                        var release = await _githubService.GetLatestAsync(plugin.CatalogRepoUrl, _cts.Token);
                        var latest = release?.TagName;
                        plugin.LatestVersion = latest;
                        plugin.HasUpdate = VersionUtil.IsNewer(latest, plugin.Version);
                        if (plugin.HasUpdate)
                        {
                            updates++;
                            SetStatus(Loc.Format("Status_UpdateAvailable", plugin.DisplayName, latest ?? ""));
                        }
                    }
                    catch (GitHubApiException ex)
                    {
                        SetStatus(ex.IsRateLimited
                            ? Loc.Get("Status_RateLimited")
                            : Loc.Format("Status_Error", HttpHelper.Describe(ex)));
                        if (ex.IsRateLimited) return;
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        SetStatus(Loc.Format("Status_Error", HttpHelper.Describe(ex)));
                    }
                }

                if (targets.Count > 1)
                    SetStatus(updates > 0
                        ? Loc.Format("Status_UpdatesChecked", updates, withRepo.Count)
                        : Loc.Get("Status_NoUpdates"));
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void Uninstall_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;
            var plugin = GetPlugin(sender);
            if (plugin == null) return;

            var confirmed = await ConfirmAsync(Loc.Get("Confirm_Uninstall_Title"),
                Loc.Format("Confirm_Uninstall_Message", plugin.DisplayName),
                Loc.Get("Confirm_Uninstall_Button"));
            if (!confirmed) return;

            SetBusy(true);
            try
            {
                var result = _installedService.Uninstall(plugin);
                if (result.Success)
                {
                    SetStatus(Loc.Format("Status_Uninstalled", plugin.DisplayName));
                    LoadInstalled();
                }
                else
                {
                    SetStatus(Loc.Format("Status_UninstallFailed", result.Error));
                }
            }
            catch (Exception ex)
            {
                SetStatus(Loc.Format("Status_UninstallFailed", HttpHelper.Describe(ex)));
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void Reveal_Click(object sender, RoutedEventArgs e)
        {
            var plugin = GetPlugin(sender);
            if (plugin == null) return;
            if (!string.IsNullOrWhiteSpace(plugin.DllPath) && File.Exists(plugin.DllPath))
                RepairService.RevealFile(plugin.DllPath);
            else
                RepairService.OpenFolder(plugin.FolderPath);
        }

        private void LoadConfigFiles()
        {
            var items = new List<ConfigItem>();
            foreach (var file in _configService.GetConfigFiles())
            {
                items.Add(new ConfigItem { DisplayName = Path.GetFileName(file), Path = file });
            }

            var logPath = _configService.LogPath;
            if (!string.IsNullOrWhiteSpace(logPath) && File.Exists(logPath))
            {
                items.Add(new ConfigItem { DisplayName = Path.GetFileName(logPath), Path = logPath });
            }

            ConfigList.ItemsSource = items;
            if (items.Count > 0)
            {
                ConfigList.SelectedIndex = 0;
            }
            else
            {
                _selectedConfigPath = null;
                ConfigText.Text = Loc.Get("Config_NoFiles");
            }
        }

        private void ConfigList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = ConfigList.SelectedItem as ConfigItem;
            if (item == null) return;
            _selectedConfigPath = item.Path;
            ConfigText.Text = _configService.ReadText(item.Path);
        }

        private void OpenConfig_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_selectedConfigPath))
            {
                SetStatus(Loc.Get("Config_NoFiles"));
                return;
            }
            RepairService.RevealFile(_selectedConfigPath);
        }

        private void CopyConfig_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ConfigText.Text)) return;
            try
            {
                Clipboard.SetText(ConfigText.Text);
                SetStatus(Loc.Get("Config_Copied"));
            }
            catch (Exception ex)
            {
                SetStatus(Loc.Format("Status_Error", HttpHelper.Describe(ex)));
            }
        }

        private async void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            var logPath = _configService.LogPath;
            if (string.IsNullOrWhiteSpace(logPath) || !File.Exists(logPath))
            {
                SetStatus(Loc.Get("Config_NoLog"));
                return;
            }

            var confirmed = await ConfirmAsync(Loc.Get("Confirm_Cleanup_Title"),
                Loc.Get("Config_Hint"), Loc.Get("Btn_ClearLog"));
            if (!confirmed) return;

            string error;
            if (_configService.TryClearLog(out error))
            {
                SetStatus(Loc.Get("Config_LogCleared"));
                LoadConfigFiles();
            }
            else
            {
                SetStatus(Loc.Format("Config_LogClearFailed", error ?? ""));
            }
        }

        private void RepairOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            var path = DataRootResolver.UserPluginPath;
            if (string.IsNullOrWhiteSpace(path)) SetStatus(Loc.Get("Status_NoDataRoot"));
            else RepairService.OpenFolder(path);
        }

        private async void RepairRestart_Click(object sender, RoutedEventArgs e)
        {
            await RestartWithConfirmAsync();
        }

        private async void Restart_Click(object sender, RoutedEventArgs e)
        {
            await RestartWithConfirmAsync();
        }

        private async Task RestartWithConfirmAsync()
        {
            if (_busy) return;
            var confirmed = await ConfirmAsync(Loc.Get("Confirm_Restart_Title"),
                Loc.Get("Confirm_Restart_Message"), Loc.Get("Btn_Restart"));
            if (!confirmed) return;

            string error;
            if (!RepairService.RestartQuickLook(out error))
                SetStatus(Loc.Format("Status_Error", error ?? ""));
        }

        private async void RepairCleanup_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;
            var confirmed = await ConfirmAsync(Loc.Get("Confirm_Cleanup_Title"),
                Loc.Get("Confirm_Cleanup_Message"), Loc.Get("Repair_Cleanup"));
            if (!confirmed) return;

            SetBusy(true);
            try
            {
                var result = RepairService.CleanupLeftovers();
                if (result.Errors.Count > 0)
                    SetStatus(Loc.Format("Status_CleanupFailed", result.Errors.Count));
                else
                    SetStatus(Loc.Format("Status_CleanupDone", result.RemovedFiles, result.RemovedDirectories));
                LoadInstalled();
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void RepairUnblock_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;
            var result = RepairService.UnblockPluginFiles();
            if (result.Errors.Count > 0)
                SetStatus(Loc.Format("Status_UnblockFailed", result.Errors[0]));
            else
                SetStatus(Loc.Format("Status_Unblocked", result.UnblockedFiles));
        }

        private async void RepairNetworkDiag_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;
            SetBusy(true);
            SetStatus(Loc.Get("Status_NetworkDiagRunning"));
            try
            {
                var result = await RepairService.ProbeNetworkAsync(_cts.Token);
                if (_disposed) return;

                // Show the report in the confirm overlay so it stays readable.
                await ConfirmAsync(Loc.Get("Repair_NetworkDiag"),
                    RepairService.FormatProbeResult(result), Loc.Get("Btn_Close"));
                SetStatus(Loc.Get("Status_NetworkDiagDone"));
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                SetStatus(Loc.Format("Status_NetworkDiagFailed", HttpHelper.Describe(ex)));
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void HandoffOpen_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_handoffPath))
                RepairService.RevealFile(_handoffPath);
        }

        private void HandoffClose_Click(object sender, RoutedEventArgs e)
        {
            HandoffOverlay.Visibility = Visibility.Collapsed;
        }

        private Task<bool> ConfirmAsync(string title, string message, string okText)
        {
            if (_confirm != null) return Task.FromResult(false);
            _confirm = new TaskCompletionSource<bool>();
            ConfirmTitle.Text = title;
            ConfirmMessage.Text = message;
            ConfirmOk.Content = okText;
            ConfirmOverlay.Visibility = Visibility.Visible;
            return _confirm.Task;
        }

        private void ConfirmOk_Click(object sender, RoutedEventArgs e)
        {
            CompleteConfirm(true);
        }

        private void ConfirmCancel_Click(object sender, RoutedEventArgs e)
        {
            CompleteConfirm(false);
        }

        private void CompleteConfirm(bool result)
        {
            var source = _confirm;
            _confirm = null;
            ConfirmOverlay.Visibility = Visibility.Collapsed;
            if (source != null) source.TrySetResult(result);
        }

        private static PluginInfo GetPlugin(object sender)
        {
            var element = sender as FrameworkElement;
            return element == null ? null : element.Tag as PluginInfo;
        }

        private static CatalogEntry GetEntry(object sender)
        {
            var element = sender as FrameworkElement;
            return element == null ? null : element.Tag as CatalogEntry;
        }

        private static string ExpectedNamespace(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            return name.StartsWith("QuickLook.Plugin.", StringComparison.OrdinalIgnoreCase)
                ? name
                : "QuickLook.Plugin." + name;
        }

        private static string RepoName(string repoUrl)
        {
            if (string.IsNullOrWhiteSpace(repoUrl)) return null;
            try
            {
                var uri = new Uri(repoUrl);
                return uri.Segments.Length == 0 ? null : uri.Segments[uri.Segments.Length - 1].Trim('/');
            }
            catch
            {
                return null;
            }
        }
    }
}
