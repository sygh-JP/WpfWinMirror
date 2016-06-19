using System;
using System.Collections.Generic;
using System.Windows.Data;
using System.Windows;

namespace MyWpfConverters
{
	[ValueConversion(typeof(bool), typeof(bool))]
	public class InverseBooleanConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			if (targetType != typeof(bool))
			{
				throw new ArgumentException("The target must be a 'bool'.");
			}
			if (!(value is bool))
			{
				throw new ArgumentException("The value must be a 'bool'.");
			}
			return !(bool)value;
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}

	[ValueConversion(typeof(double), typeof(double))]
	public class HalfDoubleConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			if (targetType != typeof(double))
			{
				throw new ArgumentException("The target must be a 'double'.");
			}
			if (!(value is double))
			{
				throw new ArgumentException("The value must be a 'double'.");
			}
			return (double)value * 0.5;
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}

	public class BooleanVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			if (targetType != typeof(Visibility))
			{
				throw new ArgumentException("The target must be a 'Visibility'.");
			}
			if (!(value is bool))
			{
				throw new ArgumentException("The value must be a 'bool'.");
			}
			var selected = (bool)value;

			return selected ? Visibility.Visible : Visibility.Collapsed;
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}

	public class NullableBooleanVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			if (targetType != typeof(Visibility))
			{
				throw new ArgumentException("The target must be a 'Visibility'.");
			}
			if (!(value is bool?))
			{
				throw new ArgumentException("The value must be a 'bool?'.");
			}

			var selected = (bool?)value;

			return (selected == null) ? Visibility.Collapsed : ((selected == false) ? Visibility.Hidden : Visibility.Visible);
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}

	/// <summary>
	/// Watermark を実現するためのヘルパー。
	/// TextBlock.Visibility などのターゲットに対して、ソースとなる TextBox.Text.IsEmpty および TextBox.IsFocused をバインディングする（MultiBinding）。
	/// </summary>
	public class TextFocusVisibilityConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			if (values[0] is bool && values[1] is bool)
			{
				bool hasText = !(bool)values[0]; // !Text.IsEmpty
				bool hasFocus = (bool)values[1]; // IsFocused

				if (hasFocus || hasText)
				{
					return Visibility.Collapsed;
				}
			}
			return Visibility.Visible;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}

	/// <summary>
	/// RectangleGeometry.Rect には直接データ バインディングできないので、MultiBinding とコンバーターを経由する必要がある。
	/// </summary>
	public class Double4XYWidthHeightToRectConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			if (values.Length == 4 &&
				values[0] is double &&
				values[1] is double &&
				values[2] is double &&
				values[3] is double)
			{
				var x = (double)values[0];
				var y = (double)values[1];
				var width = (double)values[2];
				var height = (double)values[3];

				return new Rect(x, y, width, height);
			}

			return DependencyProperty.UnsetValue;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}

	public class Double4CenterXYRadiusXYToRectConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			if (values.Length == 4 &&
				values[0] is double &&
				values[1] is double &&
				values[2] is double &&
				values[3] is double)
			{
				var cx = (double)values[0];
				var cy = (double)values[1];
				var rx = (double)values[2];
				var ry = (double)values[3];

				return new Rect(cx - rx, cy - ry, rx * 2, ry * 2);
			}

			return DependencyProperty.UnsetValue;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
