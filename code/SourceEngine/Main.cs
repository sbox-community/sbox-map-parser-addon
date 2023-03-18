// sbox.Community © 2023-2024

using Sandbox;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

using System.Threading.Tasks;
using static MapParser.SourceEngine.Materials;

namespace MapParser.SourceEngine
{
	public partial class Main
	{
		public class LooseMount
		{
			public string Path { get; }
			public List<string> Files { get; }

			public LooseMount( string path, List<string> files = null )
			{
				Path = path;
				Files = files ?? new List<string>();
			}

			public bool HasEntry( string resolvedPath )
			{
				return Files.Contains( resolvedPath );
			}

			/*public Task<byte[]> FetchEntryData( DataFetcher dataFetcher, string resolvedPath )
			{
				return dataFetcher.FetchData( $"{Path}/{resolvedPath}" );
			}*/
		}

		public static void NormalizeZip( ref ZipStorer zip )
		{
			List<ZipStorer.ZipFileEntry> dir = zip.ReadCentralDir();

			foreach ( ZipStorer.ZipFileEntry entry in dir )
			{
				entry.FilenameInZip = entry.FilenameInZip.ToLower().Replace( "\\", "/" );
				
			}
		}

		public class SourceFileSystem
		{
			public List<ZipStorer> pakfiles = new List<ZipStorer>();
			public List<ZipStorer> zip = new List<ZipStorer>();
			public List<VPKMount> vpk = new List<VPKMount>();
			public List<LooseMount> loose = new List<LooseMount>();
			public List<GMA> gma = new List<GMA>();
			public BaseFileSystem filesystem;

			//private readonly DataFetcher dataFetcher;

			public SourceFileSystem( BaseFileSystem filesystem)//DataFetcher dataFetcher )
			{
				this.filesystem = filesystem;
				//this.dataFetcher = dataFetcher;
			}

			public async Task CreateVPKMount( string path )
			{
				// This little dance here is to ensure that priorities are correctly ordered.
				VPKMount dummyMount = null!;
				var i = this.vpk.Count;
				this.vpk.Add( dummyMount );
				this.vpk[i] = await VPKParser.CreateVPKMount( filesystem, path );
			}

			public void AddPakFile( ZipStorer pakfile )
			{
				NormalizeZip( ref pakfile );
				this.pakfiles.Add( pakfile );
			}

			public async Task CreateZipMount( string path )
			{
				ZipStorer zip = ZipStorer.Open( filesystem, path, FileMode.Open );

				NormalizeZip( ref zip );
				this.zip.Add( zip );

				zip.Close();
			}

			public async Task CreateGMAMount( string path )
			{
				var data = filesystem.ReadAllBytes( path ).ToArray();
				var gma = new GMA( data );
				this.gma.Add( gma );
			}

			public string ResolvePath( string path, string ext )
			{
				path = path.ToLower().Replace( "\\", "/" );
				if ( !path.EndsWith( ext ) )
					path = $"{path}{ext}";

				if ( path.Contains( "../" ) )
				{
					// Resolve relative paths.
					var parts = path.Split( '/' ).ToList();

					while ( parts.Contains( ".." ) )
					{
						var idx = parts.IndexOf( ".." );
						parts.RemoveRange( idx - 1, 2 );
					}

					path = string.Join( "/", parts );
				}

				path = path.Replace( "./", "" );

				while ( path.Contains( "//" ) )
					path = path.Replace( "//", "/" );

				return path;
			}

			public string SearchPath( string[] searchDirs, string path, string ext )
			{
				foreach ( string searchDir in searchDirs )
				{
					// Normalize path separators.
					string normalizedSearchDir = searchDir.Replace( '\\', '/' ).Replace( "//", "/" );
					if ( normalizedSearchDir.EndsWith( "/" ) )
					{
						normalizedSearchDir = normalizedSearchDir.Substring( 0, normalizedSearchDir.Length - 1 );
					}

					// Attempt searching for a path.
					string finalPath = ResolvePath( $"{normalizedSearchDir}/{path}", ext );
					if ( HasEntry( finalPath ) )
					{
						return finalPath;
					}
				}

				return null;
			}

			public bool HasEntry( string resolvedPath )
			{
				foreach ( var loose in loose )
				{
					if ( loose.HasEntry( resolvedPath ) )
						return true;
				}

				foreach ( var vpk in vpk )
				{
					VPKFileEntry? entry = vpk.FindEntry( resolvedPath );
					if ( entry != null )
						return true;
				}

				foreach ( var pakfile in pakfiles )
				{
					if ( pakfile.ReadCentralDir().Any( entry => entry.FilenameInZip == resolvedPath ) )
						return true;
				}

				foreach ( var zip in this.zip )
				{
					if ( zip.ReadCentralDir().Any( entry => entry.FilenameInZip == resolvedPath ) )
						return true;
				}

				foreach ( var gma in gma )
				{
					if ( gma.files.Any( entry => entry.filename == resolvedPath ) )
						return true;
				}

				return false;
			}

			public async Task<byte[]?> FetchFileData( string resolvedPath )
			{
				for ( int i = 0; i < loose.Count; i++ )
				{
					var custom = loose[i];
					if ( custom.HasEntry( resolvedPath ) )
						return await this.filesystem.ReadAllBytesAsync( custom.Path );//custom.FetchEntryData( dataFetcher, resolvedPath );
				}

				for ( int i = 0; i < vpk.Count; i++ )
				{
					VPKFileEntry? entry = vpk[i].FindEntry( resolvedPath );
					if ( entry != null )
						return await vpk[i].FetchFileData( entry.Value );
				}

				for ( int i = 0; i < pakfiles.Count; i++ )
				{
					var zip = pakfiles[i];
					var entry = zip.ReadCentralDir().FirstOrDefault( e => e.FilenameInZip == resolvedPath );
					if ( entry != null )
					{
						MemoryStream memoryStream = new MemoryStream();
						_  = await zip.ExtractFileAsync( entry, memoryStream );
						return memoryStream.GetBuffer();
					}
				}

				for ( int i = 0; i < zip.Count; i++ )
				{
					var zip = this.zip[i];
					var entry = zip.ReadCentralDir().FirstOrDefault( e => e.FilenameInZip == resolvedPath );
					if ( entry != null )
					{
						MemoryStream memoryStream = new MemoryStream();
						_ = await zip.ExtractFileAsync( entry, memoryStream );
						return memoryStream.GetBuffer();
					}
				}

				for ( int i = 0; i < gma.Count; i++ )
				{
					var gma = this.gma[i];
					var entry = gma.files.FirstOrDefault( e => e.filename == resolvedPath );
					if ( entry != null )
						return entry.data;
				}

				return null;
			}
		}









