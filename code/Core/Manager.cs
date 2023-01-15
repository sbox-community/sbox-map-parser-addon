using Sandbox;
using Sandbox.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using static MapParser.Manager.ContentPackage;
using static MapParser.Render;
using Sandbox.Internal;
using System.Text;
using System.Text.Json;
using static Sandbox.Event;
using System.Diagnostics;

namespace MapParser
{
	public partial class Manager
	{
		public static Dictionary<string, MapObjects> Maps = new();
		static Dictionary<long, IClient> spamProtect = new();
		public static readonly string downloadPath = "map_parser_downloads/";

		static bool joinedCL = false;
		public static Panel mainMenu;

		// because downloaded content via url can be conflicted with other engine contents
		public static List<(string, string, List<string>)> MapMirrors = new();

		// If there is an error about texture, we can handle finding out required texture
		// TODO: checking which map for these errors, it shouldn't be for the last spawned map
		public static List<string> lastTextureErrors = new();

		// Server owners want to remove before client not downloaded yet or ongoing spawning,
		// prevent to spawn or remove on client(s), not implemented yet
		// private static List<string> ongoingSpawningCL = new();

		public static ClientSettings clientSettings = new ClientSettings();

		public struct MapObjects
		{
			public SceneObject so { get; set; }
			public ModelEntity ent { get; set; }
			//public PhysicsBody body { get; set; }
			public SpawnParameter spawnParameter { get; set; }
			public List<string> textureErrors { get; set; }
		}

		public record struct SpawnParameter
		{
			public string mapName { get; set; }
			public string mapPath { get; set; }
			public Vector3 position { get; set; }
			public Angles angles { get; set; }
			public List<string> wadList { get; set; } // GoldSrc, todo: change
			public bool removeOthers { get; set; }
			public bool clearMaterialCache { get; set; }
			public bool clearTextureCache { get; set; }
			public BaseFileSystem fileSystem { get; set; }
			public bool assetparty_version { get; set; }
			public string packageFullIdent { get; set; }
			public To clients { get; set; }

			public string baseUrl { get; set; }
			public string mapUrl { get; set; }
			public List<string> wadUrls { get; set; } // GoldSrc, todo: change
			public string saveFolder { get; set; }

		}

		[Serializable]
		public partial class ClientSettings
		{
			public int spawnPos { get; set; } = 0;
			public bool clearTextureCache { get; set; } = true;
			public bool clearMaterialCache { get; set; } = true;
			public bool clearMaps { get; set; } = true;
		}

