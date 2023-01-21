using Sandbox;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static MapParser.Manager;

namespace MapParser.GoldSrc
{
	public partial class BSPFile
	{
		public enum LumpType
		{
			ENTITIES = 0,
			PLANES = 1,
			TEXTURES = 2,
			VERTEXES = 3,
			VISIBILITY = 4,
			NODES = 5,
			TEXINFO = 6,
			FACES = 7,
			LIGHTING = 8,
			CLIPNODES = 9,
			LEAFS = 10,
			MARKSURFACES = 11,
			EDGES = 12,
			SURFEDGES = 13,
			MODELS = 14,
		};

		public struct TexinfoMapping
		{
			public Vector4 s { get; set; }
			public Vector4 t { get; set; }
		}

		public struct Texinfo
		{
			public TexinfoMapping textureMapping { get; set; }
			public int miptex { get; set; }
			public int flags { get; set; }
		}

		public struct SurfaceLightmapData
		{
			public int faceIndex { get; set; }
			public int width { get; set; }
			public int height { get; set; }
			public List<int> styles { get; set; }
			public byte[] samples { get; set; }
			public int pageIndex { get; set; }
			public int pagePosX { get; set; }
			public int pagePosY { get; set; }
		}

		public struct Surface
		{
			public string texName { get; set; }
			public int startIndex { get; set; }
			public int indexCount { get; set; }
			public List<SurfaceLightmapData> lightmapData { get; set; }
		}

		public struct Face
		{
			public int index { get; set; }
			public int texinfo { get; set; }
			public string texName { get; set; }
		}

		// For s&box
		public struct meshData
		{
			public List<SimpleVertex> vertices { get; set; }
			public List<int> indices { get; set; }
			public string textureName { get; set; }
			public int id { get; set; }
		}

		public int version { get; set; }
		//public int[] indexData { get; set; }
		//public float[] vertexData { get; set; }
		public List<Surface> surfaces { get; set; } = new();
		public List<EntityParser.EntityData> entities { get; set; }
		public List<byte[]> extraTexData = new List<byte[]>();
		public LightmapPackerPage lightmapPackerPage { get; set; } = new LightmapPackerPage( 2048, 2048 );
		public byte[] buffer { get; set; }

		// For sbox
		public List<meshData> meshDataList = new();