		public class BSPRenderer
		{
			private byte[] vertexBuffer;
			private byte[] indexBuffer;
			private VertexAttribute inputLayout;
			//private GfxInputState inputState;
			public EntitySystem entitySystem;
			//public List<BSPModelRenderer> models = new List<BSPModelRenderer>();
			//public List<DetailPropLeafRenderer> detailPropLeafRenderers = new List<DetailPropLeafRenderer>();
			//public List<StaticPropRenderer> staticPropRenderers = new List<StaticPropRenderer>();
			public HashSet<int> liveLeafSet = new HashSet<int>();
			private int startLightmapPageIndex = 0;

			public BSPRenderer( SourceRenderContext renderContext, BSPFile bsp )
			{
				//entitySystem = new EntitySystem( renderContext, this );

				renderContext.materialCache.SetRenderConfig( bsp.usingHDR, bsp.version );
				startLightmapPageIndex = renderContext.lightmapManager.AppendPackerPages( bsp.lightmapPacker );

				//var device = renderContext.device;
				//var cache = renderContext.renderCache;
				vertexBuffer = bsp.vertexData;// MakeStaticDataBuffer( device, GfxBufferUsage.Vertex, bsp.vertexData );
				indexBuffer = bsp.indexData;//MakeStaticDataBuffer( device, GfxBufferUsage.Index, bsp.indexData );

				/*var vertexAttributeDescriptors = new GfxVertexAttributeDescriptor[]
				{
			new GfxVertexAttributeDescriptor { location = MaterialShaderTemplateBase.a_Position, bufferIndex = 0, bufferByteOffset = 0*0x04, format = GfxFormat.F32_RGB },
			new GfxVertexAttributeDescriptor { location = MaterialShaderTemplateBase.a_Normal, bufferIndex = 0, bufferByteOffset = 3*0x04, format = GfxFormat.F32_RGBA },
			new GfxVertexAttributeDescriptor { location = MaterialShaderTemplateBase.a_TangentS, bufferIndex = 0, bufferByteOffset = 7*0x04, format = GfxFormat.F32_RGBA },
			new GfxVertexAttributeDescriptor { location = MaterialShaderTemplateBase.a_TexCoord01, bufferIndex = 0, bufferByteOffset = 11*0x04, format = GfxFormat.F32_RGBA }
				};
				var vertexBufferDescriptors = new GfxInputLayoutBufferDescriptor[]
				{
			new GfxInputLayoutBufferDescriptor { byteStride = (3+4+4+4)*0x04, frequency = GfxVertexBufferFrequency.PerVertex }
				};
				var indexBufferFormat = GfxFormat.U32_R;
				inputLayout = cache.CreateInputLayout( new GfxInputLayoutDescriptor { vertexAttributeDescriptors = vertexAttributeDescriptors, vertexBufferDescriptors = vertexBufferDescriptors, indexBufferFormat = indexBufferFormat } );

				inputState = device.CreateInputState( inputLayout, new GfxVertexBufferDescriptor[]
				{
			new GfxVertexBufferDescriptor { buffer = vertexBuffer, byteOffset = 0 }
				}, new GfxIndexBufferDescriptor { buffer = indexBuffer, byteOffset = 0 } );
				*/


				/*for ( int i = 0; i < bsp.models.Length; i++ )
				{
					var model = bsp.models[i];
					var modelRenderer = new BSPModelRenderer( renderContext, model, bsp, startLightmapPageIndex );
					// Non-world-spawn models are invisible by default (they're lifted into the world by entities).
					modelRenderer.visible = (i == 0);
					models.Add( modelRenderer );
				}

				// Spawn entities.
				this.entitySystem.CreateAndSpawnEntities( this.bsp.Entities );


				// Spawn static objects.
				if ( this.bsp.StaticObjects != null )
				{
					foreach ( var staticProp in this.bsp.StaticObjects.StaticProps )
					{
						this.staticPropRenderers.Add( new StaticPropRenderer( renderContext, this, staticProp ) );
					}
				}

				// Spawn detail objects.
				if ( this.bsp.DetailObjects != null )
				{
					var detailMaterial = this.GetWorldSpawn().DetailMaterial;
					foreach ( var leaf in this.bsp.DetailObjects.LeafDetailModels.Keys )
					{
						this.detailPropLeafRenderers.Add( new DetailPropLeafRenderer( renderContext, bsp, leaf, detailMaterial ) );
					}
				}*/


			}
			/*	public worldspawn GetWorldSpawn()
				{
					return AssertExists<Entity>( this.entitySystem.FindEntityByType<worldspawn>() );
				}

				public sky_camera GetSkyCamera()
				{
					return this.entitySystem.FindEntityByType<sky_camera>();
				}

				public void Movement( SourceRenderContext renderContext )
				{
					this.entitySystem.Movement( renderContext );

					for ( int i = 0; i < this.models.Count; i++ )
					{
						this.models[i].Movement( renderContext );
					}

					for ( int i = 0; i < this.detailPropLeafRenderers.Count; i++ )
					{
						this.detailPropLeafRenderers[i].Movement( renderContext );
					}

					for ( int i = 0; i < this.staticPropRenderers.Count; i++ )
					{
						this.staticPropRenderers[i].Movement( renderContext );
					}
				}*/

