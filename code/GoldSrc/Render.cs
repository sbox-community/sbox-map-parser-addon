using Sandbox;
using System.IO;
using System.Linq;
using static MapParser.GoldSrc.WAD;
using static MapParser.Manager;
using static MapParser.Render;
using System;
using System.Collections.Generic;
using static MapParser.GoldSrc.Entity;
using System.Threading.Tasks;

namespace MapParser.GoldSrc
{
	public static class TextureCache
	{
		public static Dictionary<string, Dictionary<string, byte[]>> WADCache = new();
		
		private static string previousfilePath = ""; // To prevent notify spamming
		public static void addWAD( string wadname, SpawnParameter settings )
		{
			lock ( WADCache ) { 
				// There should be CRC checking?
				if ( WADCache.TryGetValue( wadname, out var _ ) )
					return;

				var filePath = $"{(settings.assetparty_version ? "" : downloadPath)}{settings.saveFolder}{wadname}{(settings.assetparty_version ? ".txt" : "")}";

				if( !settings.fileSystem.FileExists( filePath ) )
				{
					if( previousfilePath != filePath) // TODO: not working efficiently
					{
						Notify.Create( $"{filePath} not found in the filesystem", Notify.NotifyType.Error );
						previousfilePath = filePath;
					}
					return;
				}	

				byte[] buffer;

				if ( settings.assetparty_version )
					buffer = settings.fileSystem.ReadAllBytes( filePath ).ToArray();
				else
					buffer = Convert.FromBase64String( settings.fileSystem.ReadAllText( filePath ) );

				var wad = WADParser.ParseWAD( buffer );

				if ( wad.lumps == null )
					return;

				Dictionary<string, byte[]> cacheData = new();

				for ( var i = 0; i < wad.lumps.Count(); i++ )
				{
					var lump = wad.lumps[i];
					var name = MIPTEXData.GetMipTexName( lump.data );

					if ( lump.type == WADLumpType.MIPTEX )
						_  = cacheData.TryAdd( name, lump.data );
				}

				WADCache.TryAdd(wadname, cacheData );
			}
		}
		public static void removeWAD( string wadname = "")
		{
			if ( string.IsNullOrEmpty( wadname ) )
				WADCache.Clear();
			else
				_ = WADCache.Remove( wadname );
		}
		public static void clearWAD() => removeWAD();

