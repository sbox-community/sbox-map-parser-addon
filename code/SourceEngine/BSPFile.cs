// sbox.Community © 2023-2024

using Sandbox;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static MapParser.Util;

namespace MapParser.SourceEngine
{
	public class BSPFile
	{
		public enum LumpType
		{
			ENTITIES = 0,
			PLANES = 1,
			TEXDATA = 2,
			VERTEXES = 3,
			VISIBILITY = 4,
			NODES = 5,
			TEXINFO = 6,
			FACES = 7,
			LIGHTING = 8,
			LEAFS = 10,
			EDGES = 12,
			SURFEDGES = 13,
			MODELS = 14,
			WORLDLIGHTS = 15,
			LEAFFACES = 16,
			DISPINFO = 26,
			PHYSCOLLIDE = 29,
			VERTNORMALS = 30,
			VERTNORMALINDICES = 31,
			DISP_VERTS = 33,
			GAME_LUMP = 35,
			LEAFWATERDATA = 36,
			PRIMITIVES = 37,
			PRIMINDICES = 39,
			PAKFILE = 40,
			CUBEMAPS = 42,
			TEXDATA_STRING_DATA = 43,
			TEXDATA_STRING_TABLE = 44,
			OVERLAYS = 45,
			LEAF_AMBIENT_INDEX_HDR = 51,
			LEAF_AMBIENT_INDEX = 52,
			LIGHTING_HDR = 53,
			WORLDLIGHTS_HDR = 54,
			LEAF_AMBIENT_LIGHTING_HDR = 55,
			LEAF_AMBIENT_LIGHTING = 56,
			FACES_HDR = 58,
		}

		public struct SurfaceLightmapData
		{
			public int faceIndex;
			// Size of a single lightmap.
			public int width;
			public int height;
			public List<int> styles;
			public byte[] samples;
			public bool hasBumpmapSamples;
			// Dynamic allocation
			public int pageIndex;
			public int pagePosX;
			public int pagePosY;
		}

		public struct Overlay
		{
			public int[] surfaceIndexes;
		}

		public struct Surface
		{
			public string texName;
			public bool onNode;
			public int startIndex;
			public int indexCount;
			public Vector3? center;

			// Whether we want TexCoord0 to be divided by the texture size. Needed for most BSP surfaces
			// using Texinfo mapping, but *not* wanted for Overlay surfaces. This might get rearranged if
			// we move overlays out of being BSP surfaces...
			public bool wantsTexCoord0Scale;

			// Since our surfaces are merged together from multiple BSP surfaces, we can have multiple
			// surface lightmaps, but they're guaranteed to have been packed into the same lightmap page.
			public List<SurfaceLightmapData> lightmapData;
			public int lightmapPackerPageIndex;

			public BBox bbox;
		}

		public enum TexinfoFlags
		{
			SKY2D = 0x0002,
			SKY = 0x0004,
			TRANS = 0x0010,
			NODRAW = 0x0080,
			NOLIGHT = 0x0400,
			BUMPLIGHT = 0x0800,
		}

		public struct Texinfo
		{
			public TexinfoMapping textureMapping;
			public TexinfoMapping lightmapMapping;
			public TexinfoFlags flags;

			// texdata
			public string texName;
		}

		public struct TexinfoMapping
		{
			// 2x4 matrix for texture coordinates
			public Vector4 s;
			public Vector4 t;
		}

		public static void CalcTexCoord( ref Vector2 dst, in Vector3 v, in TexinfoMapping m )
		{
			dst.x = v.x * m.s.x + v.y * m.s.y + v.z * m.s.z + m.s.w;
			dst.y = v.x * m.t.x + v.y * m.t.y + v.z * m.t.z + m.t.w;

		}

		// Place into the lightmap page.
		public void CalcLightmapTexcoords( ref Vector2 dst, Vector2 uv, SurfaceLightmapData lightmapData, LightmapPackerPage lightmapPage )
		{
			dst.x = (uv.x + lightmapData.pagePosX) / lightmapPage.Width;
			dst.y = (uv.y + lightmapData.pagePosY) / lightmapPage.Height;
		}

		public struct BSPNode
		{
			public Plane plane { get; set; }
			public int child0 { get; set; }
			public int child1 { get; set; }
			public BBox bbox { get; set; }
			public int area { get; set; }
		}

		//public delegate Color[] AmbientCube();

		public struct BSPLeafAmbientSample
		{
			public Color[] ambientCube { get; set; }
			public Vector3 pos { get; set; }
		}

		public enum BSPLeafContents
		{
			Solid = 0x001,
			Water = 0x010,
			TestWater = 0x100
		}

		public struct BSPLeaf
		{
			public BBox bbox { get; set; }
			public int area { get; set; }
			public int cluster { get; set; }
			public BSPLeafAmbientSample[] ambientLightSamples { get; set; }
			public int[] faces { get; set; }
			public int[] surfaces { get; set; }
			public int leafwaterdata { get; set; }
			public BSPLeafContents contents { get; set; }
		}

		public struct BSPLeafWaterData
		{
			public float surfaceZ { get; set; }
			public float minZ { get; set; }
			public string surfaceMaterialName { get; set; }
		}

		public struct Model
		{
			public BBox bbox { get; set; }
			public int headnode { get; set; }
			public List<int> surfaces { get; set; }
		}

		public enum WorldLightType
		{
			Surface,
			Point,
			Spotlight,
			SkyLight,
			QuakeLight,
			SkyAmbient
		}

		public enum WorldLightFlags
		{
			InAmbientCube = 0x01
		}

		public struct WorldLight
		{
			public Vector3 pos { get; set; }
			public Vector3 intensity { get; set; }
			public Vector3 normal { get; set; }
			public WorldLightType type { get; set; }
			public float radius { get; set; }
			public Vector3 distAttenuation { get; set; }
			public float exponent { get; set; }
			public float stopdot { get; set; }
			public float stopdot2 { get; set; }
			public int style { get; set; }
			public WorldLightFlags flags { get; set; }
		}

		public class DispInfo
		{
			public Vector3 startPos;
			public float power;
			public int dispVertStart;
			public int sideLength;
			public int vertexCount;
		}

		// 3 pos, 4 normal, 4 tangent, 4 uv
		public const int VERTEX_SIZE = (3 + 4 + 4 + 4);

		public class MeshVertex
		{
			public Vector3 position;
			public Vector3 normal;
			public float alpha = 1.0f;
			public Vector2 uv;
			public Vector2 lightmapUV;
		}

		public class DisplacementResult
		{
			public MeshVertex[] vertex;
			public BBox bbox;
		}

		public static DisplacementResult BuildDisplacement( DispInfo disp, List<Vector3> corners, float[] disp_verts, TexinfoMapping texMapping )
		{
			var vertex = new MeshVertex[disp.vertexCount];
			for ( int i = 0; i < disp.vertexCount; i++ )
			{
				vertex[i] = new MeshVertex();
			}

			BBox aabb = new BBox();
			Vector3 v0 = new Vector3(), v1 = new Vector3(); //sikinti olabilir

			// Positions
			for ( int y = 0; y < disp.sideLength; y++ )
			{
				float ty = y / (disp.sideLength - 1.0f);
				v0 = Vector3.Lerp( corners[0], corners[1], ty );
				v1 = Vector3.Lerp( corners[3], corners[2], ty );

				for ( int x = 0; x < disp.sideLength; x++ )
				{
					float tx = x / (disp.sideLength - 1.0f);

					// Displacement normal vertex.
					int dvidx = disp.dispVertStart + (y * disp.sideLength) + x;
					float dvx = disp_verts[dvidx * 5 + 0];
					float dvy = disp_verts[dvidx * 5 + 1];
					float dvz = disp_verts[dvidx * 5 + 2];
					float dvdist = disp_verts[dvidx * 5 + 3];
					float dvalpha = disp_verts[dvidx * 5 + 4];

					MeshVertex v = vertex[y * disp.sideLength + x]; //MeshVertex ????
					v.position = Vector3.Lerp( v0, v1, tx );

					// Calculate texture coordinates before displacement happens.
					CalcTexCoord( ref v.uv, v.position, texMapping );

					v.position.x += (dvx * dvdist);
					v.position.y += (dvy * dvdist);
					v.position.z += (dvz * dvdist);
					v.lightmapUV.x = tx;
					v.lightmapUV.y = ty;
					v.alpha = Util.Saturate( dvalpha / 0xFF );
					Util.UnionPoint( ref aabb, v.position );
				}
			}

			// Normals
			int w = disp.sideLength;
			for ( int y = 0; y < w; y++ )
			{
				for ( int x = 0; x < w; x++ )
				{
					MeshVertex v = vertex[y * w + x]; // vertex???????????????????? kontrolet
					int x0 = x - 1, x1 = x, x2 = x + 1;
					int y0 = y - 1, y1 = y, y2 = y + 1;

					int count = 0;

					// Top left
					if ( x0 >= 0 && y0 >= 0 )
					{
						v0 = vertex[y1 * w + x0].position - vertex[y0 * w + x0].position;
						v1 = vertex[y0 * w + x1].position - vertex[y0 * w + x0].position;
						v0 = Vector3.Cross( v1, v0 );
						v0 = v0.Normal;
						v.normal += v0;

						v0 = vertex[y1 * w + x0].position - vertex[y0 * w + x1].position;
						v1 = vertex[y1 * w + x1].position - vertex[y0 * w + x1].position;
						v0 = Vector3.Cross( v1, v0 );
						v0 = v0.Normal;
						v.normal += v0;

						count += 2;
					}

					// Top right
					if ( x2 < w && y0 >= 0 )
					{
						v0 = vertex[y1 * w + x1].position - vertex[y0 * w + x1].position;
						v1 = vertex[y0 * w + x2].position - vertex[y0 * w + x1].position;
						v0 = Vector3.Cross( v1, v0 );
						v0 = v0.Normal;
						v.normal += v0;

						v0 = vertex[y1 * w + x1].position - vertex[y0 * w + x2].position;
						v1 = vertex[y1 * w + x2].position - vertex[y0 * w + x2].position;
						v0 = Vector3.Cross( v1, v0 );
						v0 = v0.Normal;
						v.normal += v0;

						count += 2;
					}

					// Bottom left
					if ( x0 >= 0 && y2 < w )
					{
						v0 = vertex[y2 * w + x0].position - vertex[y1 * w + x0].position;
						v1 = vertex[y1 * w + x1].position - vertex[y1 * w + x0].position;
						v0 = Vector3.Cross( v1, v0 );
						v0 = v0.Normal;
						v.normal += v0;

						v0 = vertex[y2 * w + x0].position - vertex[y1 * w + x1].position;
						v1 = vertex[y2 * w + x1].position - vertex[y1 * w + x1].position;
						v0 = Vector3.Cross( v1, v0 );
						v0 = v0.Normal;
						v.normal += v0;

						count += 2;
					}

					// Bottom right
					if ( x2 < w && y2 < w )
					{
						v0 = vertex[(y + 1) * w + x1].position - vertex[y * w + x1].position;
						v1 = vertex[y * w + x2].position - vertex[y * w + x1].position;
						v0 = Vector3.Cross( v1, v0 );
						v0 = v0.Normal;
						v.normal += v0;

						v0 = vertex[(y + 1) * w + x1].position - vertex[y * w + x2].position;
						v1 = vertex[(y + 1) * w + x2].position - vertex[y * w + x2].position;
						v0 = Vector3.Cross( v1, v0 );
						v0 = v0.Normal;
						v.normal += v0;

						count += 2;
					}

					v.normal *= 1.0f / count;
				}
			}
			return new DisplacementResult() { vertex = vertex, bbox = aabb };
		}

		void FetchVertexFromBuffer( ref MeshVertex dst, float[] vertexData, uint i )
		{
			uint offsVertex = i * VERTEX_SIZE;

			// Position
			dst.position.x = vertexData[offsVertex++];
			dst.position.y = vertexData[offsVertex++];
			dst.position.z = vertexData[offsVertex++];

			// Normal
			dst.normal.x = vertexData[offsVertex++];
			dst.normal.y = vertexData[offsVertex++];
			dst.normal.z = vertexData[offsVertex++];
			dst.alpha = vertexData[offsVertex++];

			// Tangent
			offsVertex += 3;
			// Tangent Sign and Lightmap Offset
			offsVertex++;

			// Texture UV
			dst.uv.x = vertexData[offsVertex++];
			dst.uv.y = vertexData[offsVertex++];

			// Lightmap UV
			dst.lightmapUV.x = vertexData[offsVertex++];
			dst.lightmapUV.y = vertexData[offsVertex++];
		}


		// Stores information for each origin face to the final, packed surface data.
		private class FaceToSurfaceInfo
		{
			public int startIndex = 0;
			public int indexCount = 0;
			public SurfaceLightmapData lightmapData;
		}

		private class OverlayInfo
		{
			public int[] faces;
			public Vector3 origin = new Vector3();
			public Vector3 normal = new Vector3();
			public Vector3[] basis = new Vector3[2];
			public Vector2[] planePoints = new Vector2[4];
			public float u0 = 0.0f;
			public float u1 = 0.0f;
			public float v0 = 0.0f;
			public float v1 = 0.0f;
		}

		private struct OverlaySurface
		{
			public List<MeshVertex> vertex;
			public List<int> indices;
			public SurfaceLightmapData lightmapData;
			public List<int> originFaceList;
		}

		private struct OverlayResult
		{
			public List<OverlaySurface> surfaces;
			public BBox bbox;
		}

