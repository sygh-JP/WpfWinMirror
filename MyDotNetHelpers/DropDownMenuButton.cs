using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace MyWpfCtrls
{
	// 参考：
	// http://akabeko.me/blog/2009/10/wpf-%E3%81%A7-dropdown-%E3%83%A1%E3%83%8B%E3%83%A5%E3%83%BC%E3%83%9C%E3%82%BF%E3%83%B3/

	/// <summary>
	/// ドロップ ダウン メニューを表示するためのボタン コントロール クラスです。
	/// </summary>
	public sealed class DropDownMenuButton
		: Button
	//: System.Windows.Controls.Primitives.ToggleButton
	{
		/// <summary>
		/// インスタンスを初期化します。
		/// </summary>
		public DropDownMenuButton()
		{
			//var binding = new Binding("DropDownContextMenu.IsOpen") { Source = this, Mode = BindingMode.TwoWay };
			//this.SetBinding(DropDownMenuButton.IsCheckedProperty, binding);
		}

		// FrameworkElement.ContextMenu との衝突を避けるため、別名でプロパティを定義。
		// 右クリックでは表示しない。

		/// <summary>
		/// ドロップ ダウンとして表示するコンテキスト メニューを取得または設定します。
		/// </summary>
		public ContextMenu DropDownContextMenu
		{
#if true
			get
			{
				return this.GetValue(DropDownContextMenuProperty) as ContextMenu;
			}
			set
			{
				this.SetValue(DropDownContextMenuProperty, value);
			}
#else
			get; set;
#endif
		}

		/// <summary>
		/// コントロールがクリックされた時のイベントです。
		/// </summary>
		protected override void OnClick()
		{
			//base.OnClick(); // 不要。

			if (this.DropDownContextMenu == null) { return; }

			// NOTE: 常に IsOpen == False, Visibility == Visible になる。つまり、ボタンのクリックで IsOpen を True から False にトグルすることはできない。
			// メニューを非表示にしようとして、ボタンが配置されている領域をクリックすると、そのタイミングでメニューがクローズされ、
			// このイベントハンドラーに入ったタイミングではすでに IsOpen が False になっている模様。
			// 範囲外クリックや Esc キーなどでコンテキスト メニューは閉じることができるが、Firefox などのように、バーガーメニューを開いている状態で
			// バーガーボタン（の領域）をそのままクリックして閉じることもできたほうが便利。
			// TODO: バインディングによる制御はあきらめて、ContextMenu の Loaded/Closed イベントで、ボタンの IsEnabled を制御するとよさげ。
			System.Diagnostics.Debug.WriteLine("IsOpen = " + this.DropDownContextMenu.IsOpen);
			System.Diagnostics.Debug.WriteLine("Visibility = " + this.DropDownContextMenu.Visibility);

			this.DropDownContextMenu.PlacementTarget = this;
			this.DropDownContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
			this.DropDownContextMenu.IsOpen = true;
		}

#if true
		/// <summary>
		/// ドロップ ダウンとして表示するメニューを表す依存プロパティです。
		/// </summary>
		public static readonly DependencyProperty DropDownContextMenuProperty = DependencyProperty.Register("DropDownContextMenu", typeof(ContextMenu), typeof(DropDownMenuButton), new UIPropertyMetadata(null));
#endif
	}
}