		// Must be async, otherwise being freezing until to finish of creation, there must be custom shader to do
		// Already implemented, but, textureCoords and scales are corrupting if we creates all texture after the creation of map
		public static Texture addTextureWithMIPTEXData( byte[] data, string wadname = "" ) //async
		{
			var name = MIPTEXData.GetMipTexName( data );
			
			if( string.IsNullOrEmpty( name ) || string.IsNullOrWhiteSpace( name ) )
			{
				Notify.Create( "It looks like corrupted/non-related data for texture from the BSP, do not mind..", notifytype: Notify.NotifyType.Error );
				return Texture.Invalid;
			}

			lock( Render.TextureCache.textureData )// Need concurrent dictionary
			{ 
				var useCached = false;
				if ( Render.TextureCache.textureData.TryGetValue( name, out var datafound ) ) 
					// if wadnames are different, change texture textureData and add to textureData dict, there also should be textureData comparison
					if ( !string.IsNullOrEmpty( wadname ) && datafound.WADname != wadname )
						Render.TextureCache.textureData.Remove( name );
					else
						useCached = true;

				if ( useCached )
					return datafound.texture;
				else
				{
					PreparingIndicator.Update();
					var tex = MIPTEXData.CreateTexture( data, name );
					Render.TextureCache.textureData.TryAdd( name, new() { name = name, type = TextureCacheType.MIPTEX, WADname = wadname, texture = tex, width = tex.Width, height = tex.Height } );

					return tex;
				}
			}
		}
		public static Texture addTexture( byte[] data, string name, int width, int height, string wadname = "From Not WAD File" )
		{
			lock ( Render.TextureCache.textureData )// Need concurrent dictionary
			{
				var useCached = false;
				if ( Render.TextureCache.textureData.TryGetValue( name, out var datafound ) )
					// if wadnames are different, change texture textureData and add to textureData dict, there also should be textureData comparison
					if ( !string.IsNullOrEmpty( wadname ) && datafound.WADname != wadname )
						Render.TextureCache.textureData.Remove( name );
					else
						useCached = true;

				if ( useCached )
					return datafound.texture;
				else
				{
					PreparingIndicator.Update();
					var tex = Texture.Create( width, height ).WithData( data ).Finish();
					Render.TextureCache.textureData.TryAdd( name, new() { name = name, type = TextureCacheType.MIPTEX, WADname = wadname, texture = tex, width = width, height = height } );
					return tex;
				}
			}
		}
		public async static Task addTextures( Dictionary<int, string> textureList, SpawnParameter settings, Map map = null, MapEntity mapEntity = null )
		{
			// Must be loaded after spawning of map for async

			List<int> foundedTextures = new();

			foreach ( var wad in settings.wadList )
			{
				PreparingIndicator.Update();

				addWAD( wad, settings );
				foreach( var texturePair in textureList )
				{
					var texture = texturePair.Value;
					if ( WADCache.TryGetValue( wad, out var wadtexlist ) )
					{
						foreach ( var wadtextlisttexture in wadtexlist )
						{
							if ( wadtextlisttexture.Key == texture )
							{
								PreparingIndicator.Update();

								var tex = addTextureWithMIPTEXData( wadtextlisttexture.Value, wad );

								foundedTextures.Add( texturePair.Key );

								if ( map is not null )
									map.updateTexture( texturePair.Key, tex );

								if ( mapEntity is not null )
									mapEntity.updateTexture( texturePair.Key, tex );

								// In here, if we remove the added texture from list, there will not override adding texture to the exists texture, so will happen this if there is texture with the same name.
								break;
							}
						}
					}
					//await Task.Yield();
				}
				await Task.Yield();
			}
			foreach ( var texturePair in foundedTextures )
				textureList.Remove( texturePair );

			lock( lastTextureErrors )
			{ 
				foreach( var texturePair in textureList )
					lastTextureErrors.Add( texturePair.Value );

				// If map is removed before updating textures, might be error
				var mapObject = (map is not null ? Maps.Where( x => x.Value.clside_Map == map ) : Maps.Where( x => x.Value.clside_Map == mapEntity.parent )).FirstOrDefault().Value;
				if ( mapObject.textureErrors == null )
					mapObject.textureErrors = new();

				if ( lastTextureErrors.Count != 0 && mapObject.textureErrors.Count() == 0 )
					Notify.Create( "Similar textures are found! You can try to spawn with similar of them..", Notify.NotifyType.Info );

				mapObject.textureErrors.AddRange(lastTextureErrors.Distinct().ToList());

				lastTextureErrors.Clear();
			}
		}


