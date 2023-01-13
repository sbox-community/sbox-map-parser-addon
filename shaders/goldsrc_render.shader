//=========================================================================================================================
// Optional
//=========================================================================================================================
HEADER
{
	//DevShader = true;
	CompileTargets = (IS_SM_50 && (PC || VULKAN));
	Description = "GoldSrc Render Shader";
	Version = 1;
}


FEATURES
{
	#include "common/features.hlsl"
	//Feature(F_MORPH_SUPPORTED, 0..1, "TEST");
}

//=========================================================================================================================
COMMON
{

	#include "common/shared.hlsl"
	//#include "system.fxc"
	//#include "common.fxc" 

	struct VS_INPUT
	{
		#include "common/vertexinput.hlsl"
		//#include "vr_common_vs_input.fxc"
	
		//float3 vPositionOs : POSITION < Semantic(PosXyz); > ;
		//float2 vTexCoord : TEXCOORD0 < Semantic(LowPrecisionUv); > ;

	};

	struct PS_INPUT
	{
		#include "common/pixelinput.hlsl"
		//#include "vr_common_ps_input.fxc"
		//float4 vPositionSs		: SV_ScreenPosition;
			
		//float2 vTextureCoords : TEXCOORD0;
		//float4 vPositionPs		: SV_POSITION;
	};
}
VS
{
	#include "common/vertex.hlsl"
	//#include "vr_common_vs_code.fxc"

	PS_INPUT MainVs(INSTANCED_SHADER_PARAMS(VS_INPUT input))
	{


		PS_INPUT o = ProcessVertex(input);
		//CalculateCameraToPositionDirWs
		//o.vPositionPs = mul(g_matProjectionToWorld, float4(0,0,0, 1.0)); //float4(i.vPositionOs.xyz, 1.0f);
		//o.vPositionSs = float4(input.vPositionOs.xyz, 1.0f);
		//o.vTextureCoords = input.vTexCoord;
		return FinalizeVertex(o);

	}

}

//=========================================================================================================================

PS
{
	#include "common/pixel.hlsl"

	//CreateInputTexture2D(TextureDiffuse, Srgb, 8, "", "_color", "Color,1/1", Default3(1.0, 1.0, 1.0));
	CreateTexture2D(u_TextureDiffuse) < Attribute("ColorBuffer"); SrgbRead(true); Filter(MIN_MAG_LINEAR_MIP_POINT); AddressU(MIRROR); AddressV(MIRROR); > ; //OutputFormat(DXT5);
	//TextureAttribute(u_TextureDiffuse, u_TextureDiffuse);

	//CreateInputTexture2D(TextureLightmap, Srgb, 8, "", "_color", "Color,1/2", Default3(1.0, 1.0, 1.0));
	CreateTexture2D(u_TextureLightmap) <  Attribute("ColorBuffer"); SrgbRead(true); Filter(MIN_MAG_LINEAR_MIP_POINT); AddressU(MIRROR); AddressV(MIRROR); > ;
	//TextureAttribute(u_TextureLightmap, u_TextureLightmap);

	float4 MainPs(PS_INPUT i) : SV_Target0
	{
		Material m = GatherMaterial(i);

		float2 t_TexCoordDiffuse = i.vTextureCoords.xy / TextureDimensions2D(u_TextureDiffuse,0).xy;
		float4 t_DiffuseSample = Tex2D(u_TextureDiffuse, t_TexCoordDiffuse.xy);

		if (t_DiffuseSample.a < 0.1)
			return float4(0, 0, 0, 0);

		float2 t_TexCoordLightmap = i.vTextureCoords.xy / TextureDimensions2D(u_TextureLightmap, 0).xy;
		float4 t_LightmapSample = Tex2D(u_TextureLightmap, t_TexCoordLightmap.xy);

		float4 o = FinalizePixelMaterial(i, m);
		o.rgba = t_DiffuseSample * t_LightmapSample;
		return o;
	}
}


