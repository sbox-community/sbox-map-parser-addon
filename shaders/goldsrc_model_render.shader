//=========================================================================================================================
// Optional
//=========================================================================================================================
HEADER
{
	Description = "GoldSrc Model Render Shader";
	Version = 1;
}

MODES
{
	VrForward();
}

//=========================================================================================================================
COMMON
{
#include "system.fxc"
#include "vr_common.fxc"
	//#define S_TRANSLUCENT 1
	//#define BLEND_MODE_ALREADY_SET
	//#define COLOR_WRITE_ALREADY_SET

	struct VS_INPUT
	{
		float4 vPositionOs : POSITION < Semantic(PosXyz); > ;
		float4 vTextureCoords : TEXCOORD0 < Semantic(LowPrecisionUv); > ;
	};

	struct PS_INPUT
	{
		float4 vTextureCoords : TEXCOORD0;
		float3 vPositionWs : TEXCOORD2;

#if ( PROGRAM == VFX_PROGRAM_VS )
		float4 vPositionPs	: SV_Position;
#endif
	};
}
VS
{
	PS_INPUT MainVs(const VS_INPUT i)
	{
		PS_INPUT o;

		o.vPositionWs = i.vPositionOs.xyz;
		o.vPositionPs = Position3WsToPs(i.vPositionOs.xyz);

		o.vTextureCoords = i.vTextureCoords;

		return o;
	}
}

//=========================================================================================================================

PS
{
	RenderState(BlendEnable, true);
	RenderState(SrcBlend, SRC_ALPHA);
	RenderState(DstBlend, INV_SRC_ALPHA);
	RenderState(AlphaToCoverageEnable, true);
	RenderState(ColorWriteEnable0, RGBA);

	SamplerState TextureFiltering < Filter(POINT); > ;

	CreateTexture2D(u_TextureDiffuse) < Attribute("TextureDiffuse"); SrgbRead(true); Filter(MIN_MAG_LINEAR_MIP_POINT); > ;

	bool hlStylePixel < Attribute("Pixelation"); Default(0); > ;

	float Opacity < Attribute("Opacity"); Default(1.0); > ;

	float4 Color < Attribute("Color"); Default4(1.0,1.0,1.0,1.0); > ; // Alpha channel is for brightness

	float4 MainPs(PS_INPUT i) : SV_Target0
	{
		float4 t_DiffuseSample;
		float2 t_TexCoordDiffuse = i.vTextureCoords.xy / TextureDimensions2D(u_TextureDiffuse,0).xy;

		if (hlStylePixel)
			t_DiffuseSample = Tex2DS(u_TextureDiffuse, TextureFiltering, t_TexCoordDiffuse.xy);
		else
			t_DiffuseSample = Tex2D(u_TextureDiffuse, t_TexCoordDiffuse.xy);

		if (t_DiffuseSample.a < 0.1)
			return float4(0, 0, 0, 0);

		return float4(t_DiffuseSample.rgb * Color.rgb * Color.a, t_DiffuseSample.a * Opacity ) ;
	}
}