		[ConCmd.Server( "mapparser_command" )]
		public static void adminConCommandHandler( uint flag, string fullIdent = "", string mapName = "", int spawnPos = 0, string baseUrl = "", string wadlist = "", string savefolder = "unknown_engine", bool clearTextureCache = true, bool clearMaterialCache = true, bool clearMaps = true )
		{
			var caller = ConsoleSystem.Caller;

			if ( !caller.IsListenServerHost && ConsoleSystem.GetValue( "sv_cheats", "0" ) == "0" )
			{
				Notify.CreateCL( To.Single(caller), "sv_cheats must be 1", Notify.NotifyType.Error );
				return;
			}

			if ( flag == 0 || flag == 3 )
			{
				var pos = Vector3.Zero;
				var ang = Angles.Zero;

				if ( spawnPos == 0 ) // Player's position
				{
					pos = caller.Position;
					//ang = caller.Rotation.Angles(); // Physics is not working?
				}
				else if ( spawnPos == 1 ) // Player's aim position
					pos = caller.AimRay.Position;
				else if ( spawnPos == 2 ) // The Entity's position
				{
					var findLast = MPEntity.All.Where(x=>x.GetType()==typeof(MPEntity)).LastOrDefault();

					if( findLast != null )
						pos = findLast.Position;
				}

				var settings = new SpawnParameter()
				{
					mapName = mapName,
					position = pos,
					angles = ang,
					removeOthers = clearMaps,
					clearMaterialCache = clearMaterialCache,
					clearTextureCache = clearTextureCache,
					packageFullIdent = fullIdent,
					wadList = new(),
					clients = To.Everyone
				};

				if ( flag == 0 )
				{
					// If we have wadlist from parameter, we can use
					if( !string.IsNullOrEmpty( wadlist ) )
						settings.wadList = wadlist.Split( "," ).ToList();

					settings.assetparty_version = true;
					mapSpawnViaAssetParty( settings );
				}
				else
				{
					List<string> wadlistList = wadlist.Split( "," ).ToList();
					settings.wadUrls = new();
					foreach ( var wad in wadlistList )
						if ( !string.IsNullOrEmpty( wad ) )
							settings.wadUrls.Add( $"{baseUrl}{wad}" );

					settings.baseUrl = baseUrl;
					settings.mapUrl = $"{baseUrl}{mapName}";
					settings.assetparty_version = false;
					settings.saveFolder = $"{savefolder}/";

					mapSpawnViaURL( settings );
				}
			}
			else if ( flag == 1 )
			{
				if ( removeMap( mapName ) )
				{
					Notify.Create( "Map Removed", Notify.NotifyType.Info );
					removeMap_cl( To.Everyone, mapName );
				}
				//Package.FindAsync( "+mapparser type:addon" ).Result.Packages.FirstOrDefault().
			}
			else if ( flag == 2 )
			{
				caller.Pawn.Position = Maps.Where( x => x.Value.spawnParameter.mapName == mapName ).FirstOrDefault().Value.spawnParameter.position;
			}
			//else if ( flag == 3 ) { } // For mounted contents, is it necessary?
		}
		[ConCmd.Client( "mapparser_menu" )]
		public static void clientConCommandHandler_Menu()
		{
			if ( mainMenu != null && mainMenu.IsValid() )
				mainMenu.Delete();

			mainMenu = Game.RootPanel.AddChild<Menu>();
			_ = Util.Timer( 10, () =>
			{
				if ( mainMenu != null && mainMenu.IsValid() )
					mainMenu.AddClass( "Open" );
					(mainMenu as Menu).firstOpen();
			} );
		}

		public async static void mapSpawnViaAssetParty( SpawnParameter settings )
		{
			//var hasCached = Package.TryGetCached( settings.packageFullIdent, out Package package, false );
			//package = hasCached && !forceFetch ? package : await Package.FetchAsync( settings.packageFullIdent, false );

			var package = await Package.FetchAsync( settings.packageFullIdent, false );

			if ( package == null )
			{
				Notify.Create( "Package not found!", Notify.NotifyType.Error );
				return;
			}
			var filesystem = await package.MountAsync( false );
			var content = retrieveContent( package, filesystem ).Where( x => x.bsp.Contains( $"{settings.mapName}.bsp" ) );

			if ( !content.Any() )
			{
				Notify.Create( "Map not found!", Notify.NotifyType.Error );
				return;
			}

			var map = content.FirstOrDefault();

			settings.mapPath = map.bsp;
			settings.fileSystem = filesystem;
			if ( Game.IsClient )
			{
				settings.wadList.AddRange( map.goldsrc_wads.SelectMany( x => x.Values ).ToList());

				// If there is a .res file, we can obtain the required wads of the map
				if ( filesystem.FileExists( $"{settings.mapPath.Replace( ".bsp", ".res.txt" )}" ) )
					foreach ( string line in filesystem.ReadAllText( $"{settings.mapPath.Replace( ".bsp", ".res.txt" )}" ).Split( '\n' ) )
						if ( line.Contains( ".wad" ) )
							settings.wadList.Add( Util.PathToMapName( line ) );

				settings.wadList = settings.wadList.Distinct().ToList();
			}
			settings.position = settings.position == Vector3.Zero ? map.offset : (map.offset + settings.position);
			settings.angles = settings.angles == Angles.Zero ? map.angles : (map.angles + settings.angles);

			mapSpawner( settings );
		}