		void BuildOverlayPlane( ref MeshVertex[] dst, OverlayInfo overlayInfo )
		{
			if ( dst.Length != 4 )
			{
				// HATA VERDIR
				return;
			}

			dst[0].uv = new Vector2( overlayInfo.u0, overlayInfo.v0 );
			dst[1].uv = new Vector2( overlayInfo.u0, overlayInfo.v1 );
			dst[2].uv = new Vector2( overlayInfo.u1, overlayInfo.v1 );
			dst[3].uv = new Vector2( overlayInfo.u1, overlayInfo.v0 );

			for ( int i = 0; i < dst.Length; i++ )
			{
				MeshVertex v = dst[i];
				v.position = overlayInfo.origin + overlayInfo.basis[0] * overlayInfo.planePoints[i].x + overlayInfo.basis[1] * overlayInfo.planePoints[i].y;
				dst[i] = v;
			}
		}

		void BuildSurfacePlane( MeshVertex[] dst, OverlayInfo overlayInfo )
		{
			if ( dst.Length != 3 )
			{
				// HATA VERDIR
				return;
			}

			for ( int i = 0; i < dst.Length; i++ )
			{
				MeshVertex v = dst[i];

				// Project onto overlay plane.
				Vector3 scratchVec3a = v.position - overlayInfo.origin;
				float m = Vector3.Dot( overlayInfo.normal, scratchVec3a );
				v.position += -m * overlayInfo.normal;
			}
		}

		void clipOverlayPlane( ref MeshVertex[] dst, OverlayInfo overlayInfo, MeshVertex p0, MeshVertex p1, MeshVertex p2 )
		{
			var plane = new Plane();
			// First compute our clip plane.
			p0.normal = p1.position - p0.position;
			p0.normal = p0.normal.Normal;
			p0.normal = Vector3.Cross( overlayInfo.normal, p0.normal );
			plane.Set( p0.normal, -Vector3.Dot( p0.normal, p0.position ) );

			if ( plane.DistanceVec3( p2.position ) > 0.0f )
				plane.Negate();

			List<float> distance = new List<float>();

			var vertex = dst.ToArray();
			//dst.Clear(); // verify
			dst = new MeshVertex[dst.Length];
			for ( var i = 0; i < dst.Length; i++ )
				dst[i] = new();

			for ( var i = 0; i < vertex.Length; i++ )
			{
				var v = vertex[i];
				distance.Add(plane.DistanceVec3( v.position ));
			}

			for ( var i = 0; i < vertex.Length; i++ )
			{
				var i0 = i;
				var i1 = (i + 1) % distance.Count();
				var d0 = distance[i0];
				var d1 = distance[i1];
				var v0 = vertex[i0];
				var v1 = vertex[i1];

				if ( d0 <= 0.0f )
					dst.Append( v0 );//dst.Add( v0 );

				// Not crossing plane; no need to split.
				if ( Math.Sign( d0 ) == Math.Sign( d1 ) || d0 == 0.0f || d1 == 0.0f )
					continue;

				// Crossing plane, need to split.
				var t = d0 / (d0 - d1);

				var newVertex = new MeshVertex();
				newVertex.position = Vector3.Lerp( v0.position, v1.position, t );
				newVertex.uv = Vector2.Lerp( v0.uv, v1.uv, t );

				// Don't care about alpha / normal / lightmapUV.
				dst.Append( newVertex );
				//dst.Add( newVertex );
			}
		}

		public static float CalcWedgeArea2( Vector3 p0, Vector3 p1, Vector3 p2 )
		{
			// Compute the wedge p0..p1 / p1..p2
			Vector3 scratchVec3a = p1 - p0;
			Vector3 scratchVec3b = p2 - p0;
			Vector3 scratchVec3c = Vector3.Cross( scratchVec3a, scratchVec3b );
			return scratchVec3c.Length;
		}

		public static void CalcBarycentricsFromTri( ref Vector2 dst, Vector3 p, Vector3 p0, Vector3 p1, Vector3 p2, float outerTriArea2 )
		{
			dst.x = CalcWedgeArea2( p1, p2, p ) / outerTriArea2;
			dst.y = CalcWedgeArea2( p2, p0, p ) / outerTriArea2;
		}

		private OverlayResult BuildOverlay( OverlayInfo overlayInfo, FaceToSurfaceInfo[] faceToSurfaceInfo, uint[] indexData, float[] vertexData )
		{
			List<OverlaySurface> surfaces = new List<OverlaySurface>();
			MeshVertex[] surfacePoints = new MeshVertex[3];
			for ( var i3 = 0; i3 < 3; i3++ )
				surfacePoints[i3] = new();

			Plane surfacePlane = new Plane();
			BBox bbox = new BBox();

			for ( int i = 0; i < overlayInfo.faces.Length; i++ )
			{
				int face = overlayInfo.faces[i];
				FaceToSurfaceInfo surfaceInfo = faceToSurfaceInfo[face];

				List<MeshVertex> vertex = new List<MeshVertex>();
				List<int> indices = new List<int>();
				List<int> originSurfaceList = new List<int>();

				for ( int index = surfaceInfo.startIndex; index < surfaceInfo.startIndex + surfaceInfo.indexCount; index += 3 )
				{
					MeshVertex[] overlayPoints = new MeshVertex[4];
					for ( var i4 = 0; i4 < 4; i4++ )
						overlayPoints[i4] = new();

					BuildOverlayPlane( ref overlayPoints, overlayInfo );

					FetchVertexFromBuffer( ref surfacePoints[0], vertexData, indexData[index + 0] );
					FetchVertexFromBuffer( ref surfacePoints[1], vertexData, indexData[index + 1] );
					FetchVertexFromBuffer( ref surfacePoints[2], vertexData, indexData[index + 2] );

					// Store our surface plane for later, so we can re-project back to it...
					// XXX(jstpierre): Not the cleanest way to compute the surface normal... seems to work though?
					surfacePlane.setTri( surfacePoints[0].position, surfacePoints[2].position, surfacePoints[1].position);

					// Project surface down to the overlay plane.
					BuildSurfacePlane( surfacePoints, overlayInfo );

					float surfaceTriArea2 = CalcWedgeArea2( surfacePoints[0].position, surfacePoints[1].position, surfacePoints[2].position );

					// Clip the overlay plane to the surface.
					for ( int j0 = 0; j0 < surfacePoints.Length; j0++ )
					{
						int j1 = (j0 + 1) % surfacePoints.Length;
						int j2 = (j0 + 2) % surfacePoints.Length;
						MeshVertex p0 = surfacePoints[j0];
						MeshVertex p1 = surfacePoints[j1];
						MeshVertex p2 = surfacePoints[j2];

						clipOverlayPlane( ref overlayPoints, overlayInfo, p0, p1, p2 );
					}

					if ( overlayPoints.Length < 3 )
					{
						// Not enough to make a triangle. Just skip.
						continue;
					}

					for ( int j = 0; j < overlayPoints.Length; j++ )
					{
						var v = overlayPoints[j];

						// Assign lightmapUV from triangle barycentrics.
						CalcBarycentricsFromTri( ref v.lightmapUV, v.position, surfacePoints[0].position, surfacePoints[1].position, surfacePoints[2].position, surfaceTriArea2 );
						var baryU = v.lightmapUV.x;
						var baryV = v.lightmapUV.y;
						var baryW = (1 - baryU - baryV);
						v.lightmapUV = surfacePoints[0].lightmapUV * baryU + surfacePoints[1].lightmapUV * baryV + surfacePoints[2].lightmapUV * baryW;

						// Set the decal's normal to be the face normal...
						v.normal = surfacePlane.n;

						// Project back down to the surface plane.
						var distance = surfacePlane.DistanceVec3( v.position );
						var m = distance / MathF.Min( 1.0f, Vector3.Dot( v.normal, overlayInfo.normal ) );
						v.position += overlayInfo.normal * -m;

						// Offset the normal just a smidgen...
						v.position += v.normal * 0.1f;
						UnionPoint(ref bbox, v.position );
					}

					// We're done! Append the overlay plane to the list.
					int baseVertex = vertex.Count();
					vertex.AddRange( overlayPoints );
					int dstIndexOffs = indices.Count;
					var overlayPointsLength = overlayPoints.Length;
					indices.Capacity = indices.Capacity + TopologyHelper.GetTriangleIndexCountForTopologyIndexCount( GfxTopology.TriFans, ref overlayPointsLength ); // kontrol et

					var indicesArray = new int[indices.Capacity]; // Not the best way
					for(var i4 = 0;  i4 < indices.Count; i4++)
						indicesArray[i4] = indices[i4];

					TopologyHelper.ConvertToTrianglesRange( ref indicesArray, dstIndexOffs, GfxTopology.TriFans, baseVertex, overlayPointsLength );
					indices = indicesArray.ToList();
					indicesArray = null;
				}

				if ( vertex.Count == 0 )
					continue;

				originSurfaceList.Add( face );

				var lightmapData = surfaceInfo.lightmapData;
				var surface = new OverlaySurface() { vertex = vertex, indices = indices, lightmapData = lightmapData, originFaceList = originSurfaceList };
				surfaces.Add( surface );

			}

			// Sort surface and merge them together.
			surfaces.Sort( ( a, b ) => b.lightmapData.pageIndex.CompareTo( a.lightmapData.pageIndex ) );

			for ( int i = 1; i < surfaces.Count; i++ )
			{
				int i0 = i - 1, i1 = i;
				var s0 = surfaces[i0];
				var s1 = surfaces[i1];

				if ( s0.lightmapData.pageIndex != s1.lightmapData.pageIndex )
					continue;

				// Merge s1 into s0, then delete s0.
				int baseVertex = s0.vertex.Count;
				s0.vertex.AddRange( s1.vertex );
				for ( int j = 0; j < s1.indices.Count; j++ )
				{
					s0.indices.Add( baseVertex + s1.indices[j] );
				}
				foreach ( var item in s1.originFaceList )
				{
					ensureInList( ref s0.originFaceList, item );
				}
				surfaces.RemoveAt( i1 );
				i--;
			}
			return new OverlayResult(){ surfaces = surfaces, bbox = bbox};
		}


		public static int MagicInt( string S )
		{
			byte n0 = (byte)S[0];
			byte n1 = (byte)S[1];
			byte n2 = (byte)S[2];
			byte n3 = (byte)S[3];
			return (n0 << 24) | (n1 << 16) | (n2 << 8) | n3;
		}

		public class BSPVisibility
		{
			public BitMap[] pvs;
			public int numclusters;

			public BSPVisibility( byte[] buffer )
			{
				using ( MemoryStream stream = new MemoryStream( buffer ) )
				{
					using ( BinaryReader reader = new BinaryReader( stream ) )
					{
						this.numclusters = reader.ReadInt32();
						this.pvs = new BitMap[this.numclusters];

						for ( int i = 0; i < this.numclusters; i++ )
						{
							int pvsofs = reader.ReadInt32();
							int pasofs = reader.ReadInt32();
							this.decodeClusterTable( ref this.pvs[i], reader, pvsofs );
						}
					}
				}
			}

			private void decodeClusterTable( ref BitMap dst, BinaryReader reader, int offs )
			{
				if( dst == null )
					dst = new( this.numclusters ); //verify

				if ( offs == 0x00 )
				{
					// No visibility info; mark everything visible.
					dst.Fill( true );
					return;
				}

				// Initialize with all 0s.
				dst.Fill( false );

				int clusteridx = 0;
				while ( clusteridx < this.numclusters )
				{
					byte b = reader.ReadByte();

					if ( b != 0 )
					{
						// Transfer to bitmap. Need to reverse bits (unfortunately).
						for ( int i = 0; i < 8; i++ )
							dst.SetBit( clusteridx++, ((b & (1 << i)) != 0) );
					}
					else
					{
						// RLE.
						byte c = reader.ReadByte();
						clusteridx += c * 8;
					}
				}
			}
		}

		/*public interface ILightmapAlloc
		{
			int Width { get; }
			int Height { get; }
			int PagePosX { get; set; }
			int PagePosY { get; set; }
		}*/

		public class LightmapPackerPage
		{
			public readonly ushort[] Skyline;
			public int Width { get; private set; }
			public int Height { get; private set; }

			private readonly int _maxWidth;
			private readonly int _maxHeight;

			public LightmapPackerPage( int maxWidth, int maxHeight )
			{
				// Initialize our skyline. Note that our skyline goes horizontal, not vertical.
				if( maxWidth > 0xFFFF )
				{
					// HATA: "Max width should not exceed 65535"
					return;
				}

				_maxWidth = maxWidth;
				_maxHeight = maxHeight;
				Skyline = new ushort[maxHeight];
			}

			public bool Allocate( ref SurfaceLightmapData allocation )
			{
				int w = allocation.width, h = allocation.height;

				// March downwards until we find a span of skyline that will fit.
				int bestY = -1, minX = _maxWidth - w + 1;
				for ( int y = 0; y < _maxHeight - h; )
				{
					int searchY = SearchSkyline( y, h );
					if ( Skyline[searchY] < minX )
					{
						minX = Skyline[searchY];
						bestY = y;
					}
					y = searchY + 1;
				}

				if ( bestY < 0 )
				{
					// Could not pack.
					return false;
				}

				// Found a position!
				allocation.pagePosX = minX;
				allocation.pagePosY = bestY;
				// pageIndex filled in by caller.

				// Update our skyline.
				for ( int y = bestY; y < bestY + h; y++ )
				{
					Skyline[y] = (ushort)(minX + w);
				}

				// Update our bounds.
				Width = Math.Max( Width, minX + w );
				Height = Math.Max( Height, bestY + h );

				return true;
			}

			private int SearchSkyline( int startY, int h )
			{
				int winnerY = -1, maxX = -1;
				for ( int y = startY; y < startY + h; y++ )
				{
					if ( Skyline[y] >= maxX )
					{
						winnerY = y;
						maxX = Skyline[y];
					}
				}
				return winnerY;
			}
		}

