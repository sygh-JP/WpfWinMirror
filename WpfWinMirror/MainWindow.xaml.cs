using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

// 当初は DWM サムネイル API を使って、別アプリケーションのサムネイル画像を取得しようと考えていたが、
// DwmRegisterThumbnail(), DwmUpdateThumbnailProperties(), DWM_THUMBNAIL_PROPERTIES を見る限り、
// 描画元と描画先、およびクリッピング範囲の指定ができるだけで、ブレンド モードの制御や拡大縮小以外のアフィン変換の指定はできなさそう。
// Windows Vista/7 の Aero フリップ 3D はアフィン変換しているようなので、がんばればいけるのかも？
// ただし、ブレンド モードの制御はやはり無理そう。
// 「画像処理ソフトのアプリケーション描画内容を鏡面反転してリファレンス用にオーバーレイ表示する」などの処理を実装する目的で、
// 輝度をアルファ値として扱うなどの特殊ブレンドを高速に実行したければ、
// DIB への PrintWindow() もしくは BitBlt() の結果をプログラマブル シェーダーで加工するのがベストだが、
// ネイティブ Direct3D 11 や Direct2D 1.1 で加工してレイヤード ウィンドウに転送するよりも、
// いくつかの GDI 関連 Win32 API の P/Invoke と WPF およびカスタム エフェクトでやってしまったほうが簡単。

namespace WpfWinMirror
{
	using ThisAppResources = global::WpfWinMirror.Properties.Resources;

	/// <summary>
	/// MainWindow.xaml の相互作用ロジック
	/// </summary>
	public partial class MainWindow : Window
	{
		#region Fields

		//public static readonly RoutedCommand TransparentModeCommand = new RoutedCommand("TransparentModeCommand", typeof(MainWindow));
		public static readonly RoutedUICommand TransparentModeCommand = new RoutedUICommand();
		public static readonly RoutedUICommand EffectNoneCommand = new RoutedUICommand();
		public static readonly RoutedUICommand EffectGrayscaleCommand = new RoutedUICommand();
		public static readonly RoutedUICommand EffectNPInvertCommand = new RoutedUICommand();
		public static readonly RoutedUICommand EffectDarknessToOpacityCommand = new RoutedUICommand();
		public static readonly RoutedUICommand EffectBrightnessToOpacityCommand = new RoutedUICommand();
		public static readonly RoutedUICommand HorizontalReverseCommand = new RoutedUICommand();
		public static readonly RoutedUICommand VerticalReverseCommand = new RoutedUICommand();
		public static readonly RoutedUICommand OpenSettingsIncludeDirCommand = new RoutedUICommand();
		//public static readonly RoutedUICommand AppCloseCommand = new RoutedUICommand();

		private static readonly Brush CheckedMenuItemIconBorderBackBrush = new SolidColorBrush(Color.FromRgb(0xC4, 0xE1, 0xFF));
		private static readonly Brush CheckedMenuItemIconBorderStrokeBrush = Brushes.DodgerBlue;

		CustomEffects.GrayscaleEffect grayscaleEffect;
		CustomEffects.NPInvertEffect npInvertEffect;
		CustomEffects.BrightnessToOpacityEffect brightnessToOpacityEffect;
		CustomEffects.DarknessToOpacityEffect darknessToOpacityEffect;

		Brush originalBackground;

		IntPtr currentWinHandle = IntPtr.Zero;

		NonserializableSettingsInfo vaporSettingsInfo = new NonserializableSettingsInfo();
		SerializableSettingsInfo currentSettingsInfo = new SerializableSettingsInfo();
		List<SerializableSettingsInfo> settingsPresets = new List<SerializableSettingsInfo>();
		List<MenuItem> savePresetMenuItems = new List<MenuItem>();
		List<MenuItem> loadPresetMenuItems = new List<MenuItem>();

		const int MaxSettingsPresetsCount = 4;

		bool isDragging;
		Point dragStartPos;

		Rect defaultWindowBounds;

		MyMiscHelpers.MyWindowCaptureBuffer winCaptureBuffer;

		DispatcherTimer dispatcherTimer;

		// ユーザーが PC 能力に合わせてフレームレートを3段階くらいに設定できるようにする。一時停止もメニューに加える。
		// CPU で書き換えた WriteableBitmap を GPU に転送するのは PCI-e バス帯域が効いてくるが、全体的な負荷はほぼ CPU Bound となる。
		enum MyTimerIntervalMillisec
		{
			For10fps = 100, // 1000 / 100 = 10[FPS]
			For20fps = 50, // 1000 / 50 = 20[FPS]
			ForStop = -1,
			Default = For20fps,
		}
		MyTimerIntervalMillisec imageUpdateTimerIntervalMillisec = MyTimerIntervalMillisec.Default;

		MyMiscHelpers.MyCustomWinProc customWinProc = new MyMiscHelpers.MyCustomWinProc();

		const string AppTitle = "WPF WinMirror";

		#endregion


