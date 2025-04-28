using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ScriptSchedulerApp.Converters
{
    /// <summary>
    /// Converts a value to Visibility.Visible if it equals the parameter, otherwise to Visibility.Collapsed
    /// </summary>
    public class EqualityToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return Visibility.Collapsed;

            // Try integer comparison first
            if (value is int intValue && int.TryParse(parameter?.ToString(), out int intParameter))
            {
                return intValue == intParameter ? Visibility.Visible : Visibility.Collapsed;
            }
            
            // Default to string comparison
            return value.ToString().Equals(parameter?.ToString()) ? 
                Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null && value.Equals(Visibility.Visible) ? parameter : Binding.DoNothing;
        }
    }
}