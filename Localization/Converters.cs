using System;
using System.Globalization;
using System.Windows.Data;
using QuickLook.Plugin.PluginManager.Services;

namespace QuickLook.Plugin.PluginManager.Localization
{
    public sealed class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(value is bool && (bool)value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(value is bool && (bool)value);
        }
    }

    /// <summary>值（分组名）是否等于 parameter 指定的 Loc 键的当前语言文案。</summary>
    public sealed class LocEqualsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var key = parameter as string;
            if (key == null || value == null) return false;
            return string.Equals(value.ToString(), Loc.Get(key), StringComparison.CurrentCulture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
