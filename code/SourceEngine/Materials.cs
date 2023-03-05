// sbox.Community © 2023-2024

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static MapParser.GoldSrc.BSPFile;
using static MapParser.SourceEngine.BSPFile;
using static MapParser.SourceEngine.Main;
using static MapParser.SourceEngine.Materials;
using static MapParser.SourceEngine.VmtParser;

namespace MapParser.SourceEngine
{
	public static class Materials
	{



		private static Color scratchColor = new Color( 255, 255, 255, 255 );
		/*private static readonly TextureMapping[] textureMappings = Enumerable.Range( 0, 15 ).Select( i => new TextureMapping() ).ToArray();

		private static void resetTextureMappings( TextureMapping[] m )
		{
			foreach ( var mapping in m )
			{
				mapping.Reset();
			}
		}*/

		public enum StaticLightingMode
		{
			None,
			StudioVertexLighting,
			StudioVertexLighting3,
			StudioAmbientCube,
		}

		public enum SkinningMode
		{
			None,
			Rigid,
			Smooth,
		}

		public enum LateBindingTexture
		{
			Camera,
			FramebufferColor,
			FramebufferDepth,
			WaterReflection,
			ProjectedLightDepth,
		}


		// https://github.com/ValveSoftware/source-sdk-2013/blob/master/sp/src/public/const.h#L340-L387
		public enum RenderMode
		{
			Normal,
			TransColor,
			TransTexture,
			Glow,
			TransAlpha,
			TransAdd,
			Environmental,
			TransAddFrameBlend,
			TransAddAlphaAdd,
			WorldGlow,
			None,
		}

		public class FogParams
		{
			public Color color;
			public float start = 0;
			public float end = 0;
			public float maxdensity = 0;

			public FogParams( Color? color = null )
			{
				this.color =  color is not null ? color.Value : Color.White ; //colorNewCopy
			}

			public void copy( FogParams o )
			{
				this.color = o.color; //colorCopy
				this.start = o.start;
				this.end = o.end;
				this.maxdensity = o.maxdensity;
			}
		}











		public class Parameter
		{
			public virtual void Parse( string s ) { }
			public virtual Parameter Index( int i )	{ throw new Exception( "whoops" ); }
			public virtual void Set( Parameter param ) { }
		}

		public class ParameterNumber : Parameter
		{
			public float value;
			private bool dynamic;

			public ParameterNumber( float value, bool dynamic = true )
			{
				this.value = value;
				this.dynamic = dynamic;
			}

			public override void Parse( string S )
			{
				// Numbers and vectors are the same thing inside the Source engine, where numbers just are the first value in a vector.
				float[] v = VmtParser.VmtParseVector( S );
				this.value = v[0];
			}

			public override Parameter Index( int i )
			{
				throw new Exception( "whoops" );
			}

			public override void Set( Parameter param )
			{
				//Debug.Assert( param is ParameterNumber );
				if( param is not ParameterNumber )
				{
					return; // HATA
				}
				//Debug.Assert( this.dynamic );
				if( !this.dynamic )
				{
					return; //HATA
				}
				this.value = ((ParameterNumber)param).value;
			}
		}

		class ParameterBoolean : ParameterNumber
		{
			public ParameterBoolean( bool value, bool dynamic = true ) : base( value ? 1 : 0, dynamic ) { }

			public bool getBool()
			{
				return this.value != 0;
			}
		}
		public class EntityMaterialParameters
		{
			public float[] position = new float[3];
			public float animationStartTime = 0;
			public int textureFrameIndex = 0;
			public Color blendColor = Color.White;//ColorNewCopy( White );
			//public LightCache lightCache = null;
			public float randomNumber = (float)new Random().NextDouble();
		}

		


	

		public class ParameterTexture : Parameter
		{

			public VTF Texture { get; set; } = null;

			public string Ref { get; set; } = null;

			public bool IsSRGB { get; }

			public bool IsEnvmap { get; }

			public ParameterTexture( bool isSRGB = false, bool isEnvmap = false, string refValue = null )
			{
				IsSRGB = isSRGB;
				IsEnvmap = isEnvmap;
				Ref = refValue;
			}

			public override void Parse( string s )
			{
				Ref = s;
			}

			public override Parameter Index( int i )
			{
				throw new Exception( "whoops" );
			}

			public override void Set( Parameter param )
			{
				// Cannot dynamically change at runtime.
				throw new Exception( "whoops" );
			}

			public async Task Fetch( MaterialCache materialCache, EntityMaterialParameters entityParams = null )
			{
				if ( Ref != null )
				{
					string filename = Ref;

					if ( IsEnvmap )
					{
						// Dynamic cubemaps.
						/*if ( filename.ToLower() == "env_cubemap" && entityParams != null && entityParams.LightCache != null && entityParams.LightCache.EnvCubemap != null )
						{
							filename = entityParams.LightCache.EnvCubemap.Filename;
						}
						else */if ( materialCache.IsUsingHDR() )
						{
							string hdrFilename = $"{filename}.hdr";
							if ( materialCache.CheckVTFExists( hdrFilename ) )
							{
								filename = hdrFilename;
							}
							else if ( !materialCache.CheckVTFExists( filename ) )
							{
								//Debugger.Break();
							}
						}
					}

					Texture = await materialCache.FetchVTF( filename, IsSRGB );
				}
			}

			public bool FillTextureMapping( TextureMapping m, int frame )
			{
				if ( Texture != null )
				{
					Texture.FillTextureMapping( m, frame );
					return true;
				}
				else
				{
					return false;
				}
			}
		}



		public class ParameterVector : Parameter
		{
			public ParameterNumber[] internalArr;

			public ParameterVector( int length, float[] values = null )
			{
				internalArr = new ParameterNumber[length];
				for ( int i = 0; i < length; i++ )
				{
					internalArr[i] = new ParameterNumber( values != null ? values[i] : 0 );
				}
			}

