// sbox.Community © 2023-2024

using Sandbox;
using System;
using System.Collections.Generic;

namespace MapParser.GoldSrc.Entities
{
	public static class geometryBuilder
	{
		public static int GetTriangleSeriesType( int trianglesSeriesHead )
		{
			return trianglesSeriesHead < 0 ? Constants.TRIANGLE_FAN : Constants.TRIANGLE_STRIP;
		}

		public static int CountVertices( short[] trianglesBuffer )
		{
			int vertCount = 0;
			int p = 0;
			while ( trianglesBuffer[p] != 0 )
			{
				int verticesNum = Math.Abs( trianglesBuffer[p] );
				p += verticesNum * 4 + 1;
				vertCount += (verticesNum - 3) * 3 + 3;
			}
			return vertCount;
		}

		public static (float[] vertices, float[] uv, short[] indices, List<float[]> vertexList_sv) ReadFacesData( ref short[] trianglesBuffer, ref float[] verticesBuffer ) // , ref Structs.Texture texture , float[] lights
		{
			// Number of vertices for generating buffer
			int vertNumber = CountVertices( trianglesBuffer );

			// List of vertices data: origin and uv position on texture
			List<float[]> verticesData = new List<float[]>();

			// For server collision model
			List<float[]> vertexDataList = new();
			

			// Current position in buffer
			int trisPos = 0;

			// Processing triangle series
			while ( trianglesBuffer[trisPos] != 0 )
			{
				// Detecting triangle series type
				int trianglesType = trianglesBuffer[trisPos] < 0 ? Constants.TRIANGLE_FAN : Constants.TRIANGLE_STRIP;

				// Starting vertex for triangle fan
				float[] startVert = null;

				// Number of following triangles
				int trianglesNum = Math.Abs( trianglesBuffer[trisPos] );


				// This index is no longer needed,
				// we proceed to the following
				trisPos++;

				// For counting we will make steps for 4 array items:
				// 0 — index of the vertex origin in vertices buffer
				// 1 — light (?)
				// 2 — first uv coordinate
				// 3 — second uv coordinate
				for ( int j = 0; j < trianglesNum; j++, trisPos += 4 )
				{
					int vertIndex = trianglesBuffer[trisPos];
					int vert = vertIndex * 3;

					// Vertex data
					float[] vertexData = new float[]
					{
						// Origin
						verticesBuffer[vert + 0],
						verticesBuffer[vert + 1],
						verticesBuffer[vert + 2],

						// Light? data, didn't used anywhere
						/*trianglesBuffer[trisPos] / (float)texture.width,
						trianglesBuffer[trisPos + 1]/ (float)texture.height,*/

						// UV data
						trianglesBuffer[trisPos + 2],
						trianglesBuffer[trisPos + 3],

						// Vertex index for getting bone transforms in subsequent calculations
						vertIndex
					};

					if ( Game.IsServer )
						vertexDataList.Add( vertexData );

					// Unpacking triangle strip. Each next vertex, beginning with the third,
					// forms a triangle with the last and the penultimate vertex.
					//       1 ________3 ________ 5
					//       ╱╲        ╱╲        ╱╲
					//     ╱    ╲    ╱    ╲    ╱    ╲
					//   ╱________╲╱________╲╱________╲
					// 0          2         4          6

					if ( trianglesType == Constants.TRIANGLE_STRIP )
					{
						if ( j > 2 )
						{
							if ( j % 2 == 0 )
							{
								// even
								verticesData.Add( verticesData[verticesData.Count - 3] );// previously first one
								verticesData.Add( verticesData[verticesData.Count - 2] ); // last one, -2 because added element above
							}
							else
							{
								// odd
								verticesData.Add( verticesData[verticesData.Count - 1] ); // last one
								verticesData.Add( verticesData[verticesData.Count - 3] ); // second to last, -3 because added element above
							}
						}
					}


					// Unpacking triangle fan. Each next vertex, beginning with the third,
					// forms a triangle with the last and first vertex.
					//       2 ____3 ____ 4
					//       ╱╲    |    ╱╲
					//     ╱    ╲  |  ╱    ╲
					//   ╱________╲|╱________╲
					// 1          0            5

					if ( trianglesType == Constants.TRIANGLE_FAN )
					{
						startVert = startVert ?? vertexData;

						if ( j > 2 )
						{
							verticesData.Add( startVert );
							verticesData.Add( verticesData[verticesData.Count - 2] );
						}
					}

					// New one
					verticesData.Add( vertexData );
				}
			}

			float[] vertices = new float[vertNumber * 3];
			//float[] lights = new float[vertNumber * 2];
			float[] uv = new float[vertNumber * 2];
			short[] indices = new short[vertNumber];
			for ( var i = 0; i < vertNumber; i++ )
			{
				vertices[i * 3 + 0] = verticesData[i][0];

				vertices[i * 3 + 1] = verticesData[i][1];

				vertices[i * 3 + 2] = verticesData[i][2];

				/*lights[i * 2 + 0] = verticesData[i][3];

				lights[i * 2 + 1] = verticesData[i][4];

				uv[i * 2 + 0] = verticesData[i][5];

				uv[i * 2 + 1] = verticesData[i][6];*/

				uv[i * 2 + 0] = verticesData[i][3];

				uv[i * 2 + 1] = verticesData[i][4];

				indices[i] = (short)verticesData[i][5];
			}

			return (vertices, uv, indices, vertexDataList); //, lights
		}
	}
}