			/*public void PrepareToRenderView( SourceRenderContext renderContext, GfxRenderInstManager renderInstManager, RenderObjectKind kinds = RenderObjectKind.WorldSpawn | RenderObjectKind.StaticProps | RenderObjectKind.DetailProps | RenderObjectKind.Entities )
			{
				var template = renderInstManager.PushTemplateRenderInst();
				template.SetInputLayoutAndState( inputLayout, inputState );

				FillSceneParamsOnRenderInst( template, renderContext.CurrentView, renderContext.ToneMapParams );

				// Render the world-spawn model.
				if ( kinds.HasFlag( RenderObjectKind.WorldSpawn ) )
					models[0].PrepareToRenderWorld( renderContext, renderInstManager );

				if ( kinds.HasFlag( RenderObjectKind.Entities ) )
				{
					for ( int i = 1; i < models.Count; i++ )
						models[i].PrepareToRenderModel( renderContext, renderInstManager );
					for ( int i = 0; i < entitySystem.Entities.Count; i++ )
						entitySystem.Entities[i].PrepareToRender( renderContext, renderInstManager );
				}

				// Static props.
				if ( kinds.HasFlag( RenderObjectKind.StaticProps ) )
					for ( int i = 0; i < staticPropRenderers.Count; i++ )
						staticPropRenderers[i].PrepareToRender( renderContext, renderInstManager, bsp, renderContext.CurrentView.PVS );

				// Detail props.
				if ( kinds.HasFlag( RenderObjectKind.DetailProps ) )
				{
					liveLeafSet.Clear();
					models[0].GatherLiveSets( null, null, liveLeafSet, renderContext.CurrentView );

					for ( int i = 0; i < detailPropLeafRenderers.Count; i++ )
					{
						var detailPropLeafRenderer = detailPropLeafRenderers[i];
						if ( !liveLeafSet.Contains( detailPropLeafRenderer.Leaf ) )
							continue;
						detailPropLeafRenderer.PrepareToRender( renderContext, renderInstManager );
					}
				}

				
				//for (int i = 0; i < bsp.Worldlights.Count; i++)
				//{
				//	DrawWorldSpaceText(GetDebugOverlayCanvas2D(), view.ClipFromWorldMatrix, bsp.Worldlights[i].Pos, "" + i);
				//	DrawWorldSpacePoint(GetDebugOverlayCanvas2D(), view.ClipFromWorldMatrix, bsp.Worldlights[i].Pos);
				//}
				

				renderInstManager.PopTemplateRenderInst();
			}
			public void Destroy( GfxDevice device )
			{
				device.DestroyBuffer( vertexBuffer );
				device.DestroyBuffer( indexBuffer );
				device.DestroyInputState( inputState );

				foreach ( var detailPropLeafRenderer in detailPropLeafRenderers )
					detailPropLeafRenderer.Destroy( device );
				foreach ( var staticPropRenderer in staticPropRenderers )
					staticPropRenderer.Destroy( device );
				entitySystem.Destroy( device );
			}
			*/

		}






		public class SourceLoadContext
		{
			//public EntityFactoryRegistry entityFactoryRegistry;

			public SourceFileSystem filesystem;

			public SourceLoadContext( SourceFileSystem filesystem )
			{
				//entityFactoryRegistry = new EntityFactoryRegistry();
				this.filesystem = filesystem;
			}
		}

		public class SourceRenderContext
		{
			//public EntityFactoryRegistry entityFactoryRegistry;
			public SourceFileSystem filesystem;
			public LightmapManager lightmapManager;
			//public StudioModelCache studioModelCache;
			public MaterialCache materialCache;
			//public WorldLightingState worldLightingState = new WorldLightingState();
			public float globalTime = 0;
			public float globalDeltaTime = 0;
			public Materials.MaterialProxySystem materialProxySystem = new MaterialProxySystem();
			public float cheapWaterStartDistance = 0.0f;
			public float cheapWaterEndDistance = 0.1f;
			//public SourceWorldViewRenderer currentViewRenderer = null;
			//public SourceEngineView currentView;
			//public SourceColorCorrection colorCorrection;
			//public ToneMapParams toneMapParams = new ToneMapParams();
			//public GfxRenderCache renderCache;
			//public point_camera currentPointCamera = null;
			//public env_shake currentShake = null;

			// Public settings
			public bool enableFog = true;
			public bool enableBloom = true;
			public bool enableAutoExposure = true;
			public bool enableExpensiveWater = true;
			public bool enableCamera = true;
			public bool showToolMaterials = false;
			public bool showTriggerDebug = false;
			public bool showDecalMaterials = true;
			public int shadowMapSize = 512;

			//public DebugStatistics debugStatistics = new DebugStatistics();

			public SourceRenderContext( SourceLoadContext loadContext ) // GfxDevice device, 
			{
				//this.entityFactoryRegistry = loadContext.entityFactoryRegistry;
				this.filesystem = loadContext.filesystem;
				
				//this.renderCache = new GfxRenderCache( device );
				//this.lightmapManager = new LightmapManager( device, this.renderCache );
				this.materialCache = new MaterialCache( this.filesystem ); // device, this.renderCache,
				/*this.studioModelCache = new StudioModelCache( this, this.filesystem );
				this.colorCorrection = new SourceColorCorrection( device, this.renderCache );

				if ( !this.device.queryLimits().occlusionQueriesRecommended )
				{
					// Disable auto-exposure system on backends where we shouldn't use occlusion queries.
					// TODO(jstpierre): We should be able to do system with compute shaders instead of
					// occlusion queries on WebGPU, once that's more widely deployed.
					this.enableAutoExposure = false;
				}*/
			}

			public bool crossedTime( float time )
			{
				float oldTime = this.globalTime - this.globalDeltaTime;
				return (time >= oldTime) && (time < this.globalTime);
			}

			/*public bool crossedRepeatTime( float start, float interval )
			{
				if ( this.globalTime <= start )
					return false;

				float baseTime = start + (((this.globalTime - start) / interval) | 0) * interval;
				return this.crossedTime( baseTime );
			}

			public bool isUsingHDR()
			{
				return this.materialCache.isUsingHDR();
			}

			public void destroy( GfxDevice device )
			{
				this.renderCache.destroy();
				this.lightmapManager.destroy( device );
				this.materialCache.destroy( device );
				this.studioModelCache.destroy( device );
				this.colorCorrection.destroy( device );
			}*/
		}







		// Renders the entire world (2D skybox, 3D skybox, etc.) given a specific camera location.
		// It's distinct from a view, which is camera settings, which there can be multiple of in a world renderer view.
		public class SourceWorldViewRenderer
		{
			public bool drawSkybox2D = true;
			public bool drawSkybox3D = true;
			public bool drawIndirect = true;
			public bool drawWorld = true;
			public bool drawProjectedShadows = true;
			//public RenderObjectKind renderObjectMask = RenderObjectKind.WorldSpawn | RenderObjectKind.StaticProps | RenderObjectKind.DetailProps | RenderObjectKind.Entities;
			public bool pvsEnabled = true;
			public bool pvsFallback = true;

			/*public SourceEngineView mainView = new SourceEngineView();
			public SourceEngineView skyboxView = new SourceEngineView();*/
			public bool enabled = false;