		/// <summary>
		/// デフォルト コンストラクタ。
		/// </summary>
		public MainWindow()
		{
			InitializeComponent();

			this.grayscaleEffect = new CustomEffects.GrayscaleEffect();
			this.npInvertEffect = new CustomEffects.NPInvertEffect();
			this.brightnessToOpacityEffect = new CustomEffects.BrightnessToOpacityEffect();
			this.darknessToOpacityEffect = new CustomEffects.DarknessToOpacityEffect();

			this.originalBackground = this.mainRootGrid.Background;

			// 起動直後に1回だけ設定するので、プロパティの変更に追従するわけではない。
			this.customWinProc.MinWindowWidth = Double.IsNaN(this.MinWidth) ? 0 : (int)this.MinWidth;
			this.customWinProc.MinWindowHeight = Double.IsNaN(this.MinHeight) ? 0 : (int)this.MinHeight;

			this.UpdateMenuCheckedState();

			// 何らかのインタラクティブな UI 要素への操作（というかフォーカス）がないと、CommandBinding の CanExecute が評価されないらしい。
			this.Focus();

			// 自前の簡易タイトル バーを付けるので、クライアント領域のドラッグによる移動などはさせない。
#if false
			this.MouseLeftButtonDown += (s, e) => { this.DragMove(); };
#endif
#if false
			this.MouseDoubleClick += (s, e) =>
			{
				if (e.LeftButton == MouseButtonState.Pressed)
				{
					this.WindowState = (this.WindowState == System.Windows.WindowState.Normal)
						? System.Windows.WindowState.Maximized
						: System.Windows.WindowState.Normal;
				}
			};
#endif

			// グロー枠、左上のアプリ名、右上のシステム コマンド パネル、および選択矩形の可視状態はアクティブ状態に連動する。
			// アプリ名とシステム コマンド パネルはバインディングで可視状態を連動させる。
			Application.Current.Activated += (s, e) =>
			{
				this.rectSelectRegion.Visibility = System.Windows.Visibility.Visible;
				this.glowBorder.Visibility = System.Windows.Visibility.Visible;
			};
			Application.Current.Deactivated += (s, e) =>
			{
				this.rectSelectRegion.Visibility = System.Windows.Visibility.Collapsed;
				this.glowBorder.Visibility = System.Windows.Visibility.Collapsed;
			};

			// タイマーで定期的にイメージをキャプチャ＆更新するので、毎回ビットマップ DIB をヒープ＆破棄しないように最適化する。
			// プライマリー デスクトップ サイズの DIB を WPF/GDI+ それぞれに予約バッファとして用意して、それを再利用する。
#if false
			{
				// 最大化したときは左右上下に余白ができる。
				double maximizedW = System.Windows.SystemParameters.MaximizedPrimaryScreenWidth;
				double maximizedH = System.Windows.SystemParameters.MaximizedPrimaryScreenHeight;
				System.Diagnostics.Debug.WriteLine("MaxScreen = ({0}, {1})", maximizedW, maximizedH);

				f_winCaptureBuffer = new MyMiscHelpers.MyWindowCaptureBuffer((int)maximizedW, (int)maximizedH);
			}
#else
			{
				// デスクトップ全体＋余白分だけ画像バッファを確保する。ワークエリアではない。
				// TODO: ディスプレイ解像度が変更された場合に作り直しが必要。
				var desktopRect = MyMiscHelpers.MyWin32InteropHelper.GetWindowRect(MyMiscHelpers.MyWin32InteropHelper.GetDesktopWindow());
				System.Diagnostics.Debug.Assert(!desktopRect.IsEmpty);
				this.winCaptureBuffer = new MyMiscHelpers.MyWindowCaptureBuffer(desktopRect.Width, desktopRect.Height);
			}
#endif

#if false
			{
				var workAreaRect = System.Windows.SystemParameters.WorkArea;
				System.Diagnostics.Debug.WriteLine("WorkArea = ({0}, {1}, {2}, {3})",
					workAreaRect.X, workAreaRect.Y, workAreaRect.Width, workAreaRect.Height);
			}
#endif

#if false
			{
				var workAreaRect = NativeHelpers.MyWin32InteropHelper.GetWorkAreaRect();
				System.Diagnostics.Debug.WriteLine("WorkArea = ({0}, {1}, {2}, {3})",
					workAreaRect.X, workAreaRect.Y, workAreaRect.Width, workAreaRect.Height);
			}
#endif

			{
				this.dispatcherTimer = new DispatcherTimer(DispatcherPriority.Normal);
				this.dispatcherTimer.Interval = this.CreateImageUpdateTimerInterval();
				this.dispatcherTimer.Tick += this.dispatcherTimer_Tick;
				this.dispatcherTimer.Start();
			}

			for (int i = 0; i < MaxSettingsPresetsCount; ++i)
			{
				int index = i; // ループ カウンターをラムダ式でキャプチャする前に、ローカル変数にコピーしておく。
				var settingsInfo = new SerializableSettingsInfo();
				this.settingsPresets.Add(settingsInfo);
				var saveMenuItem = new MenuItem();
				this.savePresetMenuItems.Add(saveMenuItem);
				var loadMenuItem = new MenuItem();
				this.loadPresetMenuItems.Add(loadMenuItem);
				// TODO: メニューコマンドのセットアップ。
				string strHeader = ThisAppResources.Preset + String.Format(" #{0:00}", index + 1);
				saveMenuItem.Header = strHeader;
				saveMenuItem.Click += (s, e) =>
				{
					this.currentSettingsInfo.MainWindowBounds = this.GetWindowBounds();
					this.currentSettingsInfo.IsMainWindowMaximized = this.WindowState == System.Windows.WindowState.Maximized;
					this.settingsPresets[index] = this.currentSettingsInfo.Clone();
				};
				loadMenuItem.Header = strHeader;
				loadMenuItem.Click += (s, e) =>
				{
					this.currentSettingsInfo = this.settingsPresets[index].Clone();
					this.SetWindowBounds(this.currentSettingsInfo.MainWindowBounds);
					this.WindowState = this.currentSettingsInfo.IsMainWindowMaximized ? System.Windows.WindowState.Maximized : System.Windows.WindowState.Normal;
					// ビジュアルの選択矩形の更新。
					this.SetVisualSelectRegionRectPosition(this.currentSettingsInfo.ClippingRect);
					this.UpdateImageTransform();
				};
			}
			this.menuItemSavePresetRoot.ItemsSource = this.savePresetMenuItems;
			this.menuItemLoadPresetRoot.ItemsSource = this.loadPresetMenuItems;

			this.DataContext = this.vaporSettingsInfo;
		}

		IntPtr GetWindowHandle()
		{
			return new System.Windows.Interop.WindowInteropHelper(this).Handle;
		}

		static System.Configuration.Configuration GetUserConfig()
		{
			return System.Configuration.ConfigurationManager.OpenExeConfiguration(
				System.Configuration.ConfigurationUserLevel.PerUserRoamingAndLocal);
		}

		TimeSpan CreateImageUpdateTimerInterval()
		{
			return new TimeSpan(0, 0, 0, 0, (int)this.imageUpdateTimerIntervalMillisec);
		}


		void dispatcherTimer_Tick(object sender, EventArgs e)
		{
			// DispatcherTimer を UI スレッドから起動すると、Tick イベントは UI スレッドで発生する。
			// そのため、UI コントロールを操作する際に DispatcherObject.Dispatcher.Invoke() は必要ないが、
			// イベント ハンドラー内で時間のかかる処理を行なってはいけない。
			// タイマーを使って重い処理を行なう場合は、従来通り System.Threading.Time クラスと
			// Window クラスの Dispatcher を利用してメインスレッドを操作する必要がある。

			this.BindWindowCaptureBitmap(this.currentWinHandle);
		}