		// SBOXUN util.Compress var zaten https://github.com/magcius/noclip.website/blob/master/src/Common/Compression/LZMA.ts#L395
		/*public static byte[] DecompressLZMA( byte[] compressedData, int uncompressedSize )
		{
			using ( var compressedStream = new MemoryStream( compressedData ) )
			using ( var compressedReader = new BinaryReader( compressedStream ) )
			{
				// Parse Valve's lzma_header_t.
				if ( ReadString( compressedReader, 0x00, 0x04 ) != "LZMA" )
				{
					throw new Exception( "Invalid LZMA header." );
				}

				var actualSize = compressedReader.ReadUInt32();
				if ( actualSize != uncompressedSize )
				{
					throw new Exception( "Uncompressed size does not match expected size." );
				}

				var lzmaSize = compressedReader.ReadUInt32();
				if ( lzmaSize + 0x11 > compressedData.Length )
				{
					throw new Exception( "LZMA data size is too large." );
				}

				var lzmaProperties = DecodeLZMAProperties( new ArraySegment<byte>( compressedData, 0x0C, (int)(compressedData.Length - 0x0C) ) );

				return Decompress( new ArraySegment<byte>( compressedData, 0x11, (int)(lzmaSize) ), lzmaProperties, (int)actualSize );
			}
		}*/

		public class LightmapPacker
		{
			public List<LightmapPackerPage> pages = new List<LightmapPackerPage>();

			public int pageWidth = 2048;
			public int pageHeight = 2048;

			public void Allocate( ref SurfaceLightmapData allocation )
			{
				for ( int i = 0; i < pages.Count; i++ )
				{
					if ( pages[i].Allocate( ref allocation ) )
					{
						allocation.pageIndex = i;
						return;
					}
				}

				// Make a new page.
				LightmapPackerPage page = new LightmapPackerPage( pageWidth, pageHeight );
				pages.Add( page );
				//Debug.Assert( page.Allocate( ref allocation ) );
				if( !page.Allocate( ref allocation ) )
				{
					//HATA
					return;
				}
				allocation.pageIndex = pages.Count - 1;
			}
		}

		public class Cubemap
		{
			public Vector3 pos;
			public string filename;
		}


		Vector3 scratchVec3a = new Vector3();
		Vector3 scratchVec3b = new Vector3();
		Vector3 scratchVec3c = new Vector3();


		public int version;
		public bool usingHDR;

		// For debugging.
		public string entitiesStr;
		public List<BSPEntity> entities = new List<BSPEntity>();
		public List<Surface> surfaces = new List<Surface>();
		public List<Overlay> overlays = new List<Overlay>();
		public List<Model> models = new List<Model>();
		//public ZipFile pakfile; // Later
		public List<BSPNode> nodelist = new List<BSPNode>();
		public List<BSPLeaf> leaflist = new List<BSPLeaf>();
		public List<Cubemap> cubemaps = new List<Cubemap>();
		public List<WorldLight> worldlights = new List<WorldLight>();
		public List<BSPLeafWaterData> leafwaterdata = new List<BSPLeafWaterData>();
		//public DetailObjects detailObjects; // Later
		//public StaticObjects staticObjects; // Later
		public BSPVisibility visibility;
		public LightmapPacker lightmapPacker = new LightmapPacker();
		public byte[] gameLump;
		public byte[] buffer;

		public byte[] indexData;
		public byte[] vertexData;

		// For sbox
		List<Vertex> vertexList = new();
		//List<Vector2> lightmapList = new();
		List<int> indexList = new();

		public List<meshData> meshDataList = new();
		//public List<entityMeshData> entityMeshDataList = new();
		public struct meshData
		{
			public List<Vertex> vertices { get; set; }
			public List<int> indices { get; set; }
			public string textureName { get; set; }
			public int faceIndex { get; set; }
		}