			public void SetArray( float[] v )
			{
				//Debug.Assert( internalArr.Length == v.Length );
				if( internalArr.Length != v.Length )
				{
					return; //HATA
				}
				for ( int i = 0; i < internalArr.Length; i++ )
				{
					internalArr[i].value = v[i];
				}
			}

			public override void Parse( string S )
			{
				float[] numbers = VmtParseVector( S );
				if ( internalArr.Length == 0 )
				{
					internalArr = new ParameterNumber[numbers.Length];
				}

				for ( int i = 0; i < internalArr.Length; i++ )
				{
					internalArr[i] = new ParameterNumber( i > numbers.Length - 1 ? numbers[0] : numbers[i] );
				}
			}

			public override ParameterNumber Index( int i )
			{
				return internalArr[i];
			}

			public override void Set( Parameter param )
			{
				if ( param is ParameterVector )
				{
					internalArr[0].value = ((ParameterVector)param).internalArr[0].value; // Fix
					internalArr[1].value = ((ParameterVector)param).internalArr[1].value;
					internalArr[2].value = ((ParameterVector)param).internalArr[2].value;
				}
				else if ( param is ParameterNumber )
				{
					float value = ((ParameterNumber)param).value;
					internalArr[0].value = value;
					internalArr[1].value = value;
					internalArr[2].value = value;
				}
				else
				{
					throw new Exception( "whoops" );
				}
			}

			public void FillColor( ref Color c, float a )
			{
				c = new Color( internalArr[0].value, internalArr[1].value, internalArr[2].value, a );
			}

			public void SetFromColor( Color c )
			{
				internalArr[0].value = c.r;
				internalArr[1].value = c.g;
				internalArr[2].value = c.b;
			}

			public void MulColor( Color c )
			{
				//Debug.Assert( internalArr.Length == 3 );
				if( internalArr.Length != 3 )
				{
					return; //HATA
				}
				c.r *= internalArr[0].value;
				c.g *= internalArr[1].value;
				c.b *= internalArr[2].value;
			}

			public float Get( int i )
			{
				return internalArr[i].value;
			}
		}


		class ParameterColor : ParameterVector
		{
			public ParameterColor( float r, float g = 0.0f, float b = 0.0f ) : base( 3 )
			{
				this.internalArr[0].value = r;
				 this.internalArr[1].value = g;
				  this.internalArr[2].value = b;
			 }
	}

		public static Parameter createParameterAuto( object value )
		{
			if ( value is string )
			{
				string S = value as string;
				float n;
				if ( float.TryParse( S, out n ) )
					return new ParameterNumber( n );

				// Try Vector
				if ( S.StartsWith( "[" ) || S.StartsWith( "{" ) )
				{
					var v = new ParameterVector( 0 );
					v.Parse( S );
					return v;
				}

				if ( S.StartsWith( "center" ) )
				{
					var v = new ParameterMatrix();
					v.Parse( S );
					return v;
				}

				var s = new ParameterString();
				s.Parse( S );
				return s;
			}

			return null;
		}

		public static string ParseKey( string key, List<string> defines )
		{
			int question = key.IndexOf( '?' );
			if ( question >= 0 )
			{
				string define = key.Substring( 0, question );

				bool negate = false;
				if ( key[0] == '!' )
				{
					define = define.Substring( 1 );
					negate = true;
				}

				bool isValid = defines.Contains( define );
				if ( negate )
					isValid = !isValid;

				if ( !isValid )
					return null;

				key = key.Substring( question + 1 );
			}

			return key;
		}

		public static void SetupParametersFromVMT( Dictionary<string, Parameter> param, Dictionary<string,object> vmt, List<string> defines ) //VMT vmt
		{
			foreach ( KeyValuePair<string, object> vmtKey in vmt )
			{
				string destKey = ParseKey( vmtKey.Key, defines );
				if ( destKey == null )
					continue;
				if ( !destKey.StartsWith( "$" ) )
					continue;

				object value = vmtKey.Value;
				if ( param.ContainsKey( destKey ) )
				{
					// Easy case -- existing parameter.
					param[destKey].Parse( (string)value );
				}
				else
				{
					// Hard case -- auto-detect type from string.
					Parameter p = createParameterAuto( value );
					if ( p != null )
					{
						param[destKey] = p;
					}
					else
					{
						Log.Info( "Could not parse parameter " + destKey + " " + value );
					}
				}
			}
		}

		class ParameterString : Parameter
		{
			public string value;

			public ParameterString( string value = "" )
			{
				this.value = value;
			}

			public override void Parse( string S )
			{
				this.value = S;
			}

			public override Parameter Index( int i )
			{
				throw new Exception( "whoops" );
			}

			public override void Set( Parameter param )
			{
				// Cannot dynamically change at runtime.
				throw new Exception( "whoops" );
			}
		}

		public enum AlphaBlendMode
		{
			None,
			Blend,
			Add,
			Glow
		}

		public static int FillGammaColor( ref float[] d, int offs, Color c, float? a = null )
		{
			d[offs++] = GammaToLinear( c.r );
			d[offs++] = GammaToLinear( c.g );
			d[offs++] = GammaToLinear( c.b );
			d[offs++] = a is null ? c.a : a.Value;
			return 4;
		}

		public static float GammaToLinear( float v )
		{
			const float gamma = 2.2f;
			return MathF.Pow( v, gamma );
		}




		public class ParameterMatrix : Parameter
		{
			public Matrix4x4 matrix = Matrix4x4.Identity;

			public void setMatrix( float cx, float cy, float sx, float sy, float r, float tx, float ty )
			{
				matrix = Matrix4x4.Identity;
				matrix.M14 = -cx;
				matrix.M24 = -cy;
				matrix.M11 = sx;
				matrix.M22 = sy;
				matrix *= Matrix4x4.CreateFromAxisAngle( Vector3.Up, 0.017453292519943295f * r  ); // fix
				var scratchMat4a = Matrix4x4.Identity;
				scratchMat4a.M14 = cx + tx;
				scratchMat4a.M24 = cy + ty;
				matrix *= scratchMat4a;
			}

