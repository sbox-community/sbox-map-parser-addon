// sbox.Community © 2023-2024

using Sandbox;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
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
		public static Dictionary<string, (List<(BufferAttribute<float>[][], BufferAttribute<float>, Texture, List<float[]>)>, GoldSrc.EntityParser.EntityData, List<GoldSrc.EntityParser.EntityData>, MDLEntity)> ModelCache = new();

		public static void clearModelCache()
		{
			foreach(var model in ModelCache)
				if ( model.Value.Item4 != null && model.Value.Item4.CL != null && model.Value.Item4.CL.IsValid() )
					model.Value.Item4.Delete();
			ModelCache.Clear();
		}

		public struct BufferAttribute<T>
		{
			public IEnumerable<T> Array;
		}

		/**
		 * Mesh buffers of each frame of each sequence of the model and mesh UV-maps
		 */
		public class MeshRenderData
		{
			public BufferAttribute<float>[][] geometryBuffers { get; set; }
			public BufferAttribute<float> uvMap { get; set; }
			//public BufferAttribute<float> lightData { get; set; }
			//For Server ( might be removed )
			public List<float[]> collisionMeshData { get; set; }
		}

		/**
		 * Applies bone transforms to a position array and returns it
		 */
		public static float[] ApplyBoneTransforms(
			ref float[] vertices,
			ref short[] vertIndices,
			ref byte[] vertBoneBuffer,
			ref List<Matrix4x4> boneTransforms
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


		/*public static Structs.Mesh[][][] CreateModelMeshes( ref MeshRenderData[][][] meshesRenderData, ModelDataParser.ModelParser modelData, ref List<ushort[]> textureBuffers )
		{
			List<Sandbox.Texture> textures = textureBuffers.Select( ( textureBuffer, textureIndex ) => {
				return Sandbox.Texture.Create( modelData.textures[textureIndex].width, modelData.textures[textureIndex].height ).WithData( textureBuffer.Select( x => (byte)x ).ToArray() ).Finish();
			} ).ToList();

			List<Structs.Mesh[][]> modelMeshes = new List<Structs.Mesh[][]>( meshesRenderData.Length );

			for ( int bodyPartIndex = 0; bodyPartIndex < meshesRenderData.Length; bodyPartIndex++ )
			{
				List<Structs.Mesh[]> bodyPartMeshes = new List<Structs.Mesh[]>( meshesRenderData[bodyPartIndex].Length );

				for ( int subModelIndex = 0; subModelIndex < meshesRenderData[bodyPartIndex].Length; subModelIndex++ )
				{
					List<Structs.Mesh> subModelMeshes = new List<Structs.Mesh>( meshesRenderData[bodyPartIndex][subModelIndex].Length );

					var i = 0;
					foreach ( MeshRenderData subModel in meshesRenderData[bodyPartIndex][subModelIndex] )
					{
						BufferAttribute<float>[][] geometryBuffers = subModel.geometryBuffers;
						BufferAttribute<float> uvMap = subModel.uvMap;
						BufferAttribute<float> initialGeometryBuffer = geometryBuffers[0][0];
						int skinRef = modelData.meshes[bodyPartIndex][subModelIndex][i++].skinRef;
						int textureIndex = modelData.skinRef[skinRef];
						Sandbox.Texture texture = textures[textureIndex];

						subModelMeshes.Add( CreateMesh( initialGeometryBuffer, uvMap, texture ) );
					}

					bodyPartMeshes.Add( subModelMeshes.ToArray() );
				}

				modelMeshes.Add( bodyPartMeshes.ToArray() );
			}

			return modelMeshes.ToArray();
		}*/

		public static List<(BufferAttribute<float>[][], BufferAttribute<float>, Texture, List<float[]>)>  CreateModelEntity( ref MeshRenderData[][][] meshesRenderData, ModelDataParser.ModelParser modelData, ref List<ushort[]> textureBuffers, ref GoldSrc.EntityParser.EntityData entData, ref SpawnParameter settings, ref List<GoldSrc.EntityParser.EntityData> lightEntities )
		{
			List<Texture> textures = new();
			if (Game.IsClient) { 
				textures = textureBuffers.Select( ( textureBuffer, textureIndex ) => {
					return TextureCache.addTexture( textureBuffer.Select( x => (byte)x ).ToArray(), $"{modelData.header.name}_{textureIndex}" , modelData.textures[textureIndex].width, modelData.textures[textureIndex].height, Util.PathToMapNameWithExtension(modelData.header.name) ); //Sandbox.Texture.Create( modelData.textures[textureIndex].width, modelData.textures[textureIndex].height ).WithData( textureBuffer.Select( x => (byte)x ).ToArray() ).Finish();
				} ).ToList();
			}
			List<(BufferAttribute<float>[][], BufferAttribute<float>, Texture, List<float[]>)> subModelMeshes = new(); //BufferAttribute<float>,
			for ( int bodyPartIndex = 0; bodyPartIndex < meshesRenderData.Length; bodyPartIndex++ )
			{
				for ( int subModelIndex = 0; subModelIndex < meshesRenderData[bodyPartIndex].Length; subModelIndex++ )
				{
					var i = 0;
					foreach ( MeshRenderData subModel in meshesRenderData[bodyPartIndex][subModelIndex] )
					{
						BufferAttribute<float>[][] geometryBuffers = subModel.geometryBuffers;
						BufferAttribute<float> uvMap = subModel.uvMap;
						//BufferAttribute<float> lightData = subModel.lightData;
						int skinRef = modelData.meshes[bodyPartIndex][subModelIndex][i++].skinRef;
						int textureIndex = modelData.skinRef.Count() <= skinRef ? -1 : modelData.skinRef[skinRef];

						subModelMeshes.Add( (geometryBuffers, uvMap, Game.IsClient ? (textureIndex == -1 ? null : textures[textureIndex]) : null, subModel.collisionMeshData) ); //lightData, 
					}
				}
			}
			return subModelMeshes;
		}

			/*private static Structs.Mesh CreateMesh( BufferAttribute<float> initialGeometryBuffer, BufferAttribute<float> uvMap, Sandbox.Texture texture )
			{
				var meshArray = initialGeometryBuffer.Array.ToArray();
				var uvArray = uvMap.Array.ToArray();

				VertexBuffer vb = new();
				vb.Init( false );

				for (var ii = 0;  ii < meshArray.Count()/3; ii++)
					vb.Add(new Vertex( new Vector3( meshArray[ii*3], meshArray[ii * 3 + 1], meshArray[ii * 3 + 2] ), new Vector2( uvArray[ii * 2], uvArray[ii * 2 + 1] ), Color.White ) );

				var mat = Material.Create( "test", "simple" );
				mat.Set( "Color", texture );
				mat.Set( "g_vColorTint", Color.White );
				mat.Set( "g_flMetalness", 1.0f );
				mat.Set( "Roughness", Texture.White );
				mat.Set( "Normal", Texture.Transparent );

				var mesh = new Sandbox.Mesh( mat );
				mesh.CreateBuffers(vb);

				var mb = new ModelBuilder();
				mb.AddMesh( mesh );

				Structs.Mesh meshes = new Structs.Mesh( );/// geometry, material

				return meshes;
			}*/

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
						(float[], float[], short[], List<float[]>) meshFacesData = geometryBuilder.ReadFacesData(
							ref modelData.triangles[bodyPartIndex][subModelIndex][meshIndex],
							ref modelData.vertices[bodyPartIndex][subModelIndex]
							//ref modelData.textures[textureIndex]
							//ref lightmap
						);

						// Very expensive
						MeshRenderData meshRenderData = new MeshRenderData
						{
							// UV-map of the mesh
							uvMap = new BufferAttribute<float>() { Array = meshFacesData.Item2 },

							// Light data
							//lightData = new BufferAttribute<float>() { Array = meshFacesData.Item4},

							// For server entity collision data, it is vertices and indices without processed
							collisionMeshData = meshFacesData.Item4,

							// List of mesh buffer for each frame of each sequence
							geometryBuffers = modelData.sequences.Select( (sequence , sequenceIndex ) =>
							{
								List<BufferAttribute<float>> bufferAttributes = new List<BufferAttribute<float>>();

								for ( int frame = 0; frame < sequence.numFrames; frame++ )
								{
									List<Matrix4x4> boneTransforms = GeometryTransformer.CalcRotations( ref modelData, ref sequenceIndex, ref frame ); // expensive
									
									float[] transformedVertices = ApplyBoneTransforms(
										ref meshFacesData.Item1,
										ref meshFacesData.Item3,
										ref modelData.vertBoneBuffer[bodyPartIndex][subModelIndex],
										ref boneTransforms
									);

									bufferAttributes.Add( new BufferAttribute<float>() { Array = transformedVertices} );

									if ( Game.IsServer )
										break;
								}

								return bufferAttributes.ToArray();
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

		//	/**
		//	 * Returns generated mesh buffers and UV-maps of each frame of each sequence of
		//	 * the model
		//	 * @param modelData Model data
		//	 */
		//	public static MeshRenderData[][][] PrepareRenderData( ModelData modelData )
		//	{
		//		MeshRenderData[][][] renderData = new MeshRenderData[modelData.BodyParts.Length][][];
		//
		//		for ( int bodyPartIndex = 0; bodyPartIndex < modelData.BodyParts.Length; bodyPartIndex++ )
		//		{
		//			renderData[bodyPartIndex] = new MeshRenderData[modelData.SubModels[bodyPartIndex].Length][];
		//
		//			for ( int subModelIndex = 0; subModelIndex < modelData.SubModels[bodyPartIndex].Length; subModelIndex++ )
		//			{
		//				renderData[bodyPartIndex][subModelIndex] = new MeshRenderData[modelData.Meshes[bodyPartIndex][subModelIndex].Length];
		//
		//				for ( int meshIndex = 0; meshIndex < modelData.Meshes[bodyPartIndex][subModelIndex].Length; meshIndex++ )
		//				{
		//					int textureIndex = modelData.SkinRef[modelData.Meshes[bodyPartIndex][subModelIndex][meshIndex].SkinRef];
		//
		//					// Unpack faces of the mesh
		//					(float[] vertices, float[] uv, short[] indices) = ReadFacesData(
		//					  modelData.Triangles[bodyPartIndex][subModelIndex][meshIndex],
		//					  modelData.Vertices[bodyPartIndex][subModelIndex],
		//					  modelData.Textures[textureIndex]
		//					);
		//
		//					MeshRenderData meshRenderData = new MeshRenderData();
		//					meshRenderData.uvMap = new BufferAttribute( uv, 2 );
		//					meshRenderData.geometryBuffers = new BufferAttribute[modelData.Sequences.Length][];
		//
		//					for ( int sequenceIndex = 0; sequenceIndex < modelData.Sequences.Length; sequenceIndex++ )
		//					{
		//						meshRenderData.geometryBuffers[sequenceIndex] = new BufferAttribute[modelData.Sequences[sequenceIndex].NumFrames];
		//
		//						for ( int frame = 0; frame < modelData.Sequences[sequenceIndex].NumFrames; frame++ )
		//						{
		//							mat4[] boneTransforms = CalcRotations( modelData, sequenceIndex, frame );
		//							float[] transformedVertices = ApplyBoneTransforms(
		//							  vertices,
		//							  indices,
		//							  modelData.VertBoneBuffer[bodyPartIndex][subModelIndex],
		//							  boneTransforms
		//							);
		//							meshRenderData.geometryBuffers[sequenceIndex][frame] = new BufferAttribute( transformedVertices, 3 );
		//						}
		//					}
		//
		//					renderData[bodyPartIndex][subModelIndex][meshIndex] = meshRenderData;
		//				}
		//			}
		//		}
		//
		//		return renderData;
		//	}
		//
		//
		//public static GameObject CreateMesh(
		//	THREE.BufferAttribute geometryBuffer,
		//	THREE.BufferAttribute uvMap,
		//	THREE.Texture texture
		//)
		//	{
		//		// Mesh level
		//		var material = new THREE.MeshBasicMaterial
		//		{
		//			map = texture,
		//			side = THREE.DoubleSide,
		//			transparent = true,
		//			alphaTest = 0.5f,
		//			morphTargets = true,
		//			skinning = true
		//			// color = new Color(1, 1, 1)
		//		};
		//
		//		// Prepare geometry
		//		var geometry = new THREE.BufferGeometry();
		//		geometry.SetAttribute( "position", geometryBuffer );
		//		geometry.SetAttribute( "uv", uvMap );
		//
		//		// Prepare mesh
		//		var mesh = new THREE.Mesh( geometry, material );
		//
		//		// Create a GameObject to hold the mesh
		//		var meshObject = new GameObject( "Mesh" );
		//		meshObject.AddComponent<MeshFilter>().mesh = mesh;
		//		meshObject.AddComponent<MeshRenderer>().material = material;
		//
		//		return meshObject;
		//	}
		//
		//	/**
		//	* Creates list of meshes of every submodel
		//	*/
		//	public static List<List<List<Mesh>>> CreateModelMeshes(
		//		MeshRenderData[][][] meshesRenderData,
		//		ModelData modelData,
		//		List<byte[]> textureBuffers
		//	)
		//	{
		//		List<Texture2D> textures = textureBuffers.ConvertAll( textureBuffer => {
		//			Texture2D texture = new Texture2D(
		//				modelData.textures[textureBuffers.IndexOf( textureBuffer )].width,
		//				modelData.textures[textureBuffers.IndexOf( textureBuffer )].height,
		//				TextureFormat.RGBA32,
		//				false
		//			);
		//			texture.LoadRawTextureData( textureBuffer );
		//			texture.Apply();
		//			return texture;
		//		} );
		//
		//		List<List<List<Mesh>>> modelMeshes = new List<List<List<Mesh>>>();
		//		for ( int bodyPartIndex = 0; bodyPartIndex < meshesRenderData.Length; bodyPartIndex++ )
		//		{
		//			List<List<Mesh>> bodyPartMeshes = new List<List<Mesh>>();
		//			modelMeshes.Add( bodyPartMeshes );
		//			for ( int subModelIndex = 0; subModelIndex < meshesRenderData[bodyPartIndex].Length; subModelIndex++ )
		//			{
		//				List<Mesh> subModelMeshes = new List<Mesh>();
		//				bodyPartMeshes.Add( subModelMeshes );
		//				for ( int meshIndex = 0; meshIndex < meshesRenderData[bodyPartIndex][subModelIndex].Length; meshIndex++ )
		//				{
		//					MeshRenderData meshRenderData = meshesRenderData[bodyPartIndex][subModelIndex][meshIndex];
		//					BufferAttribute uvMap = meshRenderData.uvMap;
		//					List<BufferAttribute[]> geometryBuffers = meshRenderData.geometryBuffers;
		//					BufferAttribute initialGeometryBuffer = geometryBuffers[0][0];
		//					byte skinRef = modelData.meshes[bodyPartIndex][subModelIndex][meshIndex].skinRef;
		//					byte textureIndex = modelData.skinRef[skinRef];
		//					Texture2D texture = textures[textureIndex];
		//					Mesh mesh = CreateMesh( initialGeometryBuffer, uvMap, texture );
		//					subModelMeshes.Add( mesh );
		//				}
		//			}
		//		}
		//
		//		return modelMeshes;
		//	}
		//
		//
		//	/*public static GameObject CreateContainer( List<List<List<MeshRendererData>>> meshes )
		//	{
		//		var container = new GameObject( "Model" );
		//
		//		// Adding meshes to the container
		//		foreach ( var bodyPart in meshes )
		//		{
		//			// Body part level
		//			foreach ( var subModel in bodyPart )
		//			{
		//				// Sub model level
		//				foreach ( var meshData in subModel )
		//				{
		//					var mesh = CreateMesh( meshData.GeometryBuffers[0][0], meshData.UvMap, meshData.Texture );
		//					mesh.transform.parent = container.transform;
		//				}
		//			}
		//		}
		//
		//		// Sets to display the front of the model
		//		container.transform.rotation = Quaternion.Euler( -90f, -90f, 0f );
		//
		//		// Sets to display model on the center of camera
		//		var bounds = new Bounds( container.transform.position, Vector3.zero );
		//		foreach ( var renderer in container.GetComponentsInChildren<MeshRenderer>() )
		//		{
		//			bounds.Encapsulate( renderer.bounds );
		//		}
		//		container.transform.position -= new Vector3( 0f, (bounds.min.y + bounds.max.y) / 2f, 0f );
		//
		//		return container;

	}
}
//