		public BSPFile( byte[] buffer, string mapname ) {

			//Debug.Assert( Encoding.ASCII.GetString( buffer, 0x00, 0x04 ) == "VBSP" );
			if( Encoding.ASCII.GetString( buffer, 0x00, 0x04 ) != "VBSP" )
			{
				//HATA
				return;
			}

			this.buffer = buffer;

			//var view = new DataView( buffer );

			this.version = (int)BitConverter.ToUInt32( buffer, 0x04 );
			//Debug.Assert( this.version == 19 || this.version == 20 || this.version == 21 || this.version == 22 );
			if ( this.version != 19 && this.version != 20 && this.version != 21 && this.version != 22 )
			{
				//HATA
				return;
			}

			byte[] lighting = null;

			var preferHDR = true;
			if ( preferHDR )
			{
				lighting = GetLumpData( LumpType.LIGHTING_HDR, 1 );
				this.usingHDR = true;

				if ( lighting == null || lighting.Length == 0 )
				{
					lighting = GetLumpData( LumpType.LIGHTING, 1 );
					this.usingHDR = false;
				}
			}
			else
			{
				lighting = GetLumpData( LumpType.LIGHTING, 1 );
				this.usingHDR = false;

				if ( lighting == null || lighting.Length == 0 )
				{
					lighting = GetLumpData( LumpType.LIGHTING_HDR, 1 );
					this.usingHDR = true;
				}
			}

			this.gameLump = GetLumpData( LumpType.GAME_LUMP );

			// Parse out visibility.
			byte[] visibilityData = GetLumpData( LumpType.VISIBILITY );
			if ( visibilityData.Length > 0 )
			{
				this.visibility = new BSPVisibility( visibilityData );
			}

			// Parse out entities. Later
			//this.entitiesStr = Util.DecodeString( GetLumpData( LumpType.ENTITIES ) );
			//this.entities = ParseEntitiesLump( this.entitiesStr );

			List<Texinfo> texinfoa = new();

			// Parse out texinfo / texdata.
			var texstrTableBytes = GetLumpData( LumpType.TEXDATA_STRING_TABLE );

			int texstrTableLength = texstrTableBytes.Length / sizeof( uint );
			uint[] texstrTable = new uint[texstrTableLength];
			for ( int i = 0; i < texstrTableLength; i++ )
			{
				texstrTable[i] = BitConverter.ToUInt32( texstrTableBytes, i * sizeof( uint ) );
			}

			var texstrData = GetLumpData( LumpType.TEXDATA_STRING_DATA );
			var texdata = GetLumpData( LumpType.TEXDATA );
			var texinfo = GetLumpData( LumpType.TEXINFO );
			var texinfoCount = texinfo.Length / 0x48;

			for ( int i = 0; i < texinfoCount; i++ )
			{
				var infoOffs = i * 0x48;
				var textureMappingS = Util.ReadVec4( ref texinfo, infoOffs + 0x00 );
				var textureMappingT = Util.ReadVec4( ref texinfo, infoOffs + 0x10 );
				var textureMapping = new TexinfoMapping { s = textureMappingS, t = textureMappingT };
				var lightmapMappingS = Util.ReadVec4( ref texinfo, infoOffs + 0x20 );
				var lightmapMappingT = Util.ReadVec4( ref texinfo, infoOffs + 0x30 );
				var lightmapMapping = new TexinfoMapping { s = lightmapMappingS, t = lightmapMappingT };
				var flags = BitConverter.ToUInt32( texinfo, infoOffs + 0x40 );
				var texdataIdx = (int)BitConverter.ToUInt32( texinfo, infoOffs + 0x44 );

				var texdataOffs = texdataIdx * 0x20;
				var reflectivityR = BitConverter.ToSingle( texdata, texdataOffs + 0x00 );
				var reflectivityG = BitConverter.ToSingle( texdata, texdataOffs + 0x04 );
				var reflectivityB = BitConverter.ToSingle( texdata, texdataOffs + 0x08 );
				var nameTableStringID = BitConverter.ToUInt32( texdata, texdataOffs + 0x0C );
				var width = BitConverter.ToUInt32( texdata, texdataOffs + 0x10 );
				var height = BitConverter.ToUInt32( texdata, texdataOffs + 0x14 );
				var view_width = BitConverter.ToUInt32( texdata, texdataOffs + 0x18 );
				var view_height = BitConverter.ToUInt32( texdata, texdataOffs + 0x1C );
				var texName = Util.ReadString( ref texstrData, (int)texstrTable[nameTableStringID] ).ToLower();
				texinfoa.Add( new Texinfo { textureMapping = textureMapping, lightmapMapping = lightmapMapping, flags = (TexinfoFlags)(int)flags , texName = texName } );
			}


			// Parse materials.
			var pakfileData = GetLumpData( LumpType.PAKFILE );
			// downloadBufferSlice('de_prime_pakfile.zip', pakfileData);
			//this.pakfile = parseZipFile( pakfileData ); LATER

			// Parse out BSP tree.
			var nodes = GetLumpData( LumpType.NODES );
			var planes = GetLumpData( LumpType.PLANES );

			for ( int idx_1 = 0x00; idx_1 < nodes.Length; idx_1 += 0x20 )
			{
				int planenum = (int)BitConverter.ToUInt32( nodes, idx_1 + 0x00 );

				float planeX = BitConverter.ToSingle( planes, planenum * 0x14 + 0x00 );
				float planeY = BitConverter.ToSingle( planes, planenum * 0x14 + 0x04 );
				float planeZ = BitConverter.ToSingle( planes, planenum * 0x14 + 0x08 );
				float planeDist = BitConverter.ToSingle( planes, planenum * 0x14 + 0x0C );

				Plane plane = new Plane( planeX, planeY, planeZ, -planeDist );

				int child0 = BitConverter.ToInt32( nodes, idx_1 + 0x04 );
				int child1 = BitConverter.ToInt32( nodes, idx_1 + 0x08 );
				short bboxMinX = BitConverter.ToInt16( nodes, idx_1 + 0x0C );
				short bboxMinY = BitConverter.ToInt16( nodes, idx_1 + 0x0E );
				short bboxMinZ = BitConverter.ToInt16( nodes, idx_1 + 0x10 );
				short bboxMaxX = BitConverter.ToInt16( nodes, idx_1 + 0x12 );
				short bboxMaxY = BitConverter.ToInt16( nodes, idx_1 + 0x14 );
				short bboxMaxZ = BitConverter.ToInt16( nodes, idx_1 + 0x16 );
				BBox bbox = new BBox( new Vector3( bboxMinX, bboxMinY, bboxMinZ ), new Vector3( bboxMaxX, bboxMaxY, bboxMaxZ ) );
				ushort firstface = BitConverter.ToUInt16( nodes, idx_1 + 0x18 );
				ushort numfaces_1 = BitConverter.ToUInt16( nodes, idx_1 + 0x1A );
				short area = BitConverter.ToInt16( nodes, idx_1 + 0x1C );

				nodelist.Add( new BSPNode { plane = plane, child0 = child0, child1 = child1, bbox = bbox, area = area } );
			}


			// Build our mesh.

			// Parse out edges / surfedges.
			byte[] edgesBytes = GetLumpData( LumpType.EDGES );
			int edgesLength = edgesBytes.Length / sizeof( ushort );
			ushort[] edges = new ushort[edgesLength];
			for ( int i = 0; i < edgesLength; i++ )
			{
				edges[i] = BitConverter.ToUInt16( edgesBytes, i * sizeof( ushort ) );
			}

			byte[] surfedgesBytes = GetLumpData( LumpType.SURFEDGES );
			int surfedgesLength = surfedgesBytes.Length / sizeof( int );
			int[] surfedges = new int[surfedgesLength];
			for ( int i = 0; i < surfedgesLength; i++ )
			{
				surfedges[i] = BitConverter.ToInt32( surfedgesBytes, i * sizeof( int ) );
			}

			var vertindices = new uint[surfedges.Length];
			for ( int i = 0; i < surfedges.Length; i++ )
			{
				int surfedge = surfedges[i];
				if ( surfedges[i] >= 0 )
					vertindices[i] = edges[surfedge * 2 + 0];
				else
					vertindices[i] = edges[-surfedge * 2 + 1];
			}

			/*Texinfo[] texinfoa = new Texinfo[] { };

			// Parse out texinfo / texdata.
			var texstrTable = getLumpData( LumpType.TEXDATA_STRING_TABLE ).createTypedArray<UInt32Array>();
			var texstrData = getLumpData( LumpType.TEXDATA_STRING_DATA );
			var texdata = getLumpData( LumpType.TEXDATA ).createDataView();
			var texinfo = getLumpData( LumpType.TEXINFO ).createDataView();
			var texinfoCount = texinfo.ByteLength / 0x48;
			for ( int i = 0; i < texinfoCount; i++ )
			{
				var infoOffs = i * 0x48;
				var textureMappingS = readVec4( texinfo, infoOffs + 0x00 );
				var textureMappingT = readVec4( texinfo, infoOffs + 0x10 );
				var textureMapping = new TexinfoMapping { s = textureMappingS, t = textureMappingT };
				var lightmapMappingS = readVec4( texinfo, infoOffs + 0x20 );
				var lightmapMappingT = readVec4( texinfo, infoOffs + 0x30 );
				var lightmapMapping = new TexinfoMapping { s = lightmapMappingS, t = lightmapMappingT };
				var flags = texinfo.GetUint32( infoOffs + 0x40, true );
				var texdataIdx = texinfo.GetUint32( infoOffs + 0x44, true );

				var texdataOffs = texdataIdx * 0x20;
				var reflectivityR = texdata.GetFloat32( texdataOffs + 0x00, true );
				var reflectivityG = texdata.GetFloat32( texdataOffs + 0x04, true );
				var reflectivityB = texdata.GetFloat32( texdataOffs + 0x08, true );
				var nameTableStringID = texdata.GetUint32( texdataOffs + 0x0C, true );
				var width = texdata.GetUint32( texdataOffs + 0x10, true );
				var height = texdata.GetUint32( texdataOffs + 0x14, true );
				var view_width = texdata.GetUint32( texdataOffs + 0x18, true );
				var view_height = texdata.GetUint32( texdataOffs + 0x1C, true );
				var texName = readString( texstrData, texstrTable[nameTableStringID] ).ToLower();
				texinfoa.Add( new Texinfo { textureMapping = textureMapping, lightmapMapping = lightmapMapping, flags = flags, texName = texName } );
			}

	
			// downloadBufferSlice('de_prime_pakfile.zip', pakfileData);
			this.pakfile = parseZipFile( pakfileData );*/

			// Parse out faces.
			byte[] facelist = null;
			if ( usingHDR )
				facelist = GetLumpData( LumpType.FACES_HDR, 1 );//.createDataView();
			if ( facelist == null || facelist.Length == 0 )
				facelist = GetLumpData( LumpType.FACES, 1 );//.createDataView();

			var dispinfo = GetLumpData( LumpType.DISPINFO );
			var dispinfolist = new List<DispInfo>();

			for ( int idx2 = 0x00; idx2 < dispinfo.Length; idx2 += 0xB0 )
			{
				var startPosX = BitConverter.ToSingle( dispinfo, idx2 + 0x00 );
				var startPosY = BitConverter.ToSingle( dispinfo, idx2 + 0x04 );
				var startPosZ = BitConverter.ToSingle( dispinfo, idx2 + 0x08 );
				var startPos = new Vector3( startPosX, startPosY, startPosZ );

				var m_iDispVertStart = (int) BitConverter.ToUInt32( dispinfo, idx2 + 0x0C );
				var m_iDispTriStart = BitConverter.ToUInt32( dispinfo, idx2 + 0x10 );
				var power = BitConverter.ToUInt32( dispinfo, idx2 + 0x14 );
				var minTess = BitConverter.ToUInt32( dispinfo, idx2 + 0x18 );
				var smoothingAngle = BitConverter.ToSingle( dispinfo, idx2 + 0x1C );
				var contents = BitConverter.ToUInt32( dispinfo, idx2 + 0x20 );
				var mapFace = BitConverter.ToUInt16( dispinfo, idx2 + 0x24 );
				var m_iLightmapAlphaStart = BitConverter.ToUInt32( dispinfo, idx2 + 0x26 );
				var m_iLightmapSamplePositionStart = BitConverter.ToUInt32( dispinfo, idx2 + 0x2A );

				// neighbor rules
				// allowed verts

				// compute for easy access
				var sideLength = (1 << (int)power) + 1;
				var vertexCount = sideLength * sideLength;

				dispinfolist.Add( new DispInfo { startPos = startPos, dispVertStart = m_iDispVertStart, power = power, sideLength = sideLength, vertexCount = vertexCount } );
			}

			var primindicesBytes = GetLumpData( LumpType.PRIMINDICES );//.CreateTypedArray<ushort>();

			int primindicesLenght = primindicesBytes.Length / sizeof( ushort );
			ushort[] primindices = new ushort[primindicesLenght];
			for ( int i = 0; i < primindicesLenght; i++ )
			{
				primindices[i] = BitConverter.ToUInt16( primindicesBytes, i * sizeof( ushort ) );
			}

			var primitives = GetLumpData( LumpType.PRIMITIVES );//.CreateDataView();

			// Normals are packed in surface order (???), so we need to unpack these before the initial sort.
			int vertnormalIdx = 0;

			List<Face> faces = new List<Face>();
			int numfaces = 0;

			// Do some initial surface parsing, pack lightmaps.
			for ( int i = 0, idx_3 = 0x00; idx_3 < facelist.Count(); i++, idx_3 += 0x38, numfaces++ )
			{
				ushort planenum = BitConverter.ToUInt16( facelist, idx_3 + 0x00 );
				ushort numedges = BitConverter.ToUInt16( facelist, idx_3 + 0x08 );
				ushort texinfo_1 = BitConverter.ToUInt16( facelist, idx_3 + 0x0A );
				Texinfo tex = texinfoa[texinfo_1];

				// Normals are stored in the data for all surfaces, even for displacements.
				int vertnormalBase = vertnormalIdx;
				vertnormalIdx += numedges;

				if ( (tex.flags & (TexinfoFlags.SKY | TexinfoFlags.SKY2D)) != 0 )
					continue;

				int lightofs = BitConverter.ToInt32( facelist, idx_3 + 0x14 );
				uint[] m_LightmapTextureSizeInLuxels = new uint[2];
				for ( int j = 0; j < 2; j++ )
				{
					m_LightmapTextureSizeInLuxels[j] = BitConverter.ToUInt32( facelist, idx_3 + 0x24 + j * 4 );
				}

				// lighting info
				List<int> styles = new List<int>();
				for ( int j = 0; j < 4; j++ )
				{
					int style = (int)facelist[idx_3 + 0x10 + j];
					if ( style == 0xFF )
						break;
					styles.Add( style );
				}

				// surface lighting info
				int width = (int)m_LightmapTextureSizeInLuxels[0] + 1;
				int height = (int)m_LightmapTextureSizeInLuxels[1] + 1;
				bool hasBumpmapSamples = (tex.flags & TexinfoFlags.BUMPLIGHT) != 0;
				int srcNumLightmaps = hasBumpmapSamples ? 4 : 1;
				int srcLightmapSize = styles.Count * (width * height * srcNumLightmaps * 4);

				byte[] samples = null;
				if ( lightofs != -1 )
					samples = new ArraySegment<byte>( lighting, lightofs, srcLightmapSize ).ToArray(); // verify

				SurfaceLightmapData lightmapData = new SurfaceLightmapData()
				{
					faceIndex = i,
					width = width,
					height = height,
					styles = styles,
					samples = samples,
					hasBumpmapSamples = hasBumpmapSamples,
					pageIndex = -1,
					pagePosX = -1,
					pagePosY = -1
				};

				// Allocate ourselves a page.
				this.lightmapPacker.Allocate( ref lightmapData );

				Vector3 plane = readVec3( planes, planenum * 0x14 );

				faces.Add( new Face() { Index = i, Texinfo = texinfo_1, LightmapData = lightmapData, VertnormalBase = vertnormalBase, Plane = plane } );
			}


			byte[] models = GetLumpData( LumpType.MODELS );

			List<int> faceToModelIndex = new List<int>();
			for ( int idx4 = 0; idx4 < models.Length; idx4 += 0x30 )
			{
				float minX = BitConverter.ToSingle( models, idx4 + 0x00 );
				float minY = BitConverter.ToSingle( models, idx4 + 0x04 );
				float minZ = BitConverter.ToSingle( models, idx4 + 0x08 );
				float maxX = BitConverter.ToSingle( models, idx4 + 0x0C );
				float maxY = BitConverter.ToSingle( models, idx4 + 0x10 );
				float maxZ = BitConverter.ToSingle( models, idx4 + 0x14 );
				var bbox = new BBox( new Vector3( minX, minY, minZ ), new Vector3( maxX, maxY, maxZ ) );

				float originX = BitConverter.ToSingle( models, idx4 + 0x18 );
				float originY = BitConverter.ToSingle( models, idx4 + 0x1C );
				float originZ = BitConverter.ToSingle( models, idx4 + 0x20 );

				int headnode = (int)BitConverter.ToUInt32( models, idx4 + 0x24 );
				uint firstface = BitConverter.ToUInt32( models, idx4 + 0x28 );
				uint numfaces2 = BitConverter.ToUInt32( models, idx4 + 0x2C );

				int modelIndex = this.models.Count;
				for ( int i = (int)firstface; i < firstface + numfaces2; i++ )
				{
					if ( i < faceToModelIndex.Count )
					{
						faceToModelIndex[i] = modelIndex;
					}
					else
					{
						faceToModelIndex.Add( modelIndex );
					}
				}

				this.models.Add( new Model { bbox = bbox, headnode = headnode, surfaces = new List<int>() } );
			}



			var leafwaterdata = GetLumpData( LumpType.LEAFWATERDATA );
			var leafwaterdataCount = leafwaterdata.Length / 0x0C;
			for ( int i = 0; i < leafwaterdataCount; i++ )
			{
				int offset = i * 0x0C;
				float surfaceZ = BitConverter.ToSingle( leafwaterdata, offset );
				float minZ = BitConverter.ToSingle( leafwaterdata, offset + 0x04 );
				ushort surfaceTexInfoID = BitConverter.ToUInt16( leafwaterdata, offset + 0x08 );
				string surfaceMaterialName = texinfoa[surfaceTexInfoID].texName;
				this.leafwaterdata.Add( new BSPLeafWaterData { surfaceZ = surfaceZ, minZ = minZ, surfaceMaterialName = surfaceMaterialName } );
			}



			var leafsLump = GetLumpDataEx( LumpType.LEAFS ); //leafsLump, leafsVersion
			byte[] leafs = leafsLump.Item1;

			byte[] leafambientindex = null;
			if ( this.usingHDR )
				leafambientindex = GetLumpData( LumpType.LEAF_AMBIENT_INDEX_HDR );//.array;
			if ( leafambientindex == null || leafambientindex.Length == 0 )
				leafambientindex = GetLumpData( LumpType.LEAF_AMBIENT_INDEX );//.array;

			byte[] leafambientlightingLump = null;
			int leafambientlightingVersion = 0;
			if ( this.usingHDR )
			{
				var tuple = GetLumpDataEx( LumpType.LEAF_AMBIENT_LIGHTING_HDR );
				leafambientlightingLump = tuple.Item1;
				leafambientlightingVersion = (int)tuple.Item2;
			}
			if ( leafambientlightingLump == null || leafambientlightingLump.Length == 0 )
			{
				var tuple = GetLumpDataEx( LumpType.LEAF_AMBIENT_LIGHTING );
				leafambientlightingLump = tuple.Item1;
				leafambientlightingVersion = (int)tuple.Item2;
			}
			var leafambientlighting = leafambientlightingLump;


			byte[] leaffacelistBytes = GetLumpData( LumpType.LEAFFACES );//.CreateTypedArray<ushort>();
			int leaffacelistLenght = leaffacelistBytes.Length / sizeof( ushort );
			ushort[] leaffacelist = new ushort[leaffacelistLenght];
			for ( int i = 0; i < leaffacelistLenght; i++ )
			{
				leaffacelist[i] = BitConverter.ToUInt16( leaffacelistBytes, i * sizeof( ushort ) );
			}

			List<List<int>> faceToLeafIdx = Enumerable.Range( 0, numfaces ).Select( _ => new List<int>() ).ToList();
			for ( int i = 0, idx_5 = 0x00; idx_5 < leafs.Length; i++ )
			{
				uint contents = BitConverter.ToUInt32( leafs, idx_5 + 0x00 );
				ushort cluster = BitConverter.ToUInt16( leafs, idx_5 + 0x04 );
				ushort areaAndFlags = BitConverter.ToUInt16( leafs, idx_5 + 0x06 );
				ushort area = (ushort)(areaAndFlags & 0x01FF);
				ushort flags = (ushort)((areaAndFlags >> 9) & 0x007F);
				short bboxMinX = BitConverter.ToInt16( leafs, idx_5 + 0x08 );
				short bboxMinY = BitConverter.ToInt16( leafs, idx_5 + 0x0A );
				short bboxMinZ = BitConverter.ToInt16( leafs, idx_5 + 0x0C );
				short bboxMaxX = BitConverter.ToInt16( leafs, idx_5 + 0x0E );
				short bboxMaxY = BitConverter.ToInt16( leafs, idx_5 + 0x10 );
				short bboxMaxZ = BitConverter.ToInt16( leafs, idx_5 + 0x12 );
				BBox bbox = new BBox( new Vector3( bboxMinX, bboxMinY, bboxMinZ ), new Vector3( bboxMaxX, bboxMaxY, bboxMaxZ ) );
				ushort firstleafface = BitConverter.ToUInt16( leafs, idx_5 + 0x14 );
				ushort numleaffaces = BitConverter.ToUInt16( leafs, idx_5 + 0x16 );
				ushort firstleafbrush = BitConverter.ToUInt16( leafs, idx_5 + 0x18 );
				ushort numleafbrushes = BitConverter.ToUInt16( leafs, idx_5 + 0x1A );
				short leafwaterdata_1 = BitConverter.ToInt16( leafs, idx_5 + 0x1C );
				int leafindex = this.leaflist.Count;

				idx_5 += 0x1E;

				List<BSPLeafAmbientSample> ambientLightSamples = new List<BSPLeafAmbientSample>();
				if ( leafsLump.Item2 == 0 )
				{
					// We only have one ambient cube sample, in the middle of the leaf.
					Color[] ambientCube = new Color[6];

					for ( int j = 0; j < 6; j++ )
					{
						byte exp = leafs[idx_5 + 0x03];
						// Game seems to accidentally include an extra factor of 255.0.
						float r = Materials.UnpackColorRGBExp32( leafs[idx_5 + 0x00], exp ) * 255.0f;
						float g = Materials.UnpackColorRGBExp32( leafs[idx_5 + 0x01], exp ) * 255.0f;
						float b = Materials.UnpackColorRGBExp32( leafs[idx_5 + 0x02], exp ) * 255.0f;
						ambientCube[j] = ColorNewFromRGBA( r, g, b );
						idx_5 += 0x04;
					}

					float x = MathX.Lerp( bboxMinX, bboxMaxX, 0.5f );
					float y = MathX.Lerp( bboxMinY, bboxMaxY, 0.5f );
					float z = MathX.Lerp( bboxMinZ, bboxMaxZ, 0.5f );
					Vector3 pos = new Vector3( x, y, z );

					ambientLightSamples.Add( new() { ambientCube = ambientCube, pos = pos } );


					// Padding.
					idx_5 += 0x02;
				}
				else if ( leafambientindex.Length == 0 )
				{
					// Intermediate leafambient version.
					//Debug.Assert( leafambientlighting.Length != 0 );
					if ( leafambientlighting.Length == 0 )
					{
						//HATA
						return;
					}
					//Debug.Assert( leafambientlightingVersion != 1 );
					if ( leafambientlightingVersion == 1 )
					{
						//HATA
						return;
					}

					// We only have one ambient cube sample, in the middle of the leaf.
					List<Color> ambientCube = new List<Color>();

					for ( int j = 0; j < 6; j++ )
					{
						int ambientSampleColorIdx = (i * 6 + j) * 0x04;
						byte exp = leafambientlighting[ambientSampleColorIdx + 0x03];
						float r = Materials.UnpackColorRGBExp32( leafambientlighting[ambientSampleColorIdx + 0x00], exp ) * 255.0f;
						float g = Materials.UnpackColorRGBExp32( leafambientlighting[ambientSampleColorIdx + 0x01], exp ) * 255.0f;
						float b = Materials.UnpackColorRGBExp32( leafambientlighting[ambientSampleColorIdx + 0x02], exp ) * 255.0f;
						ambientCube.Add( new Color( r / 255.0f, g / 255.0f, b / 255.0f ) );
					}

					float x = MathX.Lerp( bboxMinX, bboxMaxX, 0.5f );
					float y = MathX.Lerp( bboxMinY, bboxMaxY, 0.5f );
					float z = MathX.Lerp( bboxMinZ, bboxMaxZ, 0.5f );
					Vector3 pos = new Vector3( x, y, z );

					ambientLightSamples.Add( new BSPLeafAmbientSample() { ambientCube = ambientCube.ToArray(), pos = pos } );

					// Padding.
					idx_5 += 0x02;
				}
				else
				{
					//Debug.Assert( leafambientlightingVersion == 1 );
					if ( leafambientlightingVersion != 1 )
					{
						//HATA
						return;
					}
					ushort ambientSampleCount = BitConverter.ToUInt16( leafambientindex, leafindex * 0x04 + 0x00 );
					ushort firstAmbientSample = BitConverter.ToUInt16( leafambientindex, leafindex * 0x04 + 0x02 );
					for ( int i_1 = 0; i_1 < ambientSampleCount; i_1++ )
					{
						int ambientSampleOffs = (firstAmbientSample + i_1) * 0x1C;

						// Ambient cube samples
						List<Color> ambientCube = new List<Color>();
						int ambientSampleColorIdx = ambientSampleOffs;
						for ( int j = 0; j < 6; j++ )
						{
							byte exp = leafambientlighting[ambientSampleColorIdx + 0x03];
							float r = Materials.UnpackColorRGBExp32( leafambientlighting[ambientSampleColorIdx + 0x00], exp ) * 255.0f;
							float g = Materials.UnpackColorRGBExp32( leafambientlighting[ambientSampleColorIdx + 0x01], exp ) * 255.0f;
							float b = Materials.UnpackColorRGBExp32( leafambientlighting[ambientSampleColorIdx + 0x02], exp ) * 255.0f;
							ambientCube.Add( new Color( r / 255.0f, g / 255.0f, b / 255.0f ) );
							ambientSampleColorIdx += 0x04;
						}

						// Fraction of bbox.
						float xf = leafambientlighting[ambientSampleOffs + 0x18] / 255f;
						float yf = leafambientlighting[ambientSampleOffs + 0x19] / 255f;
						float zf = leafambientlighting[ambientSampleOffs + 0x1A] / 255f;

						float x = MathX.Lerp( bboxMinX, bboxMaxX, xf );
						float y = MathX.Lerp( bboxMinY, bboxMaxY, yf );
						float z = MathX.Lerp( bboxMinZ, bboxMaxZ, zf );
						Vector3 pos = new Vector3( x, y, z );

						ambientLightSamples.Add( new BSPLeafAmbientSample { ambientCube = ambientCube.ToArray(), pos = pos } );

					}

					// Padding.
					idx_5 += 0x02;
				}

				ushort[] leafFaces = new ushort[numleaffaces];
				for ( int i3 = 0; i3 < numleaffaces; i3++ )
				{
					leafFaces[i3] = leaffacelist[firstleafface + i3];
				}

				var leaf = new BSPLeaf
				{
					bbox = bbox,
					cluster = cluster,
					area = area,
					ambientLightSamples = ambientLightSamples.ToArray(),
					faces = leafFaces.Select( x => (int)x ).ToArray(),
					surfaces = new int[0],
					leafwaterdata = leafwaterdata_1,
					contents = (BSPLeafContents)(int)contents
				};

				this.leaflist.Add( leaf );

				var leafidx = leaflist.Count - 1;
				for ( var i_2 = 0; i_2 < numleaffaces; i_2++ )
				{
					faceToLeafIdx[leafFaces[i_2]].Add( leafidx );
				}

			}



			// Sort faces by texinfo to prepare for splitting into surfaces.
			//faces.Sort( ( a, b ) => texinfoa[a.Texinfo].texName.CompareTo( texinfoa[b.Texinfo].texName ) ); // hallet

			FaceToSurfaceInfo[] faceToSurfaceInfo = new FaceToSurfaceInfo[numfaces];

			for ( int i = 0; i < numfaces; i++ )
			{
				faceToSurfaceInfo[i] = new FaceToSurfaceInfo();
			}

			List<byte> vertexBuffer = new(); //new ResizableArrayBuffer();
			List<byte> indexBuffer = new();//new ResizableArrayBuffer();

			byte[] vertexesBytes = GetLumpData( LumpType.VERTEXES );
			float[] vertexes = new float[vertexesBytes.Length / sizeof( float )];
			for ( int i = 0; i < vertexes.Length; i++ )
			{
				vertexes[i] = BitConverter.ToSingle( vertexesBytes, i * sizeof( float ) );
			}


			byte[] vertnormalsBytes = GetLumpData( LumpType.VERTNORMALS );
			float[] vertnormals = new float[vertnormalsBytes.Length / sizeof( float )];
			for ( int i = 0; i < vertnormals.Length; i++ )
			{
				vertnormals[i] = BitConverter.ToSingle( vertnormalsBytes, i * sizeof( float ) );
			}

			byte[] vertnormalindicesBytes = GetLumpData( LumpType.VERTNORMALINDICES );
			ushort[] vertnormalindices = new ushort[vertnormalindicesBytes.Length / sizeof( ushort )];
			for ( int i = 0; i < vertnormalindices.Length; i++ )
			{
				vertnormalindices[i] = BitConverter.ToUInt16( vertnormalindicesBytes, i * sizeof( ushort ) );
			}

			byte[] disp_vertsBytes = GetLumpData( LumpType.DISP_VERTS );
			float[] disp_verts = new float[disp_vertsBytes.Length / sizeof( float )];
			for ( int i = 0; i < disp_verts.Length; i++ )
			{
				disp_verts[i] = BitConverter.ToSingle( disp_vertsBytes, i * sizeof( float ) );
			}

			Vector3 scratchTangentS = new Vector3();
			Vector3 scratchTangentT = new Vector3();

			int dstOffsIndex = 0;
			int dstIndexBase = 0;

			Surface? mergeSurface = null;


			for ( int i = 0; i < faces.Count(); i++ )
			{
				var face = faces[i];

				// For s&box
				vertexList = new();
				indexList = new();

				var tex = texinfoa[face.Texinfo];
				var texName = tex.texName;

				//int[] indexData = new int[0];

				var isTranslucent = (tex.flags & TexinfoFlags.TRANS) != 0;
				//Vector3 center = isTranslucent ? Vector3.Zero : null;
				Vector3? center = null;

				if ( isTranslucent )
					center = Vector3.Zero;

				// Determine if we can merge with the previous surface for output.
				mergeSurface = null;
				if ( i > 0 )
				{
					var prevFace = faces[i - 1];
					bool canMerge = true;

					// Translucent surfaces require a sort, so they can't be merged.
					if ( isTranslucent )
						canMerge = false;
					else if ( texinfoa[prevFace.Texinfo].texName != texName )
						canMerge = false;
					else if ( prevFace.LightmapData.pageIndex != face.LightmapData.pageIndex )
						canMerge = false;
					else if ( faceToModelIndex[prevFace.Index] != faceToModelIndex[face.Index] )
						canMerge = false;

					if ( canMerge )
						mergeSurface = this.surfaces.LastOrDefault();//this.surfaces[this.surfaces.Count - 1];
				}

				var idx_1 = face.Index * 0x38;
				var side = facelist[idx_1 + 0x02];
				var onNode = facelist[idx_1 + 0x03] != 0;
				var firstedge = BitConverter.ToUInt32( facelist, idx_1 + 0x04 );
				var numedges = BitConverter.ToUInt16( facelist, idx_1 + 0x08 );
				var dispinfo1 = BitConverter.ToInt16( facelist, idx_1 + 0x0C );
				var surfaceFogVolumeID = BitConverter.ToUInt16( facelist, idx_1 + 0x0E );
				var area = BitConverter.ToSingle( facelist, idx_1 + 0x18 );
				var m_LightmapTextureMinsInLuxels = new int[2] { BitConverter.ToInt32( facelist, idx_1 + 0x1C ), BitConverter.ToInt32( facelist, idx_1 + 0x20 ) };
				var m_LightmapTextureSizeInLuxels = new uint[2] { BitConverter.ToUInt32( facelist, idx_1 + 0x24 ), BitConverter.ToUInt32( facelist, idx_1 + 0x28 ) };
				var origFace = BitConverter.ToUInt32( facelist, idx_1 + 0x2C );
				var m_NumPrimsRaw = BitConverter.ToUInt16( facelist, idx_1 + 0x30 );
				var m_NumPrims = (ushort)(m_NumPrimsRaw & 0x7FFF);
				var firstPrimID = BitConverter.ToUInt16( facelist, idx_1 + 0x32 );
				var smoothingGroups = BitConverter.ToUInt32( facelist, idx_1 + 0x34 );


				// Tangent space setup.
				scratchTangentS = new Vector3( tex.textureMapping.s.x, tex.textureMapping.s.y, tex.textureMapping.s.z );
				scratchTangentS = scratchTangentS.Normal;
				scratchTangentT = new Vector3( tex.textureMapping.t.x, tex.textureMapping.t.y, tex.textureMapping.t.z );
				scratchTangentT = scratchTangentT.Normal;

				var scratchNormal = scratchTangentS; // reuse
				scratchNormal = Vector3.Cross( scratchTangentS, scratchTangentT );

				// Detect if we need to flip tangents.
				float tangentSSign = Vector3.Dot( face.Plane, scratchNormal ) > 0.0f ? -1.0f : 1.0f;

				SurfaceLightmapData lightmapData = face.LightmapData;
				int lightmapPackerPageIndex = lightmapData.pageIndex;
				LightmapPackerPage lightmapPage = this.lightmapPacker.pages[lightmapData.pageIndex];

				float tangentW = tangentSSign;

				// World surfaces always want the texcoord0 scale.
				bool wantsTexCoord0Scale = true;

				FaceToSurfaceInfo unmergedFaceInfo = faceToSurfaceInfo[face.Index];
				unmergedFaceInfo.startIndex = dstOffsIndex;

				int indexCount = 0;
				int vertexCount = 0;
				Surface surface = new();

				// vertex data
				if ( dispinfo1 >= 0 )
				{
					// Build displacement data.
					var disp = dispinfolist[dispinfo1];

					//Debug.Assert( numedges == 4 );
					if( numedges != 4 )
					{
						//HATA
						return;
					}

					// Load the four corner vertices.
					var corners = new List<Vector3>();
					var startDist = float.PositiveInfinity;
					var startIndex = -1;
					for ( var j = 0; j < 4; j++ )
					{
						var vertIndex = vertindices[firstedge + j];
						var corner = new Vector3( vertexes[vertIndex * 3 + 0], vertexes[vertIndex * 3 + 1], vertexes[vertIndex * 3 + 2] );
						corners.Add( corner );
						var dist = corner.Distance( disp.startPos );
						if ( dist < startDist )
						{
							startIndex = j;
							startDist = dist;
						}
					}
					//Debug.Assert( startIndex >= 0 );
					if ( startIndex < 0 )
					{
						//HATA
						return;
					}

					// Rotate vectors so start pos corner is first
					if ( startIndex != 0 )
						corners = corners.Skip( startIndex ).Concat( corners.Take( startIndex ) ).ToList();

					var result = BuildDisplacement( disp, corners, disp_verts, tex.textureMapping );

					foreach ( var v in result.vertex )
					{
						// Put lightmap UVs in luxel space.
						v.lightmapUV.x = v.lightmapUV.x * m_LightmapTextureSizeInLuxels[0] + 0.5f;
						v.lightmapUV.y = v.lightmapUV.y * m_LightmapTextureSizeInLuxels[1] + 0.5f;

						CalcLightmapTexcoords( ref v.lightmapUV, v.lightmapUV, lightmapData, lightmapPage );
					}

					addVertexDataToBuffer( ref vertexBuffer, ref scratchTangentS, ref scratchTangentT, result.vertex, tex, ref center, tangentW );

					// Build grid index buffer.
					int[] indexDataTemp = new int[((disp.sideLength - 1) * (disp.sideLength - 1)) * 6];//indexBuffer.AddInt32( ((disp.sideLength - 1) * (disp.sideLength - 1)) * 6 );

					for ( int y = 0; y < disp.sideLength - 1; y++ )
					{
						for ( int x = 0; x < disp.sideLength - 1; x++ )
						{
							var baseIndex = dstIndexBase + y * disp.sideLength + x;
							indexDataTemp[indexCount++] = baseIndex;
							indexDataTemp[indexCount++] = (baseIndex + disp.sideLength);
							indexDataTemp[indexCount++] = (baseIndex + disp.sideLength + 1);
							indexDataTemp[indexCount++] = baseIndex;
							indexDataTemp[indexCount++] = (baseIndex + disp.sideLength + 1);
							indexDataTemp[indexCount++] = (baseIndex + 1);
						}
					}
					foreach ( int value in indexDataTemp )
					{
						byte[] valueBytes = BitConverter.GetBytes( value );
						indexBuffer.AddRange( valueBytes );
					}

					//indexBuffer.AddRange( Array.ConvertAll( indexDataTemp, Convert.ToByte ) );
					//indexBuffer.FinishAddInt32(  indexDataTemp );







					//int dstOffsIndexTemp = dstOffsIndex * 2;
					//dstOffsIndexTemp /= 2;
					var dstOffsIndexTemp = 0;
					for ( int k = 0; k < indexCount; k++ )
					{
						if ( indexList.Count >= dstOffsIndexTemp + k ) // verify
							indexList.Insert( dstOffsIndexTemp + k, indexDataTemp[dstOffsIndexTemp++] - dstIndexBase );
						else
							indexList.Add( indexDataTemp[dstOffsIndexTemp++] - dstIndexBase );
					}








					//Debug.Assert( indexCount == ((disp.sideLength - 1) * (disp.sideLength - 1)) * 6 );
					if ( indexCount != ((disp.sideLength - 1) * (disp.sideLength - 1)) * 6 )
					{
						//HATA
						return;
					}
					
					// TODO: Merge disps
					var _surface = new Surface()
					{
						texName = texName,
						onNode = onNode,
						startIndex = dstOffsIndex,
						indexCount = indexCount,
						center = center,
						wantsTexCoord0Scale = true,
						lightmapData = new(),
						lightmapPackerPageIndex = lightmapData.pageIndex,
						bbox = result.bbox
					};

					this.surfaces.Add( _surface );
					_surface.lightmapData.Add( lightmapData );

					vertexCount = disp.vertexCount;

				}
				else
				{
					BBox bbox = new BBox();
					MeshVertex[] vertex = new MeshVertex[numedges];
					for ( int j = 0; j < numedges; j++ )
					{
						MeshVertex v = vertex[j] = new MeshVertex();

						// Position
						uint vertIndex = vertindices[firstedge + j];
						v.position.x = vertexes[vertIndex * 3 + 0];
						v.position.y = vertexes[vertIndex * 3 + 1];
						v.position.z = vertexes[vertIndex * 3 + 2];
						UnionPoint( ref bbox, v.position );

						// Normal
						int vertnormalBase = face.VertnormalBase;
						int normIndex = vertnormalindices[vertnormalBase + j];
						v.normal.x = vertnormals[normIndex * 3 + 0];
						v.normal.y = vertnormals[normIndex * 3 + 1];
						v.normal.z = vertnormals[normIndex * 3 + 2];

						// Alpha (Unused)
						v.alpha = 1.0f;

						// Texture Coordinates
						CalcTexCoord( ref v.uv, v.position, tex.textureMapping );

						// Lightmap coordinates from the lightmap mapping
						CalcTexCoord( ref v.lightmapUV, v.position, tex.lightmapMapping );
						v.lightmapUV.x += 0.5f - m_LightmapTextureMinsInLuxels[0];
						v.lightmapUV.y += 0.5f - m_LightmapTextureMinsInLuxels[1];

						CalcLightmapTexcoords( ref v.lightmapUV, v.lightmapUV, lightmapData, lightmapPage );

						//Log.Info( v.position.x + " "  + v.position.y + " " + v.position.z );
						//Log.Info( vertIndex + " " + firstedge  + " " + vertindices.Count());
					}

					//foreach ( var asd in vertex )
					//	Log.Info( asd.position );

					addVertexDataToBuffer( ref vertexBuffer, ref scratchTangentS, ref scratchTangentT, vertex, tex, ref center, tangentW );

					// index buffer
					indexCount = TopologyHelper.GetTriangleIndexCountForTopologyIndexCount( GfxTopology.TriFans, ref numedges );
					int[] indexDataTemp = new int[indexCount];//Array.ConvertAll( indexBuffer.AddUint32( indexCount ), Convert.ToInt32 ); // verify

					if ( m_NumPrims != 0 )
					{
						byte primType;
						uint primFirstIndex, primIndexCount;
						ushort primFirstVert, primVertCount;
						if ( this.version == 22 )
						{
							int primOffs = firstPrimID * 0x10;
							primType = primitives[primOffs];
							primFirstIndex = BitConverter.ToUInt32( primitives, primOffs + 0x04 );
							primIndexCount = BitConverter.ToUInt32( primitives, primOffs + 0x08 );
							primFirstVert = BitConverter.ToUInt16( primitives, primOffs + 0x0C );
							primVertCount = BitConverter.ToUInt16( primitives, primOffs + 0x0E );
						}
						else
						{
							var primOffs = firstPrimID * 0x0A;
							primType = primitives[primOffs];
							primFirstIndex = BitConverter.ToUInt16( primitives, primOffs + 0x02 );
							primIndexCount = BitConverter.ToUInt16( primitives, primOffs + 0x04 );
							primFirstVert = BitConverter.ToUInt16( primitives, primOffs + 0x06 );
							primVertCount = BitConverter.ToUInt16( primitives, primOffs + 0x08 );
						}

						if ( primVertCount != 0 )
						{
							// Dynamic mesh. Skip for now.
							continue;
						}

						// We should be in static mode, so we should have 1 prim maximum.
						//Debug.Assert( m_NumPrims == 1 );
						if( m_NumPrims != 1 )
						{
							//HATA
							return;
						}
						//Debug.Assert( primIndexCount == indexCount );
						if ( primIndexCount != indexCount )
						{
							//HATA
							return;
						}
						//Debug.Assert( primType == 0x00 /* PRIM_TRILIST */);
						if ( primType != 0x00 /* PRIM_TRILIST */ )
						{
							//HATA
							return;
						}

						for ( int k = 0; k < indexCount; k++ )
							indexDataTemp[k] = (dstIndexBase + primindices[primFirstIndex + k]); //(uint) dstOffsIndex +

						//Log.Info( indexDataTemp.Count() + " " + indexCount );

						foreach ( int value in indexDataTemp )
						{
							/*if ( value == 661 )
							{
								Log.Info( "burasi1:  " + i );
							}*/
							byte[] valueBytes = BitConverter.GetBytes( value );
							indexBuffer.AddRange( valueBytes );
						}

						//indexBuffer.AddRange( Array.ConvertAll( indexDataTemp, Convert.ToByte ) );
						//indexBuffer.FinishAddUint32( Array.ConvertAll( indexDataTemp, Convert.ToUInt32 ) );
						/*if ( i == 72 )
							Log.Info( "we1" );*/
					}
					else
					{
						TopologyHelper.ConvertToTrianglesRange( ref indexDataTemp, 0, GfxTopology.TriFans, dstIndexBase, numedges );

						foreach ( int value in indexDataTemp )
						{

							/*if( value == 661 )
							{
								Log.Info( "burasi2:  " + i );
							}*/
							byte[] valueBytes = BitConverter.GetBytes( value );
							indexBuffer.AddRange( valueBytes );
						}

						/*if ( i == 72 )
							Log.Info( "we2" );*/

						//indexBuffer.AddRange( Array.ConvertAll( indexDataTemp, Convert.ToByte ) );
						//indexBuffer.FinishAddUint32( Array.ConvertAll( indexDataTemp, Convert.ToUInt32 ) );
					}





					//int dstOffsIndexTemp = dstOffsIndex * 2;
					//dstOffsIndexTemp /= 2;
					int dstOffsIndexTemp = 0;
					//var bulog = "";
					for ( int k = 0; k < indexCount; k++ )
					{
						//Log.Info( indexDataTemp.Count() + " " +  dstOffsIndexTemp  + " "+ indexCount );

						/*if ( i == 72 )
							bulog += (indexDataTemp[dstOffsIndexTemp]) + ", ";*/

						if ( indexList.Count > dstOffsIndexTemp + k ) // verify
							indexList.Insert( dstOffsIndexTemp + k, indexDataTemp[dstOffsIndexTemp++] - dstIndexBase );
						else
							indexList.Add( indexDataTemp[dstOffsIndexTemp++] - dstIndexBase );

						
					}
					/*if ( i == 72 )
						Log.Info( "bu 72: " + bulog );*/




					if ( mergeSurface is not null )
						surface = mergeSurface.Value;

					if ( mergeSurface is null )
					{
						surface = new Surface{ texName = texName, onNode = onNode, startIndex = dstOffsIndex, indexCount = 0, center = center, wantsTexCoord0Scale = wantsTexCoord0Scale, lightmapData = new(), lightmapPackerPageIndex = lightmapPackerPageIndex, bbox = bbox };
						this.surfaces.Add( surface );
					}
					else
					{
						Util.Union( ref surface.bbox, surface.bbox, bbox );
					}

					surface.indexCount += indexCount;
					surface.lightmapData.Add( lightmapData );

					vertexCount = numedges;

				}

				unmergedFaceInfo.lightmapData = lightmapData;
				unmergedFaceInfo.indexCount = indexCount;


				/*int dstOffsIndexTemp = dstOffsIndex * 2;
				dstOffsIndexTemp /= 2;
				//Log.Info( "indexcount: " + indexCount );
				for ( int k = 0; k < indexCount; k++ )
				{

					//Log.Info( indexData.Count() + "  " + dstOffsIndex  + " " + indexData[dstOffsIndex++] + " "+ (dstOffsIndex + k)  + " "+indexList.Count);
					if(indexList.Count >= dstOffsIndexTemp + k ) // verify
						indexList.Insert( dstOffsIndexTemp + k, indexData[dstOffsIndexTemp++] - dstIndexBase);
					else
						indexList.Add( indexData[dstOffsIndexTemp++] - dstIndexBase );
					//indexList.Insert( dst_temp++, indexData[dstoffset_temp++] - dstIndexBase );
				}*/

				//dstOffsIndex = (int)dstOffsIndexTemp;

				dstOffsIndex += indexCount;
				dstIndexBase += vertexCount;

				// Mark surfaces as part of the right model.
				int surfaceIndex = this.surfaces.Count - 1;

				var modelSurfaces = this.models[faceToModelIndex[face.Index]].surfaces; // verify
				ensureInList( ref modelSurfaces, surfaceIndex );

				List<int> faceleaflist = faceToLeafIdx[face.Index];
				if ( dispinfo1 >= 0 )
				{
					// Displacements don't come with surface leaf information.
					// Use the bbox to mark ourselves in the proper leaves...
					if ( faceleaflist.Count > 0 )
					{
						throw new Exception( "faceleaflist should be empty." );
					}
					this.MarkLeafSet( faceleaflist, surface.bbox );
				}

				AddSurfaceToLeaves( faceleaflist, face.Index, surfaceIndex );

				indexList.Reverse(); // Fix mesh backside problem

				/*if( i == 0)
				{ 
					var asd = "bspden: ";
					for ( int k = 0; k < indexCount; k++ )
					{

						asd += indexList[k]+", ";
					}

					Log.Info( asd );
				}*/


				/*if ( (vertexList.Count() >= 3) && ((vertexList.Count() - 2) * 3) != indexList.Count )
					Log.Info( "patladik: " +i + " " + faces.Count() + " " + indexCount);
				else*/
				meshDataList.Add( new meshData() { vertices = vertexList, indices = indexList, faceIndex = i, textureName = texName } );


			}


			// Slice up overlays
			byte[] overlays = GetLumpData( LumpType.OVERLAYS );
			int idx = 0;
			for ( int i = 0; idx < overlays.Length; i++ )
			{
				uint nId = BitConverter.ToUInt32( overlays, idx );
				ushort nTexinfo = BitConverter.ToUInt16( overlays, idx + 0x04 );
				ushort m_nFaceCountAndRenderOrder = BitConverter.ToUInt16( overlays, idx + 0x06 );
				ushort m_nFaceCount = (ushort)(m_nFaceCountAndRenderOrder & 0x3FFF);
				ushort m_nRenderOrder = (ushort)(m_nFaceCountAndRenderOrder >> 14);
				idx += 0x08;

				OverlayInfo overlayInfo = new OverlayInfo();
				overlayInfo.faces = Enumerable.Range( 0, m_nFaceCount )
					.Select( x => BitConverter.ToInt32( overlays, idx + 0x04 * x ) )
					.ToArray();
				idx += 0x100;

				overlayInfo.u0 = BitConverter.ToSingle( overlays, idx + 0x00 );
				overlayInfo.u1 = BitConverter.ToSingle( overlays, idx + 0x04 );
				overlayInfo.v0 = BitConverter.ToSingle( overlays, idx + 0x08 );
				overlayInfo.v1 = BitConverter.ToSingle( overlays, idx + 0x0C );

				float vecUVPoint0X = BitConverter.ToSingle( overlays, idx + 0x10 );
				float vecUVPoint0Y = BitConverter.ToSingle( overlays, idx + 0x14 );
				float vecUVPoint0Z = BitConverter.ToSingle( overlays, idx + 0x18 );
				float vecUVPoint1X = BitConverter.ToSingle( overlays, idx + 0x1C );
				float vecUVPoint1Y = BitConverter.ToSingle( overlays, idx + 0x20 );
				float vecUVPoint1Z = BitConverter.ToSingle( overlays, idx + 0x24 );
				float vecUVPoint2X = BitConverter.ToSingle( overlays, idx + 0x28 );
				float vecUVPoint2Y = BitConverter.ToSingle( overlays, idx + 0x2C );
				float vecUVPoint2Z = BitConverter.ToSingle( overlays, idx + 0x30 );
				float vecUVPoint3X = BitConverter.ToSingle( overlays, idx + 0x34 );
				float vecUVPoint3Y = BitConverter.ToSingle( overlays, idx + 0x38 );
				float vecUVPoint3Z = BitConverter.ToSingle( overlays, idx + 0x3C );
				idx += 0x40;

				overlayInfo.origin[0] = BitConverter.ToSingle( overlays, idx + 0x00 );
				overlayInfo.origin[1] = BitConverter.ToSingle( overlays, idx + 0x04 );
				overlayInfo.origin[2] = BitConverter.ToSingle( overlays, idx + 0x08 );
				idx += 0x0C;

				overlayInfo.normal[0] = BitConverter.ToSingle( overlays, idx + 0x00 );
				overlayInfo.normal[1] = BitConverter.ToSingle( overlays, idx + 0x04 );
				overlayInfo.normal[2] = BitConverter.ToSingle( overlays, idx + 0x08 );
				idx += 0x0C;

				// Basis normal 0 is encoded in Z of vecUVPoint data.
				overlayInfo.basis[0] = new Vector3( vecUVPoint0Z, vecUVPoint1Z, vecUVPoint2Z );
				overlayInfo.normal = Vector3.Cross( overlayInfo.basis[0], overlayInfo.basis[1] );

				overlayInfo.planePoints[0] = new Vector2( vecUVPoint0X, vecUVPoint0Y );
				overlayInfo.planePoints[1] = new Vector2( vecUVPoint1X, vecUVPoint1Y );
				overlayInfo.planePoints[2] = new Vector2( vecUVPoint2X, vecUVPoint2Y );
				overlayInfo.planePoints[3] = new Vector2( vecUVPoint3X, vecUVPoint3Y );

				Vector3? center = Vector3.Zero;
				Texinfo tex = texinfoa[nTexinfo];

				List<int> surfaceIndexes = new List<int>();

				OverlayResult overlayResult = BuildOverlay( overlayInfo, faceToSurfaceInfo, Array.ConvertAll( indexBuffer.ToArray(), Convert.ToUInt32 ), Array.ConvertAll( vertexBuffer.ToArray(), Convert.ToSingle ) ); //  //Enumerable.Range( 0, m_nFaceCount ).Select( x => BitConverter.ToInt32( overlays, idx + 0x04 * x ) ).ToArray();
				for ( int j = 0; j < overlayResult.surfaces.Count; j++ )
				{
					OverlaySurface overlaySurface = overlayResult.surfaces[j];

					// Don't care about tangentS of decals right now...
					float tangentW = 1.0f;

					var overlaySurfaceList = overlaySurface.vertex.ToArray(); // verify
					addVertexDataToBuffer( ref vertexBuffer, ref scratchTangentS, ref scratchTangentT, overlaySurfaceList, tex, ref center, tangentW );

					overlaySurface.vertex = overlaySurfaceList.ToList();

					int vertexCount = overlaySurface.vertex.Count;
					int indexCount = overlaySurface.indices.Count;

					int startIndex = dstOffsIndex;
					var dstOffsIndexTemp = 0;
					uint[] indexData = new uint[overlaySurface.indices.Count];//indexBuffer.AddUint32( overlaySurface.indices.Count );
					for ( int n = 0; n < overlaySurface.indices.Count; n++ )
						indexData[dstOffsIndexTemp++] = (uint)(dstIndexBase + overlaySurface.indices[n]);
					foreach ( uint value in indexData )
					{

						byte[] valueBytes = BitConverter.GetBytes( value );
						indexBuffer.AddRange( valueBytes );
					}
					//indexBuffer.AddRange( Array.ConvertAll( indexData, Convert.ToByte ) );
					//indexBuffer.FinishAddUint32( indexData );
					dstIndexBase += vertexCount;

					string texName = tex.texName;
					Surface surface = new Surface
					{
						texName = texName,
						onNode = false,
						startIndex = startIndex,
						indexCount = indexCount,
						center = center,
						wantsTexCoord0Scale = false,
						lightmapData = new(),
						lightmapPackerPageIndex = 0,
						bbox = overlayResult.bbox
					};

					int surfaceIndex = this.surfaces.Count;
					this.surfaces.Add( surface );
					// Currently, overlays are part of the first model. We need to track origin surfaces / models if this differs...
					this.models[0].surfaces.Add( surfaceIndex );
					surfaceIndexes.Add( surfaceIndex );

					// For each overlay surface, push it to the right leaf.
					for ( int n = 0; n < overlaySurface.originFaceList.Count; n++ )
					{
						List<int> surfleaflist = faceToLeafIdx[overlaySurface.originFaceList[n]];
						//Debug.Assert( surfleaflist.Count > 0 );
						if ( surfleaflist.Count <= 0 )
						{
							//HATA
							return;
						}
						AddSurfaceToLeaves( surfleaflist, null, surfaceIndex );
					}
				}

				this.overlays.Add( new Overlay { surfaceIndexes = surfaceIndexes.ToArray() } );
			}


			vertexData = vertexBuffer.ToArray();//.Finalize();
			indexData = indexBuffer.ToArray();//.Finalize();
			//Log.Info( "" );
			//var asd1 = "";
			
			// Bunu silme

			/*for ( var asd = 0; asd < indexData.Count()/4; asd++ )
				asd1 += BitConverter.ToInt32(indexData, asd * 4) + ",";

			Log.Info( asd1 );*/

			byte[] cubemaps = GetLumpData( LumpType.CUBEMAPS );
			string cubemapHDRSuffix = usingHDR ? ".hdr" : string.Empty;
			for ( int idx2 = 0x00; idx2 < cubemaps.Length; idx2 += 0x10 )
			{
				int posX = BitConverter.ToInt32( cubemaps, idx2 + 0x00 );
				int posY = BitConverter.ToInt32( cubemaps, idx2 + 0x04 );
				int posZ = BitConverter.ToInt32( cubemaps, idx2 + 0x08 );
				Vector3 pos = new Vector3( posX, posY, posZ );
				string filename = $"maps/{mapname}/c{posX}_{posY}_{posZ}{cubemapHDRSuffix}";
				this.cubemaps.Add( new Cubemap { pos = pos, filename = filename } );
			}

			byte[] worldlightsLump = null;
			uint worldlightsVersion = 0;
			bool worldlightsIsHDR = false;

			if ( usingHDR )
			{
				(worldlightsLump, worldlightsVersion) = GetLumpDataEx( LumpType.WORLDLIGHTS_HDR );
				worldlightsIsHDR = true;
			}
			if ( worldlightsLump == null || worldlightsLump.Length == 0 )
			{
				(worldlightsLump, worldlightsVersion) = GetLumpDataEx( LumpType.WORLDLIGHTS );
				worldlightsIsHDR = false;
			}
			byte[] worldlights = worldlightsLump;

			for ( int i = 0, idx3 = 0x00; idx3 < worldlights.Length; i++, idx3 += 0x58 )
			{
				float posX = BitConverter.ToSingle( worldlights, idx3 + 0x00 );
				float posY = BitConverter.ToSingle( worldlights, idx3 + 0x04 );
				float posZ = BitConverter.ToSingle( worldlights, idx3 + 0x08 );
				float intensityX = BitConverter.ToSingle( worldlights, idx3 + 0x0C );
				float intensityY = BitConverter.ToSingle( worldlights, idx3 + 0x10 );
				float intensityZ = BitConverter.ToSingle( worldlights, idx3 + 0x14 );
				float normalX = BitConverter.ToSingle( worldlights, idx3 + 0x18 );
				float normalY = BitConverter.ToSingle( worldlights, idx3 + 0x1C );
				float normalZ = BitConverter.ToSingle( worldlights, idx3 + 0x20 );
				float shadow_cast_offsetX = 0;
				float shadow_cast_offsetY = 0;
				float shadow_cast_offsetZ = 0;
				if ( worldlightsVersion == 1 )
				{
					shadow_cast_offsetX = BitConverter.ToSingle( worldlights, idx3 + 0x24 );
					shadow_cast_offsetY = BitConverter.ToSingle( worldlights, idx3 + 0x28 );
					shadow_cast_offsetZ = BitConverter.ToSingle( worldlights, idx3 + 0x2C );
					idx3 += 0x0C;
				}
				uint cluster = BitConverter.ToUInt32( worldlights, idx3 + 0x24 );
				WorldLightType type = (WorldLightType)BitConverter.ToUInt32( worldlights, idx3 + 0x28 );
				uint style = BitConverter.ToUInt32( worldlights, idx3 + 0x2C );
				// cone angles for spotlights
				float stopdot = BitConverter.ToSingle( worldlights, idx3 + 0x30 );
				float stopdot2 = BitConverter.ToSingle( worldlights, idx3 + 0x34 );
				float exponent = BitConverter.ToSingle( worldlights, idx3 + 0x38 );
				float radius = BitConverter.ToSingle( worldlights, idx3 + 0x3C );
				float constant_attn = BitConverter.ToSingle( worldlights, idx3 + 0x40 );
				float linear_attn = BitConverter.ToSingle( worldlights, idx3 + 0x44 );
				float quadratic_attn = BitConverter.ToSingle( worldlights, idx3 + 0x48 );
				WorldLightFlags flags = (WorldLightFlags)BitConverter.ToUInt32( worldlights, idx3 + 0x4C );
				uint texinfoUnused = BitConverter.ToUInt32( worldlights, idx3 + 0x50 );
				uint owner = BitConverter.ToUInt32( worldlights, idx3 + 0x54 );

				// Fixups for old data.
				if ( quadratic_attn == 0.0f && linear_attn == 0.0f && constant_attn == 0.0f && (type == WorldLightType.Point || type == WorldLightType.Spotlight) )
					quadratic_attn = 1.0f;

				if ( exponent == 0.0f && type == WorldLightType.Point )
					exponent = 1.0f;

				Vector3 pos = new Vector3( posX, posY, posZ );
				Vector3 intensity = new Vector3( intensityX, intensityY, intensityZ );
				Vector3 normal = new Vector3( normalX, normalY, normalZ );
				Vector3 shadow_cast_offset = new Vector3( shadow_cast_offsetX, shadow_cast_offsetY, shadow_cast_offsetZ );

				if ( radius == 0.0 )
				{
					// Compute a proper radius from our attenuation factors.
					if ( quadratic_attn == 0.0 && linear_attn == 0.0 )
					{
						// Constant light with no distance falloff. Pick a radius.
						radius = 2000.0f;
					}
					else if ( quadratic_attn == 0.0 )
					{
						// Linear falloff.
						float intensityScalar = intensity.Length;
						float minLightValue = worldlightsIsHDR ? 0.015f : 0.03f;
						radius = ((intensityScalar / minLightValue) - constant_attn) / linear_attn;
					}
					else
					{
						// Solve quadratic equation.
						float intensityScalar = intensity.Length;
						float minLightValue = worldlightsIsHDR ? 0.015f : 0.03f;
						float a = quadratic_attn, b = linear_attn, c = (constant_attn - intensityScalar / minLightValue);
						float rad = (b * b) - 4 * a * c;
						if ( rad > 0.0f )
							radius = (-b + (float)Math.Sqrt( rad )) / (2.0f * a);
						else
							radius = 2000.0f;
					}
				}

				Vector3 distAttenuation = new Vector3( constant_attn, linear_attn, quadratic_attn );

				this.worldlights.Add( new WorldLight
				{
					pos = pos,
					intensity = intensity,
					normal = normal,
					type = type,
					radius = radius,
					distAttenuation = distAttenuation,
					exponent = exponent,
					stopdot = stopdot,
					stopdot2 = stopdot2,
					style = (int)style,
					flags = flags
				} );



			}









			// Later
			/*
			var dprp = GetGameLumpData( "dprp" );
			if ( dprp is not null )
				this.detailObjects = deserializeGameLump_dprp( dprp.Value.Item1, dprp.Value.Item2 );

			var sprp = GetGameLumpData( "sprp" );
			if ( sprp is not null )
				this.staticObjects = deserializeGameLump_sprp( sprp.Value.Item1, sprp.Value.Item2, this.version );*/

		}



