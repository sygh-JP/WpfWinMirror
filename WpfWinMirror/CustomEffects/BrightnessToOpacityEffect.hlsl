#include "Common.hlsli"

sampler2D Input : register(S0);

float4 main(float2 uv : TEXCOORD) : COLOR
{
	float4 color = tex2D(Input, uv.xy);
	// Brightness to opacity.
	color.a = dot(color.rgb, GrayscaleFactor);
	color.rgb *= color.a; // Alpha pre-multiplied color for WPF/D2D (PBGRA)
	return color;
}