			public override void Parse( string S )
			{
				// "center {} {} scale {} {} rotate {} translate {} {}"
				var matches = System.Text.RegularExpressions.Regex.Matches( S, @"([a-z]+) ([^a-z]+)" );
				float cx = 0, cy = 0, sx = 1, sy = 1, r = 0, tx = 0, ty = 0;
				foreach ( System.Text.RegularExpressions.Match match in matches )
				{
					var str = match.Groups[0].Value;
					var mode = match.Groups[1].Value;
					var items = match.Groups[2].Value;
					var values = Array.ConvertAll( items.Split( ' ' ), float.Parse );
					if ( values.Length == 1 ) values = new[] { values[0], values[0] };

					if ( mode == "center" )
					{
						cx = values[0];
						cy = values[1];
					}
					else if ( mode == "scale" )
					{
						sx = values[0];
						sy = values[1];
					}
					else if ( mode == "rotate" )
					{
						r = values[0];
					}
					else if ( mode == "translate" )
					{
						tx = values[0];
						ty = values[1];
					}
				}
				setMatrix( cx, cy, sx, sy, r, tx, ty );
			}

			public override Parameter Index( int i )
			{
				throw new NotImplementedException();
			}

			public override void Set( Parameter param )
			{
				throw new NotImplementedException();
			}
		}

		/*


			public FogParams blackFogParams = new FogParams( TransparentBlack );
		public ToneMapParams noToneMapParams = new ToneMapParams();




		*/
























		public class BaseMaterial
		{
			private bool visible = true;
			public bool hasVertexColorInput = true;
			public bool wantsLightmap = false;
			public bool wantsBumpmappedLightmap = false;
			public bool wantsTexCoord0Scale = false;
			public bool isTranslucent = false;
			public bool isIndirect = false;
			public bool isToolMaterial = false;
			public ParameterMap param = new ParameterMap();
			public EntityMaterialParameters entityParams = null;
			public SkinningMode skinningMode = SkinningMode.None;
			public VTF representativeTexture = null;

			protected bool loaded = false;
			//protected MaterialProxyDriver proxyDriver = null;
			protected Vector2 texCoord0Scale = Vector2.Zero;
			protected bool isAdditive = false;
			protected bool isToneMapped = true;
			private VMT vmt;

			public BaseMaterial( VMT vmt )
			{
				this.vmt = vmt;
				this.InitParameters();
			}

			public async Task Init( Main.SourceRenderContext renderContext )
			{
				this.setupParametersFromVMT( renderContext );
				if ( this.vmt.proxies != null )
				{
					//this.proxyDriver = renderContext.materialProxySystem.CreateProxyDriver( this, this.vmt.proxies );
				}

				this.InitStaticBeforeResourceFetch();
				//await this.FetchResources( renderContext.MaterialCache );
				this.InitStatic( renderContext.materialCache );
			}

			public bool IsMaterialLoaded()
			{
				return this.loaded;
			}

			protected void InitParameters()
			{
				var p = param;

				// Material vars
				p.value["$selfillum"] = new ParameterBoolean( false, false );
				p.value["$additive"] = new ParameterBoolean( false, false );
				p.value["$alphatest"] = new ParameterBoolean( false, false );
				p.value["$translucent"] = new ParameterBoolean( false, false );
				p.value["$basealphaenvmapmask"] = new ParameterBoolean( false, false );
				p.value["$normalmapalphaenvmapmask"] = new ParameterBoolean( false, false );
				p.value["$opaquetexture"] = new ParameterBoolean( false, false );
				p.value["$vertexcolor"] = new ParameterBoolean( false, false );
				p.value["$vertexalpha"] = new ParameterBoolean( false, false );
				p.value["$nocull"] = new ParameterBoolean( false, false );
				p.value["$nofog"] = new ParameterBoolean( false, false );
				p.value["$decal"] = new ParameterBoolean( false, false );
				p.value["$model"] = new ParameterBoolean( false, false );

				// Base parameters
				p.value["$basetexture"] = new ParameterTexture( true );
				//p.value["$basetexturetransform"] = new ParameterMatrix();
				p.value["$frame"] = new ParameterNumber( 0 );
				p.value["$color"] = new ParameterColor( 1, 1, 1 );
				p.value["$color2"] = new ParameterColor( 1, 1, 1 );
				p.value["$alpha"] = new ParameterNumber( 1 );

				// Data passed from entity system.
				p.value["$rendermode"] = new ParameterNumber( 0, false );
			}

			private void setupParametersFromVMT( Main.SourceRenderContext renderContext )
			{
				List<string> materialDefines = renderContext.materialCache.materialDefines;
				SetupParametersFromVMT( this.param.value, this.vmt.Items, materialDefines );

				string shaderTypeName = this.vmt._Root.ToLower();
				Dictionary<string, object> fallback = this.findFallbackBlock( shaderTypeName, materialDefines );
				if ( fallback != null )
					SetupParametersFromVMT( this.param.value, fallback, materialDefines );
			}