		void mainContextMenu_Loaded(object sender, RoutedEventArgs e)
		{
			// ウィンドウがクローズされてハンドルが無効化されたときだけでなく、
			// 新たに追加されたときどうするのか、という問題があるので、ウィンドウのリストはコンボ ボックスやリスト ビューでなく
			// OnTopReplica のようにコンテキスト メニューで管理したほうがよさげ。
			// コンテキスト メニューが表示されるタイミングで再列挙すればよい。

			// Loaded イベントはコンテキスト メニューが表示されるたびに発生する。
			// 常に最新のウィンドウ リストが欲しい今回のパターンでは最適。
			System.Diagnostics.Debug.WriteLine(System.Reflection.MethodBase.GetCurrentMethod().Name);

			var winHandleList = MyMiscHelpers.MyWin32InteropHelper.EnumVisibleWindows();
			var menuItemsList = new List<MenuItem>();

			foreach (var hwnd in winHandleList)
			{
				// 自分自身は除外。
				if (hwnd == this.GetWindowHandle())
				{
					continue;
				}

				var menuItem = new MenuItem() { Header = WindowInfo.CreateMenuTextForWindow(hwnd), IsChecked = (this.currentWinHandle == hwnd) };
				menuItem.Click += (ss, ee) =>
				{
					this.Focus(); // これがないと描画されない。
					// 最初の1回は明示的にバインドする（たとえタイマーが一時停止されていても更新される）。
					this.BindWindowCaptureBitmap(hwnd);
				};

				menuItemsList.Add(menuItem);
			}

			this.menuItemTargetWindowRoot.ItemsSource = menuItemsList;

			foreach (var item in this.menuItemTargetWindowRoot.Items)
			{
				// WindowInfo のコレクションを直接 MenuItem.ItemsSource にバインドすれば、Items は WindowInfo のコレクションになる。
				//System.Diagnostics.Debug.WriteLine("Type of Item = " + item.GetType().Name);
			}

			this.UpdateMenuCheckedState();
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{

		}

		private void Window_KeyDown(object sender, KeyEventArgs e)
		{
			if (this.WindowState == System.Windows.WindowState.Normal)
			{
				double moveAmount = Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ? 10 : 1;
				switch (e.Key)
				{
					case Key.Left:
						this.Left -= moveAmount;
						break;
					case Key.Right:
						this.Left += moveAmount;
						break;
					case Key.Up:
						this.Top -= moveAmount;
						break;
					case Key.Down:
						this.Top += moveAmount;
						break;
					default:
						break;
				}
			}
		}

		private void UpdateTextBoxBindingSourceOnEnterKeyDown(object sender, KeyEventArgs e)
		{
			// もし再利用したい場合は、個別に KeyDown イベント ハンドラーをバインディングする方法でもよいし、ビヘイビアを書く方法もある。
			if (e.Key == Key.Enter)
			{
				var textBox = e.OriginalSource as TextBox;
				if (textBox != null)
				{
					var be = textBox.GetBindingExpression(TextBox.TextProperty);
					if (be != null)
					{
						be.UpdateSource();
					}
				}
			}
		}

		private void BindWindowCaptureBitmap(IntPtr hwnd)
		{
			// 念のため、Dispose() した後のバッファには触らないようにチェックをかける。
			if (this.winCaptureBuffer == null)
			{
				return;
			}

			this.mainImage.Source = null;
#if false
			var wicBitmap = NativeHelpers.MyWin32InteropHelper.CaptureWindow(hwnd);
			if (wicBitmap != null)
#else
			bool isCaptureSuccess = this.winCaptureBuffer.CaptureWindow(hwnd, this.currentSettingsInfo.ClippingRect);
			var wicBitmap = this.winCaptureBuffer.CapturedImage;
			if (isCaptureSuccess)
#endif
			{
				this.mainImage.Source = wicBitmap;
				this.mainImage.Width = wicBitmap.PixelWidth;
				this.mainImage.Height = wicBitmap.PixelHeight;
				this.textblockNoImage.Visibility = System.Windows.Visibility.Collapsed;
				this.currentWinHandle = hwnd;
			}
			else
			{
				this.mainImage.Width = Double.NaN;
				this.mainImage.Height = Double.NaN;
				this.textblockNoImage.Visibility = System.Windows.Visibility.Visible;
				this.currentWinHandle = IntPtr.Zero;
			}
		}

		private void UpdateImageTransform()
		{
			double scaleX = this.currentSettingsInfo.IsHorizontalReversed ? -1 : 1;
			double scaleY = this.currentSettingsInfo.IsVerticalReversed ? -1 : 1;
			if (this.currentSettingsInfo.ClippingRect.HasArea)
			{
				// ユーザー定義のクリッピング領域の中心を基点に反転する。
				// 中心位置の算出は浮動小数ではなく整数で演算して切り捨てるべきか？
#if false
				// 切り出し元の相対位置を使う場合。
				double clipCenterX = f_currentSettingsInfo.ClippingRect.X + f_currentSettingsInfo.ClippingRect.Width * 0.5;
				double clipCenterY = f_currentSettingsInfo.ClippingRect.Y + f_currentSettingsInfo.ClippingRect.Height * 0.5;
#else
				// 切り出した領域を常に左上に配置する場合。
				double clipCenterX = this.currentSettingsInfo.ClippingRect.Width * 0.5;
				double clipCenterY = this.currentSettingsInfo.ClippingRect.Height * 0.5;
#endif
				this.mainImage.RenderTransform = new ScaleTransform(
					scaleX, scaleY,
					clipCenterX, clipCenterY);
			}
			else
			{
				var targetClientRect = MyMiscHelpers.MyWin32InteropHelper.GetClientRect(this.currentWinHandle);
				if (targetClientRect.HasArea)
				{
					// ユーザー定義のクリッピング領域が無効の場合、キャプチャ対象ウィンドウのクライアント中心を基点に反転する。
					// デスクトップ領域の中心ではない。
#if false
					double clipCenterX = this.image1.Width * 0.5;
					double clipCenterY = this.image1.Height * 0.5;
#else
					double clipCenterX = targetClientRect.Width * 0.5;
					double clipCenterY = targetClientRect.Height * 0.5;
#endif
					this.mainImage.RenderTransform = new ScaleTransform(
						scaleX, scaleY,
						clipCenterX, clipCenterY);
				}
				else
				{
					this.mainImage.RenderTransform = Transform.Identity;
				}
			}
		}

		void UpdateMenuCheckedState()
		{
			// メニュー項目のグループ化（MFC の ON_COMMAND_RANGE 相当）とラジオボタン風のチェックマーク表示は、WPF では簡単にできない模様。
			// 本格的にやるならばスタイル・テンプレートをいじる必要があるらしい。正直かなり手間。
			// ネイティブ Win32/MFC や WinForms の MenuItem では簡単なのに……
			// また、MFC Feature Pack の CMFCMenuBar や WinForms の MenuStrip, ToolStripMenuItem のような、
			// アイコン画像背景に半透明矩形を使ったチェック状態表示機能も、WPF ではデフォルトで用意されていない。
			// MenuItem.IsCheckable や MenuItem.IsChecked は使わず、Border + Image や Border + Ellipse を MenuItem.Icon に設定し、
			// Border や Ellipse の状態を明示的に制御するのがてっとり早いが、WPF らしい方法ではない。
			// 
			// なお、チェック状態の連動は MVVM 的にはバインディングを使うべきだが、
			// INotifyPropertyChanged はともかくコンバーターの実装がダルいので明示的に更新する。

			//this.menuItemEffectNone.IsChecked = (this.mainImage.Effect == null);
			//this.menuItemEffectGrayscale.IsChecked = (this.mainImage.Effect == f_grayscaleEffect);
			//this.menuItemEffectDarknessToOpacity.IsChecked = (this.mainImage.Effect == f_darknessToOpacityEffect);
			//this.menuItemEffectBrightnessToOpacity.IsChecked = (this.mainImage.Effect == f_brightnessToOpacityEffect);
			//this.menuItemHorizontalReverse.IsChecked = f_isHorizontalReversed;
			//this.menuItemVerticalReverse.IsChecked = f_isVerticalReversed;
			//this.menuItemTransparentMode.IsChecked = this.vaporSettingsInfo.IsTransparentMode;
			UpdateMenuItemIconImageBorder(this.menuItemEffectNone, (this.mainImage.Effect == null));
			UpdateMenuItemIconImageBorder(this.menuItemEffectGrayscale, (this.mainImage.Effect == this.grayscaleEffect));
			UpdateMenuItemIconImageBorder(this.menuItemEffectNPInvert, (this.mainImage.Effect == this.npInvertEffect));
			UpdateMenuItemIconImageBorder(this.menuItemEffectDarknessToOpacity, (this.mainImage.Effect == this.darknessToOpacityEffect));
			UpdateMenuItemIconImageBorder(this.menuItemEffectBrightnessToOpacity, (this.mainImage.Effect == this.brightnessToOpacityEffect));
			UpdateMenuItemIconImageBorder(this.menuItemFrameRateStop, (this.imageUpdateTimerIntervalMillisec == MyTimerIntervalMillisec.ForStop));
			UpdateMenuItemIconImageBorder(this.menuItemFrameRate10fps, (this.imageUpdateTimerIntervalMillisec == MyTimerIntervalMillisec.For10fps));
			UpdateMenuItemIconImageBorder(this.menuItemFrameRate20fps, (this.imageUpdateTimerIntervalMillisec == MyTimerIntervalMillisec.For20fps));
			UpdateMenuItemIconImageBorder(this.menuItemHorizontalReverse, this.currentSettingsInfo.IsHorizontalReversed);
			UpdateMenuItemIconImageBorder(this.menuItemVerticalReverse, this.currentSettingsInfo.IsVerticalReversed);
		}

		static void UpdateMenuItemIconImageBorder(MenuItem item, bool isChecked)
		{
			// 参考までに、Vista/7 における
			// Win32/WPF メニュー（Aero テーマ）のアイコン矩形標準カラーは #EDF2F7 + #AECFF7、チェックマークの色は #1218A3、
			// WinForms ToolStripMenuItem のアイコン矩形標準カラーは #C4E1FF + #3399FF、チェックマークの色は #040204 になっている。
			// 実際は半透明でコントロール背景色に左右されるのかもしれないが……

			var border = item.Icon as Border;
			System.Diagnostics.Debug.Assert(border != null);
			if (isChecked)
			{
				border.Background = CheckedMenuItemIconBorderBackBrush;
				border.BorderBrush = CheckedMenuItemIconBorderStrokeBrush;
			}
			else
			{
				border.Background = null;
				border.BorderBrush = null;
			}
			var child = border.Child as FrameworkElement;
			if (child is Ellipse)
			{
				child.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;
			}
		}

		// メニューやボタンの Click イベント ハンドラーを直接記述してもよいが、
		// Enabled / Disabled を制御する必要がある場合や、複数のトリガー UI が存在する場合はコマンド バインディングを使うと便利。
		// ただチェック状態などの細かな UI 制御はコマンド バインディングのみでは対応不能？
		// 別途データ バインディングを使ったり、明示的に直接プロパティを制御したりする必要がありそう。
		#region Command Handlers

		private void AlwaysAvailableCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = true;
		}

