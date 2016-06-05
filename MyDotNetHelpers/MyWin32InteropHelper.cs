using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace MyMiscHelpers
{

	internal class MyDeviceHelper
	{
		/// <summary>
		/// System.Windows.Controls.Orientation と同じ。
		/// </summary>
		public enum ScanOrientation
		{
			Horizontal,
			Vertical,
		}

		/// <summary>
		/// Windows OS の標準 DPI は 96。Mac だと 72 になる。
		/// ちなみに Win32 API のヘッダーにはこのデフォルト数値に対するシンボルは特に定義されていない模様。
		/// Direct2D のヘルパーにも 96.0f という即値が直接埋め込まれている。
		/// </summary>
		public const int DefaultDpi = 96;

		public static int PixelsPerInch(ScanOrientation orientation)
		{
			int capIndex = (orientation == ScanOrientation.Horizontal) ? Gdi32DllMethodsInvoker.LOGPIXELSX : Gdi32DllMethodsInvoker.LOGPIXELSY;
			using (var handle = Gdi32DllMethodsInvoker.CreateDC("DISPLAY"))
			{
				return (handle.IsInvalid ? DefaultDpi : Gdi32DllMethodsInvoker.GetDeviceCaps(handle, capIndex));
			}
		}
	}

	public static class MyThreadHelper
	{
		/// <summary>
		/// 現在メッセージ待ちキューの中にある全ての UI メッセージを処理します。
		/// </summary>
		public static void DoEvents()
		{
			var frame = new DispatcherFrame();
			var callback = new DispatcherOperationCallback(obj => { ((DispatcherFrame)obj).Continue = false; return null; });
			Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, callback, frame);
			Dispatcher.PushFrame(frame);
		}

		private static Action EmptyMethod = () => { };

		/// <summary>
		/// 強制的に再描画を実行する。拡張メソッドにはしない。
		/// </summary>
		/// <param name="uiElement"></param>
		public static void Refresh(System.Windows.UIElement uiElement)
		{
			uiElement.Dispatcher.Invoke(DispatcherPriority.Render, EmptyMethod);
		}
	}

	public static class MyInputHelper
	{
		public static bool GetCurrentKeyboardFocusedControlExists()
		{
			return (System.Windows.Input.Keyboard.FocusedElement as System.Windows.Controls.Control) != null;
		}
	}

	public static class MyVisualUtility
	{
		/// <summary>
		/// 指定した Visual 要素を、ビットマップにレンダリングして返す。
		/// </summary>
		/// <param name="outputImageWidth">出力画像の幅 [pixel]。ゼロ以下を指定すると、Visual 要素の ActualWidth から自動取得する。</param>
		/// <param name="outputImageHeight">出力画像の高さ [pixel]。ゼロ以下を指定すると、Visual 要素の ActualHeight から自動取得する。</param>
		/// <param name="dpiX">水平方向の解像度 DPI。ゼロ以下を指定すると、ディスプレイ設定から自動取得する。</param>
		/// <param name="dpiY">垂直方向の解像度 DPI。ゼロ以下を指定すると、ディスプレイ設定から自動取得する。</param>
		/// <param name="visualToRender"></param>
		/// <param name="undoTransformation">Visual 要素のアフィン変換を解除するか否か。</param>
		/// <returns></returns>
		public static System.Windows.Media.Imaging.BitmapSource CreateBitmapFromVisual(double outputImageWidth, double outputImageHeight, double dpiX, double dpiY, System.Windows.Media.Visual visualToRender, bool undoTransformation)
		{
			// cf.
			// http://social.msdn.microsoft.com/Forums/ja-JP/wpffaqja/thread/df0c59a1-f7c0-4591-9285-eeabc252a608

			if (visualToRender == null)
			{
				return null;
			}

			if (outputImageWidth <= 0)
			{
				outputImageWidth = (double)visualToRender.GetValue(System.Windows.FrameworkElement.ActualWidthProperty);
			}
			if (outputImageHeight <= 0)
			{
				outputImageHeight = (double)visualToRender.GetValue(System.Windows.FrameworkElement.ActualHeightProperty);
			}

			if (outputImageWidth <= 0 || outputImageHeight <= 0)
			{
				return null;
			}

			// PixelsPerInch() ヘルパー メソッドを利用して、画面の DPI 設定を知ることができます。
			// 指定された解像度のビットマップを作成する必要がある場合は、
			// 指定の dpiX 値と dpiY 値を RenderTargetBitmap コンストラクタに直接送ってください。
			double displayDpiX = (double)MyDeviceHelper.PixelsPerInch(MyDeviceHelper.ScanOrientation.Horizontal);
			double displayDpiY = (double)MyDeviceHelper.PixelsPerInch(MyDeviceHelper.ScanOrientation.Vertical);
			int roundedImgWidth = (int)Math.Ceiling(outputImageWidth);
			int roundedImgHeight = (int)Math.Ceiling(outputImageHeight);
			var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap(
				(dpiX > 0) ? (int)Math.Ceiling(dpiX / displayDpiX * outputImageWidth) : roundedImgWidth,
				(dpiY > 0) ? (int)Math.Ceiling(dpiY / displayDpiY * outputImageHeight) : roundedImgHeight,
				(dpiX > 0) ? dpiX : displayDpiX,
				(dpiY > 0) ? dpiY : displayDpiY,
				System.Windows.Media.PixelFormats.Pbgra32 // 必須。PixelFormats.Bgra32 などは使えない。
				);

			// 変換を解除するには、VisualBrush という方法を使用することができます。
			if (undoTransformation)
			{
				var dv = new System.Windows.Media.DrawingVisual();
				using (var dc = dv.RenderOpen())
				{
					var vb = new System.Windows.Media.VisualBrush(visualToRender);
					dc.DrawRectangle(vb, null, new System.Windows.Rect(new System.Windows.Point(), new System.Windows.Size(outputImageWidth, outputImageHeight)));
				}
				bmp.Render(dv);
			}
			else
			{
				bmp.Render(visualToRender);
			}

			return bmp;
		}
	}

	public interface IWin32ModalDialogImpl
	{
		/// <summary>
		/// Win32 / MFC 相互運用のためのモーダル ダイアログ表示用ラッパー メソッド。
		/// </summary>
		/// <param name="ownerHwnd">オーナー ウィンドウのハンドル（HWND）。</param>
		/// <returns> Window.ShowDialog() の戻り値。</returns>
		bool? ShowModalDialog(IntPtr ownerHwnd);
	}

	/// <summary>
	/// 動的なカルチャ変更を実装するオブジェクトを表すインターフェイス。
	/// </summary>
	public interface IDynamicCultureChangeable
	{
		/// <summary>
		/// カルチャを変更する。
		/// </summary>
		/// <param name="newCulture">新しいカルチャ。</param>
		void ChangeCulture(System.Globalization.CultureInfo newCulture);
	}


	/// <summary>
	/// Win32 API の P/Invoke や GDI/GDI+ 連携の WIC ヘルパーなどをラップするクラス。
	/// </summary>
	public static class MyWin32InteropHelper
	{

		public static bool HasWindowStyleMaximize(IntPtr hwnd)
		{
			return (User32DllMethodsInvoker.GetWindowStyle(hwnd) & User32DllMethodsInvoker.WindowStyles.WS_MAXIMIZE) != 0;
		}

		public static bool HasWindowStyleExTransparent(IntPtr hwnd)
		{
			// 本来は 32bit/64bit 版兼用の GetWindowLongPtr() / SetWindowLongPtr() を使うべきだが、
			// ウィンドウ スタイル系フラグは現状 32bit 範囲分しか使われていないので問題ないらしい。
			// おそらく GWL_USERDATA でポインタ値をユーザーデータとして設定／取得する際に問題になるだけ。
			// また、32bit 版ではもともと GetWindowLongPtr() / SetWindowLongPtr() はそれぞれ
			// GetWindowLong() / SetWindowLong() にマクロで置換される仕組みで、
			// DLL に元の名前の関数エントリーポイントが存在しないはず。
			// どうしても P/Invoke で GetWindowLongPtr() / SetWindowLongPtr() を使いたい場合、
			// 実行時に IntPtr のサイズを見て分岐する必要がある。

			int extendedStyle = User32DllMethodsInvoker.GetWindowLong(hwnd, (int)User32DllMethodsInvoker.IndexOfGetWindowLong.GWL_EXSTYLE);
			return (extendedStyle & (int)User32DllMethodsInvoker.ExWindowStyles.WS_EX_TRANSPARENT) != 0;
		}

		public static void SetWindowStyleExTransparent(IntPtr hwnd, bool isTransparent = true)
		{
			int extendedStyle = User32DllMethodsInvoker.GetWindowLong(hwnd, (int)User32DllMethodsInvoker.IndexOfGetWindowLong.GWL_EXSTYLE);
			if (isTransparent)
			{
				// Change the extended window style to include WS_EX_TRANSPARENT
				User32DllMethodsInvoker.SetWindowLong(hwnd, (int)User32DllMethodsInvoker.IndexOfGetWindowLong.GWL_EXSTYLE,
					extendedStyle | (int)User32DllMethodsInvoker.ExWindowStyles.WS_EX_TRANSPARENT);
			}
			else
			{
				// Change the extended window style to exclude WS_EX_TRANSPARENT
				User32DllMethodsInvoker.SetWindowLong(hwnd, (int)User32DllMethodsInvoker.IndexOfGetWindowLong.GWL_EXSTYLE,
					extendedStyle & (~(int)User32DllMethodsInvoker.ExWindowStyles.WS_EX_TRANSPARENT));
			}
		}

		public static void SetWindowStyleExCompsited(IntPtr hwnd, bool isComposited = true)
		{
			int extendedStyle = User32DllMethodsInvoker.GetWindowLong(hwnd, (int)User32DllMethodsInvoker.IndexOfGetWindowLong.GWL_EXSTYLE);
			if (isComposited)
			{
				// Change the extended window style to include WS_EX_COMPOSITED
				User32DllMethodsInvoker.SetWindowLong(hwnd, (int)User32DllMethodsInvoker.IndexOfGetWindowLong.GWL_EXSTYLE,
					extendedStyle | (int)User32DllMethodsInvoker.ExWindowStyles.WS_EX_COMPOSITED);
			}
			else
			{
				// Change the extended window style to exclude WS_EX_COMPOSITED
				User32DllMethodsInvoker.SetWindowLong(hwnd, (int)User32DllMethodsInvoker.IndexOfGetWindowLong.GWL_EXSTYLE,
					extendedStyle & (~(int)User32DllMethodsInvoker.ExWindowStyles.WS_EX_COMPOSITED));
			}
		}

		public static bool SetWindowTransparency(IntPtr hwnd, byte opacity)
		{
			// Change the extended window style to include WS_EX_LAYERED
			int extendedStyle = User32DllMethodsInvoker.GetWindowLong(hwnd, (int)User32DllMethodsInvoker.IndexOfGetWindowLong.GWL_EXSTYLE);
			User32DllMethodsInvoker.SetWindowLong(hwnd, (int)User32DllMethodsInvoker.IndexOfGetWindowLong.GWL_EXSTYLE,
				extendedStyle | (int)User32DllMethodsInvoker.ExWindowStyles.WS_EX_LAYERED);
			return User32DllMethodsInvoker.SetLayeredWindowAttributes(hwnd, 0, opacity, User32DllMethodsInvoker.LayeredWindowAttributes.LWA_ALPHA);
		}


		public static void DisableMaximizeButton(IntPtr hwnd)
		{
			var winStyle = User32DllMethodsInvoker.GetWindowStyle(hwnd);
			User32DllMethodsInvoker.SetWindowStyle(hwnd,
				winStyle & (~User32DllMethodsInvoker.WindowStyles.WS_MAXIMIZEBOX));
		}

		public static void DisableMinimizeButton(IntPtr hwnd)
		{
			var winStyle = User32DllMethodsInvoker.GetWindowStyle(hwnd);
			User32DllMethodsInvoker.SetWindowStyle(hwnd,
				winStyle & (~User32DllMethodsInvoker.WindowStyles.WS_MINIMIZEBOX));
		}

		public static void DisableWindowResizing(IntPtr hwnd)
		{
			var winStyle = User32DllMethodsInvoker.GetWindowStyle(hwnd);
			User32DllMethodsInvoker.SetWindowStyle(hwnd,
				winStyle & (~User32DllMethodsInvoker.WindowStyles.WS_THICKFRAME));
		}

		public static void RemoveSystemMenuLast2Items(IntPtr hwnd)
		{
			IntPtr hMenu = User32DllMethodsInvoker.GetSystemMenu(hwnd, false);
			if (hMenu != IntPtr.Zero)
			{
				int num = User32DllMethodsInvoker.GetMenuItemCount(hMenu);
				if (num >= 2)
				{
					// 末尾に Close コマンドがあるという前提。
					// MSDN では MF_BYCOMMAND よりも MF_BYPOSITION の使用を推奨しているようだが……
					var removeFlags = User32DllMethodsInvoker.MenuFlags.MF_BYPOSITION | User32DllMethodsInvoker.MenuFlags.MF_REMOVE;
					User32DllMethodsInvoker.RemoveMenu(hMenu, (uint)(num - 1), removeFlags); // Remove 'Close'
					User32DllMethodsInvoker.RemoveMenu(hMenu, (uint)(num - 2), removeFlags); // Remove a separator
					//User32DllMethodsInvoker.DrawMenuBar(hwnd);
				}
			}
		}

		#region Win32/Win64 Compatible
		public static IntPtr GetWindowLongPtr(IntPtr hWnd, Int32 index)
		{
			if (System.Runtime.InteropServices.Marshal.SizeOf(typeof(IntPtr)) == sizeof(int))
			{
				// Win32
				return new IntPtr(User32DllMethodsInvoker.GetWindowLong(hWnd, index));
			}
			else
			{
				// Win64
				return User32DllMethodsInvoker.GetWindowLongPtr(hWnd, index);
			}
		}

		public static IntPtr SetWindowLongPtr(IntPtr hWnd, Int32 index, IntPtr newStyle)
		{
			if (System.Runtime.InteropServices.Marshal.SizeOf(typeof(IntPtr)) == sizeof(int))
			{
				// Win32
				return new IntPtr(User32DllMethodsInvoker.SetWindowLong(hWnd, index, newStyle.ToInt32()));
			}
			else
			{
				// Win64
				return User32DllMethodsInvoker.SetWindowLongPtr(hWnd, index, newStyle);
			}
		}
		#endregion

#if false
		// C/C++ の memcpy() 相当を実装する際、kernel32.dll の CopyMemory() Win32 API に依存したくなければこちら。
		// Xbox 360 / Xbox One や Windows ストア アプリではこちらを使うことになると思われる。
		// どのみちポインタを使う必要があるので、素直に for ループを回して1バイトずつコピーしてもよい気がするが……
		// CopyTo() メソッドはそのあたり最適化されている？
		internal static unsafe void CopyMemory(IntPtr dst, IntPtr src, int size)
		{
			using (var streamSrc = new System.IO.UnmanagedMemoryStream((byte*)src, size))
			{
				using (var streamDst = new System.IO.UnmanagedMemoryStream((byte*)dst, size))
				{
					streamSrc.CopyTo(streamDst);
				}
			}
		}
#endif

		/// <summary>
		/// 2つの矩形の共通部分（積）を返す。共通部分がない場合は System.Windows.Int32Rect.Empty を返す。
		/// </summary>
		/// <param name="r1"></param>
		/// <param name="r2"></param>
		/// <returns></returns>
		public static System.Windows.Int32Rect CreateRectIntersect(System.Windows.Int32Rect r1, System.Windows.Int32Rect r2)
		{
			// System.Drawing.Rectangle.Intersect()、
			// System.Drawing.RectangleF.Intersect()、
			// System.Windows.Rect.Intersect() および
			// Windows.Foundation.Rect.Intersect() 同様の実装になっているはず。
			// 面倒なので拡張メソッド実装にはしない。
			// GDI+、WPF、Win ストア アプリで共通して使えるデータ型（＋豊富なユーティリティ メソッド）が標準定義されていると楽なのだが……
			int left = Math.Max(r1.X, r2.X);
			int top = Math.Max(r1.Y, r2.Y);
			int right = Math.Min(r1.X + r1.Width, r2.X + r2.Width);
			int bottom = Math.Min(r1.Y + r1.Height, r2.Y + r2.Height);
			int width = right - left;
			int height = bottom - top;
			if (width <= 0 || height <= 0)
			{
				return System.Windows.Int32Rect.Empty;
			}
			return new System.Windows.Int32Rect(left, top, width, height);
		}

		/// <summary>
		/// 2つの矩形の境界矩形（和）を返す。
		/// </summary>
		/// <param name="r1"></param>
		/// <param name="r2"></param>
		/// <returns></returns>
		public static System.Windows.Int32Rect CreateRectUnion(System.Windows.Int32Rect r1, System.Windows.Int32Rect r2)
		{
			// System.Drawing.Rectangle.Union()、
			// System.Drawing.RectangleF.Union()、
			// System.Windows.Rect.Union() および
			// Windows.Foundation.Rect.Union() 同様の実装になっているはず。
			int left = Math.Min(r1.X, r2.X);
			int top = Math.Min(r1.Y, r2.Y);
			int right = Math.Max(r1.X + r1.Width, r2.X + r2.Width);
			int bottom = Math.Max(r1.Y + r1.Height, r2.Y + r2.Height);
			int width = right - left;
			int height = bottom - top;
			System.Diagnostics.Debug.Assert(width >= 0 && height >= 0);
			return new System.Windows.Int32Rect(left, top, width, height);
		}


		public static System.Windows.Int32Rect CalcClientIntersectRect(IntPtr hwnd, System.Windows.Int32Rect clippingRect)
		{
			// デスクトップに対するウィンドウのスクリーン座標を取得しておく。
			var winRect = GetWindowRect(hwnd);
			// クライアント領域のサイズを取得する。
			var clientRect = GetClientRect(hwnd); // (left, top) は常に (0, 0) が返る。
			var clientOriginPos = new Win32Commons.POINT();
			// ウィンドウのクライアント左上をスクリーン座標に直す。
			User32DllMethodsInvoker.ClientToScreen(hwnd, ref clientOriginPos);
			// ウィンドウから見たクライアント領域の相対矩形を計算。
			var targetRect = new System.Windows.Int32Rect(
				clientOriginPos.x - winRect.X,
				clientOriginPos.y - winRect.Y,
				clientRect.Width, clientRect.Height);
			clippingRect.X += targetRect.X;
			clippingRect.Y += targetRect.Y;
			// ユーザー定義クリッピング矩形も考慮する。
			var intersectRect = CreateRectIntersect(targetRect, clippingRect);
			if (!intersectRect.HasArea)
			{
				// 交差しない場合、元のクライアント領域の矩形を使う。
				intersectRect = targetRect;
			}
			return intersectRect;
		}

		#region GDI+ に直接関連するヘルパー。アセンブリ外部に公開しない。

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
				var intersectRect = CalcClientIntersectRect(hwnd, clippingRect);
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
				var winRect = GetWindowRect(hwnd);
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


		public static bool HasNoOwnerWindow(IntPtr hwnd)
		{
			return User32DllMethodsInvoker.GetWindow(hwnd, User32DllMethodsInvoker.CommandOfGetWindow.GW_OWNER) == IntPtr.Zero;
		}

		public static string GetWindowText(IntPtr hwnd)
		{
			if (!User32DllMethodsInvoker.IsWindow(hwnd))
			{
				return null;
			}
			int winTextLength = User32DllMethodsInvoker.GetWindowTextLength(hwnd);
			if (winTextLength > 0)
			{
				var sb = new StringBuilder(winTextLength + 1); // 終端 null 文字の分。
				User32DllMethodsInvoker.GetWindowText(hwnd, sb, sb.Capacity);
				return sb.ToString();
			}
			else
			{
				return String.Empty;
			}
		}

		public static IntPtr GetDesktopWindow()
		{
			return User32DllMethodsInvoker.GetDesktopWindow();
		}

		public static System.Windows.Int32Rect GetWindowRect(IntPtr hwnd)
		{
			var tempRect = new Win32Commons.RECT();
			User32DllMethodsInvoker.GetWindowRect(hwnd, ref tempRect);
			return tempRect.ToInt32Rect();
		}

		public static System.Windows.Int32Rect GetClientRect(IntPtr hwnd)
		{
			var tempRect = new Win32Commons.RECT();
			User32DllMethodsInvoker.GetClientRect(hwnd, ref tempRect);
			return tempRect.ToInt32Rect();
		}

		public static System.Windows.Int32Rect GetWorkAreaRect()
		{
			var tempRect = new Win32Commons.RECT();
			User32DllMethodsInvoker.SystemParametersInfo(User32DllMethodsInvoker.SystemParametersInfoType.SPI_GETWORKAREA, 0, ref tempRect, 0);
			return new System.Windows.Int32Rect(tempRect.left, tempRect.top, tempRect.Width, tempRect.Height);
		}

		public static List<IntPtr> EnumVisibleWindows()
		{
#if true
			// デスクトップの矩形はプライマリ モニターの矩形？
			System.Diagnostics.Debug.WriteLine("Desktop Info:");
			IntPtr hDesktopWnd = User32DllMethodsInvoker.GetDesktopWindow();
			// HWND 型自体はポインタ型で、64bit ネイティブ プログラムでは 8 バイトだが、Win64 では実質下位 4 バイト分しか使われないらしい
			// （でないと Win32 アプリと x64 アプリとで HWND の値が異なる結果になってしまい、HWND 経由でプロセス間通信できない）。
			System.Diagnostics.Debug.WriteLine("hWnd = 0x{0}, Title = \"{1}\"", hDesktopWnd.ToString("X8"), GetWindowText(hDesktopWnd));
			var desktopRect = new Win32Commons.RECT();
			User32DllMethodsInvoker.GetWindowRect(hDesktopWnd, ref desktopRect);
			System.Diagnostics.Debug.WriteLine("DesktopRect = ({0}, {1}, {2}, {3})",
				desktopRect.left, desktopRect.top, desktopRect.right, desktopRect.bottom);
#endif

			var winHandleList = new List<IntPtr>();
			// タスク マネージャーの [アプリケーション] タブに表示されるようなトップレベル ウィンドウを列挙する。
			// 自分自身も一応列挙に含める。呼び出し側でフィルタリングすればよい。
			// "スタート" を列挙したくない場合、「オーナーウィンドウを持たない」という条件でフィルタリングできる。
			// "Program Manager"（≒デスクトップ）も列挙されるのを何とかできないか？
			// GetWindowText() の結果を文字列比較するだけでは不十分。ちなみに GetDesktopWindow() で得られるデスクトップ ハンドルとは異なるウィンドウ。
			User32DllMethodsInvoker.EnumWindows(new User32DllMethodsInvoker.EnumWindowsProcDelegate((hWnd, lParam) =>
				{
					if (User32DllMethodsInvoker.IsWindowVisible(hWnd) && HasNoOwnerWindow(hWnd))
					{
						string title = GetWindowText(hWnd);
						if (title != String.Empty)
						{
#if false
							// {0:X8} ではゼロ パディングにならない。
							System.Diagnostics.Debug.WriteLine("hWnd = 0x{0}, Title = \"{1}\"", hWnd.ToString("X8"), title);

							var winPlacement = new User32DllMethodsInvoker.WINDOWPLACEMENT();
							winPlacement.InitializeLength();
							User32DllMethodsInvoker.GetWindowPlacement(hWnd, ref winPlacement);
							System.Diagnostics.Debug.WriteLine("({0}, {1}), ({2}, {3})",
								winPlacement.ptMinPosition.x,
								winPlacement.ptMinPosition.y,
								winPlacement.ptMaxPosition.x,
								winPlacement.ptMaxPosition.y);
#endif
							winHandleList.Add(hWnd);
						}
					}
					return true; // 列挙を続行。
				}),
				IntPtr.Zero);
			return winHandleList;
		}

		public static void WakeupProcesses(string procName)
		{
			var targetProcesses = System.Diagnostics.Process.GetProcessesByName(procName);
			if (targetProcesses != null)
			{
				foreach (var proc in targetProcesses)
				{
					WakeupWindow(proc.MainWindowHandle);
				}
			}
		}

		/// <summary>
		/// 特定のウィンドウを強制的に最前面に表示する。
		/// </summary>
		/// <param name="hwnd">
		/// 通常は Process.MainWindowHandle を渡せばよいが、タスク バーに表示されてないウィンドウのハンドルは取得できないので注意。
		/// タスクバーに表示されていないトップレベルのウィンドウも取得したい場合、Win32 EnumWindows() API などを使うしかないらしい。
		/// </param>
		public static void WakeupWindow(IntPtr hwnd)
		{
			// ウィンドウが最小化されていれば元に戻す。
			if (User32DllMethodsInvoker.IsIconic(hwnd))
			{
				User32DllMethodsInvoker.ShowWindowAsync(hwnd, User32DllMethodsInvoker.CommandOfShowWindow.SW_RESTORE);
			}

			// ウィンドウを最前面に表示する。
			User32DllMethodsInvoker.SetForegroundWindow(hwnd);
		}
	}


	public class MyCustomWinProc
	{
		public event Action PreMinimized;
		//public event Action PreMaximized;

		System.Windows.Interop.HwndSource source;
		System.Windows.Interop.HwndSourceHook hook;

		public int MinWindowWidth { get; set; }
		public int MinWindowHeight { get; set; }

		public void AttachCustomWndProc(IntPtr hwnd)
		{
			this.source = System.Windows.Interop.HwndSource.FromHwnd(hwnd);
			this.hook = this.MyWndProc;
			this.source.AddHook(this.hook);
		}

		public void DetachCustomWndProc()
		{
			System.Diagnostics.Debug.Assert(this.source != null && this.hook != null);
			this.source.RemoveHook(this.hook);
			this.source = null;
			this.hook = null;
		}

		private IntPtr MyWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
		{
			if (msg == (int)Win32Commons.Win32Message.WM_GETMINMAXINFO)
			{
				var result = this.OnGetMinMaxInfo(hwnd, wParam, lParam);
				if (result != null)
				{
					handled = true;
					return result.Value;
				}
			}

			if (msg == (int)Win32Commons.Win32Message.WM_WINDOWPOSCHANGING)
			{
				var param = (Win32Commons.WINDOWPOS)System.Runtime.InteropServices.Marshal.PtrToStructure(lParam, typeof(Win32Commons.WINDOWPOS));
				// 最小化するときに Windows によってこの位置に移動されるらしい？　システム設定に依らない固定値？
				// 最小化されているアプリの画面内容をキャプチャすることは不可能。
				if (param.x == -32000 && param.y == -32000)
				{
					System.Diagnostics.Debug.WriteLine("Special position for Minimized window.");
					if (this.PreMinimized != null)
					{
						this.PreMinimized();
					}
				}
#if false
				// 最大化するときに Windows によってこの位置に移動されるらしい？　システム設定に依らない固定値？
				// 実際に最大化されているアプリの画面内容を BitBlt でキャプチャしてみると、端に余白があることが分かる。
				// ただし、WM_GETMINMAXINFO で設定した値によって変化しうるらしい。つまりアプリ固有値。
				if (param.x == -7 && param.y == -7)
				{
					System.Diagnostics.Debug.WriteLine("Special position for Maximized window.");
					if (this.PreMaximized != null)
					{
						this.PreMaximized();
					}
				}
#endif
				// http://stackoverflow.com/questions/926758/window-statechanging-event-in-wpf
				// http://blogs.msdn.com/b/oldnewthing/archive/2004/10/28/249044.aspx
			}

			// process minimize button
			if (msg == (int)Win32Commons.Win32Message.WM_SYSCOMMAND)
			{
				switch ((Win32Commons.SystemCommandType)wParam.ToInt32())
				{
					case Win32Commons.SystemCommandType.SC_MINIMIZE:
						// タスク バーのクリックやシステム コマンドの [最小化] はここで事前フックできるが、
						// Aero Shake / Aero Preview による最小化はフックできない。
						// WPF の Window.WindowState を操作した場合も同様。
						System.Diagnostics.Debug.WriteLine("SystemCommand.Minimize executed.");
						break;
					case Win32Commons.SystemCommandType.SC_MAXIMIZE:
						// システム コマンドの [最大化] はここで事前フックできるが、
						// タイトル バーのダブルクリックや Aero Snap による最大化はフックできない。
						// WPF の Window.WindowState を操作した場合も同様。
						System.Diagnostics.Debug.WriteLine("SystemCommand.Maximize executed.");
						break;
					case Win32Commons.SystemCommandType.SC_RESTORE:
						// タスク バーのクリックやシステム コマンドの [元のサイズに戻す] はここで事前フックできるが、
						// タイトル バーのダブルクリックや Aero Snap / Aero Preview による復元はフックできない。
						// WPF の Window.WindowState を操作した場合も同様。
						System.Diagnostics.Debug.WriteLine("SystemCommand.Restore executed.");
						break;
					default:
						break;
				}
			}

			handled = false;
			return IntPtr.Zero;
		}

		private IntPtr? OnGetMinMaxInfo(IntPtr hwnd, IntPtr wParam, IntPtr lParam)
		{
			// WPF で WindowStyle="None" を指定したウィンドウを最大化した際にタスク バー領域を覆ってしまうのを防止する。
			// アプリ起動中にタスク バーの表示位置が変更された場合にも対応できる。
			// http://karamemo.hateblo.jp/entry/20130527/1369658222
			// ディスプレイ解像度も同様にして、システム パラメーターの変更イベント（Win32 メッセージ）をフックすることで対応できるか？
			var monitor = User32DllMethodsInvoker.MonitorFromWindow(hwnd, User32DllMethodsInvoker.MonitorFlagType.MONITOR_DEFAULTTONEAREST);
			if (monitor == IntPtr.Zero)
			{
				return null;
			}
			var monitorInfo = new User32DllMethodsInvoker.MONITORINFO();
			monitorInfo.InitializeSize();
			if (!User32DllMethodsInvoker.GetMonitorInfo(monitor, ref monitorInfo))
			{
				return null;
			}
			var workingRectangle = monitorInfo.rcWork;
			var monitorRectangle = monitorInfo.rcMonitor;
			var minmax = (User32DllMethodsInvoker.MINMAXINFO)System.Runtime.InteropServices.Marshal.PtrToStructure(lParam, typeof(User32DllMethodsInvoker.MINMAXINFO));
			minmax.ptMaxPosition.x = Math.Abs(workingRectangle.left - monitorRectangle.left);
			minmax.ptMaxPosition.y = Math.Abs(workingRectangle.top - monitorRectangle.top);
			minmax.ptMaxSize.x = workingRectangle.Width;
			minmax.ptMaxSize.y = workingRectangle.Height;
			// WPF の Window.MinWidth や Window.MinHeight にはそれぞれ限界があるが、
			// ここで MINMAXINFO.ptMinTrackSize にゼロを設定するとさらに小さいウィンドウにすることができてしまう。
			minmax.ptMinTrackSize.x = this.MinWindowWidth;
			minmax.ptMinTrackSize.y = this.MinWindowHeight;
			//minmax.ptMaxTrackSize = minmax.ptMaxSize; // いまいち用途不明。
			// マネージ コードによる変更内容をアンマネージ側に書き戻し（置き換え）。
			System.Runtime.InteropServices.Marshal.StructureToPtr(minmax, lParam, true);
			return IntPtr.Zero;
		}
	}


	// デスクトップの解像度が変更された場合に画像バッファを再作成することを考慮して、IDisposable 実装する。
	// HACK: DPI の変更（論理ピクセル サイズの変更）に対応する必要はあるか？
	// なお、WIC ビットマップのほうは明示的に解放する手段がないが、念のため GC を強制起動しておいたほうがいいかもしれない。
	// もういっそ MFC or WTL + Direct2D 1.1 でネイティブ実装したほうがリソース管理は楽になる気もするが、
	// レイヤード ウィンドウまわりの実装が恐ろしくダルい……

	public class MyWindowCaptureBuffer : IDisposable
	{
		System.Windows.Media.Imaging.WriteableBitmap wicBitmap;
		// GDI+.NET の Bitmap は IDisposable 実装だが、WIC の WriteableBitmap はそうではない。
		System.Drawing.Bitmap gdipBitmap;

		bool isDisposed = false;

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

		~MyWindowCaptureBuffer()
		{
			// リソースの解放。
			this.OnDispose(false);
		}

		protected virtual void OnDispose(bool disposesManagedResources)
		{
			// よくある IDisposable 実装のサンプルからパクってきたメソッド実装コードだが、
			// protected virtual な仮想メソッドを定義しているのは、
			// 派生クラスでもリソースを追加管理するようなときに備えるためらしい。
			// 派生クラスでオーバーライドする際には、基底クラスの OnDispose(bool) をきちんと呼び出すようにすればよい。

			lock (this)
			{
				if (this.isDisposed)
				{
					// 既に呼びだし済みであるならば何もしない。
					return;
				}

				if (disposesManagedResources)
				{
					// マネージ リソースの解放。
					MyGenericsHelper.SafeDispose(ref this.gdipBitmap);
					this.wicBitmap = null;
				}

				// TODO: IntPtr 経由などのアンマネージ リソースの解放はココで行なう。

				this.isDisposed = true;
			}
		}

		protected void ThrowExceptionIfDisposed()
		{
			if (this.isDisposed)
			{
				throw new ObjectDisposedException(this.GetType().ToString());
			}
		}

		/// <summary>
		/// IDisposable.Dispose() の実装。
		/// </summary>
		public void Dispose()
		{
			// リソースの解放。
			this.OnDispose(true);

			// このオブジェクトのデストラクタを GC 対象外とする。
			GC.SuppressFinalize(this);
		}

		public bool CaptureWindow(IntPtr hwnd, System.Windows.Int32Rect clippingRect)
		{
			System.Diagnostics.Debug.Assert(this.wicBitmap != null && this.gdipBitmap != null);
			if (!User32DllMethodsInvoker.IsWindow(hwnd))
			{
				return false;
			}

			MyWin32InteropHelper.CaptureWindow(hwnd, this.gdipBitmap, clippingRect);

			return MyWin32InteropHelper.CopyGdipBitmapToWicBitmap(this.gdipBitmap, this.wicBitmap);
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
