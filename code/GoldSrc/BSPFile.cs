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

		public enum leafContents
		{
			CONTENTS_EMPTY = -1,
			CONTENTS_SOLID = -2,
			CONTENTS_WATER = -3,
			CONTENTS_SLIME = -4,
			CONTENTS_LAVA = -5,
			CONTENTS_SKY = -6,
			CONTENTS_ORIGIN = -7,
			CONTENTS_CLIP = -8,
			CONTENTS_CURRENT_0 = -9,
			CONTENTS_CURRENT_90 = -10,
			CONTENTS_CURRENT_180 = -11,
			CONTENTS_CURRENT_270 = -12,
			CONTENTS_CURRENT_UP = -13,
			CONTENTS_CURRENT_DOWN = -14,
			CONTENTS_TRANSLUCENT = -15
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

		public struct Leaf
		{
			public int nVisOffset { get; set; }
			public BBox BBox { get; set; }
			public List<int> faceIndex { get; set; }
			public List<bool> visLeaves { get; set; }

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
			public List<Vertex> vertices { get; set; }
			public List<int> indices { get; set; }
			public string textureName { get; set; }
			public int faceIndex { get; set; }
		}

		public struct entityMeshData
		{
			public List<Vertex> vertices { get; set; }
			public List<int> indices { get; set; }
			public string textureName { get; set; }
			public int faceIndex { get; set; }
			public EntityParser.EntityData? entity { get; set; }
			public Vector3 nMins { get; set; }
			public Vector3 nMaxs { get; set; }
			public Vector3 vOrigin { get; set; }
		}

		public int version { get; set; }
		public int[] indexData { get; set; }
		public double[] vertexData { get; set; }
		public List<Surface> surfaces { get; set; } = new();
		public List<EntityParser.EntityData> entities { get; set; }
		public List<byte[]> extraTexData = new List<byte[]>();
		public LightmapPackerPage lightmapPackerPage { get; set; } = new LightmapPackerPage( 2048, 2048 );
		public Texture lightmap { get; set; }
		public byte[] buffer { get; set; }
		public string? skyname = null;
		public List<string>? WADList = null;
		public List<Leaf> leaves { get; set; } = new();
		public List<int> entityMeshStartIndex { get; set; } = new();

		// For sbox
		public List<meshData> meshDataList = new();
		public List<entityMeshData> entityMeshDataList = new();
		//public List<string> textureList = new();

		// In order to stop rendering and creating its physics. (classname, Client, Server)
		public static Dictionary<string, (bool, bool)> blacklistEnts = new() { 
			{ "func_bomb_target", (true, true) },
			{ "func_buyzone", (true, true) },
			{ "cycler_sprite", (false, true) },
			{ "func_illusionary", (false, true) },
			{ "trigger_", (true, true) },
			{ "func_water", (false, true) },
			{ "func_ladder", (true, true) },
		};

		public static byte[] DecodeRLE( byte[] rleData )
		{
			List<byte> decodedData = new List<byte>();

			for ( int i = 0; i < rleData.Length; i++ )
			{
				byte currentByte = rleData[i];
				int repeatCount = currentByte & 0x3F;
				byte repeatedByte = (byte)(currentByte >> 6);
				if ( repeatCount == 0 )
				{
					repeatCount = rleData[++i];
					repeatedByte = rleData[++i];
				}

				for ( int j = 0; j < repeatCount; j++ )
				{
					decodedData.Add( repeatedByte );
				}
			}

			return decodedData.ToArray();
		}
		public BSPFile( ref SpawnParameter settings )
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

			PreparingIndicator.Update();

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
					_ = TextureCache.addTextureWithMIPTEXData( dataextra, wadname: "FromBSP" );
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

			// Not working same as typescript version, do not use, breaking..
			//faces.Sort( ( a, b ) => b.texName.CompareTo( a.texName ) );

			double[] vertexData = new double[numVertexData * 7];
			int dstOffsVertex = 0;

			int[] indexData = new int[numIndexData];

			int dstOffsIndex = 0;
			int dstIndexBase = 0;

			// Build surface meshes
			byte[] vertexesData = GetLumpData( LumpType.VERTEXES );
			double[] vertexes;
			using ( MemoryStream stream = new MemoryStream( vertexesData ) )
			using ( BinaryReader reader = new BinaryReader( stream ) )
			{
				vertexes = new double[vertexesData.Length / sizeof( float )];
				for ( int i = 0; i < vertexes.Length; i++ )
					vertexes[i] = reader.ReadSingle();
			}


			List<(int, int, int,Vector3, Vector3, Vector3)> models = new(); // modelIndex, iFirstFace, nFaces, nMins, nMaxs, vOrigin

			var modelsLumpData = GetLumpData( LumpType.MODELS );
			using ( var streamModel = new MemoryStream( modelsLumpData ) )
			using ( var readerModel = new BinaryReader( streamModel ) )
			{
				for ( var mi = 1; mi < modelsLumpData.Length / 0x40; mi++ ) // 0 is world?
				{
					streamModel.Seek( mi * 0x40, SeekOrigin.Begin );

					var nMins = new Vector3( readerModel.ReadSingle(), readerModel.ReadSingle(), readerModel.ReadSingle() );
					var nMaxs = new Vector3( readerModel.ReadSingle(), readerModel.ReadSingle(), readerModel.ReadSingle() );

					var vOrigin = new Vector3( readerModel.ReadSingle(), readerModel.ReadSingle(), readerModel.ReadSingle() );

					int[] iHeadnodes = new int[4];
					iHeadnodes[0] = readerModel.ReadInt32();
					iHeadnodes[1] = readerModel.ReadInt32();
					iHeadnodes[2] = readerModel.ReadInt32();
					iHeadnodes[3] = readerModel.ReadInt32();

					var nVisLeafs = readerModel.ReadInt32(); // Giving only -6 ?

					var iFirstFace = readerModel.ReadInt32();
					var nFaces = readerModel.ReadInt32();

					models.Add( (mi, iFirstFace, nFaces, nMins, nMaxs, vOrigin) );
				}
			}

			entities = EntityParser.parseEntities( GetLumpData( LumpType.ENTITIES ) );
			var entitiesCount = entities.Count;

			PreparingIndicator.Update();

			var VIS = GetLumpData( LumpType.VISIBILITY );

			// In the marksurfaces lump, it have the indexes of the model's mesh, but there is no leaf to connected the models mesh for pvs.
			// the leaves of nVisOffset's values is -1 which are not included as a vis leaf. So, I implemented the models faces another way for pvs

			var markSurfaceLumpData = GetLumpData( LumpType.MARKSURFACES );
			ushort[] markSurfaces = new ushort[markSurfaceLumpData.Length / 0x02];
			using ( var streamMarkSurfaces = new MemoryStream( markSurfaceLumpData ) )
			using ( var readerMarkSurface = new BinaryReader( streamMarkSurfaces ) )
				for ( var msi = 0; msi < markSurfaceLumpData.Length / 0x02; msi++ )
					markSurfaces[msi] = readerMarkSurface.ReadUInt16();

			var leavesLumpData = GetLumpData( LumpType.LEAFS );
			using ( var streamLeaves = new MemoryStream( leavesLumpData ) )
			using ( var readerLeaves = new BinaryReader( streamLeaves ) )
			{
				for ( var li = 0; li < leavesLumpData.Length / 0x1C; li++ )
				{
					streamLeaves.Seek( li * 0x1C, SeekOrigin.Begin );

					var nContents = readerLeaves.ReadInt32();
					var nVisOffset = readerLeaves.ReadInt32();

					var nMins = new Vector3( readerLeaves.ReadInt16(), readerLeaves.ReadInt16(), readerLeaves.ReadInt16() );
					var nMaxs = new Vector3( readerLeaves.ReadInt16(), readerLeaves.ReadInt16(), readerLeaves.ReadInt16() );
					
					var iFirstMarkSurface = readerLeaves.ReadUInt16();
					var nMarkSurfaces = readerLeaves.ReadUInt16();

					byte[] nAmbientLevels = new byte[4];
					nAmbientLevels[0] = readerLeaves.ReadByte();
					nAmbientLevels[1] = readerLeaves.ReadByte();
					nAmbientLevels[2] = readerLeaves.ReadByte();
					nAmbientLevels[3] = readerLeaves.ReadByte();

					if ( nVisOffset != -1 && !(li == 0 && nContents == -2) ) // !(li == 0 && nContents == -2)  Need correction for i==0 => CONTENTS_SOLID 0 - 0 0 0 - 0 0 0 -  0 0 - 0 0 0 0 
					{
						int[] marksurfacesForLeaf = new int[nMarkSurfaces];

						for ( var nMark = 0; nMark < nMarkSurfaces; nMark++ )
							marksurfacesForLeaf[nMark] = markSurfaces[iFirstMarkSurface + nMark];

						leaves.Add( new()
						{
							nVisOffset = nVisOffset,
							BBox = new BBox( nMins + settings.position, nMaxs + settings.position ),
							faceIndex = marksurfacesForLeaf.ToList(),
							visLeaves = new()
						} );
					}
				}
			}

			for ( int i = 0; i < leaves.Count; i++ )
			{
				var value = leaves[i];
				value.visLeaves = new bool[leaves.Count].ToList();
				leaves[i] = value;
			}

			for ( int i = 0; i < leaves.Count; i++ )
			{
				PreparingIndicator.Update();

				//var str = i + "--> ";
				//var onceki = 0;
				var leaf = leaves[i];

				//if ( visibilityArrayIndex == 0 )
				//	continue;

				List<byte> decodedVIS = new();
				for ( int jj = leaf.nVisOffset; jj < (leaves.Count() + leaf.nVisOffset); jj++ )
				{
					//Log.Info( jj + " " + (leaves.Count + leaf.nVisOffset )+ " "+ VIS.Count()  + " "+ decodedVIS.Count());

					//if ( decodedVIS.Count*8 >= leaves.Count() )
					//	break;

					if ( jj >= VIS.Count() ) // || jj == -1 ( for -1 leaves )
					{
						decodedVIS.Add( 0x00 );
						continue;
					}
					if ( VIS[jj] == 0x00 )
					{
						for ( var ibyte = 0; ibyte < VIS[jj + 1]; ibyte++ )
							decodedVIS.Add( 0x00 );
						jj++;
					}
					else
						decodedVIS.Add( VIS[jj] );
				}

				for ( int j = 0; j < leaves.Count; j++ )
				{
					//if ( j == 0 )
					//	continue;

					var index =  (j / 8);//leaf.nVisOffset +

					//if ( index >= VIS.Count() )
					//	continue;

					byte visibilityByte = decodedVIS[index];

					/*if ( visibilityByte == 0x00 )
					{
						str += j + ": " + "false" + ", " + ((onceki != ((j + 1) / 8)) ? " --- " : "");
						onceki = ((j + 1) / 8);
						//int runLength = VIS[index + 1];
						j += (8 * 2)-1;
						continue;
					}*/

					int bit = j % 8;
					int mask = 1 << bit;
					leaf.visLeaves[j] = ((visibilityByte & mask) != 0);

					//str += j + ": " + ((visibilityByte & mask) != 0) + ", " + ((onceki != ((j + 1) / 8)) ? " --- " : "");
					//onceki = ((j + 1) / 8);
				}

				leaves[i] = leaf;
				//Log.Info( str );
			}

			/*int offset = 0;
			for ( int c = 0; c < leaves.Count; )
			{
				var leaf = leaves[c];
				bool[] leaves_visibility = new bool[leaves.Count];
				if ( VIS[offset] == 0 )
				{
					offset++;
					c += 8 * VIS[offset];
					offset++;
				}
				else
				{
					for ( int bit = 1; bit != 0 && c < leaves.Count; bit *= 2, c++ )
					{
						if ( (VIS[offset] & (byte)bit) != 0 )
						{
							leaves_visibility[c] = true;
						}
					}
					offset++;
				}
				leaf.visLeaves = leaves_visibility.ToList();
			}*/

			// Find wads and skyname from BSP file
			foreach ( var ent in entities )
			{
				if( ent.classname == "worldspawn")
				{
					if ( ent.data.TryGetValue( "skyname", out string _skyname ) )
						skyname = _skyname;

					if ( ent.data.TryGetValue( "wad", out string wadList ) && wadList.Length > 0 )
					{
						WADList = new();

						foreach (var line in wadList.Split(";"))
							if( !string.IsNullOrEmpty( line ) && !string.IsNullOrWhiteSpace( line ) )
								WADList.Add( $"{Util.PathToMapName( line )}.wad" );
					}

					break;
				}
			}

			if( WADList is not null)
				WADList = WADList.Distinct().ToList();

			// For sbox
			List<Vertex> vertexList = new();
			List<Vector2> lightmapList = new();
			List<int> indexList = new();

			byte[] lighting = GetLumpData( LumpType.LIGHTING );
			for ( int i = 0; i < faces.Count; i++ )
			{
				PreparingIndicator.Update();

				vertexList = new();
				lightmapList = new();

				Face face = faces[i];
				int idx = face.index * 0x14;

				EntityParser.EntityData? entity = null;
				(int, int, int, Vector3, Vector3, Vector3)? model = null;

				// Find faces if is model.
				foreach ( var _model in models )
				{
					if( i >= _model.Item2 && i <= (_model.Item2 + _model.Item3) ) 
					{
						model = _model;
						break;
					}
				}

				// If this mesh is model, get its entity data (its keyvalues)
				if ( model is not null &&  entitiesCount >= model.Value.Item1 )
				{
					foreach ( var ent in entities )
					{
						if( ent.data.TryGetValue( "model", out var modelname ) && (modelname.Length > 0) && (modelname[0] == '*') && int.Parse( modelname.Replace( "*", "" ) ) == model.Value.Item1 )
						{
							entity = ent;
							break;
						}
					}
				}

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
						stream.Seek( idx + 0x0C + j, SeekOrigin.Begin );
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

					//Surface? mergeSurface = null;
					bool mergeSurface = false;
					
					if ( i > 0 )
					{
						Face prevFace = faces[i - 1];
						bool canMerge = true;

						if ( face.texName != prevFace.texName )
							canMerge = false;

						//if ( canMerge )
						//	mergeSurface = surfaces.LastOrDefault();

						mergeSurface = canMerge;

						// TODO(jstpierre)
					}

					TexinfoMapping m = texinfoa[face.texinfo].textureMapping;
					double minTexCoordS = double.PositiveInfinity, minTexCoordT = double.PositiveInfinity;
					double maxTexCoordS = double.NegativeInfinity, maxTexCoordT = double.NegativeInfinity;

					int dstOffsVertexBase = dstOffsVertex;

					for ( int j = 0; j < numedges; j++ )
					{
						int vertIndex = vertindices[firstedge + j];
						double px = vertexes[vertIndex * 3 + 0];
						double py = vertexes[vertIndex * 3 + 1];
						double pz = vertexes[vertIndex * 3 + 2];

						double texCoordS = Math.Round( px * m.s.x + py * m.s.y + pz * m.s.z + m.s.w);
						double texCoordT = Math.Round( px * m.t.x + py * m.t.y + pz * m.t.z + m.t.w);

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

					var lightmapScale = 1f / 16f;
					int surfaceW = (int)Math.Ceiling( maxTexCoordS * lightmapScale ) - (int)Math.Floor( minTexCoordS * lightmapScale ) + 1;
					int surfaceH = (int)Math.Ceiling( maxTexCoordT * lightmapScale ) - (int)Math.Floor( minTexCoordT * lightmapScale ) + 1;
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

						double texCoordS = vertexData[offs++];
						double texCoordT = vertexData[offs++];

						double lightmapCoordS = (texCoordS * lightmapScale) - Math.Floor( minTexCoordS * lightmapScale ) + 0.5;
						double lightmapCoordT = (texCoordT * lightmapScale) - Math.Floor( minTexCoordT * lightmapScale ) + 0.5;
						vertexData[offs++] = lightmapData.pagePosX + lightmapCoordS;
						vertexData[offs++] = lightmapData.pagePosY + lightmapCoordT;

						lightmapList.Add( new Vector2( (float)lightmapCoordS, (float)lightmapCoordT ) );
					}
					indexList = new();
					int indexCount = Util.TopologyHelper.GetTriangleIndexCountForTopologyIndexCount( Util.GfxTopology.TriFans, numedges );
					Util.TopologyHelper.ConvertToTrianglesRange( ref indexData, dstOffsIndex, Util.GfxTopology.TriFans, dstIndexBase, numedges );

					Surface surface;

					if ( !mergeSurface )
					{
						surface = new Surface { texName = face.texName, startIndex = dstOffsIndex, indexCount = indexCount, lightmapData = new List<SurfaceLightmapData>() { lightmapData } };
						surfaces.Add( surface );
					}
					else
					{
						Surface surfacedata = surfaces[surfaces.Count - 1];
						surfacedata.lightmapData.Add( lightmapData );
						surfacedata.indexCount += indexCount;
						surfaces[surfaces.Count - 1] = surfacedata;
					}

					/////////////////////////////////////////////////////////////////////////////////////////////////
					//**************************************** For s&box ********************************************
					/////////////////////////////////////////////////////////////////////////////////////////////////
	
					var dstOffsVertex_temp = dstOffsVertexBase;
					for ( int j = 0; j < numedges; j++ )
					{
						//For s&box
						var found = Render.TextureCache.textureData.TryGetValue( face.texName, out var infoFromTextureData );

						var vec = new Vector3( (float)vertexData[dstOffsVertex_temp++], (float)vertexData[dstOffsVertex_temp++], (float)vertexData[dstOffsVertex_temp++] );
						var texcoordS = vertexData[dstOffsVertex_temp++];
						var texcoordT = vertexData[dstOffsVertex_temp++];
						var lightmapCoordS = vertexData[dstOffsVertex_temp++];
						var lightmapCoordT = vertexData[dstOffsVertex_temp++];

						//Vector3.One, Vector3.Left
						vertexList.Add( new Vertex( vec, Vector3.Zero, Vector3.Left, new Vector4( (float)texcoordS, (float)texcoordT, (float)lightmapCoordS, (float)lightmapCoordT ) ) );
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

					// Checking trigger or solid entities in order to find out to remove its physics, that is not good way, might me removed in the future
					// I need to seperate the model faces from map faces and create this models as an entities, by iFirstFace and nFaces, it is possible. I implemented already below.
					var pass = true;

					if ( entity is not null && blacklistEnts.TryGetValue( entity.Value.classname.Contains("trigger_") ? "trigger_": entity?.classname, out var blacklisted ) && ((blacklisted.Item1 && Game.IsClient) || (blacklisted.Item2 && Game.IsServer)) )
						pass = false;

					if ( Game.IsClient && face.texName == "sky" )
						pass = false;

					if ( pass )
					{
						//textureList.Add( face.texName );

						if ( entity is null )
							meshDataList.Add( new meshData() { vertices = vertexList, indices = indexList, faceIndex = i, textureName = face.texName } );
						else
							entityMeshDataList.Add( new entityMeshData() { vertices = vertexList, indices = indexList, faceIndex = i, textureName = face.texName, entity = entity, nMins = model is not null ? model.Value.Item4 : Vector3.Zero , nMaxs = model is not null ? model.Value.Item5 : Vector3.Zero, vOrigin = model is not null ? model.Value.Item6 : Vector3.Zero } );
					}

					//////////////////////////////////////////////////////////////////////////////////////////////////////
					//////////////////////////////////////////////////////////////////////////////////////////////////////
					
					dstOffsIndex += indexCount;
					dstIndexBase += numedges;
				}
			}

			//Log.Info( Encoding.ASCII.GetString( GetLumpData( LumpType.ENTITIES ) ));
			
			// Not required
			this.vertexData = vertexData;
			this.indexData = indexData;

			//textureList = textureList.Distinct().ToList();

			if (Game.IsClient)
			{ 
				List<SurfaceLightmapData> list = new();
				for ( var i = 0; i < surfaces.Count(); i++ )
				{
					var surface = surfaces[i];

					for ( var j = 0; j < surface.lightmapData.Count; j++ )
						list.Add(surface.lightmapData[j] );
				}
				var package = lightmapPackerPage;

				//Log.Info( "LightMap " + settings.mapName );

				PreparingIndicator.Update();
				lightmap = MIPTEXData.createLightmap( ref package, ref list, Util.PathToMapName( settings.mapName ));
			}
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