		public int FindLeafIdxForPoint( Vector3 p, int nodeid = 0 )
		{
			if ( nodeid < 0 )
			{
				return -nodeid - 1;
			}
			else
			{
				BSPNode node = this.nodelist[nodeid];
				//float dot = node.plane.DistanceToPoint( p );
				var dot = node.plane.Distance( p.x, p.y, p.z );
				return FindLeafIdxForPoint( p, dot >= 0.0f ? node.child0 : node.child1 );
			}
		}

		public BSPLeaf? FindLeafForPoint( Vector3 p )
		{
			int leafidx = FindLeafIdxForPoint( p );
			return leafidx >= 0 ? this.leaflist[leafidx] : null;
		}

		private BSPLeafWaterData? FindLeafWaterForPointR( Vector3 p, HashSet<int> liveLeafSet, int nodeid )
		{
			if ( nodeid < 0 )
			{
				int leafidx = -nodeid - 1;
				if ( liveLeafSet.Contains( leafidx ) )
				{
					BSPLeaf leaf = this.leaflist[leafidx];
					if ( leaf.leafwaterdata != -1 )
					{
						return this.leafwaterdata[leaf.leafwaterdata];
					}
				}
				return null;
			}

			BSPNode node = this.nodelist[nodeid];
			//float dot = node.plane.DistanceToPoint( p );
			var dot = node.plane.Distance( p.x, p.y, p.z );

			int check1 = dot >= 0.0f ? node.child0 : node.child1;
			int check2 = dot >= 0.0f ? node.child1 : node.child0;

			BSPLeafWaterData? w1 = FindLeafWaterForPointR( p, liveLeafSet, check1 );
			if ( w1 is not null )
			{
				return w1;
			}
			BSPLeafWaterData? w2 = FindLeafWaterForPointR( p, liveLeafSet, check2 );
			if ( w2 is not null )
			{
				return w2;
			}

			return null;
		}