		public BSPFile( SpawnParameter settings )
		{
			var filePath = $"{settings.mapPath}{(settings.assetparty_version ? ".txt" : "")}";

			if ( !settings.fileSystem.FileExists( filePath ) )
			{
				Notify.Create( $"{filePath} not found in the filesystem", Notify.NotifyType.Error );
				return;
			}

			byte[] buffer;

			if ( settings.assetparty_version )
				buffer = settings.fileSystem.ReadAllBytes( filePath ).ToArray();
			else
				buffer = Convert.FromBase64String( settings.fileSystem.ReadAllText( filePath ) );

			this.buffer = buffer;

			using ( var stream = new MemoryStream( buffer ) )
			using ( var reader = new BinaryReader( stream ) )
			{
				version = reader.ReadInt32();

				// I didn't got any version problem, if there, we can expand compatibility
				if ( version != 30 )
					Notify.Create( $"Version is not 30", Notify.NotifyType.Error );
			}

			var texinfoa = new List<Texinfo>();

			var texinfoData = GetLumpData( LumpType.TEXINFO );
			int texinfoCount = texinfoData.Length / 0x28;
			for ( int i = 0; i < texinfoCount; i++ )
			{
				int infoOffs = i * 0x28;
				var textureMappingS = Util.ReadVec4( texinfoData, infoOffs + 0x00 );
				var textureMappingT = Util.ReadVec4( texinfoData, infoOffs + 0x10 );
				var textureMapping = new TexinfoMapping { s = textureMappingS, t = textureMappingT };
				int miptex = BitConverter.ToInt32( texinfoData, infoOffs + 0x20 );
				int flags = BitConverter.ToInt32( texinfoData, infoOffs + 0x24 );
				texinfoa.Add( new Texinfo { textureMapping = textureMapping, miptex = miptex, flags = flags } );
			}

			// Parse miptex.
			var textures = GetLumpData( LumpType.TEXTURES );
			int nummiptex;
			using ( var stream = new MemoryStream( textures ) )
			using ( var reader = new BinaryReader( stream ) )
			{
				stream.Position = 0x00;
				nummiptex = reader.ReadInt32();
			}
			var textureNames = new List<string>();
			for ( int i = 0; i < nummiptex; i++ )
			{
				int miptexOffs;
				using ( var stream = new MemoryStream( textures ) )
				using ( var reader = new BinaryReader( stream ) )
				{
					stream.Position = 0x04 + i * 0x04;
					miptexOffs = reader.ReadInt32();
				}
				var texName = Util.ReadString( textures, miptexOffs + 0x00, 0x10, true );
				int hasTextureData;
				using ( var stream = new MemoryStream( textures ) )
				using ( var reader = new BinaryReader( stream ) )
				{
					stream.Position = miptexOffs + 0x18;
					hasTextureData = reader.ReadInt32();
				}
				if ( hasTextureData != 0 )
					extraTexData.Add( textures.Skip( miptexOffs ).ToArray() );
				textureNames.Add( texName );
			}

			// Must be loaded after spawning of map for async
			if ( Game.IsClient )
			{
				// Extra texture data from BSP
				for ( var i = 0; i < extraTexData.Count(); i++ )
				{
					var dataextra = extraTexData[i];
					TextureCache.addTexture( dataextra, wadname: "FromBSP" );

					// We clear manually all materials, remove comment,
					// If there is a conflict BSP version tex and wad version.

					//var name = MIPTEXData.GetMipTexName( dataextra );
					//MaterialCache.clearMaterial( matname: name );
				}
			}

			// Parse out edges / surfedges.
			byte[] data = GetLumpData( LumpType.EDGES );
			int[] edges = new int[data.Length / 2];

			for ( int i = 0; i < data.Length; i += 2 )
			{
				int value = BitConverter.ToUInt16( data, i );
				edges[i / 2] = value;
			}

			data = GetLumpData( LumpType.SURFEDGES );
			int[] surfedges = new int[data.Length / 4];

			for ( int i = 0; i < data.Length; i += 4 )
			{
				int value = BitConverter.ToInt32( data, i );
				surfedges[i / 4] = value;
			}
			var vertindices = new int[surfedges.Length];
			for ( int i = 0; i < surfedges.Length; i++ )
			{
				var surfedge = surfedges[i];
				if ( surfedges[i] >= 0 )
					vertindices[i] = edges[surfedge * 2 + 0];
				else
					vertindices[i] = edges[-surfedge * 2 + 1];
			}

			// Parse out faces, sort by texinfo.
			var facelist = GetLumpData( LumpType.FACES );

			List<Face> faces = new List<Face>();

			int numVertexData = 0, numIndexData = 0;
			using ( MemoryStream stream = new MemoryStream( facelist ) )
			using ( BinaryReader reader = new BinaryReader( stream ) )
			{
				for ( int i = 0; i < facelist.Length / 0x14; i++ )
				{
					stream.Seek( i * 0x14 + 0x08, SeekOrigin.Begin );
					int numedges = reader.ReadUInt16();

					stream.Seek( i * 0x14 + 0x0A, SeekOrigin.Begin );
					int texinfo = reader.ReadUInt16();

					string texName = textureNames[texinfoa[texinfo].miptex];
					faces.Add( new Face { index = i, texinfo = texinfo, texName = texName } );

					numVertexData += numedges;
					numIndexData += Util.TopologyHelper.GetTriangleIndexCountForTopologyIndexCount( Util.GfxTopology.TriFans, numedges );
				}
			}

			// Not working same as typescript version
			faces.Sort( ( a, b ) => b.texName.CompareTo( a.texName ) );

			float[] vertexData = new float[numVertexData * 7];
			int dstOffsVertex = 0;

			int[] indexData = new int[numIndexData];

			int dstOffsIndex = 0;
			int dstIndexBase = 0;

			// Build surface meshes
			byte[] vertexesData = GetLumpData( LumpType.VERTEXES );
			float[] vertexes;
			using ( MemoryStream stream = new MemoryStream( vertexesData ) )
			using ( BinaryReader reader = new BinaryReader( stream ) )
			{
				vertexes = new float[vertexesData.Length / sizeof( float )];
				for ( int i = 0; i < vertexes.Length; i++ )
					vertexes[i] = reader.ReadSingle();
			}

			// For sbox
			List<SimpleVertex> vertexList = new();
			List<Vector2> lightmapList = new();
			List<int> indexList = new();

			byte[] lighting = GetLumpData( LumpType.LIGHTING );
			for ( int i = 0; i < faces.Count; i++ )
			{
				vertexList = new();
				lightmapList = new();

				Face face = faces[i];
				int idx = face.index * 0x14;

				using ( MemoryStream stream = new MemoryStream( facelist ) )
				using ( BinaryReader reader = new BinaryReader( stream ) )
				{
					stream.Seek( idx + 0x00, SeekOrigin.Begin );
					int planenum = reader.ReadUInt16();
					stream.Seek( idx + 0x02, SeekOrigin.Begin );
					int side = reader.ReadUInt16();
					stream.Seek( idx + 0x04, SeekOrigin.Begin );
					uint firstedge = reader.ReadUInt32();
					stream.Seek( idx + 0x08, SeekOrigin.Begin );
					int numedges = reader.ReadUInt16();

					List<int> styles = new List<int>();
					for ( int j = 0; j < 4; j++ )
					{
						int style = reader.ReadByte();
						if ( style == 0xFF )
							break;
						styles.Add( style );
					}

					int lightofs;

					using ( MemoryStream stream1 = new MemoryStream( facelist ) )
					using ( BinaryReader reader1 = new BinaryReader( stream1 ) )
					{
						stream.Seek( idx + 0x10, SeekOrigin.Begin );
						lightofs = reader.ReadInt32();
					}

					Surface mergeSurface = new();
					if ( i > 0 )
					{
						Face prevFace = faces[i - 1];
						bool canMerge = true;

						if ( face.texName != prevFace.texName )
							canMerge = false;

						if ( canMerge )
							mergeSurface = surfaces.LastOrDefault();

						// TODO(jstpierre)
					}

					TexinfoMapping m = texinfoa[face.texinfo].textureMapping;
					float minTexCoordS = float.PositiveInfinity, minTexCoordT = float.PositiveInfinity;
					float maxTexCoordS = float.NegativeInfinity, maxTexCoordT = float.NegativeInfinity;

					int dstOffsVertexBase = dstOffsVertex;

					for ( int j = 0; j < numedges; j++ )
					{
						int vertIndex = vertindices[firstedge + j];
						float px = vertexes[vertIndex * 3 + 0];
						float py = vertexes[vertIndex * 3 + 1];
						float pz = vertexes[vertIndex * 3 + 2];

						float texCoordS = px * m.s.x + py * m.s.y + pz * m.s.z + m.s.w;
						float texCoordT = px * m.t.x + py * m.t.y + pz * m.t.z + m.t.w;

						vertexData[dstOffsVertex++] = px;
						vertexData[dstOffsVertex++] = py;
						vertexData[dstOffsVertex++] = pz;

						vertexData[dstOffsVertex++] = texCoordS;
						vertexData[dstOffsVertex++] = texCoordT;

						// Dummy lightmap textureData for now, will compute after the loop.
						vertexData[dstOffsVertex++] = 0;
						vertexData[dstOffsVertex++] = 0;

						minTexCoordS = Math.Min( minTexCoordS, texCoordS );
						minTexCoordT = Math.Min( minTexCoordT, texCoordT );
						maxTexCoordS = Math.Max( maxTexCoordS, texCoordS );
						maxTexCoordT = Math.Max( maxTexCoordT, texCoordT );
					}

					int surfaceW = (int)Math.Ceiling( maxTexCoordS / 16 ) - (int)Math.Floor( minTexCoordS / 16 ) + 1;
					int surfaceH = (int)Math.Ceiling( maxTexCoordT / 16 ) - (int)Math.Floor( minTexCoordT / 16 ) + 1;
					int lightmapSamplesSize = surfaceW * surfaceH * styles.Count * 3;
					byte[] samples = (lightofs != int.MaxValue) ? lighting.Skip( lightofs ).Take( lightmapSamplesSize ).ToArray() : null; //.Select( b => (int)b ).ToList()  List<int>

					SurfaceLightmapData lightmapData = new SurfaceLightmapData
					{
						faceIndex = face.index,
						width = surfaceW,
						height = surfaceH,
						pageIndex = 0,
						pagePosX = 0,
						pagePosY = 0,
						styles = styles,
						samples = samples
					};

					if ( !lightmapPackerPage.allocate( ref lightmapData ) )
						Notify.Create( "Could not pack", Notify.NotifyType.Error );

					// Fill in UV
					for ( int ii = 0; ii < numedges; ii++ )
					{
						int offs = dstOffsVertexBase + (ii * 7) + 3;

						float texCoordS = vertexData[offs++];
						float texCoordT = vertexData[offs++];

						float lightmapCoordS = lightmapData.pagePosX + (texCoordS - MathF.Floor( minTexCoordS )) / 16;
						float lightmapCoordT = lightmapData.pagePosY + (texCoordT - MathF.Floor( minTexCoordT )) / 16;
						vertexData[offs++] = lightmapCoordS;
						vertexData[offs++] = lightmapCoordT;

						lightmapList.Add( new Vector2( lightmapCoordS, lightmapCoordT ) );
					}
					indexList = new();
					int indexCount = Util.TopologyHelper.GetTriangleIndexCountForTopologyIndexCount( Util.GfxTopology.TriFans, numedges );
					Util.TopologyHelper.ConvertToTrianglesRange( ref indexData, dstOffsIndex, Util.GfxTopology.TriFans, dstIndexBase, numedges );

					Surface surface = mergeSurface;

					//if ( surface == null )
					//{
					surface = new Surface { texName = face.texName, startIndex = dstOffsIndex, indexCount = 0, lightmapData = new List<SurfaceLightmapData>() };
					surfaces.Add( surface );
					//}

					/////////////////////////////////////////////////////////////////////////////////////////////////
					//**************************************** For s&box ********************************************
					/////////////////////////////////////////////////////////////////////////////////////////////////

					// Can be removed, because we do not have any goldsrc skybox shader for now..
					if ( face.texName == "sky" )
						continue;

					var dstOffsVertex_temp = dstOffsVertexBase;
					for ( int j = 0; j < numedges; j++ )
					{
						//For s&box
						var found = MapParser.Render.TextureCache.textureData.TryGetValue( face.texName, out var infoFromTextureData );

						var vec = new Vector3( vertexData[dstOffsVertex_temp++], vertexData[dstOffsVertex_temp++], vertexData[dstOffsVertex_temp++] );
						var texcoordS = vertexData[dstOffsVertex_temp++];
						var texcoordT = vertexData[dstOffsVertex_temp++];
						dstOffsVertex_temp++; //LightmapTexCoordS
						dstOffsVertex_temp++; //LightmapTexCoordT

						//Vector3.One, Vector3.Left
						vertexList.Add( new SimpleVertex( vec, Vector3.One, Vector3.Right, new Vector2( texcoordS / (found ? infoFromTextureData.width : 1), texcoordT / (found ? infoFromTextureData.height : 1) ) ) );
					}

					//For sbox to read indexData for TriFans

					var dstoffset_temp = dstOffsIndex;
					var dst_temp = 0;
					for ( var il = 0; il < numedges - 2; il++ )
					{
						// Substract, because baseVertex must be 0
						indexList.Insert( dst_temp++, indexData[dstoffset_temp++] - dstIndexBase );
						indexList.Insert( dst_temp++, indexData[dstoffset_temp++] - dstIndexBase );
						indexList.Insert( dst_temp++, indexData[dstoffset_temp++] - dstIndexBase );
					}

					// For sbox to normalizing surfaces, idk is it working correctly?
					/*for ( var ij = 0; ij < indexList.Count(); ij += 3 )
					{
						var a = vertexList[indexList[ij + 0]];
						var b = vertexList[indexList[ij + 1]];
						var c = vertexList[indexList[ij + 2]];

						var surfaceNormal = Vector3.Cross( c.position - b.position, a.position - b.position ).Normal;

						a.normal += surfaceNormal;
						b.normal += surfaceNormal;
						c.normal += surfaceNormal;

						vertexList[indexList[ij + 0]] = a;
						vertexList[indexList[ij + 1]] = b;
						vertexList[indexList[ij + 2]] = c;
					}
					for ( var iv = 0; iv < vertexList.Count(); iv++ )
					{
						var val = vertexList[iv];
						val.normal = val.normal.Normal;
						vertexList[iv] = val;
					}*/

					indexList.Reverse(); // Fix mesh backside problem

					//int id = Game.Random.Int( 100000 );
					if ( Game.IsClient )
						Render.MaterialCache.CreateMaterial( face.texName ); //, ref lightmapData, id

					meshDataList.Add( new meshData() { vertices = vertexList, indices = indexList, textureName = face.texName } ); //, id=id

					//////////////////////////////////////////////////////////////////////////////////////////////////////
					//////////////////////////////////////////////////////////////////////////////////////////////////////

					surface.lightmapData.Add( lightmapData );
					surface.indexCount += indexCount;
					dstOffsIndex += indexCount;
					dstIndexBase += numedges;
				}
			}

			entities = EntityParser.parseEntities( GetLumpData( LumpType.ENTITIES ) );

			// Not required
			//this.vertexData = vertexData;
			//this.indexData = indexData;

			// For async loading, but disabled
			/*if ( Game.IsClient )
			{
				// Extra texture data from BSP
				for ( var i = 0; i < extraTexData.Count(); i++ )
				{
					var dataextra = extraTexData[i];
					TextureCache.addTexture( dataextra, wadname: "FromBSP" );

					// We clear manually all materials, remove comment,
					// If there is a conflict BSP version tex and wad version.

					//var name = MIPTEXData.GetMipTexName( dataextra );
					//MaterialCache.clearMaterial( matname: name );
				}
			}*/
		}