			/*public ProjectedLightRenderer currentProjectedLightRenderer = null;
			public GfxrRenderTargetID outputColorTargetID = null;
			public GfxrResolveTextureID outputColorTextureID = null;

			public Flashlight flashlight = null;*/

			public SourceWorldViewRenderer( string name )//, SourceEngineViewType viewType 
			{
				/*this.name = name;
				this.mainView.viewType = viewType;
				this.skyboxView.viewType = viewType;*/
			}

		/*	private void CalcProjectedLight( SourceRenderer renderer )
			{
				this.currentProjectedLightRenderer = null;


				if ( !this.drawProjectedShadows )
					return;

				float bestDistance = float.PositiveInfinity;
				ProjectedLightRenderer bestProjectedLight = null;

				for ( int i = 0; i < renderer.BspRenderers.Length; i++ )
				{
					var bspRenderer = renderer.BspRenderers[i];
					env_projectedtexture projectedLight = null;
					while ( (projectedLight = bspRenderer.EntitySystem.FindEntityByType<env_projectedtexture>( projectedLight )) != null )
					{
						if ( !projectedLight.ShouldDraw() )
							continue;

						projectedLight.GetAbsOrigin( scratchVec3 );
						float dist = Vector3.SquaredDistance( this.mainView.CameraPos, scratchVec3 );
						if ( dist < bestDistance )
						{
							bestDistance = dist;
							bestProjectedLight = projectedLight.projectedLightRenderer;
						}
					}
				}

				var renderContext = renderer.RenderContext;
				if ( bestProjectedLight == null && this.flashlight != null && this.flashlight.Enabled )
				{
					renderContext.CurrentView = this.mainView;
					this.flashlight.Movement( renderContext );
					renderContext.CurrentView = null;
					if ( this.flashlight.IsReady() )
						bestProjectedLight = this.flashlight.projectedLightRenderer;
				}

				this.currentProjectedLightRenderer = bestProjectedLight;
			}*/


