using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MapParser
{
	public static class Util
	{
		public enum GfxTopology
		{
			Triangles,
			TriStrips,
			TriFans,
			Quads,
			QuadStrips
		}
		public static string ReadString( byte[] buffer, int offs, int length = -1, bool nulTerminated = true, string encoding = null )
		{
			var buf = buffer.Skip( offs ).Take( length ).ToArray();
			int byteLength = 0;
			while ( true )
			{
				if ( length >= 0 && byteLength >= length )
					break;
				if ( nulTerminated && buf[byteLength] == 0 )
					break;
				byteLength++;
			}

			if ( byteLength == 0 )
				return "";

			if ( encoding != null )
			{
				return DecodeString( buffer, offs, byteLength, encoding );
			}
			else
			{
				return CopyBufferToString( buffer, offs, byteLength );
			}
		}
		private static string DecodeString( byte[] buffer, int offs = 0, int byteLength = -1, string encoding = "utf8" )
		{
			if ( byteLength == -1 )
				byteLength = buffer.Length - offs;

			// Use System.Text.Encoding class to decode the string.
			return Encoding.GetEncoding( encoding ).GetString( buffer, offs, byteLength );
		}
		private static string CopyBufferToString( byte[] buffer, int offs, int byteLength )
		{
			var buf = buffer.Skip( offs ).Take( byteLength ).ToArray();
			var sb = new StringBuilder();
			foreach ( var b in buf )
				sb.Append( (char)b );
			return sb.ToString();
		}
		public static Vector4 ReadVec4( byte[] buffer, int offs )
		{
			using ( var stream = new MemoryStream( buffer ) )
			using ( var reader = new BinaryReader( stream ) )
			{
				stream.Position = offs;
				float x = reader.ReadSingle();
				float y = reader.ReadSingle();
				float z = reader.ReadSingle();
				float w = reader.ReadSingle();
				return new Vector4( x, y, z, w );
			}
		}
		public static List<int> SearchBytePattern( byte[] pattern, byte[] bytes, int offset = 0, int maxLimit = 0, bool firstMatchReturn = false )
		{
			List<int> positions = new List<int>();
			int patternLength = pattern.Length;
			int totalLength = bytes.Length;
			byte firstMatchByte = pattern[0];
			for ( int i = offset; i < ( maxLimit != 0 ? maxLimit : totalLength ); i++ )
			{
				if ( firstMatchByte == bytes[i] && totalLength - i >= patternLength )
				{
					byte[] match = new byte[patternLength];
					Array.Copy( bytes, i, match, 0, patternLength );
					if ( match.SequenceEqual<byte>( pattern ) )
					{
						positions.Add( i );
						i += patternLength - 1;
						if ( firstMatchReturn )
							return positions;
					}
				}
			}
			return positions;
		}
		public static string PathToMapName( string path )
		{
			return Path.GetFileNameWithoutExtension( path );
		}
		public static string PathWithouthFile( string path )
		{
			return Path.GetDirectoryName( path );
		}
		public static string RemoveInvalidChars( string filename )
		{
			return string.Concat( filename.Split( Path.GetInvalidPathChars() ) );
		}
		public static T Decompress<T>( byte[] bytes )
		{
			using var outputStream = new MemoryStream();

			using ( var compressStream = new MemoryStream( bytes ) )
			{
				using var deflateStream = new DeflateStream( compressStream, CompressionMode.Decompress );
				deflateStream.CopyTo( outputStream );
			}

			return JsonSerializer.Deserialize<T>( outputStream.ToArray() );
		}
		async public static Task Timer( int s, Action callback )
		{
			await System.Threading.Tasks.Task.Delay( s );
			callback?.Invoke();
		}
		//https://stackoverflow.com/questions/2288498/how-do-i-get-a-rainbow-color-gradient-in-c
		public static Color Rainbow( float progress )
		{
			float div = (Math.Abs( progress % 1 ) * 6);
			int ascending = (int)((div % 1) * 255);
			int descending = 255 - ascending;

			switch ( (int)div )
			{
				case 0:
					return Color.FromBytes( 255, ascending, 0 );
				case 1:
					return Color.FromBytes( descending, 255, 0 );
				case 2:
					return Color.FromBytes( 0, 255, ascending );
				case 3:
					return Color.FromBytes( 0, descending, 255 );
				case 4:
					return Color.FromBytes( ascending, 0, 255 );
				default: // case 5:
					return Color.FromBytes( 255, 0, descending );
			}
		}
		/*public static byte[] Compress<T>( T data )
		{
			using var stream = new MemoryStream();
			using var deflate = new DeflateStream( stream, CompressionLevel.Optimal );

			var serialized = JsonSerializer.SerializeToUtf8Bytes( data );

			deflate.Write( serialized );
			deflate.Close();

			return stream.ToArray();
		}*/
		/*public static Dictionary<string,List<string>> wadIndex = new Dictionary<string,List<string>>();
		public static void generateWadIndex()
		{
			wadIndex.Clear();
			wadIndex = new();

			foreach ( var wadfile in FileSystem.Data.FindFile( "wads/", "*.wad" ) )
			{
				//Log.Info( wadfile );
				var wad = GoldSrc.WAD.WADParser.ParseWAD( FileSystem.Data.ReadAllBytes( $"wads/{wadfile}" ).ToArray() );
				if ( wad.lumps == null )
				{
					Log.Error( $"{wadfile} problem" );
					continue;
				}

				for ( var i = 0; i < wad.lumps.Count(); i++ )
				{
					var lump = wad.lumps[i];
					if ( lump.type == GoldSrc.WAD.WADLumpType.MIPTEX )
					{
						var name = GoldSrc.MIPTEXData.GetMipTexName( lump.data );

						if(!wadIndex.TryGetValue(name,out var _))
							wadIndex.Add(name, new List<string>());

						if ( !wadIndex[name].Any( x => x == wadfile ) )
							wadIndex[name].Add( wadfile );
					}
				}
			}
			FileSystem.Data.WriteAllText("wadIndex.txt",JsonSerializer.Serialize( wadIndex ));
		}*/

		/*public static void assetPartyWorkaround( ref byte[] buffer )
		{
			var findPattern1 = SearchBytePattern( new byte[] { 0x49, 0x73, 0x43, 0x68, 0x69, 0x6C, 0x64, 0x52, 0x65, 0x73, 0x6F, 0x75, 0x72, 0x63, 0x65 }, buffer, firstMatchReturn: true );
			if ( !findPattern1.Any() )
				Log.Error( "Asset.Party content compilation problem.." );
			else
			{
				var findPattern2 = SearchBytePattern( new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, buffer, offset: findPattern1.FirstOrDefault() + 15, firstMatchReturn: true );
				if ( !findPattern2.Any() )
					Log.Error( "Asset.Party content compilation problem.." );
				else
				{
					var findPattern3 = SearchBytePattern( new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, buffer, offset: findPattern2.FirstOrDefault() + 17, maxLimit: 50, firstMatchReturn: true );
					
						var lastOffset = (findPattern3.Any() ? findPattern3 : findPattern2 ).FirstOrDefault();
						Log.Info( (findPattern2.FirstOrDefault() + 17 )+ " " +findPattern3.Any() + " "+ lastOffset );
						while ( true )
							if ( buffer[++lastOffset] != 0x00 )
								break;

						buffer = buffer.Skip( lastOffset ).ToArray();
				}

			}
		}*/
		public static class TopologyHelper
		{
			public static void ConvertToTrianglesRange( ref int[] dstBuffer, int dstOffs, GfxTopology topology, int baseVertex, int numVertices )
			{
				if ( dstOffs + GetTriangleIndexCountForTopologyIndexCount( topology, numVertices ) > dstBuffer.Length )
					Notify.Create( "Array too small", Notify.NotifyType.Error );
				
				//throw new Exception( "Array too small" );

				int dst = dstOffs;
				if ( topology == GfxTopology.Quads )
				{
					for ( int i = 0; i < numVertices; i += 4 )
					{
						dstBuffer[dst++] = baseVertex + i + 0;
						dstBuffer[dst++] = baseVertex + i + 1;
						dstBuffer[dst++] = baseVertex + i + 2;
						dstBuffer[dst++] = baseVertex + i + 2;
						dstBuffer[dst++] = baseVertex + i + 3;
						dstBuffer[dst++] = baseVertex + i + 0;
					}
				}
				else if ( topology == GfxTopology.TriStrips )
				{
					for ( int i = 0; i < numVertices - 2; i++ )
					{
						if ( i % 2 == 0 )
						{
							dstBuffer[dst++] = baseVertex + i + 0;
							dstBuffer[dst++] = baseVertex + i + 1;
							dstBuffer[dst++] = baseVertex + i + 2;
						}
						else
						{
							dstBuffer[dst++] = baseVertex + i + 1;
							dstBuffer[dst++] = baseVertex + i + 0;
							dstBuffer[dst++] = baseVertex + i + 2;
						}
					}
				}
				else if ( topology == GfxTopology.TriFans )
				{
					for ( int i = 0; i < numVertices - 2; i++ )
					{
						dstBuffer[dst++] = (baseVertex + 0);
						dstBuffer[dst++] = (baseVertex + i + 1);
						dstBuffer[dst++] = (baseVertex + i + 2);
					}
				}
				else if ( topology == GfxTopology.QuadStrips )
				{
					for ( int i = 0; i < numVertices - 2; i += 2 )
					{
						dstBuffer[dst++] = (baseVertex + i + 0);
						dstBuffer[dst++] = (baseVertex + i + 1);
						dstBuffer[dst++] = (baseVertex + i + 2);
						dstBuffer[dst++] = (baseVertex + i + 2);
						dstBuffer[dst++] = (baseVertex + i + 1);
						dstBuffer[dst++] = (baseVertex + i + 3);
					}
				}
				else if ( topology == GfxTopology.Triangles )
				{
					for ( int i = 0; i < numVertices; i++ )
					{
						dstBuffer[dst++] = (baseVertex + i);
					}
				}
			}
			public static int GetTriangleIndexCountForTopologyIndexCount( GfxTopology topology, int indexCount )
			{
				// Three indexes per triangle.
				return 3 * GetTriangleCountForTopologyIndexCount( topology, indexCount );
			}

			public static int GetTriangleCountForTopologyIndexCount( GfxTopology topology, int indexCount )
			{
				switch ( topology )
				{
					case GfxTopology.Triangles:
						// One triangle per every three indexes.
						return indexCount / 3;
					case GfxTopology.TriStrips:
					case GfxTopology.TriFans:
						// One triangle per index, minus the first two.
						return (indexCount - 2);
					case GfxTopology.Quads:
						// Two triangles per four indices.
						return 2 * (indexCount / 4);
					case GfxTopology.QuadStrips:
						// Two triangles per two indexes, minus the first two.
						return (indexCount - 2);
					default:
						return 0;
				}
			}
		}
	}
}
