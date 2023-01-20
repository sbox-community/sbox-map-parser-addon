using Sandbox;
using System.IO;
using System.Linq;
using static MapParser.GoldSrc.WAD;
using static MapParser.Manager;
using static MapParser.Render;
using System;

namespace MapParser.GoldSrc
{
	public static class TextureCache
	{
		public static void addWAD( string wadname, SpawnParameter settings )
		{
			var filePath = $"{(settings.assetparty_version ? "" : Manager.downloadPath)}{settings.saveFolder}{wadname}{(settings.assetparty_version ? ".txt" : "")}";

			if( !settings.fileSystem.FileExists( filePath ) )
			{
				Notify.Create( $"{filePath} not found in the filesystem", Notify.NotifyType.Error );
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

			for ( var i = 0; i < wad.lumps.Count(); i++ )
			{
				var lump = wad.lumps[i];
				if ( lump.type == WADLumpType.MIPTEX )
					addTexture( lump.data, wadname: wadname );
			}
		}
		// Must be async, otherwise being freezing until to finish of creation, there must be custom shader to do
		// Already implemented, but, textureCoords and scales are corrupting if we creates all texture after the creation of map
		public static void addTexture( byte[] data, string wadname = "" ) //async
		{
			var name = MIPTEXData.GetMipTexName( data );

			if( string.IsNullOrEmpty( name ) || string.IsNullOrWhiteSpace( name ) )
			{
				Notify.Create( "It looks like corrupted/non-related data for texture from the BSP, do not mind..", notifytype: Notify.NotifyType.Error );
				return;
			}

			if ( Render.TextureCache.textureData.TryGetValue( name, out var datafounded ) )
			{
				// if wadnames are different, change texture textureData and add to textureData dict, there also should be textureData comparison
				if ( !string.IsNullOrEmpty( wadname ) && datafounded.WADname != wadname )
					Render.TextureCache.textureData.Remove( name );
				else
					return;
			}

			var tex = MIPTEXData.CreateTexture( data, name );
			Render.TextureCache.textureData.TryAdd( name, new() { name = name, type = TextureCacheType.MIPTEX, WADname = wadname, texture = tex, width = tex.Width, height = tex.Height } );

			/*
			await GameTask.RunInThreadAsync( () => {
				var tex = MIPTEXData.CreateTexture( data, name );
				Render.TextureCache.textureData.TryAdd( name, new() { name = name, type = TextureCacheType.MIPTEX, WADname = wadname, texture = tex, width = tex.Width, height = tex.Height } );
			} );

			var foundPending = false;

			if( pendingMaterials.TryGetValue( name, out var matName ) )
			{
				if ( Render.TextureCache.textureData.TryGetValue( name, out var tex ) )
					pendingMaterials[name].Set( "Color", tex.texture );
				else
					Log.Error( "Texture not not found!" );
				
				foundPending = true;
			}

			if ( foundPending )
				pendingMaterials.Remove( name );*/

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

				if( palOffs > reader.BaseStream.Length ) // Why is happened, idk
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

				// i < numLevels  ==>  i < 1; There is already 1,2 and 3. mips materialData but no way to import created textureName as its mips, we use sbox's mips generator, so performance issue.
				//for ( int i = 0; i < 1; i++ )
				//{
				int i = 0;
				uint[] mipData = new uint[mipW * mipH * 4];
				uint[] dst = new uint[mipW * mipH * 4];

				var dataOffs = mipOffsets[i];
				var numPixels = mipW * mipH;

				for ( int j = 0; j < numPixels; j++ )
				{
					stream.Seek( dataOffs++, SeekOrigin.Begin );
					var palIdx = reader.ReadByte(); //READUINT8

					if ( isDecal && palIdx == 255 )
					{
						mipData[j * 4 + 0] = 0x00;
						mipData[j * 4 + 1] = 0x00;
						mipData[j * 4 + 2] = 0x00;
						mipData[j * 4 + 3] = 0x00;
					}
					else
					{
						//uint lightmapValue = j < dst2Count && dst2[j * 4 + 0] != 0 ? (uint)(dst2[j]) : 1;

						mipData[j * 4 + 0] = pal[palIdx * 3 + 0];//(j * 4 + 0) < dst2Count && dst2[j * 4 + 0] != 0 ? (uint)(dst2[j * 4 + 0]) : 1;
						mipData[j * 4 + 1] = pal[palIdx * 3 + 1];//(j * 4 + 1) < dst2Count && dst2[j * 4 + 1] != 0 ? (uint)(dst2[j * 4 + 1]) : 1;
						mipData[j * 4 + 2] = pal[palIdx * 3 + 2];//(j * 4 + 2) < dst2Count && dst2[j * 4 + 2] != 0 ? (uint)(dst2[j * 4 + 2]) : 1;
						mipData[j * 4 + 3] = 0xFF;
					}
				}

				//InterpolateLightmapValues( dst2, lightmapData.width, lightmapData.height, ref mipData, mipW, mipH );

				//mipW >>= 1;
				//mipH >>= 1;

				//if ( i == 0 )
				return (mipData, Width, Height, i);
				//}
			}
			//return (null, 0, 0, 0);
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
			{
				data[iw] = (byte)textureData.Item1[iw];
			}

			if ( texName == "sky" )
			{
				return Texture.Transparent;
			}

			var texture = Texture.Create( textureData.Item2, textureData.Item3 );
			texture.WithData( data );
			texture.WithMips( 3 ); //3 or 4?
			return texture.Finish();

		}

