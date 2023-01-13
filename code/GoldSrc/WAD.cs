using Sandbox.Internal;
using System;
using System.Collections.Generic;
using System.Text;

namespace MapParser.GoldSrc
{
	public partial class WAD
	{
		// wadIndex consist of wads texture names from half-life.fr mirror
		// in order to find out not exists wad's and use
		public static Dictionary<string, List<string>> wadIndex = new();

		public List<WADLump> lumps { get; set; }

		public enum WADLumpType
		{
			MIPTEX = 0x43,
		}

		public class WADLump
		{
			public string name { get; set; }
			public WADLumpType type { get; set; }
			public byte[] data { get; set; }
		}

		public static class WADParser
		{
			public static WAD ParseWAD( byte[] buffer )
			{
				var magic = Encoding.ASCII.GetString( buffer, 0, 4 );
				if ( magic != "WAD3" )
				{
					Notify.Create( "Invalid WAD magic", Notify.NotifyType.Error );
					return new();
				}

				var numlumps = BitConverter.ToInt32( buffer, 4 );
				var infotableofs = BitConverter.ToInt32( buffer, 8 );

				var lumps = new List<WADLump>();
				var infotableidx = infotableofs;
				for ( int i = 0; i < numlumps; i++, infotableidx += 0x20 )
				{
					if ( buffer.Length <= infotableidx )
					{
						Notify.Create( "Requested view does not fit inside mapping", Notify.NotifyType.Error );
						return new();
					}
					var filepos = BitConverter.ToInt32( buffer, infotableidx );
					var disksize = BitConverter.ToInt32( buffer, infotableidx + 4 );
					var size = BitConverter.ToInt32( buffer, infotableidx + 8 );
					if ( size != disksize )
					{
						Notify.Create( "Size and disksize do not match", Notify.NotifyType.Error );
						return new();
					}

					var type = buffer[infotableidx + 12];
					var compression = buffer[infotableidx + 13];
					if ( compression != 0 )
					{
						Notify.Create( "Compression not supported", Notify.NotifyType.Error );
						return new();
					}

					var name = Encoding.ASCII.GetString( buffer, infotableidx + 16, 16 ).TrimEnd( '\0' );

					var data = new byte[disksize];
					Array.Copy( buffer, filepos, data, 0, disksize );
					lumps.Add( new WADLump
					{
						name = name,
						type = (WADLumpType)type,
						data = data
					} );
				}

				return new WAD
				{
					lumps = lumps
				};

			}
		}
		public static List<string> wadFilesFromTextureName( string textureName )
		{
			if ( wadIndex.TryGetValue( textureName, out var list ) )
				return list;
			return new();
		}
	}
}
