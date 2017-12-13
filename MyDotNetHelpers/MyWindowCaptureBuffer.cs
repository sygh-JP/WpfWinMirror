using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyMiscHelpers
{
	internal static class MyWpfGdiPlusInteropHelper
	{
		#region GDI+ に直接関連するヘルパー。System.Drawing を必要とする。アセンブリ外部に公開しない。

		/// <summary>
		/// Win32 ウィンドウのイメージを 32bit GDI+ ビットマップ（DIB）として取得する。
		/// なお、ウィンドウが最小化されていると、タイトル バーの領域しか取得できないので注意。
		/// 別プロセスのウィンドウであってもキャプチャ可能。
		/// </summary>
		/// <returns>取得できたイメージ。</returns>
		internal static System.Drawing.Bitmap CaptureWindow(IntPtr hwnd, int width, int height)
		{
			// 実際はデフォルトで System.Drawing.Imaging.PixelFormat.Format32bppArgb になる。
			var img = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
			CaptureWindow(hwnd, img, System.Windows.Int32Rect.Empty);
			return img;
		}

		internal static void CaptureWindow(IntPtr hwnd, System.Drawing.Bitmap img, System.Windows.Int32Rect clippingRect)
		{
			System.Diagnostics.Debug.Assert(img != null);
			using (var memg = System.Drawing.Graphics.FromImage(img))
			{
				memg.Clear(System.Drawing.Color.Transparent);
				IntPtr memDC = memg.GetHdc();
#if false
				// PrintWindow() は Windows XP で追加された API だが、
				// これはどうも各ウィンドウに再描画要求メッセージを送るらしく、
				// 短い時間間隔で呼び出すと Windows Calculator（電卓、calc.exe）などはちらつく。
				// Aero Glass が無効になった状態のウィンドウのスクリーンショットを撮れるので、
				// ヘルプ作成などの非リアルタイム用途であれば適している API かも。
				// たぶん Aero Auto Color も同様。
				// 最小化した状態ではタイトル バーのみがレンダリングされる。
				// http://d.hatena.ne.jp/madotate/
				User32DllMethodsInvoker.PrintWindow(hwnd, memDC, 0);
#else
				// BitBlt() を使ってウィンドウのデバイス コンテキスト経由で描画内容を DIB ビットマップに転送する。
				// 最小化した状態では PrintWindow() 同様、タイトル バーのみがレンダリングされる。
				// こちらは DWM API 同様、それなりに高速なのでリアルタイム用途に適している。
				// ただし Aero Glass の Window Chrome 領域（タイトル バー含む）がゴミデータになるときがある模様？
				// そのため、クライアント領域のみ（ただしメニューバーは含まれない）をキャプチャする。
				// DirectShow などのビデオ オーバーレイを使っているアプリでどうなるのかは試してみないと分からない。
				// キャプチャ自体は常にクライアント左上を基点、デスクトップ サイズを限界として、
				// 利用側で配置する際にそれらの情報を考慮するようにする。
				IntPtr winDC = User32DllMethodsInvoker.GetWindowDC(hwnd);
#if true
				var intersectRect = MyWin32InteropHelper.CalcClientIntersectRect(hwnd, clippingRect);
#if false
				// 切り出し元の相対位置を使う場合。
				int dstX = intersectRect.X;
				int dstY = intersectRect.Y;
#else
				// 切り出した領域を常に左上に配置する場合。
				const int dstX = 0;
				const int dstY = 0;
#endif
				// ウィンドウ クライアント領域の内容をさらにユーザー定義クリッピング矩形で切り出したものを、ビットマップに転送する。
				Gdi32DllMethodsInvoker.BitBlt(
					memDC, dstX, dstY, intersectRect.Width, intersectRect.Height,
					winDC, intersectRect.X, intersectRect.Y,
					Gdi32DllMethodsInvoker.TernaryRasterOperationType.SRCCOPY);
#else
				var winRect = MyWin32InteropHelper.GetWindowRect(hwnd);
				Gdi32DllMethodsInvoker.BitBlt(
					memDC, 0, 0, winRect.Width, winRect.Height,
					winDC, 0, 0,
				Gdi32DllMethodsInvoker.TernaryRasterOperationType.SRCCOPY);
#endif
#endif
				User32DllMethodsInvoker.ReleaseDC(hwnd, winDC);
				memg.ReleaseHdc(memDC);
			}
		}


		internal static bool CopyGdipBitmapToWicBitmap(System.Drawing.Bitmap gdipBitmap, System.Windows.Media.Imaging.WriteableBitmap wicBitmap)
		{
			System.Diagnostics.Debug.Assert(gdipBitmap != null && wicBitmap != null);
			if (wicBitmap.Format != System.Windows.Media.PixelFormats.Pbgra32)
			{
				return false;
			}
			if (gdipBitmap.PixelFormat != System.Drawing.Imaging.PixelFormat.Format32bppArgb)
			{
				return false;
			}
			wicBitmap.Lock();
			// GDI+ ビットマップから WIC ビットマップへ転送する際、ウィンドウ矩形やユーザー定義クリッピング矩形を考慮すれば、もっと効率化できる？
			// 単純にそのままごっそりブロック コピーしてしまったほうがむしろ速い？
			var gdipBitmapLockData = gdipBitmap.LockBits(
				new System.Drawing.Rectangle(0, 0, gdipBitmap.Width, gdipBitmap.Height),
				System.Drawing.Imaging.ImageLockMode.ReadOnly,
				gdipBitmap.PixelFormat);
			// GDI+ の PixelFormat と WIC の PixelFormat はメンバーの命名規則が異なるが、
			// どちらも DIB は GDI からの仕様に準じているため、BGRA の順で並んでいる。
			// GDI+ の命名は昔の Direct3D の D3DFMT_A8R8G8B8 に、WIC の命名は DXGI の DXGI_FORMAT_B8G8R8A8_UNORM に近い。
			// ともに 32bit であればパディングも考慮する必要がないので、そのまま高速にブロック コピーできる。
			Kernel32DllMethodsInvoker.CopyMemory(wicBitmap.BackBuffer, gdipBitmapLockData.Scan0,
				new IntPtr(wicBitmap.PixelHeight * wicBitmap.BackBufferStride));
			gdipBitmap.UnlockBits(gdipBitmapLockData);
			wicBitmap.AddDirtyRect(new System.Windows.Int32Rect(0, 0, wicBitmap.PixelWidth, wicBitmap.PixelHeight));
			wicBitmap.Unlock();
			return true;
		}

		#endregion

		public static System.Windows.Media.Imaging.WriteableBitmap CaptureWindow(IntPtr hwnd)
		{
			if (!User32DllMethodsInvoker.IsWindow(hwnd))
			{
				return null;
			}
			// このプロセスでの処理中に他プロセスのターゲット ウィンドウ ハンドルが無効化されることは十分にありうる。
			// 例外を投げたりしないで、各 API の戻り値を随時チェックしていくほうが無難。
			// アサーションも失敗させない。
			var winRect = new Win32Commons.RECT();
			bool retval = User32DllMethodsInvoker.GetWindowRect(hwnd, ref winRect);
			if (!retval || winRect.Width <= 0 || winRect.Height <= 0)
			{
				return null;
			}
			using (var gdipBitmap = CaptureWindow(hwnd, winRect.Width, winRect.Height))
			{
				System.Diagnostics.Debug.Assert(gdipBitmap != null);
				System.Diagnostics.Debug.Assert(gdipBitmap.PixelFormat == System.Drawing.Imaging.PixelFormat.Format32bppArgb);
				// TODO: DPI はどうする？　高 DPI 設定の場合でも論理ピクセルではなく実ピクセル単位で画像データを取得するべき。
				var wicBitmap = new System.Windows.Media.Imaging.WriteableBitmap(winRect.Width, winRect.Height,
					MyDeviceHelper.DefaultDpi, MyDeviceHelper.DefaultDpi,
					System.Windows.Media.PixelFormats.Pbgra32, null);
				CopyGdipBitmapToWicBitmap(gdipBitmap, wicBitmap);
				return wicBitmap;
			}
		}
	}

	// デスクトップの解像度が変更された場合に画像バッファを再作成することを考慮して、IDisposable 実装する。
	// HACK: DPI の変更（論理ピクセル サイズの変更）に対応する必要はあるか？
	// なお、WIC ビットマップのほうは明示的に解放する手段がないが、念のため GC を強制起動しておいたほうがいいかもしれない。
	// もういっそ MFC or WTL + Direct2D 1.1 でネイティブ実装したほうがリソース管理は楽になる気もするが、
	// WPF と比べてレイヤード ウィンドウまわりの実装が恐ろしくダルい……

	public class MyWindowCaptureBuffer : MyMiscHelpers.MyDisposableBase
	{
		System.Windows.Media.Imaging.WriteableBitmap wicBitmap;
		// GDI+.NET の Bitmap は IDisposable 実装だが、WIC の WriteableBitmap はそうではない。
		System.Drawing.Bitmap gdipBitmap;

		public System.Windows.Media.Imaging.WriteableBitmap CapturedImage { get { return this.wicBitmap; } }

		//public System.Windows.Int32Rect ClippingRect { get; set; }

		public MyWindowCaptureBuffer(int pixelWidth, int pixelHeight)
		{
			if (pixelWidth <= 0 || pixelHeight <= 0)
			{
				System.Diagnostics.Debug.Assert(false);
				throw new ArgumentException("Invalid size of image buffer!!");
			}
			this.wicBitmap = new System.Windows.Media.Imaging.WriteableBitmap(pixelWidth, pixelHeight,
				MyDeviceHelper.DefaultDpi, MyDeviceHelper.DefaultDpi,
				System.Windows.Media.PixelFormats.Pbgra32, null);
			this.gdipBitmap = new System.Drawing.Bitmap(pixelWidth, pixelHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
		}

		protected override sealed void OnDisposeManagedResources()
		{
			// マネージ リソースの解放。
			MyGenericsHelper.SafeDispose(ref this.gdipBitmap);
			this.wicBitmap = null;
		}

		protected override sealed void OnDisposeUnmamagedResources()
		{
			// 空の実装。
		}

		public bool CaptureWindow(IntPtr hwnd, System.Windows.Int32Rect clippingRect)
		{
			System.Diagnostics.Debug.Assert(this.wicBitmap != null && this.gdipBitmap != null);
			if (!User32DllMethodsInvoker.IsWindow(hwnd))
			{
				return false;
			}

			MyWpfGdiPlusInteropHelper.CaptureWindow(hwnd, this.gdipBitmap, clippingRect);

			return MyWpfGdiPlusInteropHelper.CopyGdipBitmapToWicBitmap(this.gdipBitmap, this.wicBitmap);
		}

		public void SaveImageAsPngFile(string fileName, IntPtr hwnd, System.Windows.Int32Rect clippingRect)
		{
			SaveImageAsPngFile(this.wicBitmap, fileName, hwnd, clippingRect);
		}

		private static void SaveImageAsPngFile(System.Windows.Media.Imaging.WriteableBitmap bitmap, string fileName, IntPtr hwnd, System.Windows.Int32Rect clippingRect)
		{
			if (bitmap == null)
			{
				throw new ArgumentNullException("Invalid bitmap!!");
			}

			if (!User32DllMethodsInvoker.IsWindow(hwnd))
			{
				throw new ArgumentException("Invalid window!!");
			}

			// クライアント矩形やユーザー定義クリッピング矩形で切り出した内容のみを保存する。
			var intersectRect = MyWin32InteropHelper.CalcClientIntersectRect(hwnd, clippingRect);

			System.Diagnostics.Debug.Assert(intersectRect.HasArea);

			var tempSubBitmap = new System.Windows.Media.Imaging.WriteableBitmap(intersectRect.Width, intersectRect.Height,
				MyDeviceHelper.DefaultDpi, MyDeviceHelper.DefaultDpi, System.Windows.Media.PixelFormats.Pbgra32, null);

			try
			{
				tempSubBitmap.Lock();
				bitmap.CopyPixels(new System.Windows.Int32Rect(0, 0, intersectRect.Width, intersectRect.Height),
					tempSubBitmap.BackBuffer,
					tempSubBitmap.PixelWidth * tempSubBitmap.PixelHeight * 4,
					tempSubBitmap.PixelWidth * 4);
			}
			catch (Exception)
			{
				throw;
			}
			finally
			{
				tempSubBitmap.Unlock();
			}

			using (var stream = new System.IO.FileStream(fileName,
				System.IO.FileMode.Create, System.IO.FileAccess.Write))
			{
				var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
				//encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
				encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(tempSubBitmap));
				encoder.Save(stream);
			}
			tempSubBitmap = null;
		}
	}
}
