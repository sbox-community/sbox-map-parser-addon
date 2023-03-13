// sbox.Community © 2023-2024

using Sandbox;
using Sandbox.UI;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MapParser
{
	public partial class Util //static
	{
		public enum GfxTopology
		{
			Triangles,
			TriStrips,
			TriFans,
			Quads,
			QuadStrips
		}
		public static string ReadString( ref byte[] buffer, int offs, int length = -1, bool nulTerminated = true, string encoding = null )
		{
			var buf = length == -1 ? buffer.Skip( offs ).ToArray() : buffer.Skip( offs ).Take( length ).ToArray();
			var bufLength = buf.Length;
			int byteLength = 0;
			
			while ( true )
			{
				if ( length >= 0 && byteLength >= length )
					break;
				if ( nulTerminated && ( byteLength >= bufLength || buf[byteLength] == 0 ) )
					break;
				byteLength++;
			}

			if ( byteLength == 0 )
				return "";

			if ( encoding != null )
			{
				return DecodeString( ref buffer, offs, byteLength, encoding );
			}
			else
			{
				return CopyBufferToString( ref buffer, offs, byteLength );
			}
		}
		public static string DecodeString( ref byte[] buffer, int offs = 0, int byteLength = -1, string encoding = "utf8" )
		{
			if ( byteLength == -1 )
				byteLength = buffer.Length - offs;

			// Use System.Text.Encoding class to decode the string.
			return Encoding.GetEncoding( encoding ).GetString( buffer, offs, byteLength );
		}
		private static string CopyBufferToString( ref byte[] buffer, int offs, int byteLength )
		{
			var buf = buffer.Skip( offs ).Take( byteLength ).ToArray();
			var sb = new StringBuilder();
			foreach ( var b in buf )
				sb.Append( (char)b );
			return sb.ToString();
		}
		public static Vector4 ReadVec4( ref byte[] buffer, int offs )
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
		public static string PathToMapName( string path )
		{
			return Path.GetFileNameWithoutExtension( path );
		}
		public static string PathToMapNameWithExtension( string path )
		{
			return Path.GetFileName( path );
		}
		public static string PathWithouthFile( string path )
		{
			return Path.GetDirectoryName( path );
		}
		public static string RemoveInvalidChars( string filename )
		{
			return string.Concat( filename.Split( Path.GetInvalidPathChars() ) );
		}
		public static string ReplaceInvalidChars( string filename )
		{
			return string.Join( "_", filename.Split( Path.GetInvalidFileNameChars() ) );
		}
		public static byte[] Compress<T>( T data )
		{
			using var stream = new MemoryStream();
			using var deflate = new DeflateStream( stream, CompressionLevel.Optimal );

			var serialized = JsonSerializer.SerializeToUtf8Bytes( data );

			deflate.Write( serialized );
			deflate.Close();

			return stream.ToArray();
		}
		public static T Decompress<T>( byte[] bytes ) => Decompress<T>( ref bytes );
		public static T Decompress<T>( ref byte[] bytes )
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
		public static float Saturate( float v )
		{
			return Math.Clamp( v, 0.0f, 1.0f );
		}
		public static bool UnionPoint(ref BBox AABB, Vector3 v )
		{
			bool changed = false;

			if ( v.x < AABB.Mins.x )
			{
				AABB.Mins.x = v.x;
				changed = true;
			}

			if ( v.y < AABB.Mins.y )
			{
				AABB.Mins.y = v.y;
				changed = true;
			}

			if ( v.z < AABB.Mins.z )
			{
				AABB.Mins.z = v.z;
				changed = true;
			}

			if ( v.x > AABB.Maxs.x )
			{
				AABB.Maxs.x = v.x;
				changed = true;
			}

			if ( v.y > AABB.Maxs.y )
			{
				AABB.Maxs.y = v.y;
				changed = true;
			}

			if ( v.z > AABB.Maxs.z )
			{
				AABB.Maxs.z = v.z;
				changed = true;
			}

			return changed;
		}

		public static void Union( ref BBox refBBox, BBox a, BBox b )
		{
			refBBox.Mins.x = Math.Min( a.Mins.x, b.Mins.x );
			refBBox.Mins.y = Math.Min( a.Mins.y, b.Mins.y );
			refBBox.Mins.z = Math.Min( a.Mins.z, b.Mins.z );
			refBBox.Maxs.x = Math.Max( a.Maxs.x, b.Maxs.x );
			refBBox.Maxs.y = Math.Max( a.Maxs.y, b.Maxs.y );
			refBBox.Maxs.z = Math.Max( a.Maxs.z, b.Maxs.z );
		}

		// Constructors and setters.
		public static Color ColorNewFromRGBA( float r, float g, float b, float a = 1.0f )
		{
			return new Color( r, g, b, a );
		}

		public static Color StringToColor( string str, int seed = 4 )
		{
			// Convert string to hash code
			int hashCode = str.FastHash() * seed;

			// Generate random color using hash code
			Random rand = new Random( hashCode );
			byte r = (byte)rand.Next( 0, 256 );
			byte g = (byte)rand.Next( 0, 256 );
			byte b = (byte)rand.Next( 0, 256 );

			// Return the color
			return Color.FromBytes( r, g, b, 255 ).Lighten(1);
		}
		/*public static List<int> SearchBytePattern( byte[] pattern, byte[] bytes, int offset = 0, int maxLimit = 0, bool firstMatchReturn = false )
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

		public static Dictionary<string,List<string>> wadIndex = new Dictionary<string,List<string>>();
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
		}
		[ClientRpc]
		public static void sendTexture( byte[] textureData, int Width, int Height, bool compress = false )
		{
			var textureName = Texture.Create( Width, Height );
			textureName.WithData( compress ? Util.Decompress <byte[]>( textureData ) : textureData );

			var textureFinish = textureName.Finish();

			Panel test = Game.RootPanel.FindRootPanel().Add.Panel();
			test.Style.Width = Length.Fraction( 1.0f );
			test.Style.Height = Length.Fraction( 1.0f );
			test.Style.BackgroundImage = textureFinish;
			test.Style.Position = PositionMode.Absolute;
			test.Style.BackgroundRepeat = BackgroundRepeat.NoRepeat;

			test.Style.BackgroundSizeX = Length.Fraction( 0.9f );
			test.Style.BackgroundSizeY = Length.Fraction( 0.9f );


			_ = Timer( 5000, () =>
			{
				test.Delete();
			} );
		}
		*/

		public static class TopologyHelper
		{
			public static void ConvertToTrianglesRange( ref int[] dstBuffer, int dstOffs, GfxTopology topology, int baseVertex, int numVertices )
			{
				if ( dstOffs + GetTriangleIndexCountForTopologyIndexCount( topology, ref numVertices ) > dstBuffer.Length )
				{
					Log.Info( (dstOffs + GetTriangleIndexCountForTopologyIndexCount( topology, ref numVertices ) ) + " " + dstBuffer.Length );
					Notify.Create( "Array too small", Notify.NotifyType.Error );
					return;

				}

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

			public static int GetTriangleIndexCountForTopologyIndexCount( GfxTopology topology, ref ushort indexCount ) =>  GetTriangleIndexCountForTopologyIndexCount( topology, ref indexCount );
			public static int GetTriangleIndexCountForTopologyIndexCount( GfxTopology topology, ref int indexCount )
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

		public class BitMap
		{
			public uint[] words;

			public int numBits;

			public BitMap( int numBits )
			{
				if ( numBits <= 0 )
					throw new ArgumentException( "numBits must be positive." );

				this.numBits = numBits;

				var numWords = (this.numBits + 31) >> 5;
				this.words = new uint[numWords];
			}

			public void Copy( BitMap o )
			{
				if ( this.words.Length != o.words.Length )
					throw new ArgumentException( "The length of this and o must be the same." );

				for ( var i = 0; i < o.words.Length; i++ )
					this.words[i] = o.words[i];
			}

			public void Fill( bool v )
			{
				var value = v ? 0xFFFFFFFF : 0;
				for ( var i = 0; i < this.words.Length; i++ )
					this.words[i] = value;
			}

			public void Or( BitMap o )
			{
				if ( this.words.Length < o.words.Length )
					throw new ArgumentException( "The length of this must be greater than or equal to o." );

				for ( var i = 0; i < o.words.Length; i++ )
					this.words[i] |= o.words[i];
			}

			public void SetWord( int wordIndex, uint wordValue )
			{
				this.words[wordIndex] = wordValue;
			}

			public void SetWords( uint[] wordValues, int firstWordIndex = 0 )
			{
				for ( var i = 0; i < wordValues.Length; i++ )
					this.words[firstWordIndex + i] = wordValues[i];
			}

			public void SetBit( int bitIndex, bool bitValue )
			{
				var wordIndex = bitIndex >> 5;
				var mask = (uint)(1 << (31 - (bitIndex & 0x1F)));
				this.words[wordIndex] = BitMapHelper.SetBitFlagEnabled( this.words[wordIndex], mask, bitValue );
			}

			public bool GetBit( int bitIndex )
			{
				var wordIndex = bitIndex >> 5;
				var mask = (uint)(1 << (31 - (bitIndex & 0x1F)));
				return (this.words[wordIndex] & mask) != 0;
			}

			public bool HasAnyBit()
			{
				for ( var i = 0; i < this.words.Length; i++ )
					if ( this.words[i] != 0 )
						return true;

				return false;
			}
		}

		public static class BitMapHelper
		{
			public static int BitMapGetSerializedByteLength( int numBits )
			{
				return (numBits + 7) >> 3;
			}

			public static int BitMapSerialize( byte[] buffer, int offs, BitMap bitMap )
			{
				var numBytes = BitMapGetSerializedByteLength( bitMap.numBits );
				for ( var i = 0; i < numBytes; i++ )
				{
					var shift = 24 - ((i & 0x03) << 3);
					buffer[offs++] = (byte)((bitMap.words[i >> 2] >> shift) & 0xFF);
				}
				return offs;
			}

			public static int BitMapDeserialize( byte[] buffer, int offs, BitMap bitMap )
			{
				int numBytes = BitMapGetSerializedByteLength( bitMap.numBits );
				for ( int i = 0; i < numBytes; i++ )
				{
					int shift = 24 - ((i & 0x03) << 3);
					bitMap.words[i >> 2] |= (uint)(buffer[offs++] << shift);
				}
				return offs;
			}
			public static uint SetBitFlagEnabled( uint v, uint mask, bool enabled )
			{
				if ( enabled )
					v |= mask;
				else
					v &= ~mask;
				return v;
			}
		}

		public static int FillVec4( ref float[] d, int offs, float v0, float v1 = 0, float v2 = 0, float v3 = 0 )
		{
			d[offs + 0] = v0;
			d[offs + 1] = v1;
			d[offs + 2] = v2;
			d[offs + 3] = v3;
			return 4;
		}
		public static int FillMatrix4x2( ref float[] d, int offs, in Matrix4x4 m )
		{
			// The bottom two rows are basically just ignored in a 4x2.
			d[offs + 0] = m.M11;
			d[offs + 1] = m.M21;
			d[offs + 2] = m.M31;
			d[offs + 3] = m.M41;
			d[offs + 4] = m.M12;
			d[offs + 5] = m.M22;
			d[offs + 6] = m.M32;
			d[offs + 7] = m.M42;
			return 4 * 2;
		}
		public static int FillColor( ref float[] d, int offs, in Color c, float a = 1.0f )
		{
			d[offs + 0] = c.r;
			d[offs + 1] = c.g;
			d[offs + 2] = c.b;
			d[offs + 3] = a;
			return 4;
		}

		// Buffer.BlockCopy is whitelist, using Array.Copy version causes lack of performance.
		// Because Buffer.BlockCopy is more efficient for copying large chunks of memory. 
		/*public class ResizableArrayBuffer
		{
			private byte[] buffer;
			private int byteSize;
			private int byteCapacity;

			public ResizableArrayBuffer( int initialSize = 0x400 )
			{
				byteSize = 0;
				byteCapacity = initialSize;
				buffer = new byte[initialSize];
			}

			public void EnsureSize( int byteSize )
			{
				this.byteSize = byteSize;

				if ( byteSize > byteCapacity )
				{
					byteCapacity = Math.Max( byteSize, byteCapacity * 2 );
					byte[] oldBuffer = buffer;
					buffer = new byte[byteCapacity];
					Array.Copy( oldBuffer, buffer, oldBuffer.Length );
				}
			}


			public void AddByteSize( int byteSize )
			{
				EnsureSize( this.byteSize + byteSize );
			}

			public void FinishAddUint32( uint[] arr )
			{
				Array.Copy( buffer, 0, arr, 0, byteSize );
			}

			public uint[] AddUint32( int count )
			{
				AddByteSize( count << 2 );
				uint[] arr = new uint[byteSize]; // verify
				//Array.Copy( buffer, 0, arr, 0, byteSize );
				return arr;
			}

			public void FinishAddInt32( int[] arr )
			{
				Array.Copy( buffer, 0, arr, 0, byteSize );
			}
			public int[] AddInt32( int count )
			{
				AddByteSize( count << 2 );
				int[] arr = new int[byteSize]; // verify
				//Array.Copy( buffer, 0, arr, 0, byteSize );
				return arr;
			}
			public void FinishAddFloat32( float[] arr, int offs, int count )
			{
				Array.Copy( buffer, offs, arr, 0, count * 4 );
			}
			public (float[],int) AddFloat32( int count )
			{
				int offs = byteSize + 0;
				AddByteSize( count << 2 );
				float[] arr = new float[byteSize]; // verify
				//Array.Copy( buffer, offs, arr, 0, count * 4 );
				return (arr,offs);
			}

			public uint[] GetAsUint32Array()
			{
				uint[] arr = new uint[byteSize / 4];
				for ( int i = 0; i < byteSize; i += 4 )
				{
					arr[i / 4] = BitConverter.ToUInt32( buffer, i );
				}
				return arr;
			}

			public float[] GetAsFloat32Array()
			{
				float[] arr = new float[byteSize / 4];
				for ( int i = 0; i < byteSize; i += 4 )
				{
					arr[i / 4] = BitConverter.ToSingle( buffer, i );
				}
				return arr;
			}


			public byte[] Finalize()
			{
				Log.Info( buffer.Any(x=> x > 0) );
				byte[] result = new byte[byteSize];
				Array.Copy( result, 0, buffer, 0, byteSize );
				return result;
			}
		
		}*/
	}
}