			/*
    public prepareToRender(renderer: SourceRenderer, parentViewRenderer: SourceWorldViewRenderer | null): void {
        if (this.enabled)
            return;

        this.enabled = true;
        const renderContext = renderer.renderContext, renderInstManager = renderer.renderHelper.renderInstManager;

        this.skyboxView.copy(this.mainView);

        // Position the 2D skybox around the main view.
        mat4.fromTranslation(scratchMatrix, this.mainView.cameraPos);
        mat4.mul(this.skyboxView.viewFromWorldMatrix, this.skyboxView.viewFromWorldMatrix, scratchMatrix);
        this.skyboxView.finishSetup();

        this.calcProjectedLight(renderer);

        if (this.currentProjectedLightRenderer !== null)
            this.currentProjectedLightRenderer.preparePasses(renderer);

        renderContext.currentViewRenderer = this;
        renderContext.currentView = this.skyboxView;

        if (this.drawSkybox2D && renderer.skyboxRenderer !== null)
            renderer.skyboxRenderer.prepareToRender(renderContext, renderInstManager, this.skyboxView);

        if (this.drawSkybox3D) {
            for (let i = 0; i < renderer.bspRenderers.length; i++) {
                const bspRenderer = renderer.bspRenderers[i];

                // Draw the skybox by positioning us inside the skybox area.
                const skyCamera = bspRenderer.getSkyCamera();
                if (skyCamera === null)
                    continue;

                this.skyboxView.copy(this.mainView);
                mat4.mul(this.skyboxView.viewFromWorldMatrix, this.skyboxView.viewFromWorldMatrix, skyCamera.modelMatrix);
                this.skyboxView.finishSetup();

                skyCamera.fillFogParams(this.skyboxView.fogParams);

                // If our skybox is not in a useful spot, then don't render it.
                if (!this.skyboxView.calcPVS(bspRenderer.bsp, false, parentViewRenderer !== null ? parentViewRenderer.skyboxView : null))
                    continue;

                bspRenderer.prepareToRenderView(renderContext, renderInstManager, this.renderObjectMask & (RenderObjectKind.WorldSpawn | RenderObjectKind.StaticProps | RenderObjectKind.Entities));
            }
        }

        if (this.drawWorld) {
            renderContext.currentView = this.mainView;

            for (let i = 0; i < renderer.bspRenderers.length; i++) {
                const bspRenderer = renderer.bspRenderers[i];

                if (!this.mainView.calcPVS(bspRenderer.bsp, this.pvsFallback, parentViewRenderer !== null ? parentViewRenderer.mainView : null))
                    continue;

                // Calculate our fog parameters from the local player's fog controller.
                const localPlayer = bspRenderer.entitySystem.getLocalPlayer();
                if (localPlayer.currentFogController !== null && renderer.renderContext.enableFog)
                    localPlayer.currentFogController.fillFogParams(this.mainView.fogParams);
                else
                    this.mainView.fogParams.maxdensity = 0.0;

                bspRenderer.prepareToRenderView(renderContext, renderInstManager, this.renderObjectMask);
            }
        }

        renderContext.currentView = null!;
    }

    private lateBindTextureAttachPass(renderContext: SourceRenderContext, builder: GfxrGraphBuilder, pass: GfxrPass): void {
        if (renderContext.currentPointCamera !== null && renderContext.currentPointCamera.viewRenderer !== this)
            pass.attachResolveTexture(renderContext.currentPointCamera.viewRenderer.resolveColorTarget(builder));
        if (this.currentProjectedLightRenderer !== null)
            pass.attachResolveTexture(this.currentProjectedLightRenderer.outputDepthTextureID!);
    }

    private lateBindTextureSetOnPassRenderer(renderer: SourceRenderer, scope: GfxrPassScope): void {
        const renderContext = renderer.renderContext, staticResources = renderContext.materialCache.staticResources;
        if (renderContext.currentPointCamera !== null && renderContext.currentPointCamera.viewRenderer !== this)
            renderer.setLateBindingTexture(LateBindingTexture.Camera, scope.getResolveTextureForID(renderContext.currentPointCamera.viewRenderer.outputColorTextureID!), staticResources.linearRepeatSampler);
        if (this.currentProjectedLightRenderer !== null)
            renderer.setLateBindingTexture(LateBindingTexture.ProjectedLightDepth, scope.getResolveTextureForID(this.currentProjectedLightRenderer.outputDepthTextureID!), staticResources.shadowSampler);
    }

    public pushPasses(renderer: SourceRenderer, builder: GfxrGraphBuilder, renderTargetDesc: GfxrRenderTargetDescription): void {
        assert(this.enabled);
        if (this.outputColorTextureID !== null)
            return;

        const renderContext = renderer.renderContext, staticResources = renderContext.materialCache.staticResources;

        if (this.currentProjectedLightRenderer !== null)
            this.currentProjectedLightRenderer.pushPasses(renderContext, renderer.renderHelper.renderInstManager, builder);

        const mainColorDesc = new GfxrRenderTargetDescription(GfxFormat.U8_RGBA_RT_SRGB);
        mainColorDesc.copyDimensions(renderTargetDesc);
        mainColorDesc.colorClearColor = standardFullClearRenderPassDescriptor.colorClearColor;

        const mainDepthDesc = new GfxrRenderTargetDescription(GfxFormat.D32F);
        mainDepthDesc.copyDimensions(mainColorDesc);
        mainDepthDesc.depthClearValue = standardFullClearRenderPassDescriptor.depthClearValue;

        const mainColorTargetID = builder.createRenderTargetID(mainColorDesc, `${this.name} - Main Color (sRGB)`);

        builder.pushPass((pass) => {
            pass.setDebugName('Skybox');
            pass.attachRenderTargetID(GfxrAttachmentSlot.Color0, mainColorTargetID);
            const skyboxDepthTargetID = builder.createRenderTargetID(mainDepthDesc, `${this.name} - Skybox Depth`);
            pass.attachRenderTargetID(GfxrAttachmentSlot.DepthStencil, skyboxDepthTargetID);

            pass.exec((passRenderer) => {
                renderer.executeOnPass(passRenderer, this.skyboxView.mainList);
                renderer.executeOnPass(passRenderer, this.skyboxView.translucentList);
            });
        });

        const mainDepthTargetID = builder.createRenderTargetID(mainDepthDesc, `${this.name} - Main Depth`);

        builder.pushPass((pass) => {
            pass.setDebugName('Main');
            pass.attachRenderTargetID(GfxrAttachmentSlot.Color0, mainColorTargetID);
            pass.attachRenderTargetID(GfxrAttachmentSlot.DepthStencil, mainDepthTargetID);

            this.lateBindTextureAttachPass(renderContext, builder, pass);

            pass.exec((passRenderer, scope) => {
                this.lateBindTextureSetOnPassRenderer(renderer, scope);
                renderer.executeOnPass(passRenderer, this.mainView.mainList);
            });
        });

        if (this.drawIndirect && this.mainView.indirectList.renderInsts.length > 0) {
            builder.pushPass((pass) => {
                pass.setDebugName('Indirect');
                pass.attachRenderTargetID(GfxrAttachmentSlot.Color0, mainColorTargetID);
                pass.attachRenderTargetID(GfxrAttachmentSlot.DepthStencil, mainDepthTargetID);

                const mainColorResolveTextureID = builder.resolveRenderTarget(mainColorTargetID);
                pass.attachResolveTexture(mainColorResolveTextureID);

                const mainDepthResolveTextureID = builder.resolveRenderTarget(mainDepthTargetID);
                pass.attachResolveTexture(mainDepthResolveTextureID);

                let reflectColorResolveTextureID: GfxrResolveTextureID | null = null;
                if (renderer.reflectViewRenderer.outputColorTargetID !== null) {
                    reflectColorResolveTextureID = builder.resolveRenderTarget(renderer.reflectViewRenderer.outputColorTargetID);
                    pass.attachResolveTexture(reflectColorResolveTextureID);
                }

                this.lateBindTextureAttachPass(renderContext, builder, pass);

                pass.exec((passRenderer, scope) => {
                    renderer.setLateBindingTexture(LateBindingTexture.FramebufferColor, scope.getResolveTextureForID(mainColorResolveTextureID), staticResources.linearClampSampler);
                    renderer.setLateBindingTexture(LateBindingTexture.FramebufferDepth, scope.getResolveTextureForID(mainDepthResolveTextureID), staticResources.pointClampSampler);

                    const reflectColorTexture = reflectColorResolveTextureID !== null ? scope.getResolveTextureForID(reflectColorResolveTextureID) : staticResources.opaqueBlackTexture2D;
                    renderer.setLateBindingTexture(LateBindingTexture.WaterReflection, reflectColorTexture, staticResources.linearClampSampler);

                    this.lateBindTextureSetOnPassRenderer(renderer, scope);

                    renderer.executeOnPass(passRenderer, this.mainView.indirectList);
                });
            });
        }

        if (this.mainView.translucentList.renderInsts.length > 0) {
            builder.pushPass((pass) => {
                pass.setDebugName('Translucent');
                pass.attachRenderTargetID(GfxrAttachmentSlot.Color0, mainColorTargetID);
                pass.attachRenderTargetID(GfxrAttachmentSlot.DepthStencil, mainDepthTargetID);
                
                this.lateBindTextureAttachPass(renderContext, builder, pass);

                pass.exec((passRenderer, scope) => {
                    this.lateBindTextureSetOnPassRenderer(renderer, scope);
                    renderer.executeOnPass(passRenderer, this.mainView.translucentList);
                });
            });
        }

        this.outputColorTargetID = mainColorTargetID;
        this.outputColorTextureID = null;
    }

    public resolveColorTarget(builder: GfxrGraphBuilder): GfxrResolveTextureID {
        if (this.outputColorTextureID === null)
            this.outputColorTextureID = builder.resolveRenderTarget(assertExists(this.outputColorTargetID));

        return this.outputColorTextureID;
    }

    public reset(): void {
        this.mainView.reset();
        this.skyboxView.reset();
        this.enabled = false;
        this.outputColorTargetID = null;
        this.outputColorTextureID = null;
    }*/
		}










		public class SourceRenderer// : SceneGfx
		{
			//private LuminanceHistogram luminanceHistogram;
			//public GfxRenderHelper renderHelper;
			//public SkyboxRenderer skyboxRenderer = null;
			public List<BSPRenderer> bspRenderers = new List<BSPRenderer>();

			private TextureMapping[] textureMapping = new TextureMapping[5];
			//private string[] bindingMapping = { LateBindingTexture.Camera, LateBindingTexture.FramebufferColor, LateBindingTexture.FramebufferDepth, LateBindingTexture.WaterReflection, LateBindingTexture.ProjectedLightDepth };