		public async static void mapSpawnViaURL( SpawnParameter settings )
		{
			settings.fileSystem = FileSystem.Data;
			var mapPath = Util.RemoveInvalidChars( $"{downloadPath}{settings.saveFolder}{settings.mapName}" );

			if ( !FileSystem.Data.DirectoryExists( Util.PathWithouthFile( mapPath ) ) )
				FileSystem.Data.CreateDirectory( Util.PathWithouthFile( mapPath ) );

			// Check if .bsp is not downloaded yet, maybe there can be a checksum control..
			if ( !FileSystem.Data.FileExists( mapPath ) )
			{
				DownloadNotification.CreateInfo( $"Downloading.. ( {settings.mapUrl} )" );
				var http = new Http( new Uri( $"{settings.mapUrl}" ) );
				var data = await http.GetBytesAsync();

				if ( Encoding.ASCII.GetString( data, 0, 4 ) != "<!DO" )
				{
					FileSystem.Data.WriteAllText( $"{mapPath}", Convert.ToBase64String( data ) );
					DownloadNotification.CreateInfo( $"Download successful ( {settings.mapUrl} )", 5f );
				}
				else
				{
					DownloadNotification.CreateInfo( $"Download failed ( {settings.mapUrl} )", 10f );
					Notify.Create( "Map not found in url!", Notify.NotifyType.Error );
					return;
				}

				http.Dispose();
			}

			settings.mapPath = mapPath;

			// Check if .res file is exists, in order to get the list of wads of the map
			if ( !FileSystem.Data.FileExists( $"{mapPath.Replace( ".bsp", ".res" )}" ) )
			{
				var http = new Http( new Uri( $"{settings.mapUrl.Replace( ".bsp", ".res" )}" ) );
				var data = await http.GetStringAsync();

				if ( !data.Contains( "<!DOCTYPE" ) )
				{
					DownloadNotification.CreateInfo( $"Download successful ( {settings.mapUrl.Replace( ".bsp", ".res" )} )", 5f );
					FileSystem.Data.WriteAllText( $"{mapPath.Replace( ".bsp", ".res" )}", data );
				}

				http.Dispose();

				foreach ( string line in data.Split( '\n' ) )
					if ( line.Contains( ".wad" ) )
						settings.wadList.Add( $"{Util.PathToMapName( line )}.wad" );
			}
			else
				foreach ( string line in FileSystem.Data.ReadAllText( $"{mapPath.Replace( ".bsp", ".res" )}" ).Split( '\n' ) )
					if ( line.Contains( ".wad" ) )
						settings.wadUrls.Add( $"{settings.baseUrl}{Util.PathToMapName( line )}" );

			// Download wads in .res files and the send from player
			foreach ( var wadurl in settings.wadUrls )
			{
				var wadName = Util.PathToMapName( wadurl );

				if ( string.IsNullOrEmpty( wadName ) )
					continue;

				var wadPath = $"{downloadPath}{settings.saveFolder}{wadName}.wad";
				if ( !FileSystem.Data.FileExists( wadPath ) )
				{
					DownloadNotification.CreateInfo( $"Downloading.. ( {wadurl} )" );
					var http = new Http( new Uri( $"{wadurl}" ) );
					var data = await http.GetBytesAsync();

					if ( Encoding.ASCII.GetString( data, 0, 4 ) != "<!DO" )
					{
						DownloadNotification.CreateInfo( $"Download successful ( {wadurl} )", 5f );
						FileSystem.Data.WriteAllText( wadPath, Convert.ToBase64String( data ) );
					}
					else
					{
						DownloadNotification.CreateInfo( $"Download failed ( {wadurl} )", 10f );
						Notify.Create( $"{wadName}.wad not found in url {wadurl}", Notify.NotifyType.Error );
					}

					http.Dispose();
				}
				settings.wadList.Add( $"{wadName}.wad" );
			}
			mapSpawner( settings );
		}
		/*[ConCmd.Admin( "mapparser_spawnurl" )]
		public async static void mapSpawnViaURL( string url, string mapName )
		{
		}*/

