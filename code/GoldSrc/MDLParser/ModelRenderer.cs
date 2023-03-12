// sbox.Community © 2023-2024

using Sandbox;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using static MapParser.GoldSrc.Entities.ModelDataParser;
using static MapParser.Manager;

namespace MapParser.GoldSrc.Entities
{
	/**
	 * Returns generated mesh buffers and UV-maps of each frame of each sequence of
	 * the model
	 * @param modelData Model data
	 */
	public static class ModelRenderer
	{
		// TODO: convert to struct
		public static Dictionary<string, ((float[][][], float[], int, Texture, float[][])[][], ModelParser, GoldSrc.EntityParser.EntityData, List<GoldSrc.EntityParser.EntityData>, MDLEntity)> ModelCache = new();

		public static void clearModelCache()
		{
			foreach ( var model in ModelCache )
				if ( model.Value.Item5 != null && model.Value.Item5.CL != null && model.Value.Item5.CL.IsValid() )
					model.Value.Item5.Delete();
			ModelCache.Clear();
		}

		/**
		 * Mesh buffers of each frame of each sequence of the model and mesh UV-maps
		 */
		public class MeshRenderData
		{
			public float[][][] geometryBuffers { get; set; }
			public float[] uvMap { get; set; }
			//public float[] lightData { get; set; }
			//For Server ( might be removed )
			public float[][] collisionMeshData { get; set; }
		}

		/**
		 * Applies bone transforms to a position array and returns it
		 */
		public static float[] ApplyBoneTransforms(
			ref float[] vertices,
			ref short[] vertIndices,
			ref byte[] vertBoneBuffer,
			ref Matrix4x4[] boneTransforms
		)
		{
			var posArray = new float[vertices.Length];

			for ( int i = 0; i < vertIndices.Length; i++ )
			{
				var transform = boneTransforms[vertBoneBuffer[vertIndices[i]]];

				// The vec3.transformMat4 function was removed from here, because its use
				// (creation of an additional vector) increased the code performance by
				// 4 times. Instead, it uses manual multiplication.

				var x = vertices[i * 3 + 0];
				var y = vertices[i * 3 + 1];
				var z = vertices[i * 3 + 2];
				var w = transform.M14 * x + transform.M24 * y + transform.M34 * z + transform.M44 != 0.0f ?
					transform.M14 * x + transform.M24 * y + transform.M34 * z + transform.M44 : 1.0f;

				posArray[i * 3 + 0] = (transform.M11 * x + transform.M21 * y + transform.M31 * z + transform.M41) / w;
				posArray[i * 3 + 1] = (transform.M12 * x + transform.M22 * y + transform.M32 * z + transform.M42) / w;
				posArray[i * 3 + 2] = (transform.M13 * x + transform.M23 * y + transform.M33 * z + transform.M43) / w;
			}


			return posArray;
		}

		public static (float[][][], float[], int, Texture, float[][])[][] CreateModelEntity( ref MeshRenderData[][][] meshesRenderData, ModelDataParser.ModelParser modelData, ref ushort[][] textureBuffers, ref GoldSrc.EntityParser.EntityData entData, ref SpawnParameter settings, ref List<GoldSrc.EntityParser.EntityData> lightEntities )
		{
			Texture[] textures = null;
			if ( Game.IsClient )
			{
				textures = textureBuffers.Select( ( textureBuffer, textureIndex ) =>
				{
					return TextureCache.addTexture( textureBuffer.Select( x => (byte)x ).ToArray(), $"{modelData.header.name}_{textureIndex}", modelData.textures[textureIndex].width, modelData.textures[textureIndex].height, Util.PathToMapNameWithExtension( modelData.header.name ) ); //Sandbox.Texture.Create( modelData.textures[textureIndex].width, modelData.textures[textureIndex].height ).WithData( textureBuffer.Select( x => (byte)x ).ToArray() ).Finish();
				} ).ToArray();
			}
			(float[][][], float[], int, Texture, float[][])[][] meshes = new (float[][][], float[], int, Texture, float[][])[meshesRenderData.Length][]; //float[],
			for ( int bodyPartIndex = 0; bodyPartIndex < meshesRenderData.Length; bodyPartIndex++ )
			{
				List<(float[][][], float[], int, Texture, float[][])> submodels = new();
				for ( int subModelIndex = 0; subModelIndex < meshesRenderData[bodyPartIndex].Length; subModelIndex++ )
				{
					var i = 0;
					foreach ( MeshRenderData mesh in meshesRenderData[bodyPartIndex][subModelIndex] )
					{
						float[][][] geometryBuffers = mesh.geometryBuffers;
						float[] uvMap = mesh.uvMap;
						//float[] lightData = mesh.lightData;
						int skinRef = modelData.meshes[bodyPartIndex][subModelIndex][i++].skinRef;
						int textureIndex = modelData.skinRef.Count() <= skinRef ? -1 : modelData.skinRef[skinRef];

						submodels.Add( (geometryBuffers, uvMap, meshesRenderData[bodyPartIndex][subModelIndex].Length, Game.IsClient ? (textureIndex == -1 ? null : textures[textureIndex]) : null, mesh.collisionMeshData) ); //lightData, 
					}
				}
				meshes[bodyPartIndex] = submodels.ToArray(); // Might cause lack of performance
			}
			return meshes;
		}

