// sbox.Community � 2023-2024

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
		//float3 vPositionWs : TEXCOORD2;

#if ( PROGRAM == VFX_PROGRAM_VS )
		float4 vPositionPs	: SV_Position;
#endif
	};
}
VS
{
	float3 Position < Attribute("Position"); > ;
	float3 Angles < Attribute("Angles"); > ;

	float3x3 RotationMatrixFromAngles(float3 angles)
	{
		float3 c = cos(angles);
		float3 s = sin(angles);
		float3x3 rotationMatrix = float3x3(
			c.y * c.z, c.y * s.z, -s.y,
			-c.x * s.z + c.z * s.x * s.y, c.x * c.z + s.x * s.y * s.z, c.y * s.x,
			s.x * s.z + c.x * c.z * s.y, -c.z * s.x + c.x * s.y * s.z, c.x * c.y
			);
		return rotationMatrix;
	}

	PS_INPUT MainVs(const VS_INPUT i)
	{
		PS_INPUT o;

		float3x3 rotationMatrix = RotationMatrixFromAngles(radians(float3(Angles.z, Angles.x, Angles.y)));

		float3 vPositionWs = mul(i.vPositionOs.xyz,rotationMatrix) + Position;
		o.vPositionPs = Position3WsToPs(vPositionWs.xyz);

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