			protected void InitStaticBeforeResourceFetch()
			{
			}
			private Dictionary<string, object> findFallbackBlock( string shaderTypeName, List<string> materialDefines )
			{
				for ( int i = 0; i < materialDefines.Count(); i++ )
				{
					string suffix = materialDefines[i];
					if ( this.vmt.Items.ContainsKey( suffix ) )
						return (Dictionary<string, object>)this.vmt.Items[suffix];

					if ( this.vmt.Items.ContainsKey( $"{shaderTypeName}_{suffix}" ) )
						return (Dictionary<string, object>)this.vmt.Items[$"{shaderTypeName}_{suffix}"];
				}

				return null;
			}
			public bool IsMaterialVisible( SourceRenderContext renderContext )
			{
				if ( !visible )
					return false;

				if ( !IsMaterialLoaded() )
					return false;

				if ( isToolMaterial && !renderContext.showToolMaterials )
					return false;

				if ( paramGetBoolean( "$decal" ) && !renderContext.showDecalMaterials )
					return false;

				/*if ( renderContext.currentView.viewType == SourceEngineViewType.ShadowMap )
				{
					if ( isTranslucent )
						return false;
				}*/

				return true;
			}
			protected bool paramGetBoolean( string name )
			{
				return (this.param.value[name] as ParameterBoolean).getBool();
			}
			public void SetStaticLightingMode( StaticLightingMode staticLightingMode )
			{
				// Nothing by default.
			}
			public void ParamSetColor( string name, Color c )
			{
				(param.value[name] as ParameterColor).SetFromColor( c );
			}
			public void ParamSetNumber( string name, float v )
			{
				(param.value[name] as ParameterNumber).value = v;
			}
			public int GetNumFrames()
			{
				if ( representativeTexture != null )
					return representativeTexture.numFrames;
				else
					return 1;
			}
			public string ParamGetString( string name )
			{
				return ((ParameterString)this.param.value[name]).value;
			}
			protected ParameterTexture ParamGetTexture( string name )
			{
				return (ParameterTexture)this.param.value[name];
			}
			protected VTF ParamGetVTF( string name )
			{
				return this.ParamGetTexture( name ).Texture;
			}
			protected float paramGetNumber( string name )
			{
				return (this.param.value[name] as ParameterNumber).value;
			}

			public int paramGetInt( string name )
			{
				return (int)this.paramGetNumber( name );
			}
			public ParameterVector paramGetVector( string name )
			{
				return (this.param.value[name] as ParameterVector);
			}

			protected Matrix4x4 paramGetMatrix( string name )
			{
				return (this.param.value[name] as ParameterMatrix).matrix;
			}
			/*protected bool paramGetFlipY( SourceRenderContext renderContext, string name )
			{
				if ( !renderContext.materialCache.deviceNeedsFlipY )
					return false;

				ParameterTexture texture = this.ParamGetTexture( name );
				if ( texture == null )
					return false;

				return texture.Texture.lateBinding != null;
			}*/
			protected int paramFillVector4( float[] d, int offs, string name )
			{
				var m = (this.param.value[name] as ParameterVector).internalArr;
				//Debug.Assert(m.Length == 4);
				if( m.Length != 4 )
				{
					return -1; //HATA
				}
				return Util.FillVec4(ref d, offs, m[0].value, m[1].value, m[2].value, m[3].value);
			}
			protected int paramFillScaleBias( float[] d, int offs, string name )
			{
				var m = (this.param.value[name] as ParameterMatrix).matrix;
				// Make sure there's no rotation. We should definitely handle this eventually, though.
				//Debug.Assert( m.M12 == 0f && m.M13 == 0f );
				if( m.M12 != 0f || m.M13 != 0f )
				{
					return -1; //HATA
				}
					float scaleS = m.M11 * this.texCoord0Scale.x;
				float scaleT = m.M22 * this.texCoord0Scale.y;
				float transS = m.M41;
				float transT = m.M42;
				return Util.FillVec4( ref d, offs, scaleS, scaleT, transS, transT );
			}
			protected int paramFillTextureMatrix( float[] d, int offs, string name, bool flipY = false, float extraScale = 1.0f )
			{
				Matrix4x4 m = ((ParameterMatrix)this.param.value[name]).matrix;
				Matrix4x4 scratchMat4a = Matrix4x4.Identity;
				scratchMat4a = m;
				if ( extraScale != 1.0f )
					scratchMat4a = Matrix4x4.CreateScale( new Vector3( extraScale, extraScale, extraScale ) );
				scratchMat4a = Matrix4x4.CreateScale( new Vector3( this.texCoord0Scale.x, this.texCoord0Scale.y, 1.0f ) );
				if ( flipY )
				{
					scratchMat4a[1, 1] *= -1;
					scratchMat4a[1, 3] += 2;
				}
				return Util.FillMatrix4x2( ref d, offs, scratchMat4a );
			}

