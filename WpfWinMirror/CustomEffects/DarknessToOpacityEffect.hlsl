#include "Common.hlsli"

sampler2D Input : register(S0);

float4 main(float2 uv : TEXCOORD) : COLOR
{
	float4 color = tex2D(Input, uv.xy);
	// Darkness to opacity.
	color.a = 1 - dot(color.rgb, GrayscaleFactor);
	color.rgb *= color.a; // Alpha pre-multiplied color for WPF/D2D (PBGRA)
	return color;
}