		private void TransparentModeCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			System.Diagnostics.Debug.WriteLine(System.Reflection.MethodBase.GetCurrentMethod().Name);

			this.vaporSettingsInfo.IsTransparentMode = !this.vaporSettingsInfo.IsTransparentMode;

			this.UpdateWindowTransparent();
		}

		void UpdateWindowTransparent()
		{
			// WS_EX_TRANSPARENT をセットすることで、Window 全体のヒットテストを完全に無効化できる。
			// 完全にヒットテストを切ってしまうとユーザーが混乱するおそれがあるので、描画領域だけのヒットテストを切りたいが、
			// 不透明画像を Window に描画した時点でヒットテストが有効になってしまうらしい。
			MyMiscHelpers.MyWin32InteropHelper.SetWindowStyleExTransparent(this.GetWindowHandle(), this.vaporSettingsInfo.IsTransparentMode);
			// Grid 背景を null にして透過する。
			// HACK: 不透明の白背景、半透明の黒背景、および完全透明（null）の中からメニューで選べるようにする？
			this.mainRootGrid.Background = this.vaporSettingsInfo.IsTransparentMode ? null : this.originalBackground;
#if false
			// Image が完全不透明だと混乱しやすくなりそうなので、わずかに透過する。
			this.mainImage.Opacity = this.vaporSettingsInfo.IsTransparentMode ? 0.9 : 1.0;
#endif

			string strAppTitle = AppTitle;
			if (this.vaporSettingsInfo.IsTransparentMode)
			{
				strAppTitle += " " + ThisAppResources.TransparentMode;
			}
			this.textblockAppTitle.Text = strAppTitle;

			//this.UpdateMenuCheckedState();
		}

