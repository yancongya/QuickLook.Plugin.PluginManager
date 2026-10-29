using System;
using System.Windows.Markup;
using QuickLook.Plugin.PluginManager.Services;

namespace QuickLook.Plugin.PluginManager.Localization
{
    [MarkupExtensionReturnType(typeof(string))]
    public sealed class LocExtension : MarkupExtension
    {
        public LocExtension()
        {
        }

        public LocExtension(string key)
        {
            Key = key;
        }

        public string Key { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return Loc.Get(Key);
        }
    }
}
