// sbox.Community © 2023-2024

//=========================================================================================================================
// Optional
//=========================================================================================================================
HEADER
{
	Description = "GoldSrc Sky Shader";
}

//=========================================================================================================================
// Optional
//=========================================================================================================================
FEATURES
{
	#include "common/features.hlsl"
}

//=========================================================================================================================
// Optional
//=========================================================================================================================
MODES
{
	VrForward();													// Indicates this shader will be used for main rendering
	Depth("vr_depth_only.shader"); 									// Shader that will be used for shadowing and depth prepass
	ToolsVis(S_MODE_TOOLS_VIS); 									// Ability to see in the editor
	ToolsWireframe("vr_tools_wireframe.shader"); 					// Allows for mat_wireframe to work
	ToolsShadingComplexity("vr_tools_shading_complexity.shader"); 	// Shows how expensive drawing is in debug view
}

//=========================================================================================================================
COMMON
{
	#include "common/shared.hlsl"
}

//=========================================================================================================================

struct VertexInput
{
#include "common/vertexinput.hlsl"
};

//=========================================================================================================================

struct PixelInput
{
#include "common/pixelinput.hlsl"
};

//=========================================================================================================================

VS
{
	#include "common/vertex.hlsl"
	//
	// Main
	//

	float3 g_vPosition < Attribute("g_vPosition"); > ;

	PixelInput MainVs(INSTANCED_SHADER_PARAMS(VS_INPUT i))
	{
		PixelInput o = ProcessVertex(i);

		float3 vPositionWs = g_vCameraPositionWs.xyz + i.vPositionOs.xyz + g_vPosition;

		o.vPositionPs.xyzw = Position3WsToPs(vPositionWs.xyz);

		return o;
	}
}

//=========================================================================================================================


PS
{ 
	CreateTexture2D(u_TextureDiffuse) < Attribute("g_vSkyTexture"); SrgbRead(true); Filter(MIN_MAG_LINEAR_MIP_POINT); AddressU(CLAMP); AddressV(CLAMP); > ;

	float4 MainPs(PixelInput i) : SV_Target0
	{
		float2 t_TexCoordDiffuse = i.vTextureCoords.xy;
		float4 t_DiffuseSample = Tex2D(u_TextureDiffuse, t_TexCoordDiffuse.xy);

		return t_DiffuseSample;
	}
}