using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace WpfWinMirror
{
	/// <summary>
	/// AboutWindow.xaml の相互作用ロジック
	/// </summary>
	public partial class AboutWindow : Window
	{
		public AboutWindow()
		{
			InitializeComponent();

			var asm = System.Reflection.Assembly.GetExecutingAssembly();
			var versionInfo = asm.GetName().Version;
			System.Diagnostics.Debug.Assert(versionInfo != null);
			this.textblockVersionNumber.Text = String.Format("Version {0}.{1}", versionInfo.Major, versionInfo.Minor);

			var asmCopyright = (System.Reflection.AssemblyCopyrightAttribute)
				Attribute.GetCustomAttribute(asm,
					typeof(System.Reflection.AssemblyCopyrightAttribute));
			System.Diagnostics.Debug.Assert(asmCopyright != null);
			this.textblockCopyright.Text = asmCopyright.Copyright;
		}

		private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			// 右ボタンや中ボタンで発行された MouseDown イベントを処理する際にハンドラーで DragMove() を呼び出すと、
			// メイン ボタンの押下イベント以外で DragMove() を呼び出すことはできないという旨の例外が出るが、
			// Windows 設定で右ボタンと左ボタンを入れ替えていた場合はどうなる？　WPF フレームワークはちゃんと入れ替えてくれるのか？
			this.DragMove();
		}

		private void Window_KeyDown(object sender, KeyEventArgs e)
		{
			switch (e.Key)
			{
				case Key.Enter:
				case Key.Escape:
					this.Close();
					break;
				default:
					break;
			}
		}

		private void buttonClose_Click(object sender, RoutedEventArgs e)
		{
			this.Close();
		}
	}
}
