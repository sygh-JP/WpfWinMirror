sampler2D Input : register(S0);

static const float3 Ones3 = { 1, 1, 1 };

float4 main(float2 uv : TEXCOORD) : COLOR
{
	float4 color = tex2D(Input, uv.xy);
	// Invert RGB.
	color.rgb = Ones3 - color.rgb;
	return color;
}