		/*public static Texture createLightmap( ref LightmapPackerPage lightmapPackerPage, ref List<SurfaceLightmapData> lightmap )
		{
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
						int dstOffs = 0;
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
		}

		public static Texture createLightmappedTexture(ref TextureCacheData texData, ref SurfaceLightmapData lightmapData )
		{
			byte[] buffer = texData.textureData;
			var textureData = Create( buffer );

			if ( textureData.Item1 == null )
			{
				Log.Error( "Error when creating texture" );
				return Texture.Invalid;
			}

			var data = textureData.Item1;

			// Construct a new lightmap
			//int numPixels2 = lightmapPackerPage.width * lightmapPackerPage.height;
			int numPixels2 = lightmapData.width * lightmapData.height;
			//Log.Info( lightmapPackerPage.width + " " + lightmapPackerPage.height );
			byte[] dst2 = new byte[numPixels2 * 4];


			if ( lightmapData.samples != null && lightmapData.samples.Count() != 0 )
			{

				// TODO(jstpierre): Add up light styles
				byte[] src = lightmapData.samples; //byte[]
				int srcOffs = 0;
				for ( int y = 0; y < lightmapData.height; y++ )
				{
					//int dstOffs = (lightmapPackerPage.width * (lightmapData.pagePosY + y) + lightmapData.pagePosX) * 4;
					int dstOffs = 0;
					for ( int x = 0; x < lightmapData.width; x++ )
					{

						dst2[dstOffs++] = src[srcOffs++];
						dst2[dstOffs++] = src[srcOffs++];
						dst2[dstOffs++] = src[srcOffs++];
						dst2[dstOffs++] = 0xFF;
					}
				}
			}

			InterpolateLightmapValues( dst2, lightmapData.width, lightmapData.height, ref data, textureData.Item2, textureData.Item3 );

			byte[] dataFinal = new byte[data.Length];
			for ( int iw = 0; iw < data.Length; iw++ )
			{
				dataFinal[iw] = (byte)data[iw];
			}

			texData.width = textureData.Item2;
			texData.height= textureData.Item3;

			var texture = Texture.Create( textureData.Item2, textureData.Item3 );
			texture.WithData( dataFinal );
			texture.WithMips( 3 ); //3 or 4?
			return texture.Finish();

		}
		public static void InterpolateLightmapValues( byte[] lightmapData, int lightmapWidth, int lightmapHeight, ref uint[] textureData, int textureWidth, int textureHeight )
		{
			// Calculate the scaling factor based on the size of the lightmap and texture
			float xScale = (float)textureWidth / lightmapWidth;
			float yScale = (float)textureHeight / lightmapHeight;

			// Iterate over each pixel in the texture
			for ( int y = 0; y < textureHeight; y++ )
			{
				for ( int x = 0; x < textureWidth; x++ )
				{
					// Calculate the corresponding lightmap pixel based on the scaling factor
					int lightmapX = (int)(x / xScale);
					int lightmapY = (int)(y / yScale);

					// Calculate the index of the lightmap pixel in the lightmap materialData array
					int lightmapIndex = (lightmapY * lightmapWidth + lightmapX) * 4;

					// Calculate the index of the texture pixel in the texture materialData array
					int textureIndex = (y * textureWidth + x) * 4;

					// Set the texture pixel to the value of the lightmap pixel
					textureData[textureIndex] *= lightmapData[lightmapIndex];
					textureData[textureIndex + 1] *= lightmapData[lightmapIndex + 1];
					textureData[textureIndex + 2] *= lightmapData[lightmapIndex + 2];
				}
			}
		}*/

		public static string GetMipTexName( byte[] buffer )
		{
			return Util.ReadString( buffer, 0x00, 0x10, true );
		}
	}
}

