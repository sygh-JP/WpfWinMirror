using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

// unsafe キーワードを使ってポインタ経由でメモリーを直接操作するメソッドを定義する場合、アプリケーション アセンブリとは切り離す。

namespace MyMiscHelpers
{
	#region P/Invoke

	// 特定の Win32 DLL に属しているわけではないが、kernel32.dll, user32.dll, gdi32.dll で共通して使われる型や定数を定義する。
	namespace Win32Commons
	{
		[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
		internal struct RECT
		{
			public Int32 left;
			public Int32 top;
			public Int32 right;
			public Int32 bottom;

			public int Width { get { return this.right - this.left; } }
			public int Height { get { return this.bottom - this.top; } }

			public System.Windows.Int32Rect ToInt32Rect()
			{
				return new System.Windows.Int32Rect(this.left, this.top, this.Width, this.Height);
			}

			/// <summary>
			/// MFC の CRect::NormalizeRect() 同様。
			/// </summary>
			public void Normalize()
			{
				if (this.right < this.left)
				{
					MyGenericsHelper.Swap(ref this.right, ref this.left);
				}
				if (this.bottom < this.top)
				{
					MyGenericsHelper.Swap(ref this.bottom, ref this.top);
				}
			}
		}

		[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
		internal struct POINT
		{
			public Int32 x;
			public Int32 y;
		}

		[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
		internal struct SIZE
		{
			public Int32 cx;
			public Int32 cy;
		}


		[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
		internal struct WINDOWPOS
		{
			public IntPtr hwnd;
			public IntPtr hwndInsertAfter;
			public int x;
			public int y;
			public int cx;
			public int cy;
			public uint flags;
		}

		internal enum Win32Message : int
		{
			WM_SIZE = 0x0005,
			WM_GETMINMAXINFO = 0x0024,
			WM_WINDOWPOSCHANGING = 0x0046,
			WM_SYSCOMMAND = 0x0112,
			WM_SIZING = 0x0214,
		}

		internal enum SystemCommandType : int
		{
			SC_MINIMIZE = 0xF020,
			SC_MAXIMIZE = 0xF030,
			SC_CLOSE = 0xF060,
			SC_RESTORE = 0xF120,
		}
	}


	/// <summary>
	/// デバイス コンテキストのハンドル HDC を IDisposable としてラップするクラス。確実な解放を保証する。
	/// HDC は 0 および -1 が無効値。
	/// ちなみに Win32 ファイル ハンドルは 0 と -1 が無効値で、HWND は -1 が無効値。
	/// ただし IsWindow(0) もエラーになる。
	/// </summary>
	internal sealed class SafeHDC : Microsoft.Win32.SafeHandles.SafeHandleZeroOrMinusOneIsInvalid
	{
		private SafeHDC() : base(true) { }

		protected override bool ReleaseHandle()
		{
			bool retVal = Gdi32DllMethodsInvoker.DeleteDC(base.handle);
			//base.SetHandleAsInvalid(); // SetHandleAsInvalid() は handle に無効値を入れてくれるわけではないらしい。
			base.handle = IntPtr.Zero;
			return retVal;
		}
	}

	// CreateDC() に対応するのは DeleteDC()。
	// GetDC() や GetWindowDC() に対応するのは ReleaseDC()。
	// それぞれ実装されている DLL が異なる。

	/// <summary>
	/// 定数は WinGDI.h より抜粋。
	/// </summary>
	[System.Security.SuppressUnmanagedCodeSecurity]
	internal static class Gdi32DllMethodsInvoker
	{
		public const Int32 LOGPIXELSX = 88;
		public const Int32 LOGPIXELSY = 90;

		[System.Runtime.InteropServices.DllImport("gdi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, ExactSpelling = true)]
		public static extern Boolean DeleteDC(IntPtr hDC);

		[System.Runtime.InteropServices.DllImport("gdi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, ExactSpelling = true)]
		public static extern Int32 GetDeviceCaps(IntPtr hDC, Int32 nIndex);

		[System.Runtime.InteropServices.DllImport("gdi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, ExactSpelling = true)]
		public static extern Int32 GetDeviceCaps(SafeHDC hDC, Int32 nIndex);

		[System.Runtime.InteropServices.DllImport("gdi32.dll", EntryPoint = "CreateDC", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
		public static extern SafeHDC CreateDC(String lpszDriver, String lpszDeviceName, String lpszOutput, IntPtr devMode);

		public static SafeHDC CreateDC(String lpszDriver)
		{
			return Gdi32DllMethodsInvoker.CreateDC(lpszDriver, null, null, IntPtr.Zero);
		}

		public enum TernaryRasterOperationType : uint
		{
			SRCCOPY = 0x00CC0020,
			SRCPAINT = 0x00EE0086,
			SRCAND = 0x008800C6,
			SRCINVERT = 0x00660046,
			SRCERASE = 0x00440328,
			NOTSRCCOPY = 0x00330008,
			NOTSRCERASE = 0x001100A6,
			MERGECOPY = 0x00C000CA,
			MERGEPAINT = 0x00BB0226,
			PATCOPY = 0x00F00021,
			PATPAINT = 0x00FB0A09,
			PATINVERT = 0x005A0049,
			DSTINVERT = 0x00550009,
			BLACKNESS = 0x00000042,
			WHITENESS = 0x00FF0062,
			CAPTUREBLT = 0x40000000,
		}

		[System.Runtime.InteropServices.DllImport("gdi32.dll")]
		[return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
		public static extern bool BitBlt(IntPtr hdc, int nXDest, int nYDest, int nWidth, int nHeight,
			IntPtr hdcSrc, int nXSrc, int nYSrc, TernaryRasterOperationType dwRop);
	}

	/// <summary>
	/// 定数は WinUser.h より抜粋。
	/// </summary>
	internal static class User32DllMethodsInvoker
	{
		public enum IndexOfGetWindowLong : int
		{
			GWL_STYLE = (-16),
			GWL_EXSTYLE = (-20),
			GWL_USERDATA = (-21),
		}

		[Flags]
		public enum WindowStyles : uint
		{
			WS_OVERLAPPED = 0x00000000,
			WS_POPUP = 0x80000000,
			WS_CHILD = 0x40000000,
			WS_MINIMIZE = 0x20000000,
			WS_VISIBLE = 0x10000000,
			WS_DISABLED = 0x08000000,
			WS_CLIPSIBLINGS = 0x04000000,
			WS_CLIPCHILDREN = 0x02000000,
			WS_MAXIMIZE = 0x01000000,
			WS_BORDER = 0x00800000,
			WS_DLGFRAME = 0x00400000,
			WS_VSCROLL = 0x00200000,
			WS_HSCROLL = 0x00100000,
			WS_SYSMENU = 0x00080000,
			WS_THICKFRAME = 0x00040000,
			WS_GROUP = 0x00020000,
			WS_TABSTOP = 0x00010000,

			WS_MINIMIZEBOX = 0x00020000,
			WS_MAXIMIZEBOX = 0x00010000,

			WS_CAPTION = WS_BORDER | WS_DLGFRAME,
			WS_TILED = WS_OVERLAPPED,
			WS_ICONIC = WS_MINIMIZE,
			WS_SIZEBOX = WS_THICKFRAME,
			WS_TILEDWINDOW = WS_OVERLAPPEDWINDOW,

			WS_OVERLAPPEDWINDOW = WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX,
			WS_POPUPWINDOW = WS_POPUP | WS_BORDER | WS_SYSMENU,
			WS_CHILDWINDOW = WS_CHILD,
		}

		[Flags]
		public enum ExWindowStyles : int
		{
			WS_EX_TRANSPARENT = 0x00000020,
			WS_EX_LAYERED = 0x00080000,
			WS_EX_COMPOSITED = 0x02000000,
		}

		[Flags]
		public enum LayeredWindowAttributes : uint
		{
			LWA_COLORKEY = 0x00000001,
			LWA_ALPHA = 0x00000002,
		}

		[Flags]
		public enum MenuFlags : uint
		{
			MF_BYCOMMAND = 0x000,
			MF_BYPOSITION = 0x400,
			MF_ENABLED = 0x00,
			MF_GRAYED = 0x01,
			MF_DISABLED = 0x02,
			MF_REMOVE = 0x1000,
		}

		public enum SystemParametersInfoType : uint
		{
			SPI_GETWORKAREA = 0x0030,
		}

		/// <summary>
		/// ShowWindow(), ShowWindowAsync() 関数のパラメータに渡す定数。
		/// WINDOWPLACEMENT 構造体メンバーにも使われている。
		/// </summary>
		public enum CommandOfShowWindow : uint
		{
			SW_HIDE = 0,
			SW_SHOWNORMAL = 1,
			SW_NORMAL = 1,
			SW_SHOWMINIMIZED = 2,
			SW_SHOWMAXIMIZED = 3,
			SW_MAXIMIZE = 3,
			SW_SHOWNOACTIVATE = 4,
			SW_SHOW = 5,
			SW_MINIMIZE = 6,
			SW_SHOWMINNOACTIVE = 7,
			SW_SHOWNA = 8,
			SW_RESTORE = 9,
			SW_SHOWDEFAULT = 10,
			SW_FORCEMINIMIZE = 11,
			SW_MAX = 11,
		}

		public enum CommandOfGetWindow : uint
		{
			GW_HWNDFIRST = 0,
			GW_HWNDLAST = 1,
			GW_HWNDNEXT = 2,
			GW_HWNDPREV = 3,
			GW_OWNER = 4,
			GW_CHILD = 5,
			GW_ENABLEDPOPUP = 6,
		}

		public enum MonitorFlagType : uint
		{
			MONITOR_DEFAULTTONULL = 0,
			MONITOR_DEFAULTTOPRIMARY = 1,
			MONITOR_DEFAULTTONEAREST = 2,
		}

		[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
		public struct MONITORINFO
		{
			public uint cbSize;
			public Win32Commons.RECT rcMonitor;
			public Win32Commons.RECT rcWork;
			public uint dwFlags;

			public void InitializeSize()
			{
				this.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(this);
			}
		}

		[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
		public struct WINDOWPLACEMENT
		{
			public uint length;
			public uint flags;
			public CommandOfShowWindow showCmd;
			public Win32Commons.POINT ptMinPosition;
			public Win32Commons.POINT ptMaxPosition;
			public Win32Commons.RECT rcNormalPosition;

			public void InitializeLength()
			{
				this.length = (uint)System.Runtime.InteropServices.Marshal.SizeOf(this);
			}
		}

		[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
		public struct MINMAXINFO
		{
			public Win32Commons.POINT ptReserved;
			/// <summary>
			/// ウィンドウが最大化されるときの、ウィンドウの幅 (point.x) と高さ (point.y) を指定します。
			/// </summary>
			public Win32Commons.POINT ptMaxSize;
			/// <summary>
			/// 最大化されるときの、ウィンドウの左辺の位置 (point.x) と上辺の位置 (point.y) を指定します。
			/// </summary>
			public Win32Commons.POINT ptMaxPosition;
			/// <summary>
			/// ウィンドウの最小トラッキングの幅 (point.x) と最小トラッキングの高さ (point.y) を指定します。
			/// </summary>
			public Win32Commons.POINT ptMinTrackSize;
			/// <summary>
			/// ウィンドウの最大トラッキングの幅 (point.x) と最大トラッキングの高さ (point.y) を指定します。
			/// </summary>
			public Win32Commons.POINT ptMaxTrackSize;
		}

		[System.Runtime.InteropServices.DllImport("user32.dll")]
		public static extern Int32 GetWindowLong(IntPtr hWnd, Int32 index);

		[System.Runtime.InteropServices.DllImport("user32.dll")]
		public static extern Int32 SetWindowLong(IntPtr hWnd, Int32 index, Int32 newStyle);

		// Win64 には関数エントリーポイントが存在するが、Win32 ではマクロ実装なのでエントリーポイントを取得できないことに注意。
		// P/Invoke は基本的に遅延バインディングなので、DLL に関数エントリーポイントが存在していなくても実際に呼び出さなければ問題ない。
		#region Win64 Only
		[System.Runtime.InteropServices.DllImport("user32.dll")]
		public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, Int32 index);

		[System.Runtime.InteropServices.DllImport("user32.dll")]
		public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, Int32 index, IntPtr newStyle);
		#endregion

		public static WindowStyles GetWindowStyle(IntPtr hWnd)
		{
			return (WindowStyles)GetWindowLong(hWnd, (Int32)IndexOfGetWindowLong.GWL_STYLE);
		}

		public static void SetWindowStyle(IntPtr hWnd, WindowStyles windowStyle)
		{
			SetWindowLong(hWnd, (Int32)IndexOfGetWindowLong.GWL_STYLE, (Int32)windowStyle);
		}

		[System.Runtime.InteropServices.DllImport("user32.dll")]
		public static extern bool SetForegroundWindow(IntPtr hWnd);
		[System.Runtime.InteropServices.DllImport("user32.dll")]
		public static extern bool ShowWindow(IntPtr hWnd, CommandOfShowWindow nCmdShow);
		[System.Runtime.InteropServices.DllImport("user32.dll")]
		public static extern bool ShowWindowAsync(IntPtr hWnd, CommandOfShowWindow nCmdShow);
		[System.Runtime.InteropServices.DllImport("user32.dll")]
		public static extern bool IsIconic(IntPtr hWnd);

		[System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
		public static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, LayeredWindowAttributes dwFlags);

		[System.Runtime.InteropServices.DllImport("user32.dll")]
		public static extern bool PrintWindow(IntPtr hwnd, IntPtr hDC, uint nFlags);

		[System.Runtime.InteropServices.DllImport("user32.dll")]
		public static extern bool GetWindowRect(IntPtr hwnd, ref Win32Commons.RECT lpRect);

		[System.Runtime.InteropServices.DllImport("user32.dll")]
		public static extern bool GetClientRect(IntPtr hwnd, ref Win32Commons.RECT lpRect);

		[System.Runtime.InteropServices.DllImport("user32.dll")]
		public static extern bool ClientToScreen(IntPtr hWnd, ref Win32Commons.POINT lpPoint);

		[System.Runtime.InteropServices.DllImport("user32.dll")]
		public static extern bool ScreenToClient(IntPtr hWnd, ref Win32Commons.POINT lpPoint);

		public delegate bool EnumWindowsProcDelegate(IntPtr hWnd, IntPtr lParam);

		[System.Runtime.InteropServices.DllImport("user32.dll")]
		public static extern bool EnumWindows(EnumWindowsProcDelegate lpEnumFunc, IntPtr lParam);
		[System.Runtime.InteropServices.DllImport("user32.dll")]
		public static extern bool IsWindowVisible(IntPtr hWnd);
		[System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
		public static extern Int32 GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
		[System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
		public static extern Int32 GetWindowTextLength(IntPtr hWnd);
		[System.Runtime.InteropServices.DllImport("user32.dll")]
		public static extern bool IsWindow(IntPtr hWnd);

		[System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
		public static extern IntPtr GetWindow(IntPtr hWnd, CommandOfGetWindow uCmd);

		[System.Runtime.InteropServices.DllImport("user32.dll")]
		public static extern IntPtr GetDesktopWindow();

		[System.Runtime.InteropServices.DllImport("user32.dll")]
		public static extern IntPtr GetWindowDC(IntPtr hWnd);

		[System.Runtime.InteropServices.DllImport("user32.dll")]
		public static extern Int32 ReleaseDC(IntPtr hWnd, IntPtr hDC);

		[System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
		public static extern bool SystemParametersInfo(SystemParametersInfoType uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

		[System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
		public static extern bool SystemParametersInfo(SystemParametersInfoType uiAction, uint uiParam, ref Win32Commons.RECT pvParam, uint fWinIni);

		// P/Invoke でジェネリクスは使えない。明示的にオーバーロードを定義してやる必要がある。

		[System.Runtime.InteropServices.DllImport("user32.dll")]
		[return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
		public static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

		[System.Runtime.InteropServices.DllImport("user32.dll")]
		public static extern IntPtr MonitorFromWindow(IntPtr hwnd, MonitorFlagType dwFlags);

		[System.Runtime.InteropServices.DllImport("user32.dll")]
		[return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
		public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

		[System.Runtime.InteropServices.DllImport("user32.dll")]
		public static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

		[System.Runtime.InteropServices.DllImport("user32.dll")]
		public static extern int GetMenuItemCount(IntPtr hMenu);

		[System.Runtime.InteropServices.DllImport("user32.dll")]
		public static extern bool DrawMenuBar(IntPtr hWnd);

		[System.Runtime.InteropServices.DllImport("user32.dll")]
		public static extern bool RemoveMenu(IntPtr hMenu, uint uPosition, MenuFlags uFlags);

		[System.Runtime.InteropServices.DllImport("user32.dll")]
		static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, MenuFlags uEnable);
	}

	internal static class Kernel32DllMethodsInvoker
	{
		// NOTE: GetLastError() に関しては、System.Runtime.InteropServices.Marshal.GetLastWin32Error() が用意されている。

		[System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
		public static extern bool CloseHandle(IntPtr hObject);

		[System.Runtime.InteropServices.DllImport("kernel32.dll")]
		public static extern void CopyMemory(IntPtr dst, IntPtr src, IntPtr size);
	}

	#endregion
}
