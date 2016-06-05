#include "Common.hlsli"

sampler2D Input : register(S0);

float4 main(float2 uv : TEXCOORD) : COLOR
{
	float4 color = tex2D(Input, uv.xy);
	// Make grayscale color.
	color.rgb = dot(color.rgb, GrayscaleFactor);
	return color;
}