			protected int paramFillGammaColor( float[] d, int offs, string name, float alpha = 1.0f )
			{
				this.paramGetVector( name ).FillColor( ref scratchColor, alpha ); // ilginc
				return FillGammaColor( ref d, offs, scratchColor );
			}
			protected int paramFillColor( float[] d, int offs, string name, float alpha = 1.0f )
			{
				this.paramGetVector( name ).FillColor( ref scratchColor, alpha );
				return Util.FillColor( ref d, offs, in scratchColor );
			}
			/*protected bool vtfIsIndirect( VTF vtf )
			{
				// These bindings only get resolved in indirect passes...
				if ( vtf.lateBinding == LateBindingTexture.FramebufferColor )
					return true;
				if ( vtf.lateBinding == LateBindingTexture.FramebufferDepth )
					return true;
				if ( vtf.lateBinding == LateBindingTexture.WaterReflection )
					return true;

				return false;
			}*/
			protected bool textureIsIndirect( string name )
			{
				var vtf = this.ParamGetVTF( name );

				//if ( vtf != null && this.vtfIsIndirect( vtf ) )
				//	return true;

				return false;
			}
			protected bool TextureIsTranslucent( string name )
			{
				var texture = ParamGetVTF( name );

				if ( texture == null )
					return false;

				if ( texture == ParamGetVTF( "$basetexture" ) )
				{
					// Special consideration.
					if ( paramGetBoolean( "$opaquetexture" ) )
						return false;
					if ( paramGetBoolean( "$selfillum" ) || paramGetBoolean( "$basealphaenvmapmask" ) )
						return false;
					if ( !(paramGetBoolean( "$translucent" ) || paramGetBoolean( "$alphatest" )) )
						return false;
				}

				return texture.IsTranslucent();
			}
			/*protected void SetSkinningMode( UberShaderInstanceBasic p )
			{
				p.SetDefineString( "SKINNING_MODE", "" + SkinningMode );
			}

			protected void SetFogMode( UberShaderInstanceBasic p )
			{
				p.SetDefineBool( "USE_FOG", !paramGetBoolean( "$nofog" ) );
			}
			protected void SetCullMode( ref GfxMegaStateDescriptor megaStateFlags )
			{
				megaStateFlags.FrontFace = GfxFrontFaceMode.CW;

				if ( paramGetBoolean( "$nocull" ) )
					megaStateFlags.CullMode = GfxCullMode.None;
			}
			protected void setAlphaBlendMode( ref GfxMegaStateDescriptor megaStateFlags, AlphaBlendMode alphaBlendMode )
			{
				if ( alphaBlendMode == AlphaBlendMode.Glow )
				{
					setAttachmentStateSimple( ref megaStateFlags, new AttachmentState
					{
						blendMode = GfxBlendMode.Add,
						blendSrcFactor = GfxBlendFactor.SrcAlpha,
						blendDstFactor = GfxBlendFactor.One,
					} );
					megaStateFlags.depthWrite = false;
					this.isAdditive = true;
					this.isTranslucent = true;
				}
				else if ( alphaBlendMode == AlphaBlendMode.Blend )
				{
					setAttachmentStateSimple( ref megaStateFlags, new AttachmentState
					{
						blendMode = GfxBlendMode.Add,
						blendSrcFactor = GfxBlendFactor.SrcAlpha,
						blendDstFactor = GfxBlendFactor.OneMinusSrcAlpha,
					} );
					megaStateFlags.depthWrite = false;
					this.isTranslucent = true;
				}
				else if ( alphaBlendMode == AlphaBlendMode.Add )
				{
					setAttachmentStateSimple( ref megaStateFlags, new AttachmentState
					{
						blendMode = GfxBlendMode.Add,
						blendSrcFactor = GfxBlendFactor.One,
						blendDstFactor = GfxBlendFactor.One,
					} );
					megaStateFlags.depthWrite = false;
					this.isAdditive = true;
					this.isTranslucent = true;
				}
				else if ( alphaBlendMode == AlphaBlendMode.None )
				{
					setAttachmentStateSimple( ref megaStateFlags, new AttachmentState
					{
						blendMode = GfxBlendMode.Add,
						blendSrcFactor = GfxBlendFactor.One,
						blendDstFactor = GfxBlendFactor.Zero,
					} );
					megaStateFlags.depthWrite = true;
					this.isTranslucent = false;
				}
				else
				{
					throw new Exception( "whoops" );
				}
			}*/


			protected AlphaBlendMode getAlphaBlendMode( bool isTextureTranslucent )
			{
				bool isTranslucent = isTextureTranslucent;

				if ( paramGetBoolean( "$vertexalpha" ) )
				{
					isTranslucent = true;
				}

				if ( isTranslucent && paramGetBoolean( "$additive" ) )
				{
					return AlphaBlendMode.Glow;
				}
				else if ( paramGetBoolean( "$additive" ) )
				{
					return AlphaBlendMode.Add;
				}
				else if ( isTranslucent )
				{
					return AlphaBlendMode.Blend;
				}
				else
				{
					return AlphaBlendMode.None;
				}
			}

			/*protected async Task FetchResources( MaterialCache materialCache )
			{
				// Load all the texture parameters we have.
				var promises = new List<Task>();
				foreach ( var kvp in param.value )
				{
					var v = kvp.Value;
					if ( v is ParameterTexture parameterTexture )
						promises.Add( parameterTexture.Fetch( materialCache, entityParams ) );
				}
				await Task.WhenAll( promises );
				loaded = true;
			}*/
			private VTF ParamGetVTFPossiblyMissing( string name )
			{
				if ( !param.value.TryGetValue( name, out var value ) || !(value is ParameterTexture) )
					return null;
				return ParamGetVTF( name );
			}
			private bool VtfIsRepresentative( VTF vtf )
			{
				if ( vtf == null )
					return false;

				//if ( VtfIsIndirect( vtf ) )
				//	return false;

				return true;
			}
			private VTF CalcRepresentativeTexture()
			{
				VTF vtf = null;

				vtf = ParamGetVTFPossiblyMissing( "$basetexture" );
				if ( VtfIsRepresentative( vtf ) )
					return vtf;

				vtf = ParamGetVTFPossiblyMissing( "$envmapmask" );
				if ( VtfIsRepresentative( vtf ) )
					return vtf;

				vtf = ParamGetVTFPossiblyMissing( "$bumpmap" );
				if ( VtfIsRepresentative( vtf ) )
					return vtf;

				vtf = ParamGetVTFPossiblyMissing( "$normalmap" );
				if ( VtfIsRepresentative( vtf ) )
					return vtf;

				return null;
			}

			private void CalcTexCoord0Scale()
			{
				float w, h;
				if ( !wantsTexCoord0Scale )
				{
					w = h = 1;
				}
				else if ( representativeTexture == null )
				{
					w = h = 64;
				}
				else
				{
					w = representativeTexture.width;
					h = representativeTexture.height;
				}

				texCoord0Scale = new Vector2( 1 / w, 1 / h );
			}

