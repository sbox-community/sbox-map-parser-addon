// sbox.Community � 2023-2024

using Sandbox;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

		/*public enum leafContents
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
		};*/

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
		//public int[] indexData { get; set; }
		//public double[] vertexData { get; set; }
		public List<Surface> surfaces { get; set; } = new();
		public List<EntityParser.EntityData> entities { get; set; }
		public LightmapPackerPage lightmapPackerPage { get; set; } = new LightmapPackerPage( 2048, 2048 );
		public Texture lightmap { get; set; }
		public byte[] buffer { get; set; }
		public string? skyname = null;
		public List<string>? WADList = null;
		public List<Leaf> leaves { get; set; } = new();
		//public List<int> entityMeshStartIndex { get; set; } = new();

		// For sbox
		public List<meshData> meshDataList = new();
		public List<entityMeshData> entityMeshDataList = new();
		//public List<string> textureList = new();

		// In order to stop rendering and creating its physics. (classname, Client, Server)
		public static Dictionary<string, (bool, bool)> blacklistEnts = new() {
			{ "func_bomb_target", (true, true) },
			{ "func_buyzone", (true, true) },
			{ "cycler_sprite", (false, true) },
			//{ "func_illusionary", (false, true) },
			{ "trigger_", (true, true) },
			{ "func_water", (false, true) },
			{ "func_ladder", (true, true) },
			{ "func_rotating", (false, true) },
		};

		/*public static byte[] DecodeRLE( byte[] rleData )
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
		}*/

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

			version = BitConverter.ToInt32( buffer, 0x00 );

			// I didn't got any version problem, if there, we can expand compatibility
			if ( version != 30 )
				Notify.Create( $"Version is not 30", Notify.NotifyType.Error );

			PreparingIndicator.Update( "Map" );

			// Parse miptex and texture names
			var textures = GetLumpData( LumpType.TEXTURES );
			int nummiptex = BitConverter.ToInt32( textures, 0x00 );
			string[] textureNames = new string[nummiptex];
			
			using ( var reader = new BinaryReader( new MemoryStream( textures ) ) )
			{
				for ( int i = 0; i < nummiptex; i++ )
				{
					reader.BaseStream.Position = 0x04 + i * 0x04;
					int miptexOffs = reader.ReadInt32();
					var texName = Util.ReadString( ref textures, miptexOffs + 0x00, 0x10, true );

					if ( Game.IsClient )
					{
						PreparingIndicator.Update( "Texture" );
						reader.BaseStream.Position = miptexOffs + 0x18;
						int hasTextureData = reader.ReadInt32();

						if ( hasTextureData != 0 && miptexOffs != -1 ) // verify: miptexOffs != -1 (fy_cbble)
						{
							//var mapname = settings.mapName;
							//_ = TextureCache.LoadTexturesFromBSP (async () => {
							//	await Task.Yield();
								// Must be loaded after spawning of map for async
								// Extra texture data from BSP
								var extraTexData = new byte[textures.Length - miptexOffs];
								Array.Copy( textures, miptexOffs, extraTexData, 0, textures.Length - miptexOffs );
								_ = TextureCache.addTextureWithMIPTEXData( extraTexData, wadname: Util.PathToMapNameWithExtension( settings.mapName ) );
							//	await Task.Yield();

							//} );
						}
					}

					textureNames[i] = texName;
				}
			}

			var texinfoData = GetLumpData( LumpType.TEXINFO );
			int texinfoCount = texinfoData.Length / 0x28;
			var texinfoa = new Texinfo[texinfoCount];

			for ( int i = 0; i < texinfoCount; i++ )
			{
				int infoOffs = i * 0x28;

				if ( Game.IsClient )
				{
					var textureMappingS = Util.ReadVec4( ref texinfoData, infoOffs + 0x00 );
					var textureMappingT = Util.ReadVec4( ref texinfoData, infoOffs + 0x10 );
					var textureMapping = new TexinfoMapping { s = textureMappingS, t = textureMappingT };
					int miptex = BitConverter.ToInt32( texinfoData, infoOffs + 0x20 );
					int flags = BitConverter.ToInt32( texinfoData, infoOffs + 0x24 );

					texinfoa[i] = new Texinfo { textureMapping = textureMapping, miptex = miptex, flags = flags };
				}
				else
				{
					int miptex = BitConverter.ToInt32( texinfoData, infoOffs + 0x20 );
					texinfoa[i] = new Texinfo { miptex = miptex };
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

			int numVertexData = 0, numIndexData = 0;

			Face[] faces = new Face[facelist.Length / 0x14];

			for ( int i = 0; i < facelist.Length / 0x14; i++ )
			{
				int numedges = BitConverter.ToUInt16( facelist, i * 0x14 + 0x08 );
				int texinfo = BitConverter.ToUInt16( facelist, i * 0x14 + 0x0A );

				string texName = textureNames[texinfoa[texinfo].miptex];
				faces[i] = new Face { index = i, texinfo = texinfo, texName = texName };

				numVertexData += numedges;
				numIndexData += Util.TopologyHelper.GetTriangleIndexCountForTopologyIndexCount( Util.GfxTopology.TriFans, ref numedges );
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
			double[] vertexes = new double[vertexesData.Length / sizeof( float )];
			for ( int i = 0; i < vertexes.Length; i++ )
				vertexes[i] = BitConverter.ToSingle( vertexesData, i * 0x04 );

			var modelsLumpData = GetLumpData( LumpType.MODELS );

			(int, int, int, Vector3, Vector3, Vector3)[] models = new (int, int, int, Vector3, Vector3, Vector3)[modelsLumpData.Length / 0x40]; // modelIndex, iFirstFace, nFaces, nMins, nMaxs, vOrigin

			for ( var mi = 0x01; mi < modelsLumpData.Length / 0x40; mi++ )
			{
				var offset = mi * 0x40;

				var nMins = new Vector3(
					BitConverter.ToSingle( modelsLumpData, offset ),
					BitConverter.ToSingle( modelsLumpData, offset + 0x04 ),
					BitConverter.ToSingle( modelsLumpData, offset + 0x08 )
				);
				var nMaxs = new Vector3(
					BitConverter.ToSingle( modelsLumpData, offset + 0x0C ),
					BitConverter.ToSingle( modelsLumpData, offset + 0x10 ),
					BitConverter.ToSingle( modelsLumpData, offset + 0x14 )
				);

				var vOrigin = new Vector3(
					BitConverter.ToSingle( modelsLumpData, offset + 0x18 ),
					BitConverter.ToSingle( modelsLumpData, offset + 0x1C ),
					BitConverter.ToSingle( modelsLumpData, offset + 0x20 )
				);

				int[] iHeadnodes = new int[4];
				iHeadnodes[0] = BitConverter.ToInt32( modelsLumpData, offset + 0x24 );
				iHeadnodes[1] = BitConverter.ToInt32( modelsLumpData, offset + 0x28 );
				iHeadnodes[2] = BitConverter.ToInt32( modelsLumpData, offset + 0x2C );
				iHeadnodes[3] = BitConverter.ToInt32( modelsLumpData, offset + 0x30 );

				//var nVisLeafs = BitConverter.ToUInt32( modelsLumpData, offset + 0x34 );

				var iFirstFace = BitConverter.ToInt32( modelsLumpData, offset + 0x38 );
				var nFaces = BitConverter.ToInt32( modelsLumpData, offset + 0x3C );

				models[mi] = (mi, iFirstFace, nFaces, nMins, nMaxs, vOrigin);
			}

			entities = EntityParser.parseEntities( GetLumpData( LumpType.ENTITIES ) );
			var entitiesCount = entities.Count;

			PreparingIndicator.Update( "Map" );

			if ( Game.IsClient )
			{
				PreparingIndicator.Update( "VisLeaves" );

				var VIS = GetLumpData( LumpType.VISIBILITY );

				// In the marksurfaces lump, it have the indexes of the model's mesh, but there is no leaf to connected the models mesh for pvs.
				// the leaves of nVisOffset's values is -1 which are not included as a vis leaf. So, I implemented the models faces another way for pvs

				var markSurfaceLumpData = GetLumpData( LumpType.MARKSURFACES );
				ushort[] markSurfaces = new ushort[markSurfaceLumpData.Length / 0x02];
				for ( var msi = 0; msi < markSurfaceLumpData.Length / 0x02; msi++ )
					markSurfaces[msi] = BitConverter.ToUInt16( markSurfaceLumpData, msi * 0x02 );

				var leavesLumpData = GetLumpData( LumpType.LEAFS );

				PreparingIndicator.Update( "VisLeaves" );

				for ( var li = 0; li < leavesLumpData.Length / 0x1C; li++ )
				{
					var offset = li * 0x1C;

					var nContents = BitConverter.ToInt32( leavesLumpData, offset );
					var nVisOffset = BitConverter.ToInt32( leavesLumpData, offset + 0x04 );

					var nMins = new Vector3( BitConverter.ToInt16( leavesLumpData, offset + 0x08 ),
											 BitConverter.ToInt16( leavesLumpData, offset + 0x0A ),
											 BitConverter.ToInt16( leavesLumpData, offset + 0x0C ) );
					var nMaxs = new Vector3( BitConverter.ToInt16( leavesLumpData, offset + 0x0E ),
											 BitConverter.ToInt16( leavesLumpData, offset + 0x10 ),
											 BitConverter.ToInt16( leavesLumpData, offset + 0x12 ) );

					var iFirstMarkSurface = BitConverter.ToUInt16( leavesLumpData, offset + 0x14 );
					var nMarkSurfaces = BitConverter.ToUInt16( leavesLumpData, offset + 0x16 );

					byte[] nAmbientLevels = new byte[4];
					nAmbientLevels[0] = leavesLumpData[offset + 0x18];
					nAmbientLevels[1] = leavesLumpData[offset + 0x19];
					nAmbientLevels[2] = leavesLumpData[offset + 0x1A];
					nAmbientLevels[3] = leavesLumpData[offset + 0x1B];

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

				PreparingIndicator.Update( "VisLeaves" );

				for ( int i = 0; i < leaves.Count; i++ )
				{
					var value = leaves[i];
					value.visLeaves = new bool[leaves.Count].ToList();
					leaves[i] = value;
				}

				PreparingIndicator.Update( "VisLeaves" );

				for ( int i = 0; i < leaves.Count; i++ )
				{
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

						var index = (j / 8);//leaf.nVisOffset +

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

			}

			// Find wads and skyname from BSP file
			foreach ( var ent in entities )
			{
				if ( ent.classname == "worldspawn" )
				{
					if ( ent.data.TryGetValue( "skyname", out string _skyname ) )
						skyname = _skyname;

					if ( ent.data.TryGetValue( "wad", out string wadList ) && wadList.Length > 0 )
					{
						WADList = new();

						foreach ( var line in wadList.Split( ";" ) )
							if ( !string.IsNullOrEmpty( line ) && !string.IsNullOrWhiteSpace( line ) )
								WADList.Add( $"{Util.PathToMapName( line )}.wad" );
					}

					break;
				}
			}

			if ( WADList is not null )
				WADList = WADList.Distinct().ToList();

			byte[] lighting = GetLumpData( LumpType.LIGHTING );

			for ( int i = 0; i < faces.Length; i++ )
			{
				PreparingIndicator.Update( "Map" );

				Face face = faces[i];
				int idx = face.index * 0x14;

				//int planenum = BitConverter.ToUInt16( facelist, idx );
				//int side = BitConverter.ToUInt16( facelist, idx + 0x02 );
				uint firstedge = BitConverter.ToUInt32( facelist, idx + 0x04 );
				int numedges = BitConverter.ToUInt16( facelist, idx + 0x08 );

				List<Vertex> vertexList = new( numedges );
				//List<Vector2> lightmapList = new( numedges );
				List<int> indexList = new( (numedges - 2) * 3 );

				EntityParser.EntityData? entity = null;
				(int, int, int, Vector3, Vector3, Vector3)? model = null;

				// Find faces if is model.
				foreach ( var _model in models )
				{
					if ( i >= _model.Item2 && i <= (_model.Item2 + _model.Item3) )
					{
						model = _model;
						break;
					}
				}

				// If this mesh is model, get its entity data (its keyvalues)
				if ( model is not null && entitiesCount >= model.Value.Item1 )
				{
					foreach ( var ent in entities )
					{
						if ( ent.data.TryGetValue( "model", out var modelname ) && (modelname.Length > 0) && (modelname[0] == '*') && int.Parse( modelname.Replace( "*", "" ) ) == model.Value.Item1 )
						{
							entity = ent;
							break;
						}
					}
				}

				//Surface? mergeSurface = null;
				bool mergeSurface = false;

				if ( i > 0 )
				{
					Face prevFace = faces[i - 1];
					bool canMerge = true;

					if ( face.texName != prevFace.texName )
						canMerge = false;
					//else if ( models[prevFace.index] != models[face.index] ) // verify
					//	canMerge = false;

					//if ( canMerge )
					//	mergeSurface = surfaces.LastOrDefault();

					mergeSurface = canMerge;

					// TODO(jstpierre)
				}

				List<int> styles = new List<int>();
				if ( Game.IsClient )
				{
					for ( int j = 0; j < 4; j++ )
					{
						int style = facelist[idx + 0x0C + j];
						if ( style == 0xFF )
							break;
						styles.Add( style );
					}
				}

				int lightofs = BitConverter.ToInt32( facelist, idx + 0x10 );
				TexinfoMapping? m_info = Game.IsClient ? texinfoa[face.texinfo].textureMapping : null;
				double minTexCoordS = double.PositiveInfinity, minTexCoordT = double.PositiveInfinity;
				double maxTexCoordS = double.NegativeInfinity, maxTexCoordT = double.NegativeInfinity;

				int dstOffsVertexBase = dstOffsVertex;

				for ( int j = 0; j < numedges; j++ )
				{
					int vertIndex = vertindices[firstedge + j];
					double px = vertexes[vertIndex * 3 + 0];
					double py = vertexes[vertIndex * 3 + 1];
					double pz = vertexes[vertIndex * 3 + 2];

					vertexData[dstOffsVertex++] = px;
					vertexData[dstOffsVertex++] = py;
					vertexData[dstOffsVertex++] = pz;

					if ( Game.IsClient )
					{
						var m = m_info.Value;
						// There are still rounding errors..
						double texCoordS = Math.Round( px * m.s.x + py * m.s.y + pz * m.s.z + m.s.w );
						double texCoordT = Math.Round( px * m.t.x + py * m.t.y + pz * m.t.z + m.t.w );

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
					else
						dstOffsVertex += 4;
				}

				int indexCount = Util.TopologyHelper.GetTriangleIndexCountForTopologyIndexCount( Util.GfxTopology.TriFans, ref numedges );
				Util.TopologyHelper.ConvertToTrianglesRange( ref indexData, dstOffsIndex, Util.GfxTopology.TriFans, dstIndexBase, numedges );

				if ( Game.IsClient )
				{
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

						//lightmapList.Add( new Vector2( (float)lightmapCoordS, (float)lightmapCoordT ) );
					}

					Surface surface;

					if ( !mergeSurface )
					{
						//surface = new Surface { texName = face.texName, startIndex = dstOffsIndex, indexCount = indexCount, lightmapData = new List<SurfaceLightmapData>() { lightmapData } };
						surface = new Surface { lightmapData = new List<SurfaceLightmapData>() { lightmapData } };
						surfaces.Add( surface );
					}
					else
					{
						Surface surfacedata = surfaces[surfaces.Count - 1];
						surfacedata.lightmapData.Add( lightmapData );
						//surfacedata.indexCount += indexCount;
						surfaces[surfaces.Count - 1] = surfacedata;
					}
				}

				/////////////////////////////////////////////////////////////////////////////////////////////////
				//**************************************** For s&box ********************************************
				/////////////////////////////////////////////////////////////////////////////////////////////////

				var dstOffsVertex_temp = dstOffsVertexBase;
				for ( int j = 0; j < numedges; j++ )
				{
					//For s&box
					//var found = Render.TextureCache.textureData.TryGetValue( face.texName, out var infoFromTextureData );

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

				// TODO: Revise
				//if ( entity is not null && blacklistEnts.TryGetValue( entity.Value.classname.Contains( "trigger_" ) ? "trigger_": entity?.classname, out var blacklisted ) && ((Game.IsClient && (blacklisted.Item1)) || (Game.IsServer && blacklisted.Item2)) )
				if ( (face.texName.ToLower() == "aaatrigger") || (entity is not null && blacklistEnts.TryGetValue( entity.Value.classname.Contains( "trigger_" ) ? "trigger_" : entity?.classname, out var blacklisted ) && ((Game.IsClient && (blacklisted.Item1)) || (Game.IsServer && blacklisted.Item2))) )
					pass = false;

				if ( Game.IsClient && face.texName == "sky" )
					pass = false;

				if ( pass )
				{
					//textureList.Add( face.texName );

					if ( entity is null )
						meshDataList.Add( new meshData() { vertices = vertexList, indices = indexList, faceIndex = i, textureName = face.texName } );
					else
						entityMeshDataList.Add( new entityMeshData() { vertices = vertexList, indices = indexList, faceIndex = i, textureName = face.texName, entity = entity, nMins = model is not null ? model.Value.Item4 : Vector3.Zero, nMaxs = model is not null ? model.Value.Item5 : Vector3.Zero, vOrigin = model is not null ? model.Value.Item6 : Vector3.Zero } );
				}

				//////////////////////////////////////////////////////////////////////////////////////////////////////
				//////////////////////////////////////////////////////////////////////////////////////////////////////

				dstOffsIndex += indexCount;
				dstIndexBase += numedges;

			}

			//Log.Info( Encoding.ASCII.GetString( GetLumpData( LumpType.ENTITIES ) ));

			// Not required
			//this.vertexData = vertexData;
			//this.indexData = indexData;

			//textureList = textureList.Distinct().ToList();

			if ( Game.IsClient )
			{
				PreparingIndicator.Update( "Lightmap" );
				List<SurfaceLightmapData> list = new();
				for ( var i = 0; i < surfaces.Count; i++ )
				{
					var surface = surfaces[i];

					for ( var j = 0; j < surface.lightmapData.Count; j++ )
						list.Add( surface.lightmapData[j] );
				}
				var package = lightmapPackerPage;

				PreparingIndicator.Update( "Lightmap" );
				lightmap = MIPTEXData.createLightmap( ref package, ref list, Util.PathToMapName( settings.mapName ) );
			}

			// Clear unnecessary data
			this.buffer = null;
			this.surfaces = null;
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