		public byte[] GetLumpData( LumpType lumpType )
		{
			const int lumpsStart = 0x04;
			int idx = lumpsStart + (int)lumpType * 0x08;
			int offs = BitConverter.ToInt32( buffer, idx + 0x00 );
			int size = BitConverter.ToInt32( buffer, idx + 0x04 );

			return buffer.Skip( offs ).Take( size ).ToArray();
		}

		public class LightmapPackerPage
		{
			public ushort[] skyline;

			public int width = 0;
			public int height = 0;
			public int maxWidth = 0;
			public int maxHeight = 0;

			public LightmapPackerPage( int MaxWidth, int MaxHeight )
			{
				// Initialize our skyline. Note that our skyline goes horizontal, not vertical.

				if ( !(maxWidth <= 0xFFFF) )
					Notify.Create( "Error lightmappackerpage!", Notify.NotifyType.Error );

				skyline = new ushort[MaxHeight];
				maxWidth = MaxWidth;
				maxHeight = MaxHeight;
			}

			public bool allocate( ref SurfaceLightmapData allocation ) // was LightmapAlloc
			{
				int w = allocation.width, h = allocation.height;

				// March downwards until we find a span of skyline that will fit.
				int bestY = -1, minX = maxWidth - w + 1;
				for ( int y = 0; y < maxHeight - h; )
				{
					int searchY = SearchSkyline( y, h );
					if ( skyline[searchY] < minX )
					{
						minX = skyline[searchY];
						bestY = y;
					}
					y = searchY + 1;
				}

				if ( bestY < 0 )
					return false; // Could not pack.

				// Found a position!
				allocation.pagePosX = minX;
				allocation.pagePosY = bestY;
				// pageIndex filled in by caller.

				// Update our skyline.
				for ( int y = bestY; y < bestY + h; y++ )
					skyline[y] = (ushort)(minX + w);

				// Update our bounds.
				width = Math.Max( width, minX + w );
				height = Math.Max( height, bestY + h );

				return true;
			}