		public BSPLeafWaterData? FindLeafWaterForPoint( Vector3 p, HashSet<int> liveLeafSet )
		{
			if ( leafwaterdata.Count() == 0 )
				return null;

			return FindLeafWaterForPointR( p, liveLeafSet, 0 );
		}

		public void MarkLeafSet( List<int> dst, BBox aabb, int nodeid = 0 )
		{
			if ( nodeid < 0 )
			{
				var leafidx = -nodeid - 1;
				ensureInList( ref dst, leafidx );
			}
			else
			{
				var node = this.nodelist[nodeid];
				var signs = 0;

				// This can be done more effectively...
				for ( var i = 0; i < 8; i++ )
				{
					//aabb.CornerPoint( scratchVec3a, i );
					var scratchVec3a = aabb.Corners.ElementAt(i);
					var dot = node.plane.Distance( scratchVec3a.x, scratchVec3a.y, scratchVec3a.z );
					signs |= (dot >= 0 ? 1 : 2);
				}

				if ( (signs & 1) != 0 )
					this.MarkLeafSet( dst, aabb, node.child0 );
				if ( (signs & 2) != 0 )
					this.MarkLeafSet( dst, aabb, node.child1 );
			}
		}

		public void Destroy()
		{
			// Nothing to do...
		}