			//public SourceWorldViewRenderer mainViewRenderer = new SourceWorldViewRenderer( "Main View", SourceEngineViewType.MainView );
			//public SourceWorldViewRenderer reflectViewRenderer = new SourceWorldViewRenderer( "Reflection View", SourceEngineViewType.WaterReflectView );

			/*private GfxProgram bloomDownsampleProgram;
			private GfxProgram bloomBlurXProgram;
			private GfxProgram bloomBlurYProgram;
			private GfxProgram fullscreenPostProgram;
			private GfxProgram fullscreenPostProgramBloom;*/

			public SourceRenderContext renderContext;

			public SourceRenderer( SourceRenderContext renderContext ) //SceneContext sceneContext, 
			{
				this.renderContext = renderContext;
				// Make the reflection view a bit cheaper.
				//reflectViewRenderer.drawProjectedShadows = false;
				//reflectViewRenderer.pvsFallback = false;
				//reflectViewRenderer.renderObjectMask &= ~(RenderObjectKind.DetailProps);

				//renderHelper = new GfxRenderHelper( renderContext.device, sceneContext, renderContext.renderCache );
				//renderHelper.renderInstManager.disableSimpleMode();

				//luminanceHistogram = new LuminanceHistogram( renderContext.renderCache );

				/*var cache = renderContext.renderCache;
				bloomDownsampleProgram = cache.CreateProgram( new BloomDownsampleProgram() );
				bloomBlurXProgram = cache.CreateProgram( new BloomBlurProgram( false ) );
				bloomBlurYProgram = cache.CreateProgram( new BloomBlurProgram( true ) );
				fullscreenPostProgram = cache.CreateProgram( new FullscreenPostProgram( false ) );
				fullscreenPostProgramBloom = cache.CreateProgram( new FullscreenPostProgram( true ) );*/

				for ( int i = 0; i < textureMapping.Length; i++ )
				{
					textureMapping[i] = new TextureMapping();
				}
			}


			private void ResetTextureMappings()
			{
				for ( int i = 0; i < this.textureMapping.Length; i++ )
				{
					this.textureMapping[i].Reset();
				}
			}

			/*public void SetLateBindingTexture( LateBindingTexture binding, GfxTexture texture, GfxSampler sampler )
			{
				TextureMapping m = this.textureMapping[this.bindingMapping.IndexOf( binding )];
				Debug.Assert( m != null, "TextureMapping is null" );
				m.gfxTexture = texture;
				m.gfxSampler = sampler;
			}

			public void ExecuteOnPass( GfxRenderPass passRenderer, GfxRenderInstList list )
			{
				var cache = this.renderContext.renderCache;
				for ( int i = 0; i < this.bindingMapping.Length; i++ )
				{
					list.ResolveLateSamplerBinding( this.bindingMapping[i], this.textureMapping[i] );
				}
				list.DrawOnPassRenderer( cache, passRenderer );
			}*/

			/*private void ProcessInput()
			{
				if ( this.sceneContext.inputManager.IsKeyDownEventTriggered( Keys.F ) )
				{
					// happy birthday shigeru miyamoto
					if ( this.mainViewRenderer.Flashlight == null )
					{
						this.mainViewRenderer.Flashlight = new Flashlight( this.renderContext );
					}

					var flashlight = this.mainViewRenderer.Flashlight;
					flashlight.Enabled = !flashlight.Enabled;
					if ( flashlight.Enabled )
					{
						flashlight.Reset( this.renderContext );
					}
				}
			}
			private void Movement()
			{
				// Update render context.

				// TODO(jstpierre): The world lighting state should probably be moved to the BSP? Or maybe SourceRenderContext is moved to the BSP...
				this.renderContext.worldLightingState.Update( this.renderContext.globalTime );

				// Update BSP (includes entities).
				this.renderContext.currentView = this.mainViewRenderer.mainView;

				this.ProcessInput();

				for ( int i = 0; i < this.bspRenderers.Length; i++ )
				{
					this.bspRenderers[i].Movement( this.renderContext );
				}

				this.renderContext.currentView = null;
			}

			private void ResetViews()
			{
				this.mainViewRenderer.Reset();
				this.reflectViewRenderer.Reset();
			}*/

			/*
			public void GetDefaultWorldMatrix(mat4 dst)
			{
				mat4.Identity(dst);
				if (this.bspRenderers.Length == 1)
				{
					var player_start = this.bspRenderers[0].EntitySystem.FindEntityByType(info_player_start);
					if (player_start != null)
					{
						mat4.Mul(dst, noclipSpaceFromSourceEngineSpace, player_start.UpdateModelMatrix());
					}
				}
			}
			*/

			/*public void AdjustCameraController( CameraController c )
			{
				c.SetSceneMoveSpeedMult( 1 / 20 );
			}*/