			protected virtual void InitStatic( MaterialCache materialCache )
			{
				if ( representativeTexture == null )
					representativeTexture = CalcRepresentativeTexture();

				CalcTexCoord0Scale();
			}
			/*public virtual void Movement( SourceRenderContext context )
			{
				if ( !visible || !IsMaterialLoaded() )
					return;

				if ( EntityParams != null )
				{
					// Update our color/alpha based on entity params.
					Vector3 color = paramGetVector( "$color" );
					if ( color != null )
						color.SetFromColor( EntityParams.BlendColor );

					ParameterNumber alpha = param.value["$alpha"] as ParameterNumber;
					if ( alpha != null )
						alpha.value = EntityParams.BlendColor.A;
				}

				if ( ProxyDriver != null )
					ProxyDriver.Update( context, EntityParams );
			}
			protected void SetupOverrideSceneParams( SourceRenderContext renderContext, GfxRenderInst renderInst )
			{
				var fogParams = this.IsAdditive ? BlackFogParams : renderContext.CurrentView.FogParams;
				var toneMapParams = this.IsToneMapped ? renderContext.ToneMapParams : NoToneMapParams;

				if ( fogParams != renderContext.CurrentView.FogParams || toneMapParams != renderContext.ToneMapParams )
				{
					FillSceneParamsOnRenderInst( renderInst, renderContext.CurrentView, toneMapParams, BlackFogParams );
				}
			}
			public abstract void SetOnRenderInst( SourceRenderContext renderContext, GfxRenderInst renderInst, int lightmapPageIndex = -1 );

			public void SetOnRenderInstModelMatrix( GfxRenderInst renderInst, Matrix4x4? modelMatrix )
			{
				if ( this.skinningMode == SkinningMode.None )
				{
					var offs = renderInst.AllocateUniformBuffer( ShaderTemplate_Generic.ub_SkinningParams, 12 );
					var d = renderInst.MapUniformBufferF32( ShaderTemplate_Generic.ub_SkinningParams );

					if ( modelMatrix != null )
					{
						offs += FillMatrix4x3( d, offs, (Matrix4x4)modelMatrix );
					}
					else
					{
						Matrix4x4.Identity( scratchMat4a );
						offs += FillMatrix4x3( d, offs, scratchMat4a );
					}
				}
			}
			public void SetOnRenderInstSkinningParams( GfxRenderInst renderInst, Matrix4x4[] boneMatrix, int[] bonePaletteTable )
			{
			if ( this.SkinningMode == SkinningMode.Smooth )
			{
				Debug.Assert( bonePaletteTable.Length <= ShaderTemplate_Generic.MaxSkinningParamsBoneMatrix );

				var offs = renderInst.AllocateUniformBuffer( ShaderTemplate_Generic.ub_SkinningParams, 12 * ShaderTemplate_Generic.MaxSkinningParamsBoneMatrix );
				var d = renderInst.MapUniformBufferF32( ShaderTemplate_Generic.ub_SkinningParams );

				Matrix4x4.Identity( scratchMat4a );
				for ( var i = 0; i < ShaderTemplate_Generic.MaxSkinningParamsBoneMatrix; i++ )
				{
					var boneIndex = bonePaletteTable[i];
					var m = boneIndex != -1 ? boneMatrix[boneIndex] : scratchMat4a;
					offs += FillMatrix4x3( d, offs, m );
				}
			}
			else if ( this.SkinningMode == SkinningMode.Rigid )
			{
				Debug.Assert( bonePaletteTable.Length == 1 );

				var offs = renderInst.AllocateUniformBuffer( ShaderTemplate_Generic.ub_SkinningParams, 12 );
				var d = renderInst.MapUniformBufferF32( ShaderTemplate_Generic.ub_SkinningParams );

				var boneIndex = bonePaletteTable[0];
				var m = boneMatrix[boneIndex];
				offs += FillMatrix4x3( d, offs, m );
			}
		}
			public GfxRenderInstList GetRenderInstListForView( SourceEngineView view )
			{
				// Choose the right list.
				if ( this.IsIndirect )
				{
					return view.IndirectList;
				}
				else if ( this.IsTranslucent || this.IsAdditive )
				{
					return view.TranslucentList;
				}
				else
				{
					return view.MainList;
				}
			}
			 */
			public void CalcProjectedLight( SourceRenderContext renderContext, BBox bbox )
			{
			}
		}
		

































		public partial class MaterialCache
		{



			private readonly Dictionary<string, VTF> textureCache = new Dictionary<string, VTF>();
			private readonly Dictionary<string, Task<VTF>> texturePromiseCache = new Dictionary<string, Task<VTF>>();
			private readonly Dictionary<string, Task<VMT>> materialPromiseCache = new Dictionary<string, Task<VMT>>();
			private bool usingHDR = false;
			//public readonly ParticleSystemCache particleSystemCache;
			public bool ssbumpNormalize = false;
			//public StaticResources staticResources;
			public List<string> materialDefines = new List<string>();
			public bool deviceNeedsFlipY;
			//public ShaderTemplates shaderTemplates = new ShaderTemplates();
			public Main.SourceFileSystem filesystem;

			public MaterialCache( Main.SourceFileSystem filesystem ) //GfxDevice device, GfxRenderCache cache,
			{
				this.filesystem = filesystem;
				// Install render targets
				/*var _rt_Camera = new VTF( device, cache, null, "_rt_Camera", false, LateBindingTexture.Camera );
				_rt_Camera.width = 256;
				_rt_Camera.height = 256;
				textureCache.Add( "_rt_Camera", _rt_Camera );
				textureCache.Add( "_rt_RefractTexture", new VTF( device, cache, null, "_rt_RefractTexture", false, LateBindingTexture.FramebufferColor ) );
				textureCache.Add( "_rt_WaterRefraction", new VTF( device, cache, null, "_rt_WaterRefraction", false, LateBindingTexture.FramebufferColor ) );
				textureCache.Add( "_rt_WaterReflection", new VTF( device, cache, null, "_rt_WaterReflection", false, LateBindingTexture.WaterReflection ) );
				textureCache.Add( "_rt_Depth", new VTF( device, cache, null, "_rt_Depth", false, LateBindingTexture.FramebufferDepth ) );*/



				//staticResources = new StaticResources( device, cache );

				//particleSystemCache = new ParticleSystemCache( filesystem );

				//deviceNeedsFlipY = GfxDeviceNeedsFlipY( device );
			}