		// This is in the same file because it also parses keyfiles, even though it's not material-related.
		public interface BSPEntity
		{
			string classname { get; set; }
			string this[string key] { get; set; }
		}
		
		// Later
	/*	public static List<BSPEntity> ParseEntitiesLump( string str )
		{
			var p = new ValveKeyValueParser( str );
			var entities = new List<BSPEntity>();
			while ( p.HasTok() )
			{
				entities.Add( Pairs2Obj( p.Unit() as VKFPair[] ) as BSPEntity );
				p.SkipWhite();
			}
			return entities;
		}
		*/

		Tuple<byte[], uint> GetLumpDataEx( LumpType lumpType )
		{
			const int lumpsStart = 0x08;
			int idx = lumpsStart + (int)lumpType * 0x10;
			uint offs = BitConverter.ToUInt32( buffer, idx + 0x00 );
			uint size = BitConverter.ToUInt32( buffer, idx + 0x04 );
			uint version = BitConverter.ToUInt32( buffer, idx + 0x08 );
			uint uncompressedSize = BitConverter.ToUInt32( buffer, idx + 0x0C );

			if ( uncompressedSize != 0 )
			{
				// LZMA compression.
				var compressedData = new byte[size];
				for ( int i = 0; i < size; i++ )
				{
					compressedData[i] = buffer[offs + i];
				}
				var decompressed = Decompress<byte[]>( ref compressedData );//,(int)uncompressedSize
				return Tuple.Create( decompressed, version );
			}
			else
			{
				var data = new byte[size];
				for ( int i = 0; i < size; i++ )
				{
					data[i] = buffer[offs + i];
				}
				return Tuple.Create( data, version );
			}

		}