			private int SearchSkyline( int startY, int h )
			{
				int winnerY = -1, maxX = -1;
				for ( int y = startY; y < startY + h; y++ )
				{
					if ( this.skyline[y] >= maxX )
					{
						winnerY = y;
						maxX = this.skyline[y];
					}
				}
				return winnerY;
			}
		}

		/*public struct LightmapAlloc
		{
			public int width { get; set; }
			public int height { get; set; }
			public int pagePosX { get; set; }
			public int pagePosY { get; set; }
		}

		public class LightmapPacker
		{
			public List<LightmapPackerPage> pages = new List<LightmapPackerPage>();

			public int pageWidth { get; }
			public int pageHeight { get; }

			public LightmapPacker( int pageWidth = 2048, int pageHeight = 2048 )
			{
				this.pageWidth = pageWidth;
				this.pageHeight = pageHeight;
			}

			public void allocate( SurfaceLightmapData allocation )
			{
				for ( int i = 0; i < pages.Count; i++ )
				{

					if ( pages[i].allocate( ref allocation  ) )
					{
						allocation.pageIndex = i;
						return;
					}
				}

				// Make a new page.
				var page = new LightmapPackerPage( this.pageWidth, this.pageHeight );
				this.pages.Add( page );
				//assert( page.allocate( allocation ) );// ilerde

				page.allocate( ref allocation );
				//System.Diagnostics.Debug.Assert( page.allocate( allocation ) );
				allocation.pageIndex = this.pages.Count - 1;
			}
		}*/
	}
}