			public bool IsInitialized()
			{
				/*if ( !particleSystemCache.IsLoaded )
				{
					return false;
				}*/

				return true;
			}
			public void SetRenderConfig( bool hdr, int bspVersion )
			{
				SetUsingHDR( hdr );

				// Portal 2 has a fix for ssbump materials being too bright.
				ssbumpNormalize = (bspVersion >= 21);
			}

			private void SetUsingHDR( bool hdr )
			{
				usingHDR = hdr;

				materialDefines = new List<string> { "gpu>=1", "gpu>=2", "gpu>=3", ">=dx90_20b", ">=dx90", ">dx90", "srgb", "srgb_pc", "dx9" };
				materialDefines.Add( usingHDR ? "hdr" : "ldr" );
			}

			public bool IsUsingHDR()
			{
				return usingHDR;
			}

			public async Task BindLocalCubemap( Cubemap cubemap )
			{
				var vtf = await FetchVTF( cubemap.filename, true );
				textureCache["env_cubemap"] = vtf;
			}

			private string ResolvePath( string path )
			{
				if ( !path.StartsWith( "materials/" ) )
					path = "materials/" + path;
				return path;
			}

			private async Task<VMT> FetchMaterialDataInternal( string name )
			{
				return await ParseVMT( filesystem, ResolvePath( name ) );
			}

			private Task<VMT> FetchMaterialData( string path )
			{
				if ( !materialPromiseCache.ContainsKey( path ) )
					materialPromiseCache[path] = FetchMaterialDataInternal( path );
				return materialPromiseCache[path];
			}
			private BaseMaterial CreateMaterialInstanceInternal( VMT vmt )
			{
				// Dispatch based on shader type.
				/*var shaderType = vmt._Root.ToLower();
				if ( shaderType == "water" )
					return new Material_Water( vmt );
				else if ( shaderType == "modulate" )
					return new Material_Modulate( vmt );
				else if ( shaderType == "unlittwotexture" || shaderType == "monitorscreen" )
					return new Material_UnlitTwoTexture( vmt );
				else if ( shaderType == "refract" )
					return new Material_Refract( vmt );
				else if ( shaderType == "solidenergy" )
					return new Material_SolidEnergy( vmt );
				else if ( shaderType == "sky" )
					return new Material_Sky( vmt );
				else if ( shaderType == "spritecard" )
					return new Material_SpriteCard( vmt );
				else*/
				return null;//new Material_Generic( vmt );
			}

			public async Task<BaseMaterial> CreateMaterialInstance( string path )
			{
				var vmt = await FetchMaterialData( path );
				var materialInstance = CreateMaterialInstanceInternal( vmt );
				if ( vmt.Items["%compiletrigger"] != null )
					materialInstance.isToolMaterial = true;
				return materialInstance;
			}

			public bool CheckVTFExists( string name )
			{
				var path = filesystem.ResolvePath( ResolvePath( name ), ".vtf" );
				return filesystem.HasEntry( path );
			}

			private async Task<VTF> FetchVTFInternal( string name, bool srgb, string cacheKey )
			{
				var path = filesystem.ResolvePath( ResolvePath( name ), ".vtf" );
				var data = await filesystem.FetchFileData( path );
				var vtf = new VTF( data, path, srgb ); //device, cache,
				textureCache[cacheKey] = vtf;
				return vtf;
			}

			private string GetCacheKey( string name, bool srgb )
			{
				// Special runtime render target
				if ( name.StartsWith( "_rt_" ) )
					return name;

				return srgb ? $"{name}_srgb" : name;
			}

			public async Task<VTF> FetchVTF( string name, bool srgb )
			{
				var cacheKey = GetCacheKey( name, srgb );

				if ( textureCache.TryGetValue( cacheKey, out var texture ) )
					return texture;

				if ( !texturePromiseCache.TryGetValue( cacheKey, out var promise ) )
				{
					promise = FetchVTFInternal( name, srgb, cacheKey );
					texturePromiseCache[cacheKey] = promise;
				}

				return await promise;
			}

			/*public void Destroy( GfxDevice device )
			{
				staticResources.Destroy( device );
				shaderTemplates.Destroy( device );
				foreach ( var vtf in textureCache.Values )
					vtf.Destroy( device );
			}*/

		}



	
	public static class ParameterFactory
	{
		public static Parameter CreateParameterAuto( object value )
		{
			if ( value is string S )
			{
				if ( float.TryParse( S, out float n ) )
					return new ParameterNumber( n );

				if ( S.StartsWith( "[" ) || S.StartsWith( "{" ) )
				{
					var vv = new ParameterVector( 0 );
					vv.Parse( S );
					return vv;
				}

				if ( S.StartsWith( "center" ) )
				{
					var vv = new ParameterMatrix();
					vv.Parse( S );
					return vv;
				}

				var v = new ParameterString();
				v.Parse( S );
				return v;
			}

			return null;
		}
	}


	public class ParameterReference
		{
			public string name = null;
			public int index = -1;
			public Parameter value = null;

			public ParameterReference( string str, float? defaultValue = null, bool required = true )
			{
				if ( str == null )
				{
					if ( required || defaultValue != null )
						value = new ParameterNumber( defaultValue.Value );
				}
				else if ( str.StartsWith( "$" ) )
				{
					var match = Regex.Match( str, @"([a-zA-Z0-9$_]+)(?:\[(\d+)\])?" );
					if ( match.Success )
					{
						name = match.Groups[1].Value.ToLower();
						if ( match.Groups[2].Success )
							index = int.Parse( match.Groups[2].Value );
					}
				}
				else
				{
					value = ParameterFactory.CreateParameterAuto( str );
				}
			}
		}

