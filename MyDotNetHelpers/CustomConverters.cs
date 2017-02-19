using System;
using System.Collections.Generic;
using System.Windows.Data;
using System.Windows;

namespace MyWpfConverters
{
	public class IntToBinaryStringConverter : IValueConverter
	{
		// 16進数は StringFormat でなんとかなるが、2進数は対応していないのが不便。
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			if (!(parameter is string))
			{
				throw new ArgumentException("The parameter must be a 'string'.", "parameter");
			}
			int totalWidth = Int32.Parse((string)parameter);
			if (value == null)
			{
				// Nullable は許可する。
				return null;
			}
			else if (value is byte)
			{
				return MyMiscHelpers.MyBitOpHelper.ConvertToBinaryDigitsString((byte)value, totalWidth);
			}
			else if (value is short)
			{
				return MyMiscHelpers.MyBitOpHelper.ConvertToBinaryDigitsString((short)value, totalWidth);
			}
			else if (value is int)
			{
				return MyMiscHelpers.MyBitOpHelper.ConvertToBinaryDigitsString((int)value, totalWidth);
			}
			else if (value is long)
			{
				return MyMiscHelpers.MyBitOpHelper.ConvertToBinaryDigitsString((long)value, totalWidth);
			}
			else if (value is sbyte)
			{
				return MyMiscHelpers.MyBitOpHelper.ConvertToBinaryDigitsString((sbyte)value, totalWidth);
			}
			else if (value is ushort)
			{
				return MyMiscHelpers.MyBitOpHelper.ConvertToBinaryDigitsString((ushort)value, totalWidth);
			}
			else if (value is uint)
			{
				return MyMiscHelpers.MyBitOpHelper.ConvertToBinaryDigitsString((uint)value, totalWidth);
			}
			else if (value is ulong)
			{
				return MyMiscHelpers.MyBitOpHelper.ConvertToBinaryDigitsString((ulong)value, totalWidth);
			}
			else
			{
				throw new ArgumentException("The value must be an integer.", "value");
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}

	/// <summary>
	/// 実際の入力には bool だけでなく bool? も使える。ただし出力は bool のみ。
	/// </summary>
	[ValueConversion(typeof(bool), typeof(bool))]
	public class InverseBooleanConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			if (targetType != typeof(bool))
			{
				throw new ArgumentException("The target must be a 'bool'.", "targetType");
			}
			if (!(value is bool))
			{
				throw new ArgumentException("The value must be a 'bool'.", "value");
			}
			return !(bool)value;
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}

	/// <summary>
	/// 入出力ともに bool? のみサポート。
	/// </summary>
	[ValueConversion(typeof(bool?), typeof(bool?))]
	public class InverseNullableBooleanConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			if (targetType != typeof(bool?))
			{
				throw new ArgumentException("The target must be a 'bool?'.", "targetType");
			}
			if (!(value is bool?))
			{
				throw new ArgumentException("The value must be a 'bool?'.", "value");
			}
			return !(bool?)value; // null は否定しても null のまま。
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
				throw new ArgumentException("The target must be a 'double'.", "targetType");
			}
			if (!(value is double))
			{
				throw new ArgumentException("The value must be a 'double'.", "value");
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
				throw new ArgumentException("The target must be a 'Visibility'.", "targetType");
			}
			if (!(value is bool))
			{
				throw new ArgumentException("The value must be a 'bool'.", "value");
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
				throw new ArgumentException("The target must be a 'Visibility'.", "targetType");
			}
			if (!(value is bool?))
			{
				throw new ArgumentException("The value must be a 'bool?'.", "value");
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

	public class Double4XYXYToLengthConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			if (values.Length == 4 &&
				values[0] is double &&
				values[1] is double &&
				values[2] is double &&
				values[3] is double)
			{
				var x0 = (double)values[0];
				var y0 = (double)values[1];
				var x1 = (double)values[2];
				var y1 = (double)values[3];

				// NaN は考慮しない。
				var dx = x0 - x1;
				var dy = y0 - y1;
				return Math.Sqrt(dx * dx + dy * dy);
			}

			return DependencyProperty.UnsetValue;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}

	public class Double2XYToPointConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			if (values.Length == 2 &&
				values[0] is double &&
				values[1] is double)
			{
				var x = (double)values[0];
				var y = (double)values[1];

				return new Point(x, y);
			}

			return DependencyProperty.UnsetValue;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}

	public class Int2XYToPointConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			if (values.Length == 2 &&
				values[0] is int &&
				values[1] is int)
			{
				var x = (int)values[0];
				var y = (int)values[1];

				return new Point(x, y);
			}

			return DependencyProperty.UnsetValue;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}

	public class Int4XYXYToMeanPointConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			if (values.Length == 4 &&
				values[0] is int &&
				values[1] is int &&
				values[2] is int &&
				values[3] is int)
			{
				var x0 = (int)values[0];
				var y0 = (int)values[1];
				var x1 = (int)values[2];
				var y1 = (int)values[3];

				return new Point(0.5 * (x0 + x1), 0.5 * (y0 + y1));
			}

			return DependencyProperty.UnsetValue;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}

	public class Double2ToMeanDoubleConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			if (values.Length == 2 &&
				values[0] is double &&
				values[1] is double)
			{
				var v0 = (double)values[0];
				var v1 = (double)values[1];

				return 0.5 * (v0 + v1);
			}

			return DependencyProperty.UnsetValue;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
		{
			throw new NotSupportedException();
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

	/// <summary>
	/// RectangleGeometry.Rect には直接データ バインディングできないので、MultiBinding とコンバーターを経由する必要がある。
	/// </summary>
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

	/// <summary>
	/// Viewbox 内で拡大縮小率によらず画面表示上の線幅などを一定に保つために利用する。
	/// </summary>
	public class DivideDoubleByScalingRatioConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			if (values.Length == 3 &&
				values[0] is double &&
				values[1] is double &&
				values[2] is double)
			{
				var v0 = (double)values[0]; // Main source value
				var v1 = (double)values[1]; // Original (non-scaled) size of UI element
				var v2 = (double)values[2]; // Actual (scaled) size of UI element
				// 初回表示の際に v2 はゼロになりうる。浮動小数点数のゼロ割の結果は Infinity や NaN になる。
				// しかし、Infinity を TextBlock.FontSize にバインドすると、
				// デバッグ セッションにて IDE 出力ウィンドウにエラーを示す診断メッセージが出力されてうっとうしい。
				// かといって、Infinity の代わりにゼロを強制的に返すようにすると、
				// 今度は TextBlock.FontSize や CenteredEllipse.RadiusX/RadiusY のバインドでエラーメッセージが出力されてしまう。
				// FontSize は 0.0 を許可しているはずなのに……
				// ここは異常値として DependencyProperty.UnsetValue を返すのが正解らしい。
				if (Math.Abs(v2) > Double.Epsilon)
				{
					return v0 * (v1 / v2);
				}
			}
			return DependencyProperty.UnsetValue;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
