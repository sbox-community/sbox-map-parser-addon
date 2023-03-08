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



		public class SourceRenderContext
		{
			//public EntityFactoryRegistry entityFactoryRegistry;
			public SourceFileSystem filesystem;
			//public LightmapManager lightmapManager;
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

			public SourceRenderContext() // GfxDevice device, SourceLoadContext loadContext
			{
				/*this.entityFactoryRegistry = loadContext.entityFactoryRegistry;
				this.filesystem = loadContext.filesystem;

				this.renderCache = new GfxRenderCache( device );
				this.lightmapManager = new LightmapManager( device, this.renderCache );
				this.materialCache = new MaterialCache( device, this.renderCache, this.filesystem );
				this.studioModelCache = new StudioModelCache( this, this.filesystem );
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




	}
}