		/*public interface MaterialProxyFactory
		{
			string type { get; }
			MaterialProxy Create( VKFParamMap parameters );
		}


	public class MaterialProxySystem
	{
		public Dictionary<string, MaterialProxyFactory> proxyFactories = new Dictionary<string, MaterialProxyFactory>();

		public MaterialProxySystem()
		{
			RegisterDefaultProxyFactories();
		}

		private void RegisterDefaultProxyFactories()
		{
			RegisterProxyFactory( new MaterialProxy_Equals(null) );
			RegisterProxyFactory( new MaterialProxy_Add() );
			RegisterProxyFactory( new MaterialProxy_Subtract() );
			RegisterProxyFactory( new MaterialProxy_Multiply() );
			RegisterProxyFactory( new MaterialProxy_Clamp() );
			RegisterProxyFactory( new MaterialProxy_Abs() );
			RegisterProxyFactory( new MaterialProxy_LessOrEqual() );
			RegisterProxyFactory( new MaterialProxy_LinearRamp() );
			RegisterProxyFactory( new MaterialProxy_Sine() );
			RegisterProxyFactory( new MaterialProxy_TextureScroll() );
			RegisterProxyFactory( new MaterialProxy_PlayerProximity() );
			RegisterProxyFactory( new MaterialProxy_GaussianNoise() );
			RegisterProxyFactory( new MaterialProxy_AnimatedTexture() );
			RegisterProxyFactory( new MaterialProxy_MaterialModify() );
			RegisterProxyFactory( new MaterialProxy_MaterialModifyAnimated() );
			RegisterProxyFactory( new MaterialProxy_WaterLOD() );
			RegisterProxyFactory( new MaterialProxy_TextureTransform() );
			RegisterProxyFactory( new MaterialProxy_ToggleTexture() );
			RegisterProxyFactory( new MaterialProxy_EntityRandom() );
			RegisterProxyFactory( new MaterialProxy_FizzlerVortex() );
		}

		public void RegisterProxyFactory( MaterialProxyFactory factory )
		{
			proxyFactories[factory.type] = factory;
		}

		public MaterialProxyDriver CreateProxyDriver( BaseMaterial material, List<(string, VKFParamMap)> proxyDefs )
		{
			List<MaterialProxy> proxies = new List<MaterialProxy>();
			for ( int i = 0; i < proxyDefs.Count; i++ )
			{
				(string name, VKFParamMap params) = proxyDefs[i];
			if ( proxyFactories.TryGetValue( name, out MaterialProxyFactory proxyFactory ) )
			{
				MaterialProxy proxy = proxyFactory.Create ( params);
				proxies.Add( proxy );
			}
			else
			{
				Console.WriteLine( "unknown proxy type: " + name );
			}
		}
        return new MaterialProxyDriver( material, proxies);
	}
}*/



		class MaterialProxyDriver
	{
		private BaseMaterial material;
		private List<MaterialProxy> proxies;

		public MaterialProxyDriver( BaseMaterial material, List<MaterialProxy> proxies )
		{
			this.material = material;
			this.proxies = proxies;
		}

		public void Update( Main.SourceRenderContext renderContext, EntityMaterialParameters entityParams )
		{
			foreach ( MaterialProxy proxy in this.proxies )
			{
				proxy.Update( this.material.param, renderContext, entityParams );
			}
		}
	}


		public static T ParamLookupOptional<T>( Dictionary<string, Parameter> map, ParameterReference reference ) where T : Parameter
		{
			if ( reference.name != null )
			{
				if ( map.TryGetValue( reference.name, out Parameter pm ) )
				{
					return reference.index != -1 ? pm.Index( reference.index ) as T : pm as T;
				}
				else
				{
					return null;
				}
			}
			else
			{
				return reference.value as T;
			}
		}

		public class ParameterMap {
			public Dictionary<string, Parameter> value;
		}
		public static T ParamLookup<T>( ParameterMap map, ParameterReference reference ) where T : Parameter
		{
			return ParamLookupOptional<T>( map.value, reference ) ?? throw new KeyNotFoundException( $"Parameter {reference} not found in parameter map" );
		}

		public static float ParamGetNum( ParameterMap map, ParameterReference reference )
		{
			return ((ParameterNumber)ParamLookup<ParameterNumber>( map, reference )).value;
		}

		public static void ParamSetNum( ParameterMap map, ParameterReference reference, float value )
		{
			var param = ParamLookupOptional<ParameterNumber>( map.value, reference );
			if ( param == null )
			{
				// Perhaps put in a warning, but this seems to happen in live content (TF2's hwn_skeleton_blue.vmt)
				return;
			}
			param.value = value;
		}


		public class MaterialProxy
	{
			public virtual void Update( ParameterMap paramsMap, Main.SourceRenderContext renderContext, EntityMaterialParameters entityParams ) {}
	}

	class MaterialProxy_Equals : MaterialProxy
	{
		public static string type = "equals";
		private ParameterReference srcvar1;
		private ParameterReference resultvar;

		public MaterialProxy_Equals( VKFParamMap parameters )
		{
			this.srcvar1 = new ParameterReference( parameters.val["srcvar1"] );
			this.resultvar = new ParameterReference( parameters.val["resultvar"] );
		}

		public override void Update( ParameterMap map, Main.SourceRenderContext renderContext, EntityMaterialParameters entityParams )
		{
			Parameter srcvar1 = ParamLookup<Parameter>( map, this.srcvar1 );
			Parameter resultvar = ParamLookup<Parameter>( map, this.resultvar );
			resultvar.Set( srcvar1 );
		}
	}















































	public static float UnpackColorRGBExp32( byte v, byte exp )
		{
			// exp comes in unsigned, sign extend
			exp = (byte)((exp << 24) >> 24);
			float m = (float)Math.Pow( 2.0, exp ) / 0xFF;
			return v * m;
		}
	}
}
