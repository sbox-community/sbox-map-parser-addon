// sbox.Community © 2023-2024

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MapParser.SourceEngine
{

	class Stream
	{
		private int offset = 0;
		private byte[] buffer;

		public Stream( byte[] buffer )
		{
			this.buffer = buffer;
		}

		public int Tell()
		{
			return offset;
		}

		public byte ReadUint8()
		{
			return buffer[offset++];
		}

		public uint ReadUint32()
		{
			uint value = BitConverter.ToUInt32( buffer, offset );
			offset += 4;
			return value;
		}

		public ulong ReadUInt64()
		{
			ulong value = BitConverter.ToUInt64( buffer, offset );
			offset += 8;
			return value;
		}

		public float ReadFloat32()
		{
			float value = BitConverter.ToSingle( buffer, offset );
			offset += 4;
			return value;
		}

		public string ReadByteString( int n )
		{
			string value = System.Text.Encoding.UTF8.GetString( buffer, offset, n );
			offset += n;
			return value;
		}

		public string ReadString()
		{
			int length = 0;
			int stringEnd = -1;
			for ( int i = offset; i < buffer.Length; i++ )
			{
				if ( buffer[i] == 0 )
				{
					stringEnd = i;
					break;
				}
				length++;
			}
			if ( stringEnd != -1 )
			{
				string value = System.Text.Encoding.UTF8.GetString( buffer, offset, length );
				offset = stringEnd + 1;
				return value;
			}
			return null;
		}
	}
	public class GMAFile
	{
		public int fileID { get; set; }
		public string filename { get; set; }
		public byte[] data { get; set; }
	}



	public class GMA
	{
		public string name;
		public object desc;
		public string author;

		public List<GMAFile> files = new List<GMAFile>();

		public GMA( byte[] buffer )
		{
			var stream = new Stream( buffer );
			const string expectedHeader = "GMAD";
			string header = stream.ReadByteString( 4 );
			if ( header != expectedHeader )
				throw new Exception( $"Unexpected header, expected '{expectedHeader}', but got '{header}'" );

			byte formatVersion = stream.ReadUint8();
			if ( formatVersion != 0x03 )
				throw new Exception( $"Unexpected format version, expected 0x03, but got {formatVersion:X2}" );

			ulong steamID = stream.ReadUInt64();
			ulong timestamp = stream.ReadUInt64();

			List<string> requiredContents = new List<string>();
			if ( formatVersion > 0x01 )
			{
				while ( true )
				{
					string str = stream.ReadString();
					if ( string.IsNullOrEmpty( str ) )
						break;
					requiredContents.Add( str );
				}
			}

			name = stream.ReadString();
			string descJson = stream.ReadString();
			desc = JsonSerializer.Deserialize<object>( descJson );
			author = stream.ReadString();
			uint addonVersion = stream.ReadUint32();

			List<GMAFileEntry> entries = new List<GMAFileEntry>();

			uint fileID = 1;
			ulong fileOffset = 0;
			while ( true )
			{
				uint sentinel = stream.ReadUint32();
				if ( sentinel == 0 )
					break;

				string filename = stream.ReadString();
				ulong fileSize = stream.ReadUInt64();
				uint crc = stream.ReadUint32();
				entries.Add( new GMAFileEntry { fileID = fileID, filename = filename, offset = fileOffset, fileSize = fileSize } );
				fileOffset += fileSize;
				fileID++;
			}

			ulong dataOffset = (ulong)stream.Tell();

			foreach ( GMAFileEntry entry in entries )
			{
				uint fileID2 = entry.fileID;
				string filename = entry.filename;
				ulong offset = entry.offset;
				ulong fileSize = entry.fileSize;
				ulong fileOffset2 = dataOffset + offset;
				if ( fileOffset2 > 0xFFFFFFFF || fileSize > 0xFFFFFFFF )
					throw new Exception( $"File offset or size is too large to fit into a 32-bit integer" );
				byte[] data = buffer[(int)fileOffset2..(int)(fileOffset2 + fileSize)];
				files.Add( new GMAFile { fileID = fileID2, filename = filename, data = data } );
			}
		}

		private class GMAFileEntry
		{
			public uint fileID;
			public string filename;
			public ulong offset;
			public ulong fileSize;
		}

		public class GMAFile
		{
			public uint fileID;
			public string filename;
			public byte[] data;
		}
	}

}