		public async static Task<MeshRenderData[][][]> PrepareRenderData( ModelDataParser.ModelParser modelData )
		{
			MeshRenderData[][][] renderData = new MeshRenderData[modelData.bodyParts.Length][][];

			for ( int bodyPartIndex = 0; bodyPartIndex < modelData.bodyParts.Length; bodyPartIndex++ )
			{
				PreparingIndicator.Update( "Models" );

				renderData[bodyPartIndex] = new MeshRenderData[modelData.subModels[bodyPartIndex].Length][];

				for ( int subModelIndex = 0; subModelIndex < modelData.subModels[bodyPartIndex].Length; subModelIndex++ )
				{
					PreparingIndicator.Update( "Models" );

					renderData[bodyPartIndex][subModelIndex] = new MeshRenderData[modelData.meshes[bodyPartIndex][subModelIndex].Length];

					for ( int meshIndex = 0; meshIndex < modelData.meshes[bodyPartIndex][subModelIndex].Length; meshIndex++ )
					{
						//int textureIndex = modelData.skinRef[modelData.meshes[bodyPartIndex][subModelIndex][meshIndex].skinRef];

						// Unpack faces of the mesh, Vertices, UV, indices, lights
						(float[], float[], short[], float[][]) meshFacesData = geometryBuilder.ReadFacesData(
							ref modelData.triangles[bodyPartIndex][subModelIndex][meshIndex],
							ref modelData.vertices[bodyPartIndex][subModelIndex]
						//ref modelData.textures[textureIndex]
						//ref lightmap
						);

						// Very expensive
						MeshRenderData meshRenderData = new MeshRenderData
						{
							// UV-map of the mesh
							uvMap = meshFacesData.Item2,

							// Light data
							//lightData = new float[]() { Array = meshFacesData.Item4},

							// For server entity collision data, it is vertices and indices without processed
							collisionMeshData = meshFacesData.Item4,

							// List of mesh buffer for each frame of each sequence
							geometryBuffers = modelData.sequences.Select( ( sequence, sequenceIndex ) =>
							{
								float[][] bufferAttributes = new float[sequence.numFrames][];

								for ( int frame = 0; frame < sequence.numFrames; frame++ )
								{
									Matrix4x4[] boneTransforms = GeometryTransformer.CalcRotations( ref modelData, ref sequenceIndex, ref frame ); // expensive

									float[] transformedVertices = ApplyBoneTransforms(
										ref meshFacesData.Item1,
										ref meshFacesData.Item3,
										ref modelData.vertBoneBuffer[bodyPartIndex][subModelIndex],
										ref boneTransforms
									);

									bufferAttributes[frame] = transformedVertices;

									if ( Game.IsServer )
										break;
								}

								return bufferAttributes;
							} ).ToArray()
						};

						renderData[bodyPartIndex][subModelIndex][meshIndex] = meshRenderData;

						await Task.Yield();
					}
					await Task.Yield();
				}
				await Task.Yield();
			}

			return renderData;
		}
	}
}
