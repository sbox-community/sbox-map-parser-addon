// sbox.Community © 2023-2024

using Sandbox;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MapParser.SourceEngine
{
	public struct VPKFileEntryChunk
	{
		public uint PackFileIdx;
		public uint ChunkOffset;
		public uint ChunkSize;
	}

	public struct VPKFileEntry
	{
		public string Path;
		public uint Crc;
		public List<VPKFileEntryChunk> Chunks;
		public byte[] MetadataChunk;
	}

	public struct VPKDirectory
	{
		public Dictionary<string, VPKFileEntry> Entries;
		public int MaxPackFile;
	}

	public static class VPKParser
	{
		public static VPKDirectory ParseVPKDirectory( byte[] buffer )
		{
			using var memorystream = new MemoryStream( buffer );
			using var view = new BinaryReader( memorystream );

			memorystream.Seek( 0x00, SeekOrigin.Begin );
			if ( view.ReadUInt32() != 0x55AA1234 )
			{
				throw new Exception( "Invalid VPK directory signature." );
			}

			memorystream.Seek( 0x04, SeekOrigin.Begin );
			var version = view.ReadUInt32();
			memorystream.Seek( 0x08, SeekOrigin.Begin );
			var directorySize = view.ReadUInt32();

			var idx = version switch
			{
				0x01 => 0x0C,
				0x02 => 0x1C,
				_ => throw new Exception( "Unknown VPK directory version." )
			};

			var entries = new Dictionary<string, VPKFileEntry>();
			var maxPackFile = 0;

			while ( true )
			{
				var ext = Util.ReadString( ref buffer, idx );
				idx += ext.Length + 1;

				if ( ext.Length == 0 )
				{
					break;
				}

				while ( true )
				{
					var dir = Util.ReadString( ref buffer, idx );
					idx += dir.Length + 1;

					if ( dir.Length == 0 )
					{
						break;
					}

					while ( true )
					{
						var filename = Util.ReadString( ref buffer, idx );
						idx += filename.Length + 1;

						if ( filename.Length == 0 )
						{
							break;
						}

						var dirPrefix = (dir == "" || dir == " ") ? "" : $"{dir}/";
						var path = $"{dirPrefix}{filename}.{ext}";

						memorystream.Seek( (uint)idx, SeekOrigin.Begin );
						var crc = view.ReadUInt32();
						idx += 0x04;
						
						memorystream.Seek( (uint)idx, SeekOrigin.Begin );
						var metadataSize = view.ReadUInt16();
						idx += 0x02;

						var chunks = new List<VPKFileEntryChunk>();

						while ( true )
						{
							memorystream.Seek( (uint)idx, SeekOrigin.Begin );
							var packFileIdx = view.ReadUInt16();
							idx += 0x02;

							if ( packFileIdx == 0xFFFF )
							{
								break;
							}

							if ( packFileIdx != 0x07FF )
							{
								maxPackFile = Math.Max( maxPackFile, packFileIdx );
							}
							memorystream.Seek( (uint)idx, SeekOrigin.Begin );
							var chunkOffset = view.ReadUInt32();
							memorystream.Seek( (uint)idx + 0x04, SeekOrigin.Begin );
							var chunkSize = view.ReadUInt32();
							idx += 0x08;

							if ( chunkSize == 0 )
							{
								continue;
							}

							chunks.Add( new VPKFileEntryChunk
							{
								PackFileIdx = packFileIdx,
								ChunkOffset = chunkOffset,
								ChunkSize = chunkSize
							} );


						}

						var metadataChunk = metadataSize != 0 ? buffer[(int)idx..((int)idx + metadataSize)] : null;

						idx += metadataSize;

						entries[path] = new VPKFileEntry
						{
							Crc = crc,
							Path = path,
							Chunks = chunks,
							MetadataChunk = metadataChunk
						};
					}
				}
			}

			return new VPKDirectory
			{
				Entries = entries,
				MaxPackFile = maxPackFile
			};
		}

		public static async Task<VPKMount> CreateVPKMount( BaseFileSystem filesystem, string basePath )
		{
			VPKDirectory dir = ParseVPKDirectory( filesystem.ReadAllBytes( $"{basePath}_dir.vpk" ).ToArray() );
			return new VPKMount( basePath, dir, filesystem );
		}
	}
	public class VPKMount
	{
		private Dictionary<string, Task<byte[]>> cache = new Dictionary<string, Task<byte[]>>(); //fileDataPromise

		private readonly string basePath;
		private readonly BaseFileSystem filesystem;
		public readonly VPKDirectory dir;

		public VPKMount( string basePath, VPKDirectory dir, BaseFileSystem filesystem )
		{
			this.basePath = basePath;
			this.dir = dir;
			this.filesystem = filesystem;
		}

		/*private async Task<byte[]> FetchChunk( VPKFileEntryChunk chunk, Action abortedCallback, string debugName )
		{
			uint packFileIdx = chunk.PackFileIdx;
			long rangeStart = chunk.ChunkOffset;
			long rangeSize = chunk.ChunkSize;
			return await this.filesystem.ReadAllBytesAsync( $"{basePath}_{packFileIdx:D3}.vpk" ); //dataFetcher.FetchData( $"{basePath}_{packFileIdx:D3}.vpk", new FetchDataOptions { DebugName = debugName, RangeStart = rangeStart, RangeSize = rangeSize, AbortedCallback = abortedCallback } );
		}*/

		private async Task<byte[]> FetchChunk( VPKFileEntryChunk chunk, Action abortedCallback, string debugName )
		{
			uint packFileIdx = chunk.PackFileIdx;
			long rangeStart = chunk.ChunkOffset;
			long rangeSize = chunk.ChunkSize;

			string filePath = $"{basePath}_{packFileIdx:D3}.vpk";

			System.IO.Stream stream = this.filesystem.OpenRead( filePath, FileMode.Open ) ;
			stream.Seek( rangeStart, SeekOrigin.Begin );

			byte[] buffer = new byte[rangeSize];
			int bytesRead = await stream.ReadAsync( buffer, 0, (int)rangeSize );

			if ( bytesRead != rangeSize )
			{
				throw new Exception( $"Failed to read chunk of size {rangeSize} from {filePath}" );
			}

			return buffer;
		}

		public VPKFileEntry FindEntry( string path )
		{
			return this.dir.Entries.GetValueOrDefault( path, new() );
		}

		private async Task<byte[]> FetchFileDataInternal( VPKFileEntry entry, Action abortedCallback )
		{
			List<Task<byte[]>> promises = new List<Task<byte[]>>();
			int size = 0;
			
			int metadataSize = entry.MetadataChunk != null ? entry.MetadataChunk.Length : 0;
			size += metadataSize;

			foreach ( VPKFileEntryChunk chunk in entry.Chunks )
			{

				promises.Add( FetchChunk(  chunk, abortedCallback, entry.Path ));
				size += (int)chunk.ChunkSize;
			}
			if ( promises.Count == 0 )
			{
				//Debug.Assert( entry.MetadataChunk != null );

				if ( entry.MetadataChunk == null )
					return null; // HATA

				return entry.MetadataChunk;
			}
		

			byte[] metadataChunk = entry.MetadataChunk ?? new byte[0];
			byte[] fileData = new byte[metadataSize + size];

			int offs = 0;

			// Metadata comes first.
			if ( metadataChunk.Length > 0 )
			{
				metadataChunk.CopyTo( fileData, offs );
				offs += metadataChunk.Length;
			}

			foreach ( Task<byte[]> task in promises )
			{
				byte[] chunk = await task;
				chunk.CopyTo( fileData, offs );
				offs += chunk.Length;
			}

			return fileData;
		}

		public Task<byte[]> FetchFileData( VPKFileEntry entry )
		{
			if ( !cache.ContainsKey( entry.Path ) )
			{
				Task<byte[]> task = FetchFileDataInternal( entry, () =>
				{
					cache.Remove( entry.Path );
				} );
				cache.Add( entry.Path, task );
			}
			return cache[entry.Path];
		}
	}


}