		public static void mapSpawner( SpawnParameter settings )
		{
			if ( Game.IsServer && spawnMap( settings ) )
			{
				Notify.Create( "Map Created", Notify.NotifyType.Info );

				if ( settings.assetparty_version )
					spawnMap_cl( To.Everyone, settings.mapName, settings.position, settings.angles, settings.removeOthers, settings.clearMaterialCache, settings.clearTextureCache, settings.packageFullIdent, assetparty_version: true, wadlist: string.Join( ",", settings.wadList ) );
				else
					spawnMap_cl( To.Everyone, settings.mapName, settings.position, settings.angles, settings.removeOthers, settings.clearMaterialCache, settings.clearTextureCache, assetparty_version: false, baseurl: settings.baseUrl, savefolder: settings.saveFolder, wadlist: string.Join( ",", settings.wadList ) );
			}

			if ( Game.IsClient && spawnMap( settings ) )
				Notify.Create( "Map Created", Notify.NotifyType.Info );
		}

		public static bool spawnMap( SpawnParameter settings )
		{
			if ( settings.removeOthers )
				removeAllMap();
			else
				removeMap( settings.mapName );

			if ( Game.IsClient )
			{
				//pendingMaterials.Clear();
				lastTextureErrors.Clear();

				if ( settings.clearMaterialCache )
					MaterialCache.clearMaterial();

				if ( settings.clearTextureCache )
					TextureCache.clearTexture();

				// Must be loaded after spawning of map for async
				foreach ( var wad in settings.wadList )
					GoldSrc.TextureCache.addWAD( wad, settings );
			}

			MapObjects mapObject = new MapObjects();
			mapObject.spawnParameter = settings;

			//mapObject.body = new PhysicsBody( Game.PhysicsWorld );

			var bspData = new GoldSrc.BSPFile( settings );

			var model = Model.Builder;
			List<Mesh> meshes = new List<Mesh>();

			foreach ( var meshdata in bspData.meshDataList )
			{
				var mesh = Game.IsClient ? new Mesh( MaterialCache.materialData[meshdata.textureName].material ) : new Mesh();
				mesh.CreateVertexBuffer( meshdata.vertices.Count(), SimpleVertex.Layout, meshdata.vertices );
				mesh.CreateIndexBuffer( meshdata.indices.Count(), meshdata.indices.ToArray() );
				meshes.Add( mesh );

				if ( Game.IsServer )
					model.AddCollisionMesh( meshdata.vertices.Select( x => x.position ).ToArray(), meshdata.indices.ToArray() );

				//body.AddMeshShape( meshdata.vertices.Select( x => x.position ).ToArray(), meshdata.indices.ToArray() );
			}

			if ( Game.IsClient )
				model.AddMeshes( meshes.ToArray() );

			mapObject.ent = new();
			mapObject.ent.Model = model.Create();

			if ( Game.IsServer )
				mapObject.ent.SetupPhysicsFromModel( PhysicsMotionType.Static );

			mapObject.ent.Position = settings.position;
			mapObject.ent.PhysicsEnabled = false;
			mapObject.ent.EnableDrawing = false;
			mapObject.ent.Tags.Add( "solid" );
			mapObject.ent.EnableTraceAndQueries = true;
			mapObject.ent.Predictable = false;

			// There are problems like bullet holes..
			//mapObject.ent.Transmit = TransmitType.Never;

			if ( Game.IsClient )
			{
				mapObject.so = new SceneObject( Game.SceneWorld, mapObject.ent.Model, new Transform( settings.position, Rotation.From( settings.angles ) ) );

				mapObject.textureErrors = lastTextureErrors.ToList();
				if( mapObject.textureErrors.Count() != 0 )
					Notify.Create( "Similar textures are found! You can try to spawn with similar of them..", Notify.NotifyType.Info );
				lastTextureErrors.Clear();

				// For async loading, but disabled
				//foreach ( var wad in settings.wadList )
				//GoldSrc.TextureCache.addWAD( wad, settings );
			}

			Maps.Add( settings.mapName, mapObject );

			return true;
		}