		private void EffectNoneCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			this.mainImage.Effect = null;
			this.UpdateMenuCheckedState();
		}

		private void EffectGrayscaleCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			this.mainImage.Effect = this.grayscaleEffect;
			this.UpdateMenuCheckedState();
		}

		private void EffectNPInvertCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			this.mainImage.Effect = this.npInvertEffect;
			this.UpdateMenuCheckedState();
		}

		private void EffectDarknessToOpacityCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			this.mainImage.Effect = this.darknessToOpacityEffect;
			this.UpdateMenuCheckedState();
		}

		private void EffectBrightnessToOpacityCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			this.mainImage.Effect = this.brightnessToOpacityEffect;
			this.UpdateMenuCheckedState();
		}

		private void HorizontalReverseCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			this.currentSettingsInfo.ToggleHorizontalReversed();

			this.UpdateImageTransform();
			this.UpdateMenuCheckedState();
		}

		private void VerticalReverseCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			this.currentSettingsInfo.ToggleVerticalReversed();

			this.UpdateImageTransform();
			this.UpdateMenuCheckedState();
		}

		private void OpenSettingsIncludeDirCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			//var psi = new System.Diagnostics.ProcessStartInfo();
			var config = GetUserConfig();
			if (System.IO.File.Exists(config.FilePath))
			{
				System.Diagnostics.Process.Start("explorer.exe", @"/select," + config.FilePath);
			}
			else
			{
				MessageBox.Show("Failed to find the settings file of this application!!", AppTitle, MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private void OpenSettingsIncludeDirCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			// これはメニューを表示したタイミングでの判定なので、実際にコマンドを実行するときにもチェックする必要がある。
			var config = GetUserConfig();
			if (System.IO.File.Exists(config.FilePath))
			{
				e.CanExecute = true;
			}
		}

		private void AppCloseCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			this.Close();
		}

		#endregion

		private void menuItemReset_Click(object sender, RoutedEventArgs e)
		{
			this.vaporSettingsInfo.Reset();
			this.UpdateWindowTransparent();
			this.currentSettingsInfo.Reset();

			this.rectSelectRegion.Width = 0;
			this.rectSelectRegion.Height = 0;

			this.mainImage.Effect = null;

			this.UpdateImageTransform();

			// ターゲット ウィンドウのリセット。
			this.BindWindowCaptureBitmap(IntPtr.Zero);

			// アプリケーション ウィンドウのリセット。
			this.Left = this.defaultWindowBounds.Left;
			this.Top = this.defaultWindowBounds.Top;
			this.Width = this.defaultWindowBounds.Width;
			this.Height = this.defaultWindowBounds.Height;
			this.WindowState = System.Windows.WindowState.Normal;
		}

		private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			this.CaptureMouse();
			this.dragStartPos = e.GetPosition(this.canvasMainImage);
			this.rectSelectRegion.Width = 0;
			this.rectSelectRegion.Height = 0;
			//this.rectSelectRegion.Visibility = System.Windows.Visibility.Visible;
			this.isDragging = true;
		}

		private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			this.ReleaseMouseCapture();
			//this.rectSelectRegion.Visibility = System.Windows.Visibility.Collapsed;
			// 等倍表示でない場合、実際のクリッピング矩形はスケーリングしてやる必要がある。
			this.isDragging = false;
			var newRect = this.GetVisualSelectRegionRectPosition();
			if (newRect.Width > 0 && newRect.Height > 0)
			{
				this.currentSettingsInfo.ClippingRect = newRect;
			}
			else
			{
				this.currentSettingsInfo.ClippingRect = Int32Rect.Empty;
			}
			this.UpdateImageTransform();
		}

		static void ClampPair(double curr, double start, double max, out double pos, out double length)
		{
			if (curr < start)
			{
				pos = Math.Max(curr, 0);
				length = start - pos;
			}
			else if (curr > start)
			{
				pos = start;
				length = Math.Min(curr, max) - pos;
			}
			else
			{
				pos = 0;
				length = 0;
			}
			System.Diagnostics.Debug.Assert(length >= 0);
		}

		private void SetVisualSelectRegionRectPosition(double left, double top, double width, double height)
		{
			System.Diagnostics.Debug.Assert(width >= 0 && height >= 0);
			Canvas.SetLeft(this.rectSelectRegion, left);
			Canvas.SetTop(this.rectSelectRegion, top);
			this.rectSelectRegion.Width = width;
			this.rectSelectRegion.Height = height;
		}

		private void SetVisualSelectRegionRectPosition(Int32Rect rect)
		{
			this.SetVisualSelectRegionRectPosition(rect.X, rect.Y, rect.Width, rect.Height);
		}

		private Int32Rect GetVisualSelectRegionRectPosition()
		{
			return new Int32Rect(
				(int)Canvas.GetLeft(this.rectSelectRegion),
				(int)Canvas.GetTop(this.rectSelectRegion),
				(int)this.rectSelectRegion.Width,
				(int)this.rectSelectRegion.Height);
		}

		private void Window_MouseMove(object sender, MouseEventArgs e)
		{
			if (this.isDragging)
			{
				var currentPos = e.GetPosition(this.canvasMainImage);
				double left, top, width, height;
				//ClampPair(currentPos.X, f_dragStartPos.X, this.canvasMainImage.ActualWidth, out left, out width);
				//ClampPair(currentPos.Y, f_dragStartPos.Y, this.canvasMainImage.ActualHeight, out top, out height);
				ClampPair(currentPos.X, this.dragStartPos.X, System.Windows.SystemParameters.MaximizedPrimaryScreenWidth, out left, out width);
				ClampPair(currentPos.Y, this.dragStartPos.Y, System.Windows.SystemParameters.MaximizedPrimaryScreenHeight, out top, out height);
				// ビジュアルの選択矩形の更新。
				// ドラッグ中はクリッピング領域のデータは更新しない（データ バインディングなどで自動的に連動されるようにはしない）。
				this.SetVisualSelectRegionRectPosition(left, top, width, height);
			}
		}

		private void Window_StateChanged(object sender, EventArgs e)
		{
			// StateChanged は WindowState が変更された「後」に発生するイベントなので、
			// 変更「直前」になんらかの処理をはさみたい場合は P/Invoke とメッセージ フックを行なう必要がある。

			if (this.WindowState == System.Windows.WindowState.Normal)
			{
				this.buttonRestore.Visibility = System.Windows.Visibility.Collapsed;
				this.buttonMaximize.Visibility = System.Windows.Visibility.Visible;
			}
			if (this.WindowState == System.Windows.WindowState.Maximized)
			{
				this.buttonRestore.Visibility = System.Windows.Visibility.Visible;
				this.buttonMaximize.Visibility = System.Windows.Visibility.Collapsed;
			}
			else
			{
			}
		}

		private Int32Rect GetWindowBounds()
		{
			// NOTE: 最大化されているときは Window.Width と Window.Height ではなく、最大化前の通常状態における値を保存するべき。
			// Visual Studio 2012 は、終了前に最小化している場合（タスク バーのシステム メニューで [ウィンドウを閉じる] を選択したときなど）、
			// いったん復元してからウィンドウを閉じる処理になっているのが分かる。
			// 一方、Visual Studio 2008 や MS Office Outlook 2010 などでは、最小化されたまま閉じてしまうため、
			// 次回起動時には最大化状態がキャンセルされている。
			// 最小化されているときは設定を保存しない、もしくはデフォルト値に戻してしまうようにするほうが楽。

			if (this.WindowState == System.Windows.WindowState.Maximized && !this.RestoreBounds.IsEmpty)
			{
				return new Int32Rect(
					(int)this.RestoreBounds.Left, (int)this.RestoreBounds.Top,
					(int)this.RestoreBounds.Width, (int)this.RestoreBounds.Height);
			}
			else if (this.WindowState == System.Windows.WindowState.Normal)
			{
				// Aero Snap で左右分割した直後は、Window.RestoreBounds に復元位置情報が保存されている。
				// そのため、Window.WindowState が Normal のときにも RestoreBounds をシリアライズに使ってしまうと、
				// 次回起動時は左右分割時の最終位置ではなく復元位置に初期化されることになる。
				// ちなみに左右分割した直後は、単に位置とサイズが変更されているだけでなく、
				// 最大化時と同様にタイトル バーがエッジに吸着しているような動作をする。
				return new Int32Rect(
					(int)this.Left, (int)this.Top,
					(int)this.Width, (int)this.Height);
			}
			else
			{
				// App.config の初期値同様に無効値を入れておく。
				// 最小化されている状態で、タスク バーの [ウィンドウを閉じる] を実行した場合などにはリセット動作を兼ねる。
				return Int32Rect.Empty;
			}
		}

		private bool SetWindowBounds(Int32Rect winBounds)
		{
			if (winBounds.HasArea)
			{
				// 前回の位置が負（ワークエリアからはみ出す）の場合はそのまま復元せず、エッジにクランプするアプリのほうが一般的だが……
				this.Left = winBounds.X;
				this.Top = winBounds.Y;
				this.Width = winBounds.Width;
				this.Height = winBounds.Height;
				return true;
			}
			return false;
		}

		void LoadSettings()
		{
			var settings = global::WpfWinMirror.Properties.Settings.Default;
			this.SetWindowBounds(settings.LastWindowBounds);
			if (settings.IsMainWindowMaximized)
			{
				this.WindowState = System.Windows.WindowState.Maximized;
			}

			// プリセットの読み込み。
			if (settings.Presets != null)
			{
				for (int i = 0; i < settings.Presets.Count && i < MaxSettingsPresetsCount; ++i)
				{
					var preset = SerializableSettingsInfo.FromStringLine(settings.Presets[i]);
					if (preset != null)
					{
						this.settingsPresets[i] = preset;
					}
				}
			}
		}

		void SaveSettings()
		{
			// 設定は非ローミング ユーザー（ローカル）のアプリケーション データ フォルダー（%LocalAppData%）に、
			// user.config という名前の XML ファイルとして保存される。

			var settings = global::WpfWinMirror.Properties.Settings.Default;
			//settings.IsMainWindowMaximized = MyMiscHelpers.MyWin32InteropHelper.HasWindowStyleMaximize(this);
			settings.IsMainWindowMaximized = (this.WindowState == System.Windows.WindowState.Maximized);
			settings.LastWindowBounds = this.GetWindowBounds();

			// プリセットの保存。あらかじめプレースホルダーとして string を固定数だけ用意しておくのではなく、
			// 可変長のコレクション（StringCollection）にする。
			// ただし読み込むときには要素数に制限を設ける。
			// なお、StringCollection は <ArrayOfString/> として XML 形式で保存される。
			// HACK: Dictionary のコレクションのほうがよいか？
			settings.Presets = new System.Collections.Specialized.StringCollection();
			foreach (var p in this.settingsPresets)
			{
				settings.Presets.Add(p.ToStringLine());
			}

			// XML ファイルに保存。
			// ちなみに StringCollection を保存するとき、ApplicationSettingsBase.Save() 内部で
			// System.XmlSerializers.dll が見つからないという旨の System.IO.FileNotFoundException が発生するが、
			// これは無視してよいらしい（内部でハンドリングされる）。
			settings.Save();

			var config = GetUserConfig();
			System.Diagnostics.Debug.WriteLine(config.FilePath);
		}

		private void Window_SourceInitialized(object sender, EventArgs e)
		{
#if false
			f_customWinProc.PreMaximized += () =>
			{
				//this.outerBorder.Visibility = System.Windows.Visibility.Collapsed;
			};
#endif

			this.customWinProc.AttachCustomWndProc(this.GetWindowHandle());

			// 最大化コマンドを無効にすると、Aero Snap による左右分割も使えなくなる。
			//MyMiscHelpers.MyWin32InteropHelper.DisableMaximizeButton(this.GetWindowHandle());
			//MyMiscHelpers.MyWin32InteropHelper.SetWindowStyleExCompsited(this.GetWindowHandle());

			// Window.Left, Window.Top は XAML で指定されない場合、コンストラクタでは NaN になっている。
			// SourceInitialized イベント受信のタイミングであれば、
			// WindowStartupLocation="CenterScreen" でセンタリングされたときの実際の初期位置情報を取得できる。
			// Win32 ウィンドウ ハンドルが正しく取得できるのもこのタイミング以降になるはず。
			this.defaultWindowBounds = new Rect(this.Left, this.Top, this.Width, this.Height);

			// アプリケーション設定の読み込み。
			this.LoadSettings();
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			// アプリケーション設定の保存。
			this.SaveSettings();
		}

		private void Window_Closed(object sender, EventArgs e)
		{
			this.dispatcherTimer.Stop();
			this.dispatcherTimer.Tick -= this.dispatcherTimer_Tick;
			MyMiscHelpers.MyGenericsHelper.SafeDispose(ref this.winCaptureBuffer);
			this.customWinProc.DetachCustomWndProc();
		}

		private void menuItemSaveImageAs_Click(object sender, RoutedEventArgs e)
		{
			// モーダル ダイアログを表示している間もメッセージ ループは回っているため、キャプチャ画像の更新は行なわれる。
			// UNDONE: ユーザーが任意のタイミングでフリーズした画像を保存したければ、
			// タイマーを一時停止したり画像を複製してキャッシュしておいたりする機能を追加する必要がある。

			// WinForms の System.Windows.Forms.CommonDialog は IDisposable 実装だが、
			// WPF 向けの Microsoft.Win32.CommonDialog はただの抽象クラス。
			try
			{
				var sfd = new Microsoft.Win32.SaveFileDialog();
				sfd.FileName = "WpfWinMirrorSS.png";
				sfd.DefaultExt = ".png";
				sfd.Filter = "PNG Image Files|*.png";

				var result = sfd.ShowDialog();
				if (result == true)
				{
					this.winCaptureBuffer.SaveImageAsPngFile(sfd.FileName, this.currentWinHandle, this.currentSettingsInfo.ClippingRect);
				}
			}
			catch (Exception err)
			{
				System.Diagnostics.Debug.WriteLine(err.Message);
				MessageBox.Show(ThisAppResources.ErrMsgFailedToSaveImage, AppTitle, MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private void buttonMinimize_Click(object sender, RoutedEventArgs e)
		{
			this.WindowState = System.Windows.WindowState.Minimized;
		}

		private void buttonMaximize_Click(object sender, RoutedEventArgs e)
		{
			this.WindowState = System.Windows.WindowState.Maximized;
		}

		private void buttonRestore_Click(object sender, RoutedEventArgs e)
		{
			this.WindowState = System.Windows.WindowState.Normal;
		}

		private void menuItemFitWindowToImage_Click(object sender, RoutedEventArgs e)
		{
			if (this.currentWinHandle != IntPtr.Zero)
			{
				if (this.currentSettingsInfo.ClippingRect.HasArea)
				{
					this.Width = this.currentSettingsInfo.ClippingRect.Width;
					this.Height = this.currentSettingsInfo.ClippingRect.Height;
					// 最大化されている場合は元に戻す。
					this.WindowState = System.Windows.WindowState.Normal;
				}
				else
				{
					// ターゲット ウィンドウのクライアント領域の大きさに合わせる。
					var clientRect = MyMiscHelpers.MyWin32InteropHelper.GetClientRect(this.currentWinHandle);
					if (clientRect.HasArea)
					{
						this.Width = clientRect.Width;
						this.Height = clientRect.Height;
						this.WindowState = System.Windows.WindowState.Normal;
					}
				}
			}
		}

		private void menuItemAboutThisApp_Click(object sender, RoutedEventArgs e)
		{
			var aboutWnd = new AboutWindow();
			aboutWnd.Owner = this;
			aboutWnd.ShowDialog();
		}

		void ResetImageUpdateTimer(MyTimerIntervalMillisec newVal)
		{
			System.Diagnostics.Debug.Assert(this.dispatcherTimer != null);
			this.dispatcherTimer.Stop();
			this.imageUpdateTimerIntervalMillisec = newVal;
			if (newVal > 0)
			{
				this.dispatcherTimer.Interval = this.CreateImageUpdateTimerInterval();
				this.dispatcherTimer.Start();
			}
		}

		private void menuItemFrameRateStop_Click(object sender, RoutedEventArgs e)
		{
			this.ResetImageUpdateTimer(MyTimerIntervalMillisec.ForStop);
		}

		private void menuItemFrameRate10fps_Click(object sender, RoutedEventArgs e)
		{
			this.ResetImageUpdateTimer(MyTimerIntervalMillisec.For10fps);
		}

		private void menuItemFrameRate20fps_Click(object sender, RoutedEventArgs e)
		{
			this.ResetImageUpdateTimer(MyTimerIntervalMillisec.For20fps);
		}
	}


	// MenuItem.Command にバインドできるクラス。
	internal class WindowInfo : ICommand
	{
		// ICommand の直接実装だけでは、メニューのチェック状態を制御できない。

		IntPtr windowHandle;

		public WindowInfo(IntPtr hwnd)
		{
			this.WindowHandle = hwnd;
		}

		public static string CreateMenuTextForWindow(IntPtr hwnd)
		{
			return MyMiscHelpers.MyWin32InteropHelper.GetWindowText(hwnd) ??
				ThisAppResources.InvalidWindowHandle;
		}

		public IntPtr WindowHandle
		{
			get { return this.windowHandle; }
			protected set
			{
				this.windowHandle = value;
				if (this.CanExecuteChanged != null)
				{
					this.CanExecuteChanged(this, EventArgs.Empty);
				}
			}
		}

		public override string ToString()
		{
			// MenuItem.ItemsSource に直接バインドする場合、ToString() がラベルに使われる。
			return CreateMenuTextForWindow(this.WindowHandle);
		}

		public bool CanExecute(Object parameter)
		{
			return true;
		}

		public void Execute(Object parameter)
		{
			// Click イベント オブジェクトは管理せず、MenuItem.Click に直接処理をバインドする。ICommand の意味がない……
		}

		public event EventHandler CanExecuteChanged;
	}

	/// <summary>
	/// シリアライズ可能な設定データを管理する。プリセット保存対象。
	/// </summary>
	internal class SerializableSettingsInfo
	{
		const char separator = ';';

		static readonly Regex regexIsHorizontalReversed;
		static readonly Regex regexIsVerticalReversed;
		static readonly Regex regexClippingRect;
		static readonly Regex regexIsMainWindowMaximized;
		static readonly Regex regexMainWindowBounds;

		public bool IsHorizontalReversed { get; set; }
		public bool IsVerticalReversed { get; set; }
		public Int32Rect ClippingRect { get; set; }
		public bool IsMainWindowMaximized { get; set; }
		public Int32Rect MainWindowBounds { get; set; }

		static string CreateElementRegex(string targetName)
		{
			return @"\s*" + targetName + @"\s*\=\s*(.+)\s*";
		}

		static SerializableSettingsInfo()
		{
			// 文字列解析用正規表現をコンパイルしておく。
			regexIsHorizontalReversed = new Regex(CreateElementRegex("IsHorizontalReversed"),
				RegexOptions.IgnoreCase | RegexOptions.ECMAScript | RegexOptions.Compiled);
			regexIsVerticalReversed = new Regex(CreateElementRegex("IsVerticalReversed"),
				RegexOptions.IgnoreCase | RegexOptions.ECMAScript | RegexOptions.Compiled);
			regexClippingRect = new Regex(@"\s*ClippingRect\s*\=\s*(.+)\s*",
				RegexOptions.IgnoreCase | RegexOptions.ECMAScript | RegexOptions.Compiled);
			regexIsMainWindowMaximized = new Regex(@"\s*IsMainWindowMaximized\s*\=\s*(.+)\s*",
				RegexOptions.IgnoreCase | RegexOptions.ECMAScript | RegexOptions.Compiled);
			regexMainWindowBounds = new Regex(@"\s*MainWindowBounds\s*\=\s*(.+)\s*",
				RegexOptions.IgnoreCase | RegexOptions.ECMAScript | RegexOptions.Compiled);
		}

		public SerializableSettingsInfo()
		{
		}

		public SerializableSettingsInfo Clone()
		{
			// TODO: 参照型をメンバーに追加した場合、実装を修正する必要がある。
			return (SerializableSettingsInfo)base.MemberwiseClone();
		}

		public void Reset()
		{
			this.IsHorizontalReversed = false;
			this.IsVerticalReversed = false;
			this.ClippingRect = Int32Rect.Empty;
			this.IsMainWindowMaximized = false;
			this.MainWindowBounds = Int32Rect.Empty;
		}

		public void CopyFrom(SerializableSettingsInfo src)
		{
			this.IsHorizontalReversed = src.IsHorizontalReversed;
			this.IsVerticalReversed = src.IsVerticalReversed;
			this.ClippingRect = src.ClippingRect;
			this.IsMainWindowMaximized = src.IsMainWindowMaximized;
			this.MainWindowBounds = src.MainWindowBounds;
		}

		public string ToStringLine()
		{
			// XML 要素値として書き出すため、インライン文字列化する。
			// XML ファイルを直接見たときに各値の意味が分かりやすいように、
			// 単純な CSV ではなくセミコロン区切りの独自フォーマット文字列に変換する。
			return
				"IsHorizontalReversed = " + this.IsHorizontalReversed.ToString() + separator + " " +
				"IsVerticalReversed = " + this.IsVerticalReversed.ToString() + separator + " " +
				"ClippingRect = " + this.ClippingRect.ToString() + separator + " " +
				"IsMainWindowMaximized = " + this.IsMainWindowMaximized.ToString() + separator + " " +
				"MainWindowBounds = " + this.MainWindowBounds.ToString() + separator + " " +
				null;
		}

		public static SerializableSettingsInfo FromStringLine(string src)
		{
			// 正規表現を使ってパースする。
			// HACK: もう少しスマートに実装したい……
			try
			{
				var newInfo = new SerializableSettingsInfo();
				var strSet = src.Split(separator);
				foreach (string s in strSet)
				{
					{
						var match = regexIsHorizontalReversed.Match(s);
						if (match.Success)
						{
							//System.Diagnostics.Debug.WriteLine(match.Groups[1]);
							newInfo.IsHorizontalReversed = Convert.ToBoolean(match.Groups[1].Value);
						}
					}
					{
						var match = regexIsVerticalReversed.Match(s);
						if (match.Success)
						{
							//System.Diagnostics.Debug.WriteLine(match.Groups[1]);
							newInfo.IsVerticalReversed = Convert.ToBoolean(match.Groups[1].Value);
						}
					}
					{
						var match = regexClippingRect.Match(s);
						if (match.Success)
						{
							//System.Diagnostics.Debug.WriteLine(match.Groups[1]);
							newInfo.ClippingRect = Int32Rect.Parse(match.Groups[1].Value);
						}
					}
					{
						var match = regexIsMainWindowMaximized.Match(s);
						if (match.Success)
						{
							//System.Diagnostics.Debug.WriteLine(match.Groups[1]);
							newInfo.IsMainWindowMaximized = Convert.ToBoolean(match.Groups[1].Value);
						}
					}
					{
						var match = regexMainWindowBounds.Match(s);
						if (match.Success)
						{
							//System.Diagnostics.Debug.WriteLine(match.Groups[1]);
							newInfo.MainWindowBounds = Int32Rect.Parse(match.Groups[1].Value);
						}
					}
				}
				return newInfo;
			}
			catch (Exception err)
			{
				System.Diagnostics.Debug.WriteLine(err.Message);
				return null;
			}
		}

		public void ToggleHorizontalReversed() { this.IsHorizontalReversed = !this.IsHorizontalReversed; }
		public void ToggleVerticalReversed() { this.IsVerticalReversed = !this.IsVerticalReversed; }
	}

	/// <summary>
	/// アプリ終了時に保存されない、揮発性の設定データを管理する。データ バインディングに使う。
	/// </summary>
	internal class NonserializableSettingsInfo : MyWpfHelpers.MyNotifyPropertyChangedBase
	{
		bool isTransparentMode = false;
		double imageOpacity = 1;

		public NonserializableSettingsInfo()
		{
		}

		public bool IsTransparentMode
		{
			get { return this.isTransparentMode; }
			set
			{
				if (!Equals(this.isTransparentMode, value))
				{
					this.isTransparentMode = value;
					this.RaisePropertyChanged();
				}
			}
		}

		public double ImageOpacity
		{
			get { return this.imageOpacity; }
			set
			{
				if (!Equals(this.imageOpacity, value))
				{
					this.imageOpacity = value;
					this.RaisePropertyChanged();
				}
			}
		}

		public void Reset()
		{
			this.IsTransparentMode = false;
			this.ImageOpacity = 1;
		}
	}
}

namespace MyWpfHelpers
{
	/// <summary>
	/// INotifyPropertyChanged の実装を補助する抽象クラス。
	/// </summary>
	public abstract class MyNotifyPropertyChangedBase : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;
		// Target から Source へのバインディングだけであれば、INotifyPropertyChanged を実装する必要はない。また、自動プロパティでも OK。
		// Source から Target へのバインディングもサポートして双方向バインディングするためには、
		// INotifyPropertyChanged の実装が必須であり、また自動プロパティが使えないので注意。
		// なお、OnPropertyChanged() 仮想メソッドの個別実装は骨が折れるので、System.Linq.Expressions によるバインディング ヘルパーを使う。
		// MyMiscHelpers.MyGenericsHelper.GetMemberName() にラムダ式を渡すことで、プロパティの文字列表現を取得できる。
		// C# 5.0 であれば、Caller Info 属性を使うともっと簡潔かつ効率的に実装できそう。
		// Caller Info は C/C++ の __FILE__ や __LINE__ 同様コンパイル時に処理されるので、リフレクションよりも実行効率がよい。

		protected void NotifyPropertyChanged(string propertyName)
		{
			if (this.PropertyChanged != null)
			{
				this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}

		protected void RaisePropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
		{
			if (this.PropertyChanged != null)
			{
				this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}
	}
}
