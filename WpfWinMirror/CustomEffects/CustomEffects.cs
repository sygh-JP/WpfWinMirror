using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace WpfWinMirror.CustomEffects
{
	// 参照アセンブリのリソースの URI を絶対パスで指定する。
	// ImageSource として使える BitmapImage も同様に Uri を指定して構築できる。
	// [ビルド アクション] を [Resource] にしておくこと。

	class GrayscaleEffect : ShaderEffect
	{
		public GrayscaleEffect()
		{
			var ps = new PixelShader();
			ps.UriSource = new Uri("pack://application:,,,/WpfWinMirror;component/CustomEffects/GrayscaleEffect.psbin");
			this.PixelShader = ps;
			UpdateShaderValue(InputProperty);
		}

		public Brush Input
		{
			get { return (Brush)GetValue(InputProperty); }
			set { SetValue(InputProperty, value); }
		}

		public static readonly DependencyProperty InputProperty = ShaderEffect.RegisterPixelShaderSamplerProperty("Input", typeof(GrayscaleEffect), 0);
	}

	/// <summary>
	/// ソース ピクセルの RGB 値を反転させる（ネガポジ反転）。
	/// </summary>
	class NPInvertEffect : ShaderEffect
	{
		public NPInvertEffect()
		{
			var ps = new PixelShader();
			ps.UriSource = new Uri("pack://application:,,,/WpfWinMirror;component/CustomEffects/NPInvertEffect.psbin");
			this.PixelShader = ps;
			UpdateShaderValue(InputProperty);
		}

		public Brush Input
		{
			get { return (Brush)GetValue(InputProperty); }
			set { SetValue(InputProperty, value); }
		}

		public static readonly DependencyProperty InputProperty = ShaderEffect.RegisterPixelShaderSamplerProperty("Input", typeof(NPInvertEffect), 0);
	}

	/// <summary>
	/// ソース ピクセルが明るいほど不透明になる。
	/// </summary>
	class BrightnessToOpacityEffect : ShaderEffect
	{
		public BrightnessToOpacityEffect()
		{
			var ps = new PixelShader();
			ps.UriSource = new Uri("pack://application:,,,/WpfWinMirror;component/CustomEffects/BrightnessToOpacityEffect.psbin");
			this.PixelShader = ps;
			UpdateShaderValue(InputProperty);
		}

		public Brush Input
		{
			get { return (Brush)GetValue(InputProperty); }
			set { SetValue(InputProperty, value); }
		}

		public static readonly DependencyProperty InputProperty = ShaderEffect.RegisterPixelShaderSamplerProperty("Input", typeof(BrightnessToOpacityEffect), 0);
	}

	/// <summary>
	/// ソース ピクセルが暗いほど不透明になる。
	/// </summary>
	class DarknessToOpacityEffect : ShaderEffect
	{
		public DarknessToOpacityEffect()
		{
			var ps = new PixelShader();
			ps.UriSource = new Uri("pack://application:,,,/WpfWinMirror;component/CustomEffects/DarknessToOpacityEffect.psbin");
			this.PixelShader = ps;
			UpdateShaderValue(InputProperty);
		}

		public Brush Input
		{
			get { return (Brush)GetValue(InputProperty); }
			set { SetValue(InputProperty, value); }
		}

		public static readonly DependencyProperty InputProperty = ShaderEffect.RegisterPixelShaderSamplerProperty("Input", typeof(DarknessToOpacityEffect), 0);
	}
}