		[ClientRpc]
		public static void spawnMap_cl( string mapName, Vector3 position, Angles angles, bool removeOthers, bool clearMaterialCache, bool clearTextureCache, string packageFullIdent = "", bool assetparty_version = true, string baseurl = "", string savefolder = "", string wadlist = "" )
		{

			/*List<string> wadList = new();
			using ( var stream = new MemoryStream( wadListData ) )
			using ( var reader = new BinaryReader( stream ) )
			{
				var count = reader.ReadInt32();
				for ( var i = 0; i < count; i++ )
					wadList.Add( reader.ReadString() );
			}
			*/
			var settings = new SpawnParameter()
			{
				mapName = mapName,
				position = position,
				angles = angles,
				removeOthers = removeOthers,
				clearMaterialCache = clearMaterialCache,
				clearTextureCache = clearTextureCache,
				assetparty_version = assetparty_version,
				packageFullIdent = packageFullIdent,
				wadList = new()
			};

			if ( !assetparty_version )
			{
				settings.baseUrl = baseurl;
				settings.mapUrl = $"{baseurl}{mapName}";
				settings.saveFolder = savefolder;

				List<string> wadlistList = wadlist.Split( "," ).ToList();
				settings.wadUrls = new();

				foreach ( var wad in wadlistList )
					settings.wadUrls.Add( $"{settings.baseUrl}{wad}" );
			}
			else
			{
				if(!string.IsNullOrEmpty(wadlist))
					settings.wadList = wadlist.Split( "," ).ToList();
			}

			if ( assetparty_version )
				mapSpawnViaAssetParty( settings );
			else
				mapSpawnViaURL( settings );
		}

		public static bool removeMap( string mapName )
		{
			if ( Maps.TryGetValue( mapName, out var map ) )
			{

				if ( map.so != null && map.so.IsValid() )
					map.so.Delete();

				if ( map.ent != null && map.ent.IsValid() )
					map.ent.Delete();

				//if ( map.body != null && map.body.IsValid() )
				//map.body.Remove();

				_ = Maps.Remove( mapName );

				return true;
			}
			return false;
		}

		public static bool removeAllMap()
		{
			List<string> mapNames = new List<string>();
			foreach ( var name in Maps.Keys )
				mapNames.Add( name );
			foreach ( var name in mapNames )
				_ = removeMap( name );
			return true;
		}
		[ClientRpc]
		public static void removeMap_cl( string mapName = "" )
		{
			bool all = string.IsNullOrEmpty( mapName );
			if ( all ? removeAllMap() : removeMap( mapName ) )
				Notify.Create( $"Map{(all ? "s" : "")} Removed", Notify.NotifyType.Info );
		}

		public partial class ContentPackage
		{
			[Serializable]
			public struct PackageInfo
			{
				public string name { get; set; }
				public string desp { get; set; }
				public string engine { get; set; }
				public string game { get; set; }
				public string bsp { get; set; }
				public string dependencies { get; set; }
				public Vector3 offset { get; set; }
				public Angles angles { get; set; }

				// GoldSrc info
				public List<Dictionary<string, string>> goldsrc_wads { get; set; }
			}

			private static bool isMPContentPackage( Package package, BaseFileSystem filesystem ) => filesystem.FileExists( $"{package.FullIdent}.txt" );

			public static List<PackageInfo> retrieveContent( Package package, BaseFileSystem filesystem )
			{
				if ( !isMPContentPackage( package, filesystem ) )
				{
					Notify.Create( "Map information not found", Notify.NotifyType.Error);
					return new();
				}

				return filesystem.ReadJson<List<PackageInfo>>( $"{package.FullIdent}.txt" );
			}
		}

		// Events and game's overrides can't accessible?

		[ConCmd.Server]
		public static void ClientJoinedHandler()
		{
			var client = ConsoleSystem.Caller;

			if ( spamProtect.TryGetValue( client.SteamId, out var cl ) )
			{
				if ( cl.Equals( client ) )
					return;
				else
				{
					spamProtect.Remove( client.SteamId );
					spamProtect.Add( client.SteamId, client );
				}
			}
			else
				spamProtect.Add( client.SteamId, client );

			if ( Maps.Count > 0 )
				foreach ( var map in Maps )
					spawnMap_cl( To.Single( client ),
						map.Value.spawnParameter.mapName,
						map.Value.spawnParameter.position,
						map.Value.spawnParameter.angles,
						map.Value.spawnParameter.removeOthers,
						map.Value.spawnParameter.clearMaterialCache,
						map.Value.spawnParameter.clearTextureCache,
						map.Value.spawnParameter.packageFullIdent,
						map.Value.spawnParameter.assetparty_version,
						string.Join(",", map.Value.spawnParameter.wadList) );
		}

