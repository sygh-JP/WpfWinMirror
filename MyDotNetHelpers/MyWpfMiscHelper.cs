using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
//using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MyWpfHelpers
{
	public static class MyWpfMiscHelper
	{
		public static string GetAppName()
		{
			return Application.ResourceAssembly.GetName().Name;
			// Assembly.GetExecutingAssembly().GetName().Name はコード実行中のアセンブリの名前を取得するものであることに注意。
		}

		/// <summary>
		/// デザインモードかどうかを取得します。
		/// </summary>
		/// <param name="dpObj">操作対象オブジェクト</param>
		/// <returns>true:デザインモード</returns>
		public static bool IsInDesignMode(DependencyObject dpObj)
		{
			return System.ComponentModel.DesignerProperties.GetIsInDesignMode(dpObj);
		}
	}

	public static class MyWpfImageHelper
	{
		public static WriteableBitmap CreateBitmapFromSharableFileStream(string filePath, bool freezesBitmap, BitmapCreateOptions createOptions, BitmapCacheOption cacheOption, PixelFormat? newPixelFormat = null)
		{
			using (var stream = new System.IO.FileStream(
				filePath,
				System.IO.FileMode.Open,
				System.IO.FileAccess.Read,
				System.IO.FileShare.ReadWrite | System.IO.FileShare.Delete))
			{
				// BitmapCreateOptions.None だと Pbgra32 あたりに自動変換されるらしく、
				// 元画像が 8bpp/16bpp などだとメモリ（と変換処理時間）を余計に消費することになる。
				// しかし、BitmapCreateOptions.PreservePixelFormat を使っても、Image.Source に設定する際に
				// 表示用の Pbgra32 フォーマット画像オブジェクトが別途内部で作成されるらしい？
				// 結果として、PreservePixelFormat を使うとストレージからの再ロードとビットマップ再生成の時間はキャッシュにより短くなるが、
				// その後の初回の表示の際に必ずコンバートが入るため若干重くなる模様。
				// また、元画像用のメモリ領域と同時に表示画像用のメモリ領域も確保することになるので、メモリ消費量もかえって増える。
				// WriteableBitmap を直接の表示用途ではなく、画像データ保持用途に使うのであれば問題ないが……
				// なお、BitmapCacheOption.None 以外で読み込んだ場合、その際に同時指定した BitmapCreateOptions で画像がキャッシュされ、
				// 以後は Create 時にキャッシュから再読込されるときには BitmapCreateOptions が無視されてしまうらしい？

				var decoder = BitmapDecoder.Create(
					stream,
					createOptions,
					cacheOption
				);
				BitmapSource src = decoder.Frames[0];
				System.Diagnostics.Debug.WriteLine("Original Pixel format = " + src.Format);
				if (newPixelFormat != null && src.Format != newPixelFormat)
				{
					// 強制変換。
					var fmtConvertedBmp = new FormatConvertedBitmap();
					fmtConvertedBmp.BeginInit();
					fmtConvertedBmp.Source = src;
					fmtConvertedBmp.DestinationFormat = newPixelFormat.Value;
					fmtConvertedBmp.EndInit();
					src = fmtConvertedBmp;
				}

				var outBmp = new WriteableBitmap(src);
				if (freezesBitmap)
				{
					outBmp.Freeze();
				}
				System.Diagnostics.Debug.WriteLine("Final Pixel format = " + outBmp.Format);
				return outBmp;
			}
		}
	}

	public static class MyWpfClearTypeHelper
	{
		public static void EnableClearType(Type forType)
		{
			RenderOptions.ClearTypeHintProperty.OverrideMetadata(forType,
				new FrameworkPropertyMetadata(ClearTypeHint.Enabled,
					FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.Inherits));
			TextOptions.TextFormattingModeProperty.OverrideMetadata(forType,
				new FrameworkPropertyMetadata(TextFormattingMode.Ideal,
					FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.Inherits));
			TextOptions.TextRenderingModeProperty.OverrideMetadata(forType,
				new FrameworkPropertyMetadata(TextRenderingMode.ClearType,
					FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.Inherits));
			TextOptions.TextHintingModeProperty.OverrideMetadata(forType,
				//new FrameworkPropertyMetadata(TextHintingMode.Fixed,
				new FrameworkPropertyMetadata(TextHintingMode.Animated,
					FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.Inherits));
		}
	}
}