		/*public static Texture createTexture(string name)
		{
			if ( textureData.TryGetValue( name, out var datafounded ) )
				return MIPTEXData.CreateTexture( ref datafounded );
			else
			{
				Log.Error( "Texture not found!" );
				return Texture.Invalid;
			}
		}
		public static Texture createTextureWithLightmapped( string name, ref SurfaceLightmapData lightmapData )
		{
			if ( textureData.TryGetValue( name, out var datafounded ) )
				return MIPTEXData.createLightmappedTexture( ref datafounded, ref lightmapData );
			else
			{
				Log.Error( "Texture not found!" );
				return Texture.Invalid;
			}
		}*/
	}
	public static class MIPTEXData
	{
		private static (uint[], int, int, int) Create( byte[] buffer )
		{
			using ( var stream = new MemoryStream( buffer ) )
			using ( var reader = new BinaryReader( stream ) )
			{
				var Name = GetMipTexName( buffer );
				stream.Seek( 0x10, SeekOrigin.Begin );
				var Width = reader.ReadInt32();
				stream.Seek( 0x14, SeekOrigin.Begin );
				var Height = reader.ReadInt32();

				var isDecal = Name[0] == '{';

				const int numLevels = 4;

				var mipOffsets = Enumerable.Range( 0, numLevels )
				.Select( ( i ) =>
				{
					stream.Seek( 0x18 + i * 4, SeekOrigin.Begin );
					return reader.ReadUInt32();
				} )
				.ToArray();

				// Find the palette offset.
				var palOffs = mipOffsets[3] + (Width * Height >> 6);
				if( palOffs > int.MaxValue )
				{
					// Probably coming from the extra texture data from the bsp is corrupted/non-relative
					Notify.Create( "Palette offset length is too much", Notify.NotifyType.Error );
					return (null, 0, 0, 0);
				}

				if( palOffs > reader.BaseStream.Length ) // Why is happens, idk
				{
					Notify.Create( "Palette offset is beyond the data length", Notify.NotifyType.Error );
					return (null, 0, 0, 0);
				}

				stream.Seek( palOffs + 0x00, SeekOrigin.Begin );
				var palSize = reader.ReadUInt16();
				if ( palSize != 0x100 )
				{
					Notify.Create( "Palette size is not 0x100", Notify.NotifyType.Error );
					return (null, 0, 0, 0);
				}

				uint[] pal;
				using ( var memoryStream = new MemoryStream( buffer ) )
				using ( var binaryReader = new BinaryReader( memoryStream ) )
				{
					memoryStream.Seek( palOffs + 0x02, SeekOrigin.Begin );
					var byteler = binaryReader.ReadBytes( (int)(binaryReader.BaseStream.Length - binaryReader.BaseStream.Position) );
					pal = byteler.Select( x => (uint)x ).ToArray();
				}

				int mipW = Width, mipH = Height;

				// There are already 1,2 and 3. mips in materialData, but no way to import the mips of created texture. Fortunately, we can use sbox's mips generator.

				int i = 0;
				uint[] mipData = new uint[mipW * mipH * 4];
				uint[] dst = new uint[mipW * mipH * 4];

				var dataOffs = mipOffsets[i];
				var numPixels = mipW * mipH;
				
				for ( int j = 0; j < numPixels; j++ )
				{
					stream.Seek( dataOffs++, SeekOrigin.Begin );
					var palIdx = reader.ReadByte();

					if ( isDecal && palIdx == 255 )
					{
						mipData[j * 4 + 0] = 0x00;
						mipData[j * 4 + 1] = 0x00;
						mipData[j * 4 + 2] = 0x00;
						mipData[j * 4 + 3] = 0x00;
					}
					else
					{
						mipData[j * 4 + 0] = pal[palIdx * 3 + 0];
						mipData[j * 4 + 1] = pal[palIdx * 3 + 1];
						mipData[j * 4 + 2] = pal[palIdx * 3 + 2];
						mipData[j * 4 + 3] = 0xFF;
					}
				}
				return (mipData, Width, Height, i);
			}

		}

		public static Texture CreateTexture( byte[] buffer, string texName )
		{			
			var textureData = Create( buffer );

			if ( textureData.Item1 == null )
			{
				Notify.Create( "Error when creating texture", Notify.NotifyType.Error );
				return Texture.Invalid;
			}

			byte[] data = new byte[textureData.Item1.Length];
			for ( int iw = 0; iw < textureData.Item1.Length; iw++ )
				data[iw] = (byte)textureData.Item1[iw];

			if ( texName == "sky" )
				return Texture.Transparent;

			var texture = Texture.Create( textureData.Item2, textureData.Item3 );
			texture.WithData( data );
			texture.WithMips( 3 ); //3 or 4?
			return texture.Finish();

		}

		public static Texture createLightmap( ref BSPFile.LightmapPackerPage lightmapPackerPage, ref List<BSPFile.SurfaceLightmapData> lightmap )
		{
			PreparingIndicator.Update();

			var numPixels = lightmapPackerPage.width * lightmapPackerPage.height;
			byte[] dst = new byte[numPixels * 4];
			
			for ( var i = 0; i < lightmap.Count(); i++ )
			{
				var lightmapData = lightmap[i];
				if ( lightmapData.samples != null && lightmapData.samples.Count() != 0 )
				{
					// TODO(jstpierre): Add up light styles
					byte[] src = lightmapData.samples;
					int srcOffs = 0;
					for ( int y = 0; y < lightmapData.height; y++ )
					{
						int dstOffs = (lightmapPackerPage.width * (lightmapData.pagePosY + y) + lightmapData.pagePosX) * 4;
						for ( int x = 0; x < lightmapData.width; x++ )
						{
							dst[dstOffs++] = src[srcOffs++];
							dst[dstOffs++] = src[srcOffs++];
							dst[dstOffs++] = src[srcOffs++];
							dst[dstOffs++] = 0xFF;
						}
					}
				}
			}

			if ( !dst.Any( x => x != 0 ) ) // If lightmap completely black
			{
				Notify.Create( "Lightmap is disabled", Notify.NotifyType.Error );
				return Texture.Transparent;
			}

			var texture = Texture.Create( lightmapPackerPage.width, lightmapPackerPage.height );
			texture.WithData( dst );

			return texture.Finish();
		}
		public static string GetMipTexName( byte[] buffer )
		{
			return Util.ReadString( buffer, 0x00, 0x10, true );
		}
	}
}

