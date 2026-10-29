using System;
using System.IO;
using System.Windows;
using QuickLook.Common.Plugin;
using QuickLook.Plugin.PluginManager.Services;

namespace QuickLook.Plugin.PluginManager
{
    public class Plugin : IViewer
    {
        private static double _lastWidth = 1000;
        private static double _lastHeight = 700;

        private PluginManagerPanel _panel;

        public int Priority => 100;

        public void Init()
        {
        }

        public bool CanHandle(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            if (path.EndsWith(".qlplugin", StringComparison.OrdinalIgnoreCase)) return false;
            return DataRootResolver.IsUnderDataRoot(path);
        }

        public void Prepare(string path, ContextObject context)
        {
            context.SetPreferredSizeFit(new Size(_lastWidth, _lastHeight), 0.9);
        }

        public void View(string path, ContextObject context)
        {
            _panel = new PluginManagerPanel();
            _panel.Initialize(path, context);

            context.ViewerContent = _panel;
            context.Title = Loc.Get("Title");

            _panel.Dispatcher.Invoke(() => { context.IsBusy = false; },
                System.Windows.Threading.DispatcherPriority.Loaded);
        }

        public void Cleanup()
        {
            if (_panel != null)
            {
                _lastWidth = _panel.ActualWidth > 0 ? _panel.ActualWidth : _lastWidth;
                _lastHeight = _panel.ActualHeight > 0 ? _panel.ActualHeight : _lastHeight;
                _panel.Dispose();
                _panel = null;
            }
            GC.SuppressFinalize(this);
        }
    }
}