		byte[] GetLumpData( LumpType lumpType, uint expectedVersion = 0 )
		{
			var data = GetLumpDataEx( lumpType );
			if ( data.Item1.Length != 0 )
			{
				//Debug.Assert( data.Item2 == expectedVersion );
				if ( data.Item2 != expectedVersion )
				{
					//HATA
					return null;
				}
			}
			return data.Item1;
		}


		private (byte[], int)? GetGameLumpData( string magic )
		{
			uint lumpCount = BitConverter.ToUInt32( gameLump, 0 );
			uint needle = (uint)MagicInt( magic );
			int idx = 4;
			for ( int i = 0; i < lumpCount; i++ )
			{
				uint lumpmagic = BitConverter.ToUInt32( gameLump, idx );
				if ( lumpmagic == needle )
				{
					const ushort COMPRESSED = 0x01;
					ushort flags = BitConverter.ToUInt16( gameLump, idx + 4 );
					ushort version = BitConverter.ToUInt16( gameLump, idx + 6 );
					uint fileofs = BitConverter.ToUInt32( gameLump, idx + 8 );
					uint filelen = BitConverter.ToUInt32( gameLump, idx + 12 );

					if ( (flags & COMPRESSED) != 0 )
					{
						// Find next offset to find compressed size length.
						int compressedEnd;
						if ( i + 1 < lumpCount )
							compressedEnd = (int)BitConverter.ToUInt32( gameLump, idx + 0x10 + 0x08 );
						else
							compressedEnd = gameLump.Length;
						byte[] compressed = new byte[compressedEnd - fileofs];

						for ( int j = 0; j < compressedEnd - fileofs; j += 4 )
						{
							compressed[j] = gameLump[fileofs + j + 3];
							compressed[j + 1] = gameLump[fileofs + j + 2];
							compressed[j + 2] = gameLump[fileofs + j + 1];
							compressed[j + 3] = gameLump[fileofs + j];
						}

						byte[] lump = Decompress<byte[]>( compressed );//decompressLZMA( compressed, (int)filelen );
						return (lump, version);
					}
					else
					{
						byte[] lump = new byte[filelen];
						int intSize = sizeof( int );
						for ( int j = 0; j < filelen; j += intSize )
						{
							lump[j] = gameLump[fileofs + j + 3];
							lump[j + 1] = gameLump[fileofs + j + 2];
							lump[j + 2] = gameLump[fileofs + j + 1];
							lump[j + 3] = gameLump[fileofs + j];
						}
						return (lump, version);
					}
				}
				idx += 16;
			}
			return null;
		}


		private Vector4 readVec4( byte[] buffer, int offs )
		{
			float x = BitConverter.ToSingle( buffer, offs );
			float y = BitConverter.ToSingle( buffer, offs + 4 );
			float z = BitConverter.ToSingle( buffer, offs + 8 );
			float w = BitConverter.ToSingle( buffer, offs + 12 );
			return new Vector4( x, y, z, w );
		}


		Vector3 readVec3( byte[] data, int offset )
		{
			float x = BitConverter.ToSingle( data, offset + 0x00 );
			float y = BitConverter.ToSingle( data, offset + 0x04 );
			float z = BitConverter.ToSingle( data, offset + 0x08 );
			return new Vector3( x, y, z );
		}

		public struct Face
		{
			public int Index { get; set; }
			public int Texinfo { get; set; }
			public SurfaceLightmapData LightmapData { get; set; }
			public int VertnormalBase { get; set; }
			public Vector3 Plane { get; set; }
		}

		void AddSurfaceToLeaves( List<int> faceleaflist, int? faceIndex, int surfaceIndex )
		{
			for ( int j = 0; j < faceleaflist.Count; j++ )
			{
				var leaf = leaflist[faceleaflist[j]];
				var leafSurfaces = leaf.surfaces.ToList();
				ensureInList( ref leafSurfaces, surfaceIndex );
				if ( faceIndex != null )
				{
					var leafFaces = leaf.faces.ToList();
					ensureInList( ref leafFaces, faceIndex.Value );
				}
			}
		}

		void addVertexDataToBuffer( ref List<byte> vertexBuffer, ref Vector3 scratchTangentS, ref Vector3 scratchTangentT, MeshVertex[] vertex, Texinfo tex, ref Vector3? center, float tangentW )
		{
			//var floatarrayandoffs = vertexBuffer.AddFloat32( vertex.Length * VERTEX_SIZE );
			//float[] vertexData = floatarrayandoffs.Item1;

			float[] vertexData = new float[vertex.Length * VERTEX_SIZE];

			int dstOffsVertex = 0;


			for ( int j = 0; j < vertex.Length; j++ )
			{
				MeshVertex v = vertex[j];

				// Position
				vertexData[dstOffsVertex++] = v.position.x;
				vertexData[dstOffsVertex++] = v.position.y;
				vertexData[dstOffsVertex++] = v.position.z;

				if ( center is not null )
				{
					center = center + v.position / vertex.Length;
				}

				// Normal
				vertexData[dstOffsVertex++] = v.normal.x;
				vertexData[dstOffsVertex++] = v.normal.y;
				vertexData[dstOffsVertex++] = v.normal.z;
				vertexData[dstOffsVertex++] = v.alpha;

				// Tangent
				scratchTangentS = Vector3.Cross( v.normal, scratchTangentT );
				scratchTangentS = scratchTangentS.Normal;
				vertexData[dstOffsVertex++] = scratchTangentS.x;
				vertexData[dstOffsVertex++] = scratchTangentS.y;
				vertexData[dstOffsVertex++] = scratchTangentS.z;
				// Tangent Sign
				vertexData[dstOffsVertex++] = tangentW;

				// Texture UV
				vertexData[dstOffsVertex++] = v.uv.x;
				vertexData[dstOffsVertex++] = v.uv.y;

				// Lightmap UV
				if ( (tex.flags & TexinfoFlags.NOLIGHT) != 0 )
				{
					vertexData[dstOffsVertex++] = 0.5f;
					vertexData[dstOffsVertex++] = 0.5f;
				}
				else
				{
					vertexData[dstOffsVertex++] = v.lightmapUV.x;
					vertexData[dstOffsVertex++] = v.lightmapUV.y;
				}

				// For s&box
				dstOffsVertex -= VERTEX_SIZE;
				var new_vertex = new Vertex( new Vector3( vertexData[dstOffsVertex++], vertexData[dstOffsVertex++], vertexData[dstOffsVertex++] ), new Vector4( vertexData[dstOffsVertex++], vertexData[dstOffsVertex++], vertexData[dstOffsVertex++], vertexData[dstOffsVertex++] ), new Vector4( vertexData[dstOffsVertex++], vertexData[dstOffsVertex++], vertexData[dstOffsVertex++], vertexData[dstOffsVertex++] ), new Vector4( vertexData[dstOffsVertex++], vertexData[dstOffsVertex++], vertexData[dstOffsVertex++], vertexData[dstOffsVertex++] ) );
				vertexList.Add( new_vertex );
				//Log.Info( new_vertex.Position );
			}
			//Log.Info( vertexData.Count() );

			foreach ( float value in vertexData )
			{
				byte[] valueBytes = BitConverter.GetBytes( value );
				vertexBuffer.AddRange( valueBytes );
			}

			/*var asd = Enumerable.Range( 0, vertexData.Count() ).Select( x => { Log.Info(x); return BitConverter.GetBytes( x ); } ).ToArray();//Array.ConvertAll( vertexData, Convert.ToByte );
			Log.Info( "ASD");
			vertexBuffer.AddRange( asd ); // verify */

			//vertexBuffer.FinishAddFloat32( vertexData, floatarrayandoffs.Item2, vertex.Length * VERTEX_SIZE );
		}
		private static void ensureInList<T>( ref List<T> list, T value )
		{
			if ( !list.Contains( value ) )
			{
				Log.Info( "EKLENDI" );
				list.Add( value );
			}
		}
	}


	public class Plane
	{
		private static readonly Vector3[] scratchVec3 = new Vector3[2];
		public Vector3 n = Vector3.Zero;
		public float d;

		public Plane( float x = 0, float y = 0, float z = 0, float d = 0 )
		{
			n = new Vector3( x, y, z );
		}

		public void Set( Vector3 n, float d )
		{
			this.n = n;
			this.d = d;
		}

		public void Copy( Plane o )
		{
			Set( o.n, o.d );
		}

		public void Negate()
		{
			n = -n;
			d *= -1;
		}

		public float Distance( float x, float y, float z )
		{
			float nx = n.x, ny = n.y, nz = n.z;
			float dot = x * nx + y * ny + z * nz;
			return dot + d;
		}

		public float DistanceVec3( Vector3 p )
		{
			return Vector3.Dot( p, n ) + d;
		}

		// Assumes input normal is not normalized.
		public void Set4Unnormalized( float nx, float ny, float nz, float d )
		{
			float h = (float)Math.Sqrt( nx * nx + ny * ny + nz * nz );
			n = new Vector3( nx / h, ny / h, nz / h );
			this.d = d / h;
		}


		public void getVec4v( out Vector4 dst )
		{
			dst = new Vector4( n.x, n.y, n.z, d );
		}

		public void setVec4v( Vector4 v )
		{
			n = new Vector3( v.x, v.y, v.z );
			d = v.w;
		}

		public void setTri( Vector3 p0, Vector3 p1, Vector3 p2 )
		{
			scratchVec3[0] = p1 - p0;
			scratchVec3[1] = p2 - p0;

			n = Vector3.Cross( scratchVec3[0], scratchVec3[1] );

			n = n.Normal;
			d = -Vector3.Dot( n, p0 );
		}

		public void intersectLine( out Vector3 dst, Vector3 p0, Vector3 dir )
		{
			var t = -(Vector3.Dot( n, p0 ) + d) / Vector3.Dot( n, dir );
			dst = p0 + dir * t;
		}
		
		/*// Compute point where line segment intersects plane
		public void intersectLineSegment( out Vector3 dst, Vector3 p0, Vector3 p1 )
		{
			Plane.scratchVec3[1] = p1 -  p0;
			intersectLine( out dst, p0, Plane.scratchVec3[1] );
		}

		public void transform( Matrix4x4 mtx )
		{
			getVec4v( out var scratchVec4 );
			Matrix4x4.Invert( mtx, out var scratchMatrix );
			scratchMatrix = Matrix4x4.Transpose( scratchMatrix );
			Vector4.Transform( scratchVec4, scratchVec4, scratchMatrix );
			setVec4v( scratchVec4 );
		}*/

	}

}

