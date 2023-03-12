// sbox.Community © 2023-2024

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
using System.Threading.Tasks;
using MapParser.SourceEngine;
using static MapParser.SourceEngine.Main;
using MapParser.GoldSrc.Entities;

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
			public GoldSrc.Entities.Map Map { get; set; }
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
			public BaseFileSystem fileSystem { get; set; }
			public bool assetparty_version { get; set; }
			public string packageFullIdent { get; set; }
			public To clients { get; set; }
			public List<Vector3> AABB { get; set; } // From server
			public Vector3 center { get; set; } // From server
			public string skyPath { get; set; }
			public string skyName { get; set; }
			public List<Vector3>? spawnPoints { get; set; }
			public SceneWorld sceneWorld { get; set; }
			public bool spawnModels { get; set; }

			public string baseUrl { get; set; }
			public string mapUrl { get; set; }
			public List<string> wadUrls { get; set; } // GoldSrc, todo: change
			public List<string> skyUrls { get; set; } // GoldSrc, todo: change
			public List<string> mdlUrls { get; set; } // GoldSrc, todo: change
			public string saveFolder { get; set; }
			public IClient owner { get; set; }
		}

		[Serializable]
		public partial class ClientSettings
		{
			public int spawnPos { get; set; } = 0;
			public bool clearMaps { get; set; } = true;
			public bool pixelation { get; set; } = false;
			public bool spawnModels { get; set; } = false;
		}

		[ConCmd.Server( "mapparser_command" )]
		public static void adminConCommandHandler( uint flag, string fullIdent = "", string mapName = "", int spawnPos = 0, string baseUrl = "", string wadlist = "", string savefolder = "unknown_engine", bool clearMaps = true, bool spawnModels = false )
		{
			var caller = ConsoleSystem.Caller;

			if ( !caller.IsListenServerHost && ConsoleSystem.GetValue( "sv_cheats", "0" ) == "0" )
			{
				Notify.CreateCL( To.Single( caller ), "sv_cheats must be 1", Notify.NotifyType.Error );
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

				var settings = new SpawnParameter()
				{
					mapName = mapName,
					position = pos,
					angles = ang,
					removeOthers = clearMaps,
					packageFullIdent = fullIdent,
					wadList = new(),
					owner = caller,
					clients = To.Everyone,
					spawnModels = spawnModels
				};

				Notify.tryRemoveNotificationPanel();
				DownloadNotification.RemoveAll();

				if ( !string.IsNullOrEmpty( wadlist ) )
					wadlist = Util.Decompress<string>( Convert.FromBase64String( wadlist ) );

				if ( flag == 0 )
				{
					// If we have wadlist from parameter, we can use
					if ( !string.IsNullOrEmpty( wadlist ) )
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
				var spawnParameter = Maps.Where( x => x.Value.spawnParameter.mapName == mapName ).FirstOrDefault().Value.spawnParameter;
				// TODO: Set position even on noclip
				caller.Pawn.Position = spawnParameter.spawnPoints is not null ? spawnParameter.spawnPoints[Game.Random.Int( 0, spawnParameter.spawnPoints.Count - 1 )] + spawnParameter.position : spawnParameter.position; // + meshdata.vOrigin, is it needed?
			}
			//else if ( flag == 3 ) { } // For mounted contents, is it necessary?
		}
		[ConCmd.Client( "mapparser_menu" )]
		public static void clientConCommandHandler_Menu()
		{
			if ( mainMenu != null && mainMenu.IsValid())
				(mainMenu as Menu).CloseMenu();

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
			settings.position = settings.position == Vector3.Zero ? map.offset : (map.offset + settings.position);
			settings.angles = settings.angles == Angles.Zero ? map.angles : (map.angles + settings.angles);

			if ( Game.IsClient )
			{
				// If there is a .res file, we can obtain the required wads of the map
				if ( filesystem.FileExists( $"{settings.mapPath.Replace( ".bsp", ".res.txt" )}" ) )
					foreach ( string line in filesystem.ReadAllText( $"{settings.mapPath.Replace( ".bsp", ".res.txt" )}" ).Split( '\n' ) )
					{
						if ( line.Contains( ".wad" ) )
							settings.wadList.Add( $"{Util.PathToMapName( line )}.wad" );

						if ( line.Contains( ".tga" ) )
							if ( string.IsNullOrEmpty( settings.skyPath ) )
								settings.skyPath = $"{Util.PathWithouthFile( line )}\\";
					}
						
				settings.wadList = settings.wadList.Distinct().ToList();
			}
			
			_ = await spawnMap( settings );
		}

		public async static void mapSpawnViaURL( SpawnParameter settings )
		{
			// TODO: Checking Server or Client

			settings.fileSystem = FileSystem.Data;
			var mapPath = Util.RemoveInvalidChars( $"{downloadPath}{settings.saveFolder}{settings.mapName}" );

			if ( !FileSystem.Data.DirectoryExists( Util.PathWithouthFile( mapPath ) ) )
				FileSystem.Data.CreateDirectory( Util.PathWithouthFile( mapPath ) );

			// Check if .bsp is not downloaded yet, maybe there can be a checksum control..
			if ( !FileSystem.Data.FileExists( mapPath ) )
			{
				var notify = DownloadNotification.CreateInfo( $"Downloading.. ( {settings.mapUrl} )" );
				var http = new Http( new Uri( $"{settings.mapUrl}" ) );

				try
				{
					var data = await http.GetBytesAsync();

					if ( Encoding.ASCII.GetString( data, 0, 4 ) != "<!DO" )
					{
						FileSystem.Data.WriteAllText( $"{mapPath}", Convert.ToBase64String( data ) );
						notify?.FinishDownload();
					}
					else
					{
						notify?.FailedDownload();
						Notify.Create( "Map not found in url!", Notify.NotifyType.Error );
						http.Dispose();
						return;
					}
				}
				catch
				{
					Notify.Create( "Map not found in url!", Notify.NotifyType.Error ); // Generally giving 404
					http.Dispose();
					return;
				}
				http.Dispose();
			}

			settings.mapPath = mapPath;
			settings.mdlUrls = new(); // don't initialize here

			if(Game.IsClient)
				settings.skyUrls = new(); // don't initialize here

			// Check if .res file is exists, in order to get the list of wads of the map and also sky files
			if ( !FileSystem.Data.FileExists( $"{mapPath.Replace( ".bsp", ".res" )}" ) )
			{
				var http = new Http( new Uri( $"{settings.mapUrl.Replace( ".bsp", ".res" )}" ) );
				var data = await http.GetStringAsync();

				if ( !data.Contains( "<!DOCTYPE" ) )
				{
					var notify = DownloadNotification.CreateInfo( "", 5f );
					notify?.FinishDownload( $"Download successful ( {settings.mapUrl.Replace( ".bsp", ".res" )} )" );
					FileSystem.Data.WriteAllText( $"{mapPath.Replace( ".bsp", ".res" )}", data );
				}
				else
				{
					var notify = DownloadNotification.CreateInfo( "", 5f );
					notify?.FailedDownload( $"Download failed ( {settings.mapUrl.Replace( ".bsp", ".res" )} )" );
				}

				http.Dispose();

				foreach ( string line in data.Split( '\n' ) )
				{
					if ( line.Contains( ".mdl" ) )
						settings.mdlUrls.Add( line );

					if ( Game.IsClient )
					{ 
						if ( line.Contains( ".wad" ) )
						settings.wadUrls.Add( $"{settings.baseUrl}{Util.PathToMapName( line )}" );

						if ( line.Contains( ".tga" ) )
						{
							// If skyname is not found in .bsp, we trying to find from the .res file
							var sanitized = Util.RemoveInvalidChars( line );
							var filename = Util.PathToMapName( sanitized );

							if ( string.IsNullOrEmpty( settings.skyName ) || string.IsNullOrWhiteSpace( settings.skyName ) )
								settings.skyName = filename.Substring( 0, filename.Length - 2 );

							settings.skyUrls.Add( $"{settings.baseUrl}{sanitized}" );
						}
					}
				}
			}
			else
				foreach ( string line in FileSystem.Data.ReadAllText( $"{mapPath.Replace( ".bsp", ".res" )}" ).Split( '\n' ) )
				{
					if ( line.Contains( ".mdl" ) )
						settings.mdlUrls.Add( line );

					if ( Game.IsClient )
					{
						if ( line.Contains( ".wad" ) )
							settings.wadUrls.Add( $"{settings.baseUrl}{Util.PathToMapName( line )}" );

						if ( line.Contains( ".tga" ) )
						{
							var sanitized = Util.RemoveInvalidChars( line );
							var filename = Util.PathToMapName( sanitized );
							if ( string.IsNullOrEmpty( settings.skyName ) || string.IsNullOrWhiteSpace( settings.skyName ) )
								settings.skyName = filename.Substring( 0, filename.Length - 2 );

							settings.skyUrls.Add( $"{settings.baseUrl}{sanitized}" );
						}
					}
				}

			

			// Download .mdl in .res files, also we need look into entity lump in order to get the other .mdls
			// Should parallel downloading, model spawning is could be done after map spawning like textures
			for ( var i = 0; i < settings.mdlUrls.Count(); i++ )
			{
				var mdlUrl = Util.RemoveInvalidChars( settings.mdlUrls[i] );
				var mdlName = Util.PathToMapName( mdlUrl );

				if ( string.IsNullOrEmpty( mdlName ) )
					continue;

				var mdlPath = $"{downloadPath}{settings.saveFolder}{mdlUrl}";
				if ( !FileSystem.Data.FileExists( mdlPath ) )
				{
					var notify = DownloadNotification.CreateInfo( $"Downloading.. ( {mdlUrl} )" );
					var http = new Http( new Uri( $"{settings.baseUrl}{mdlUrl}" ) );

					try
					{
						var data = await http.GetBytesAsync();

						if ( Encoding.ASCII.GetString( data, 0, 4 ) != "<!DO" )
						{
							notify?.FinishDownload( $"Download successful ( {mdlUrl} )" );

							if ( !FileSystem.Data.DirectoryExists( Util.PathWithouthFile( mdlPath ) ) )
								FileSystem.Data.CreateDirectory( Util.PathWithouthFile( mdlPath ) );

							FileSystem.Data.WriteAllText( mdlPath, Convert.ToBase64String( data ) );
						}
						else
						{
							notify?.FailedDownload();
							Notify.Create( $"{mdlName}.mdl not found in url {mdlUrl}", Notify.NotifyType.Error );
						}
					}
					catch
					{
						Notify.Create( $"{mdlPath}.mdl not found in url {mdlPath}", Notify.NotifyType.Error );
					}

					http.Dispose();
				}
			}

			if ( Game.IsClient )
			{
				// Download wads in .res files
				for ( var i = 0; i < settings.wadUrls.Count(); i++ )
				{
					var wadurl = settings.wadUrls[i];
					var wadName = Util.PathToMapName( wadurl );

					if ( string.IsNullOrEmpty( wadName ) )
						continue;

					var wadPath = $"{downloadPath}{settings.saveFolder}{wadName}.wad";
					if ( !FileSystem.Data.FileExists( wadPath ) )
					{
						var notify = DownloadNotification.CreateInfo( $"Downloading.. ( {wadurl} )" );
						var http = new Http( new Uri( $"{wadurl}" ) );

						try
						{
							var data = await http.GetBytesAsync();

							if ( Encoding.ASCII.GetString( data, 0, 4 ) != "<!DO" )
							{
								notify?.FinishDownload( $"Download successful ( {wadurl} )" );
								FileSystem.Data.WriteAllText( wadPath, Convert.ToBase64String( data ) ); // don't have to convert to base64
							}
							else
							{
								notify?.FailedDownload();
								Notify.Create( $"{wadName}.wad not found in url {wadurl}", Notify.NotifyType.Error );

								// Some wad filename has uppercase
								http = new Http( new Uri( $"{wadurl.Replace( wadName, wadName.ToUpper() )}" ) );
								try
								{
									data = await http.GetBytesAsync();
									if ( Encoding.ASCII.GetString( data, 0, 4 ) != "<!DO" )
									{
										settings.wadUrls[i] = settings.wadUrls[i].Replace( wadName, wadName.ToUpper() );
										i--;
										http.Dispose();
										continue;
									}
								}
								catch { }
							}
						}
						catch
						{
							Notify.Create( $"{wadName}.wad not found in url {wadurl}", Notify.NotifyType.Error );
						}

						http.Dispose();
					}
					settings.wadList.Add( $"{Util.PathToMapName( wadName )}.wad" );
					settings.wadList = settings.wadList.Distinct().ToList();
				}

				// If not found .res files for sky files, try to look gfx/env/ folder
				if ( settings.skyUrls.Count == 0 && settings.skyName != null && !string.IsNullOrEmpty( settings.skyName ) )
				{
					Notify.Create( $"Sky files not found in .res, trying to look 'gfx/env/'", Notify.NotifyType.Info );

					for ( var i = 0; i < 6; i++ )
					{
						var suffix = "";
						if ( i == 0 )
							suffix = "dn";
						if ( i == 1 )
							suffix = "up";
						if ( i == 2 )
							suffix = "rt";
						if ( i == 3 )
							suffix = "ft";
						if ( i == 4 )
							suffix = "lf";
						if ( i == 5 )
							suffix = "bk";

						settings.skyPath = $"{downloadPath}{settings.saveFolder}gfx/env/";
						settings.skyUrls.Add( $"{settings.baseUrl}gfx/env/{settings.skyName}{suffix}.tga" );
					}
				}

				if( settings.skyUrls.Count != 0 )
				{
					// Download sky tga in .res files
					foreach ( var skyurl in settings.skyUrls )
					{
						var skyName = Util.PathToMapName( skyurl );

						if ( string.IsNullOrEmpty( skyName ) )
							continue;

						var skyPath = $"{downloadPath}{settings.saveFolder}{skyurl.Replace( settings.baseUrl, "")}";
						if ( !FileSystem.Data.FileExists( skyPath ) )
						{
							var notify = DownloadNotification.CreateInfo( $"Downloading.. ( {skyurl} )" );
							var http = new Http( new Uri( $"{skyurl}" ) );

							try {
								var data = await http.GetBytesAsync();

								if ( !FileSystem.Data.DirectoryExists( Util.PathWithouthFile( skyPath ) ) )
									FileSystem.Data.CreateDirectory( Util.PathWithouthFile( skyPath ) );

								if ( Encoding.ASCII.GetString( data, 0, 4 ) != "<!DO" )
								{
									notify?.FinishDownload( $"Download successful ( {skyurl} )" );
									FileSystem.Data.WriteAllText( skyPath, Convert.ToBase64String( data ) );
								}
								else
								{
									notify?.FailedDownload( $"Download failed ( {skyurl} )" );
									Notify.Create( $"{skyName}.tga not found in url {skyurl}", Notify.NotifyType.Error );
								}
							}
							catch
							{
								notify?.FailedDownload( $"Download failed ( {skyurl} )" );
							}

							http.Dispose();
						}
						if ( string.IsNullOrEmpty( settings.skyPath ) )
							settings.skyPath = $"{Util.PathWithouthFile( skyPath )}\\{Util.PathWithouthFile( skyName )}";
					}
				}
			}

			_ = await spawnMap( settings );

		}

		public async static Task<bool> spawnMap( SpawnParameter settings )
		{
			PreparingIndicator.Update("Spawning");

			if ( settings.removeOthers )
			{
				removeAllMap();
				ModelRenderer.clearModelCache();
			}
			else
				removeMap( settings.mapName );

			if ( Game.IsClient )
			{
				lastTextureErrors.Clear();

				if ( settings.removeOthers )
					TextureCache.clearTexture();
			}

			MapObjects mapObject = new MapObjects();
			mapObject.Map = new();
			mapObject.spawnParameter = settings;

			settings.sceneWorld = Game.SceneWorld;

			//mapObject.body = new PhysicsBody( Game.PhysicsWorld );

			var bspData = await GameTask.RunInThreadAsync( () => new GoldSrc.BSPFile( ref settings ) );
			if ( bspData.WADList is not null )
			{
				settings.wadList.AddRange( bspData.WADList );
				settings.wadList = settings.wadList.Distinct().ToList();
			}

			// Priority skyName from .bsp
			settings.skyName = (!string.IsNullOrEmpty( bspData.skyname ) && !string.IsNullOrWhiteSpace( bspData.skyname )) ? bspData.skyname : settings.skyName;

			var mb = Model.Builder;
			List<Mesh> meshes = new List<Mesh>();

			List<(List<Vertex>, List<int>, string, int)> mapMeshInfo = new();

			Dictionary<GoldSrc.EntityParser.EntityData, (List<(List<Vertex>, List<int>, string, int)>, Vector3, Vector3, Vector3)> entitiesMeshInfo = new();

			// Map meshes
			await GameTask.RunInThreadAsync( async () =>
			{
				var batchSize = 300;
				var count = 0;
				foreach ( var meshdata in bspData.meshDataList )
				{
					PreparingIndicator.Update( "Spawning" );

					if ( Game.IsServer )
					{
						var mesh = new Mesh();
						mesh.CreateVertexBuffer( meshdata.vertices.Count(), Vertex.Layout, meshdata.vertices );
						mesh.CreateIndexBuffer( meshdata.indices.Count(), meshdata.indices.ToArray() );
						meshes.Add( mesh );

						if ( Game.IsServer )
							mb.AddCollisionMesh( meshdata.vertices.Select( x => x.Position ).ToArray(), meshdata.indices.ToArray() );

						//model.WithSurface( "wood" );

						//body.AddMeshShape( meshdata.vertices.Select( x => x.position ).ToArray(), meshdata.indices.ToArray() );
					}
					else
						mapMeshInfo.Add( (meshdata.vertices, meshdata.indices, meshdata.textureName, meshdata.faceIndex) );

					if( count++ % batchSize == 0)
						await Task.Yield();
				}

				count = 0;
				// Entity Meshes ( entity collisions are not seperated for temporary ) 
				foreach ( var meshdata in bspData.entityMeshDataList )
				{
					if ( Game.IsServer )
					{
						PreparingIndicator.Update( "Spawning" );

						List<Vertex> VertexList = new();

						var ent_origin = Vector3.Zero;
						if ( meshdata.entity.Value.data.TryGetValue( "origin", out var origin ) )
							ent_origin = Vector3.Parse( origin );


						foreach ( var vertex in meshdata.vertices )
						{
							var Vertex = vertex;
							Vertex.Position += ent_origin + meshdata.vOrigin;
							VertexList.Add( Vertex );
						}

						var mesh = new Mesh();
						mesh.CreateVertexBuffer( VertexList.Count(), Vertex.Layout, VertexList );
						mesh.CreateIndexBuffer( meshdata.indices.Count(), meshdata.indices.ToArray() );
						meshes.Add( mesh );

						if ( Game.IsServer )
							mb.AddCollisionMesh( VertexList.Select( x => x.Position ).ToArray(), meshdata.indices.ToArray() );

						//model.WithSurface( "wood" );

						//body.AddMeshShape( meshdata.vertices.Select( x => x.position ).ToArray(), meshdata.indices.ToArray() );
					}
					else
					{
						if ( entitiesMeshInfo.TryGetValue( meshdata.entity.Value, out var entdata ) )
							entdata.Item1.Add( (meshdata.vertices, meshdata.indices, meshdata.textureName, meshdata.faceIndex) );
						else
							entitiesMeshInfo.Add( meshdata.entity.Value, (new() { (meshdata.vertices, meshdata.indices, meshdata.textureName, meshdata.faceIndex) }, meshdata.nMins, meshdata.nMaxs, meshdata.vOrigin) );
					}

					if ( count++ % batchSize == 0 )
						await Task.Yield();
				}
			} );


			Vector3? findTeleportPoint = null;

			if ( settings.spawnPoints == null )
			{
				foreach ( var classname in new List<string>() { "info_player_start", "info_player_deathmatch", "info_player_allies" } )
				{
					var infoPlayer = bspData.entities.Where( x => x.classname == classname );

					if ( infoPlayer.Any() )
					{
						foreach ( var info in infoPlayer )
						{
							if ( info.data.TryGetValue( "origin", out var origin ) )
							{
								settings.spawnPoints ??= new();
								settings.spawnPoints.Add( Vector3.Parse( origin ) );
							}
						}
					}
				}
			}
			if ( settings.spawnPoints is not null )
				findTeleportPoint = settings.spawnPoints[Game.Random.Int( 0, settings.spawnPoints.Count - 1)]; // + meshdata.vOrigin, is it needed?

			if ( Game.IsClient )
			{
				// Creating sky
				var SkyTextures = new List<Texture>();

				await GameTask.RunInThreadAsync( async () =>
				{
					for ( var i = 0; i < 6; i++ )
					{
						PreparingIndicator.Update( "Sky" );

						var suffix = "";
						if ( i == 0 )
							suffix = "dn";
						if ( i == 1 )
							suffix = "up";
						if ( i == 2 )
							suffix = "rt";
						if ( i == 3 )
							suffix = "ft";
						if ( i == 4 )
							suffix = "lf";
						if ( i == 5 )
							suffix = "bk";

						var path = $"{settings.skyPath}{settings.skyName}{suffix}.tga";
						if ( settings.assetparty_version )
							path = $"gfx/env/{settings.skyName}{suffix}.tga.txt";

						if ( settings.fileSystem.FileExists( path ) )
						{
							var decoder = TgaDecoderTest.TgaDecoder.FromBinary( settings.assetparty_version ? settings.fileSystem.ReadAllBytes( path ).ToArray() : Convert.FromBase64String( settings.fileSystem.ReadAllText( path ) ) );

							byte[] data = new byte[decoder.Width * decoder.Height * 4];
							var index = 0;
							for ( int y = 0; y < decoder.Height; y++ )
							{
								for ( int x = 0; x < decoder.Width; x++ )
								{
									var color = Color.FromRgba( (uint)decoder.GetPixel( x, y ) );
									data[index++] = (byte)(color.g * 255f);
									data[index++] = (byte)(color.b * 255f);
									data[index++] = (byte)(color.a * 255f);
									data[index++] = (byte)((1 - color.r) * 255f);
								}
							}

							SkyTextures.Add( GoldSrc.TextureCache.addTexture( data, $"{settings.skyName}{suffix}", decoder.Width, decoder.Height ) );
						}
						else
						{
							for ( var j = 0; j < 6; j++ )
							{
								var decoder = TgaDecoderTest.TgaDecoder.FromBinary( Util.Decompress<byte[]>( Convert.FromBase64String(
										j == 0 ? GoldSrc.StaticData.skydn :
										j == 1 ? GoldSrc.StaticData.skyup :
										j == 2 ? GoldSrc.StaticData.skyrt :
										j == 3 ? GoldSrc.StaticData.skyft :
										j == 4 ? GoldSrc.StaticData.skylf :
										GoldSrc.StaticData.skybk
								) ) );

								byte[] data = new byte[decoder.Width * decoder.Height * 4];
								var index = 0;
								for ( int y = 0; y < decoder.Height; y++ )
								{
									for ( int x = 0; x < decoder.Width; x++ )
									{
										var color = Color.FromRgba( (uint)decoder.GetPixel( x, y ) );
										data[index++] = (byte)(color.g * 255f);
										data[index++] = (byte)(color.b * 255f);
										data[index++] = (byte)(color.a * 255f);
										data[index++] = (byte)((1 - color.r) * 255f);
									}
								}

								SkyTextures.Add( GoldSrc.TextureCache.addTexture( data, $"{settings.skyName}{suffix}", decoder.Width, decoder.Height ) );
							}
							if ( i == 5 )
								Notify.Create( $"Sky not found, default sky is loaded! (Sky Name: {(string.IsNullOrEmpty( settings.skyName) ? "Not Found" : settings.skyName)})", Notify.NotifyType.Error );
						}
						if ( i % 2 == 0 )
							await Task.Yield();
					}
				} );

				mapObject.Map = GoldSrc.Entities.Map.Create( ref settings, ref bspData, ref SkyTextures, ref mapMeshInfo, ref mb);

				// Create entities on CL for now
				foreach(var ent in entitiesMeshInfo)
					mapObject.Map.CL.entities.Add( new GoldSrc.Entities.MapModelEntity( settings, bspData.lightmap, mapObject.Map.CL, ent.Key, ent.Value.Item1, ent.Value.Item2, ent.Value.Item3, ent.Value.Item4 ) );

				//mapObject.Map.CL.updateEntityRenderBounds(); // Not a good idea
				mapObject.Map.CL.findPVSForEntities();
				
			}
			else
			{
				List<Texture> empty = new();
				mapObject.Map = GoldSrc.Entities.Map.Create( ref settings, ref bspData, ref empty, ref mapMeshInfo, ref mb );

				if ( findTeleportPoint != null )
					settings.owner.Pawn.Position = findTeleportPoint.Value + settings.position;

				spawnMap_cl( To.Everyone, settings.mapName, settings.position, settings.angles, mapObject.Map.SV.WorldSpaceBounds.Center, mapObject.Map.SV.WorldSpaceBounds.Corners.ToArray(), settings.removeOthers,settings.spawnModels, settings.packageFullIdent, assetparty_version: settings.assetparty_version, wadlist: string.Join( ",", settings.wadList ),skyName: bspData.skyname, baseurl: !settings.assetparty_version ? settings.baseUrl : "", savefolder: !settings.assetparty_version ? settings.saveFolder : "" );
			}

			// Create Models
			if( settings.spawnModels )
			{ 
				var modelEntities = bspData.entities.Where( x => x.data.TryGetValue("model", out var model) && model.EndsWith(".mdl") );
				var lightEntities = bspData.entities.Where( x => x.classname == "light_environment" || x.classname == "light" ).ToList(); // is "light" important?
				if ( modelEntities.Any() )
				{
					foreach ( var ent in modelEntities )
					{
						MDLEntity createdModel = await spawnModels( ent, settings, lightEntities );
						if ( mb is not null && createdModel != null ) // null checking is not working...
						{
							if ( Game.IsServer )
								mapObject.Map.SV.models.Add( createdModel );
							else
								mapObject.Map.CL.models.Add( createdModel );
						}
						await Task.Yield();
					}

					if ( Game.IsClient )
						mapObject.Map.CL.findPVSForModelEntities();
				}
			}
			if ( Game.IsClient )
				mapObject.Map.CL.RegisterEvent(); // Register tick event

			mapObject.spawnParameter = settings; // Update
			Maps.Add( settings.mapName, mapObject );

			Notify.Create( "Map Created", Notify.NotifyType.Info );

			return true;
		}

		[ClientRpc]
		public static void spawnMap_cl( string mapName, Vector3 position, Angles angles, Vector3 center, Vector3[] AABB, bool removeOthers, bool spawnModels = false, string packageFullIdent = "", bool assetparty_version = true, string baseurl = "", string savefolder = "", string wadlist = "", string skyName = "")
		{
			var settings = new SpawnParameter()
			{
				mapName = mapName,
				position = position,
				angles = angles,
				removeOthers = removeOthers,
				assetparty_version = assetparty_version,
				packageFullIdent = packageFullIdent,
				AABB = AABB.ToList(),
				center = center,
				skyName = skyName,
				spawnModels = spawnModels,
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
				if ( !string.IsNullOrEmpty( wadlist ) )
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
				if ( map.Map.SV != null && map.Map.SV.IsValid() && map.Map.SV.models.Count > 0 )
					foreach ( var model in map.Map.SV.models )
						if( model != null )
							model.Delete();

				if ( map.Map.CL != null && map.Map.CL.IsValid() && map.Map.CL.entities.Count > 0)
					foreach ( var ent in map.Map.CL.entities )
						if ( ent != null )
							ent.Delete();

				if ( map.Map.CL != null && map.Map.CL.IsValid() && map.Map.CL.models.Count > 0 )
					foreach ( var model in map.Map.CL.models )
						if ( model != null )
							model.Delete();

				map.Map.Delete();

				foreach ( var ent in Game.SceneWorld.SceneObjects )
					if ( ent != null && ent.IsValid() && ent.Flags.IsOpaque && !ent.Flags.NeedsLightProbe )
						ent.RenderingEnabled = true;

				//if ( map.body != null && map.body.IsValid() )
				//map.body.Remove();

				_ = Maps.Remove( mapName );

				return true;
			}
			return false;
		}

		[Event.Entity.PreCleanup]
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

		// need assetpartyversion
		public async static Task<MDLEntity?> spawnModels( GoldSrc.EntityParser.EntityData entData, SpawnParameter settings, List<GoldSrc.EntityParser.EntityData> lightEntities)
		{
			var path = settings.assetparty_version ? $"{entData.data["model"]}.txt" : $"{downloadPath}{settings.saveFolder}{entData.data["model"]}";
			if ( !settings.fileSystem.FileExists( path ) )
			{
				Notify.Create( $"Model not found ({path})" );
				return null;
			}

			if ( ModelRenderer.ModelCache.TryGetValue( entData.data["model"], out var cache ))
				return MDLEntity.Create( ref cache.Item1, ref entData, ref settings, ref lightEntities );

			var data = await GameTask.RunInThreadAsync( async () =>
			{
				PreparingIndicator.Update( "Models" );
				var buffer = settings.assetparty_version ? await settings.fileSystem.ReadAllBytesAsync( $"{path}" ) : Convert.FromBase64String( await settings.fileSystem.ReadAllTextAsync( $"{path}" ) );

				// Parsing the model buffer
				var modelData = await ModelDataParser.ParseModel( buffer );

				// Meshes render
				var meshesRenderData = await ModelRenderer.PrepareRenderData( modelData );

				// Textures preparing (clientside)
				List<ushort[]> textures = new();

				if ( Game.IsClient )
					foreach ( var tex in modelData.textures )
						textures.Add( GoldSrc.Entities.TextureBuilder.BuildTexture( ref buffer, tex ) );

				// Generation meshes and modelData
				return (ModelRenderer.CreateModelEntity( ref meshesRenderData, modelData, ref textures, ref entData, ref settings, ref lightEntities ), modelData);
			} );

			var ent = MDLEntity.Create( ref data.Item1, ref entData, ref settings, ref lightEntities );
			ModelRenderer.ModelCache.TryAdd( data.Item2.header.name, (data.Item1, data.Item2, entData, lightEntities, null) );
			return ent;
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
			}

			private static bool isMPContentPackage( Package package, BaseFileSystem filesystem ) => filesystem.FileExists( $"{package.FullIdent}.txt" );

			public static List<PackageInfo> retrieveContent( Package package, BaseFileSystem filesystem )
			{
				if ( !isMPContentPackage( package, filesystem ) )
				{
					Notify.Create( "Map information not found", Notify.NotifyType.Error );
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
						map.Value.Map.SV.WorldSpaceBounds.Center,
						map.Value.Map.SV.WorldSpaceBounds.Corners.ToArray(),
						map.Value.spawnParameter.removeOthers,
						map.Value.spawnParameter.spawnModels,
						map.Value.spawnParameter.packageFullIdent,
						map.Value.spawnParameter.assetparty_version,
						string.Join( ",", map.Value.spawnParameter.wadList ),
						map.Value.spawnParameter.skyName );
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
			Notify.Create( "Settings have been saved!" );
		}

		[ClientRpc]
		public static void ServerInfo()
		{
			PreparingIndicator.Update( "Server", 60f );
		}
	}

	// TODO: When spawn from entities menu, the menu must be directly opened, not spawn any entity
	public partial class MPEntity : AnimatedEntity, IUse, IHotloadManaged
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

			//Log.Info( this.Owner );
			//Log.Info( this.Client );
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
			var ent = new MapParser.MPEntity();
			ent.Position = ply.Position;
		}*/


		void IHotloadManaged.Destroyed( Dictionary<string, object> state )
		{

		}
		void IHotloadManaged.Created( IReadOnlyDictionary<string, object> state )
		{
		
		}


		///////////////////////////////////////////////////////////////
		/// <summary>
		/// Test section, debug purposes
		/// </summary>
		/////////////////////////////////////////////////////////////// 



		/*[ConCmd.Client( "modeltest" )]
		public async static void modeltest()
		{
			var ply = ConsoleSystem.Caller;

			TextureCache.clearTexture();

			var buffer = FileSystem.Data.ReadAllBytes( "3dm_bik_brick.mdl" ).ToArray();
			
			// Parsing the model buffer
			var modelData = await ModelDataParser.ParseModel( buffer );

			var lightmap = Texture.Transparent;

			// Meshes render
			var meshesRenderData = ModelRenderer.PrepareRenderData( modelData );

			// Textures preparing
			List<ushort[]> textures = new();

			foreach ( var tex in modelData.textures )
				textures.Add(GoldSrc.Entities.TextureBuilder.BuildTexture( ref buffer, tex ));

		}*/

		static Sandbox.ModelEntity ent;
		static SourceFileSystem? filesystem;

		[ConCmd.Client( "vpk" )]
		public static void vpk()
		{
			if ( filesystem is null)
			{
				filesystem = new SourceFileSystem(FileSystem.Data);
				filesystem.CreateVPKMount( "hl2_misc" );
			}

			if( filesystem  is not null)
			{
				var asd = VmtParser.ParseVMT( filesystem, "materials/props/combine_display01a.vmt" ).Result; //"materials /metal/metalbar001a.vmt" 

				foreach (var vpk in filesystem.vpk)
				{
					var dosya = vpk.FindEntry( "materials/props/combine_display01a.vmt" );
					if( dosya.Path != null)
					{
						var veri = filesystem.FetchFileData( "materials/props/combine_display01a.vmt" ).Result;
						//if ( veri != null )
						//		Log.Info( Encoding.ASCII.GetString( veri ) );

					}
				}
			}
			//var materialInstance = await materialCache.createMaterialInstance( "materials/metal/metalbar001a.vmt" ); //surface.texName

		}

		[ConCmd.Client( "test11" )]
		public static void testsource1()
		{
			var buffer = FileSystem.Data.ReadAllBytes( "d1_canals_01.bsp" ).ToArray();
			var bspData = new SourceEngine.BSPFile( buffer,"test" );

			var model = Model.Builder;
			List<Mesh> meshes = new List<Mesh>();

			//Log.Info( bspData.vertexData.Length + " " + bspData.meshDataList.Count() );

			var mat = Material.Load( "materials/dev/gray_grid_4.vmat" );

			foreach ( var meshdata in bspData.meshDataList )
			{

				/*if ( Game.IsServer )
				{
					var mesh = new Mesh();
					mesh.CreateVertexBuffer( meshdata.vertices.Count(), Vertex.Layout, meshdata.vertices );
					mesh.CreateIndexBuffer( meshdata.indices.Count(), meshdata.indices.ToArray() );
					meshes.Add( mesh );

					if ( Game.IsServer )
						model.AddCollisionMesh( meshdata.vertices.Select( x => x.Position ).ToArray(), meshdata.indices.ToArray() );

					//model.WithSurface( "wood" );

					//body.AddMeshShape( meshdata.vertices.Select( x => x.position ).ToArray(), meshdata.indices.ToArray() );
				}
				else
					mapMeshInfo.Add( (meshdata.vertices, meshdata.indices, meshdata.textureName, meshdata.faceIndex) );
				*/


				//Log.Info( meshdata.textureName );

				var mesh = new Mesh( mat );
				mesh.CreateVertexBuffer( meshdata.vertices.Count(), Vertex.Layout, meshdata.vertices.ToList() );
				mesh.CreateIndexBuffer( meshdata.indices.Count(), meshdata.indices.ToArray() );
				model.AddCollisionMesh( meshdata.vertices.Select( x => x.Position ).ToArray(), meshdata.indices.ToArray() );
				meshes.Add( mesh );

			}

			//Log.Info( str );

			model.AddMeshes( meshes.ToArray() );

			if ( ent != null )
				ent.Delete();
						ent = new();
						ent.Model = model.Create();//Model.Load( "models/arrow.vmdl" );//model.Create();
												   ent.SetupPhysicsFromModel( PhysicsMotionType.Static );
						ent.Position = new Vector3(0,0,500f);
							//ent.Rotation = Rotation.From( settings.angles );
							ent.PhysicsEnabled = false;
						

			/*model.AddMeshes( meshes.ToArray() );
			var so = new SceneObject( Game.SceneWorld, model.Create(), new Transform( Vector3.Zero ) );*/



			//ent.EnableDrawing = true;
			//ent.Tags.Add( "solid" );
			//ent.EnableTraceAndQueries = true;
			//ent.Predictable = false;

			//int dstOffsVertex = 0;

			//List<Vertex> vertexList = new();
			//List<int> indexList = new();


			// Unpack data
			//for ( int j = 0; j < bspData.vertexData.Length / BSPFile.VERTEX_SIZE; j++ )
			//{
			/*// Position
			vertexData[dstOffsVertex++] = v.position.x;
			vertexData[dstOffsVertex++] = v.position.y;
			vertexData[dstOffsVertex++] = v.position.z;

			if ( center is not null )
			{
				center = center + v.position / vertex.Length;
			}

			// Normal
			vertexData[dstOffsVertex++] = v.normal.x;
			vertexData[dstOffsVertex++] = v.normal.y;
			vertexData[dstOffsVertex++] = v.normal.z;
			vertexData[dstOffsVertex++] = v.alpha;

			// Tangent
			scratchTangentS = Vector3.Cross( v.normal, scratchTangentT );
			scratchTangentS = scratchTangentS.Normal;
			vertexData[dstOffsVertex++] = scratchTangentS.x;
			vertexData[dstOffsVertex++] = scratchTangentS.y;
			vertexData[dstOffsVertex++] = scratchTangentS.z;
			// Tangent Sign
			vertexData[dstOffsVertex++] = tangentW;

			// Texture UV
			vertexData[dstOffsVertex++] = v.uv.x;
			vertexData[dstOffsVertex++] = v.uv.y;

			// Lightmap UV
			if ( (tex.flags & TexinfoFlags.NOLIGHT) != 0 )
			{
				vertexData[dstOffsVertex++] = 0.5f;
				vertexData[dstOffsVertex++] = 0.5f;
			}
			else
			{
				vertexData[dstOffsVertex++] = v.lightmapUV.x;
				vertexData[dstOffsVertex++] = v.lightmapUV.y;
			}*/

			//vertexList.Add( new Vertex( new Vector3( bspData.vertexData[dstOffsVertex++] , bspData.vertexData[dstOffsVertex++] , bspData.vertexData[dstOffsVertex++] ), new Vector4( bspData.vertexData[dstOffsVertex++] , bspData.vertexData[dstOffsVertex++] , bspData.vertexData[dstOffsVertex++], bspData.vertexData[dstOffsVertex++] ), new Vector4( bspData.vertexData[dstOffsVertex++], bspData.vertexData[dstOffsVertex++], bspData.vertexData[dstOffsVertex++], bspData.vertexData[dstOffsVertex++] ), new Vector4( bspData.vertexData[dstOffsVertex++], bspData.vertexData[dstOffsVertex++], bspData.vertexData[dstOffsVertex++], bspData.vertexData[dstOffsVertex++] ) ) );

			//}
		}
	}
}