		[Event.Client.Frame]
		public static void ClientJoinedHandlerCL()
		{
			if ( !joinedCL )
			{
				joinedCL = true;
				ClientJoinedHandler();
				loadSettings();
			}
		}

		public static void loadSettings()
		{
			if ( FileSystem.Data.FileExists( "map_parser_cl_settings.txt" ) )
			{
				clientSettings = JsonSerializer.Deserialize<ClientSettings>( FileSystem.Data.ReadAllText( "map_parser_cl_settings.txt" ) );
				Notify.Create( "Settings have been loaded!" );
			}
		}

		public static void saveSettings()
		{
			FileSystem.Data.WriteAllText( "map_parser_cl_settings.txt", JsonSerializer.Serialize( clientSettings ) );
			Notify.Create("Settings have been saved!");
		}
		
		/*public static byte[] wadWriter( List<string> wadLists )
		{
			//byte wadData = new byte[wadLists.Count];
			var count = wadLists.Count;
			if ( count == 0 )d
				return new byte[0];

			using ( var stream = new MemoryStream() )
			{
				using ( var writer = new BinaryWriter( stream ) )
				{
					
					writer.Write( count );
					for ( var i = 0; i < count; i++ )
						writer.Write( wadLists[i] );
					return stream.ToArray();
				}
			}
		}*/

		/*public static class Debug
		{ 
			public static bool debug = false;

			public static List<string> loadedTextures = new();
			public static List<string> failedTextures = new();
			public static List<string> usedWAD = new();

			[ConCmd.Admin( "maploader_toggledebug" )]
			public static void toggleDebug() => Log.Info( $"[MapLoader] Debug { ((debug = !debug) ? "Enabled" : "Disabled")}" );

			public static void printDebugLogs()
			{
				loadedTextures.ForEach( x => { Log.Info(x); } );
				failedTextures.ForEach( x => { Log.Info(x); } );
			}

			public static void clearDebugLogs()
			{
				loadedTextures.Clear();
				failedTextures.Clear();
			}
		}*/

		/*[ClientRpc]
		public static void sendTexture( byte[] textureData, int Width, int Height )
		{
			var textureName = Texture.Create( Width, Height );
			textureName.WithData( textureData );

			var textureFinish = textureName.Finish();

			Panel test = Game.RootPanel.FindRootPanel().Add.Panel();
			test.Style.Width = Length.Fraction( 0.5f );
			test.Style.Height = Length.Fraction( 0.5f );
			test.Style.BackgroundImage = textureFinish;
			test.Style.Position = PositionMode.Absolute;

			_ = Timer( 3000, () =>
			{
				test.Delete();
			} );
		}
		async public static Task Timer( int s, Action callback )
		{
			await System.Threading.Tasks.Task.Delay( s );
			callback?.Invoke();
		}*/
	}
	public partial class MPEntity : AnimatedEntity, IUse
	{
		private EntityTag tag;
		public MPEntity()
		{
			if ( Game.IsServer )
			{
				SetModel( "models/editor/cordon_helper.vmdl" );
				SetupPhysicsFromModel( PhysicsMotionType.Dynamic );
			}
			if ( Game.IsClient )
			{
				tag = Game.RootPanel.FindRootPanel().AddChild<EntityTag>();
				tag.EntMP = this;

				Notify.Create( "Map Parser is loaded!" );
			}
		}
		~MPEntity()
		{
			if ( Game.IsClient )
				tag.Delete();
		}
		public bool IsUsable( Sandbox.Entity user ) => true;

		public bool OnUse( Sandbox.Entity user )
		{
			user.Client.SendCommandToClient( "mapparser_menu" );
			return false;
		}
		/*[ConCmd.Server( "mp_spawn" )]
		public static void asd()
		{
			var ply = ConsoleSystem.Caller;
			var ent = new MapParser.Manager.Entity();
			ent.Position = ply.Position;
		}*/
	}
}