			/*            const v = showEntityDebug.checked;
            for (let i = 0; i < this.bspRenderers.length; i++) {
                const entityDebugger = this.bspRenderers[i].entitySystem.debugger;
                entityDebugger.capture = v;
                entityDebugger.draw = v;
            }
        };
        renderHacksPanel.contents.appendChild(showEntityDebug.elem);
        const showDebugThumbnails = new UI.Checkbox('Show Debug Thumbnails', false);
        showDebugThumbnails.onchanged = () => {
            const v = showDebugThumbnails.checked;
            this.renderHelper.debugThumbnails.enabled = v;
        };
        renderHacksPanel.contents.appendChild(showDebugThumbnails.elem);

        return [renderHacksPanel];
    }

    public prepareToRender(viewerInput: ViewerRenderInput): void {
        const renderContext = this.renderContext, device = renderContext.device;

        // globalTime is in seconds.
        renderContext.globalTime = viewerInput.time / 1000.0;
        renderContext.globalDeltaTime = viewerInput.deltaTime / 1000.0;
        renderContext.debugStatistics.reset();

        // Update the main view early, since that's what movement/entities will use
        this.mainViewRenderer.mainView.setupFromCamera(viewerInput.camera);
        if (renderContext.currentShake !== null)
            renderContext.currentShake.adjustView(this.mainViewRenderer.mainView);
        this.mainViewRenderer.mainView.finishSetup();

        renderContext.currentPointCamera = null;

        this.movement();

        const renderInstManager = this.renderHelper.renderInstManager;

        const template = this.renderHelper.pushTemplateRenderInst();
        template.setMegaStateFlags({ cullMode: GfxCullMode.Back });
        template.setBindingLayouts(bindingLayouts);

        if (renderContext.currentPointCamera !== null)
            (renderContext.currentPointCamera as point_camera).preparePasses(this);

        this.mainViewRenderer.prepareToRender(this, null);

        // Reflection is only supported on the first BSP renderer (maybe we should just kill the concept of having multiple...)
        if (this.renderContext.enableExpensiveWater && this.mainViewRenderer.drawWorld) {
            const bspRenderer = this.bspRenderers[0], bsp = bspRenderer.bsp;
            bspRenderer.models[0].gatherLiveSets(null, null, bspRenderer.liveLeafSet, this.mainViewRenderer.mainView);
            const leafwater = bsp.findLeafWaterForPoint(this.mainViewRenderer.mainView.cameraPos, bspRenderer.liveLeafSet);
            if (leafwater !== null) {
                const waterZ = leafwater.surfaceZ;

                // Reflect around waterZ
                const cameraZ = this.mainViewRenderer.mainView.cameraPos[2];
                if (cameraZ > waterZ) {
                    // There's probably a much cleaner way to do this, tbh.
                    const reflectView = this.reflectViewRenderer.mainView;
                    reflectView.copy(this.mainViewRenderer.mainView);

                    // Flip the camera around the reflection plane.

                    // This is in Source space
                    computeModelMatrixS(scratchMatrix, 1, 1, -1);
                    mat4.mul(reflectView.worldFromViewMatrix, scratchMatrix, this.mainViewRenderer.mainView.worldFromViewMatrix);

                    // Flip the view upside-down so that when we invert later, the winding order comes out correct.
                    // This will mean we'll have to flip the texture in the shader though. Intentionally adding a Y-flip for once!

                    // This is in noclip space
                    computeModelMatrixS(scratchMatrix, 1, -1, 1);
                    mat4.mul(reflectView.worldFromViewMatrix, reflectView.worldFromViewMatrix, scratchMatrix);

                    const reflectionCameraZ = cameraZ - 2 * (cameraZ - waterZ);
                    reflectView.worldFromViewMatrix[14] = reflectionCameraZ;
                    mat4.invert(reflectView.viewFromWorldMatrix, reflectView.worldFromViewMatrix);

                    scratchPlane.set(Vec3UnitZ, -waterZ);
                    scratchPlane.transform(reflectView.viewFromWorldMatrix);
                    modifyProjectionMatrixForObliqueClipping(reflectView.clipFromViewMatrix, scratchPlane, viewerInput.camera.clipSpaceNearZ);

                    this.reflectViewRenderer.mainView.finishSetup();
                    this.reflectViewRenderer.prepareToRender(this, this.mainViewRenderer);
                }
            }
        }

        this.mainViewRenderer.mainView.useExpensiveWater = this.reflectViewRenderer.enabled;
        renderInstManager.popTemplateRenderInst();

        // Update our lightmaps right before rendering.
        renderContext.lightmapManager.prepareToRender(device);
        renderContext.colorCorrection.prepareToRender(device);
    }

    private pushBloomPasses(builder: GfxrGraphBuilder, mainColorTargetID: GfxrRenderTargetID): GfxrRenderTargetID | null {
        if (!this.renderContext.enableBloom)
            return null;

        if (!this.renderContext.isUsingHDR())
            return null;

        const toneMapParams = this.renderContext.toneMapParams;
        let bloomScale = toneMapParams.bloomScale;
        if (bloomScale <= 0.0)
            return null;

        const renderInstManager = this.renderHelper.renderInstManager;
        const cache = this.renderContext.renderCache;
        const staticResources = this.renderContext.materialCache.staticResources;

        const renderInst = renderInstManager.newRenderInst();
        renderInst.setBindingLayouts(bindingLayoutsBloom);
        renderInst.setInputLayoutAndState(null, null);
        renderInst.setMegaStateFlags(fullscreenMegaState);
        renderInst.drawPrimitives(3);

        let offs = renderInst.allocateUniformBuffer(0, 8);
        const d = renderInst.mapUniformBufferF32(0);
        offs += fillColor(d, offs, toneMapParams.bloomTint, toneMapParams.bloomExp);
        offs += fillVec4(d, offs, bloomScale);

        const mainColorTargetDesc = builder.getRenderTargetDescription(mainColorTargetID);

        const downsampleColorDesc = new GfxrRenderTargetDescription(GfxFormat.U8_RGBA_RT);
        downsampleColorDesc.setDimensions(mainColorTargetDesc.width >>> 2, mainColorTargetDesc.height >>> 2, 1);
        const downsampleColorTargetID = builder.createRenderTargetID(downsampleColorDesc, 'Bloom Buffer');

        builder.pushPass((pass) => {
            pass.setDebugName('Bloom Downsample');
            pass.attachRenderTargetID(GfxrAttachmentSlot.Color0, downsampleColorTargetID);
            pass.pushDebugThumbnail(GfxrAttachmentSlot.Color0);

            const mainColorResolveTextureID = builder.resolveRenderTarget(mainColorTargetID);
            pass.attachResolveTexture(mainColorResolveTextureID);

            pass.exec((passRenderer, scope) => {
                this.resetTextureMappings();

                renderInst.setGfxProgram(this.bloomDownsampleProgram);
                this.textureMapping[0].gfxTexture = scope.getResolveTextureForID(mainColorResolveTextureID);
                this.textureMapping[0].gfxSampler = staticResources.linearClampSampler;
                renderInst.setSamplerBindingsFromTextureMappings(this.textureMapping);
                renderInst.drawOnPass(cache, passRenderer);
            });
        });

        builder.pushPass((pass) => {
            pass.setDebugName('Bloom Blur X');
            pass.attachRenderTargetID(GfxrAttachmentSlot.Color0, downsampleColorTargetID);
            pass.pushDebugThumbnail(GfxrAttachmentSlot.Color0);

            const downsampleResolveTextureID = builder.resolveRenderTarget(downsampleColorTargetID);
            pass.attachResolveTexture(downsampleResolveTextureID);

            pass.exec((passRenderer, scope) => {
                renderInst.setGfxProgram(this.bloomBlurXProgram);
                this.textureMapping[0].gfxTexture = scope.getResolveTextureForID(downsampleResolveTextureID);
                this.textureMapping[0].gfxSampler = staticResources.linearClampSampler;
                renderInst.setSamplerBindingsFromTextureMappings(this.textureMapping);
                renderInst.drawOnPass(cache, passRenderer);
            });
        });

        builder.pushPass((pass) => {
            pass.setDebugName('Bloom Blur Y');
            pass.attachRenderTargetID(GfxrAttachmentSlot.Color0, downsampleColorTargetID);
            pass.pushDebugThumbnail(GfxrAttachmentSlot.Color0);

            const downsampleResolveTextureID = builder.resolveRenderTarget(downsampleColorTargetID);
            pass.attachResolveTexture(downsampleResolveTextureID);

            pass.exec((passRenderer, scope) => {
                renderInst.setGfxProgram(this.bloomBlurYProgram);
                this.textureMapping[0].gfxTexture = scope.getResolveTextureForID(downsampleResolveTextureID);
                this.textureMapping[0].gfxSampler = staticResources.linearClampSampler;
                renderInst.setSamplerBindingsFromTextureMappings(this.textureMapping);
                renderInst.drawOnPass(cache, passRenderer);
            });
        });

        return downsampleColorTargetID;
    }

    public render(device: GfxDevice, viewerInput: ViewerRenderInput) {
        const renderInstManager = this.renderHelper.renderInstManager;
        const renderContext = this.renderContext, cache = renderContext.renderCache;
        const staticResources = renderContext.materialCache.staticResources;
        const builder = this.renderHelper.renderGraph.newGraphBuilder();

        this.resetTextureMappings();

        this.prepareToRender(viewerInput);

        const mainColorDesc = new GfxrRenderTargetDescription(GfxFormat.U8_RGBA_RT_SRGB);
        setBackbufferDescSimple(mainColorDesc, viewerInput);

        // Render the camera
        if (renderContext.currentPointCamera !== null)
            renderContext.currentPointCamera.pushPasses(this, builder, mainColorDesc);

        // Render reflection view first.
        if (this.reflectViewRenderer.enabled)
            this.reflectViewRenderer.pushPasses(this, builder, mainColorDesc);

        this.mainViewRenderer.pushPasses(this, builder, mainColorDesc);
        const mainColorTargetID = assertExists(this.mainViewRenderer.outputColorTargetID);

        this.renderHelper.pushTemplateRenderInst();

        if (this.renderContext.enableAutoExposure && this.renderContext.isUsingHDR()) {
            this.luminanceHistogram.pushPasses(renderInstManager, builder, mainColorTargetID);
            this.luminanceHistogram.updateToneMapParams(this.renderContext.toneMapParams, this.renderContext.globalDeltaTime);
            this.luminanceHistogram.debugDraw(this.renderContext, this.renderContext.toneMapParams);
        }

        const bloomColorTargetID = this.pushBloomPasses(builder, mainColorTargetID);
        renderInstManager.popTemplateRenderInst();

        const mainColorGammaDesc = new GfxrRenderTargetDescription(GfxFormat.U8_RGBA_RT);
        mainColorGammaDesc.copyDimensions(mainColorDesc);
        const mainColorGammaTargetID = builder.createRenderTargetID(mainColorGammaDesc, 'Main Color (Gamma)');

        builder.pushPass((pass) => {
            // Now do a fullscreen color-correction pass to output to our UNORM backbuffer.
            pass.setDebugName('Color Correction & Gamma Correction');
            pass.attachRenderTargetID(GfxrAttachmentSlot.Color0, mainColorGammaTargetID);

            const mainColorResolveTextureID = builder.resolveRenderTarget(mainColorTargetID);
            pass.attachResolveTexture(mainColorResolveTextureID);

            let postProgram = this.fullscreenPostProgram;
            let bloomResolveTextureID: GfxrResolveTextureID | null = null;
            if (bloomColorTargetID !== null) {
                bloomResolveTextureID = builder.resolveRenderTarget(bloomColorTargetID);
                pass.attachResolveTexture(bloomResolveTextureID);
                postProgram = this.fullscreenPostProgramBloom;
            }

            const postRenderInst = renderInstManager.newRenderInst();
            postRenderInst.setBindingLayouts(bindingLayoutsPost);
            postRenderInst.setInputLayoutAndState(null, null);
            postRenderInst.setGfxProgram(postProgram);
            postRenderInst.setMegaStateFlags(fullscreenMegaState);
            postRenderInst.drawPrimitives(3);

            pass.exec((passRenderer, scope) => {
                this.textureMapping[0].gfxTexture = scope.getResolveTextureForID(mainColorResolveTextureID);
                this.textureMapping[0].gfxSampler = staticResources.linearClampSampler;
                this.renderContext.colorCorrection.fillTextureMapping(this.textureMapping[1]);
                this.textureMapping[2].gfxTexture = bloomResolveTextureID !== null ? scope.getResolveTextureForID(bloomResolveTextureID) : null;
                this.textureMapping[2].gfxSampler = staticResources.linearClampSampler;
                postRenderInst.setSamplerBindingsFromTextureMappings(this.textureMapping);
                postRenderInst.drawOnPass(cache, passRenderer);
            });
        });
        this.renderHelper.debugThumbnails.pushPasses(builder, renderInstManager, mainColorGammaTargetID, viewerInput.mouseLocation);

        pushAntialiasingPostProcessPass(builder, this.renderHelper, viewerInput, mainColorGammaTargetID);
        builder.resolveRenderTargetToExternalTexture(mainColorGammaTargetID, viewerInput.onscreenTexture);

        this.renderHelper.prepareToRender();
        this.renderHelper.renderGraph.execute(builder);
        this.resetViews();
        renderInstManager.resetRenderInsts();

        this.renderContext.debugStatistics.addToConsole(viewerInput);
        const camPositionX = this.mainViewRenderer.mainView.cameraPos[0].toFixed(2), camPositionY = this.mainViewRenderer.mainView.cameraPos[1].toFixed(2), camPositionZ = this.mainViewRenderer.mainView.cameraPos[2].toFixed(2);
        viewerInput.debugConsole.addInfoLine(`Source Camera Pos: ${camPositionX} ${camPositionY} ${camPositionZ}`);
    }

    public destroy(device: GfxDevice): void {
        this.renderHelper.destroy();
        this.renderContext.destroy(device);
        this.luminanceHistogram.destroy(device);
        if (this.skyboxRenderer !== null)
            this.skyboxRenderer.destroy(device);
        for (let i = 0; i < this.bspRenderers.length; i++)
            this.bspRenderers[i].destroy(device);
    }*/

		}



	}
}
