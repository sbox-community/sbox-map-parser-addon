// sbox.Community © 2023-2024

using System;
using System.Linq;
using System.Threading.Tasks;
using static MapParser.GoldSrc.Entities.Constants;
using static MapParser.GoldSrc.Entities.Structs;

namespace MapParser.GoldSrc.Entities
{
	public static class ModelDataParser
	{
		/** Parses header of the MDL file */
		public static Header ParseHeader( ref byte[] bytes )
		{
			return new Header
			{
				id = BitConverter.ToInt32( bytes, 0 ),
				version = BitConverter.ToInt32( bytes, 4 ),
				name = Util.ReadString( ref bytes, 8, 64 ),
				length = BitConverter.ToInt32( bytes, 72 ),
				eyePosition = new Vector3(
					BitConverter.ToSingle( bytes, 76 ),
					BitConverter.ToSingle( bytes, 80 ),
					BitConverter.ToSingle( bytes, 84 )
				),
				min = new Vector3(
					BitConverter.ToSingle( bytes, 88 ),
					BitConverter.ToSingle( bytes, 92 ),
					BitConverter.ToSingle( bytes, 96 )
				),
				max = new Vector3(
					BitConverter.ToSingle( bytes, 100 ),
					BitConverter.ToSingle( bytes, 104 ),
					BitConverter.ToSingle( bytes, 108 )
				),
				bbmin = new Vector3(
					BitConverter.ToSingle( bytes, 112 ),
					BitConverter.ToSingle( bytes, 116 ),
					BitConverter.ToSingle( bytes, 120 )
				),
				bbmax = new Vector3(
					BitConverter.ToSingle( bytes, 124 ),
					BitConverter.ToSingle( bytes, 128 ),
					BitConverter.ToSingle( bytes, 132 )
				),
				flags = BitConverter.ToInt32( bytes, 136 ),
				numBones = BitConverter.ToInt32( bytes, 140 ),
				boneIndex = BitConverter.ToInt32( bytes, 144 ),
				numBoneControllers = BitConverter.ToInt32( bytes, 148 ),
				boneControllerIndex = BitConverter.ToInt32( bytes, 152 ),
				numHitboxes = BitConverter.ToInt32( bytes, 156 ),
				hitBoxIndex = BitConverter.ToInt32( bytes, 160 ),
				numSeq = BitConverter.ToInt32( bytes, 164 ),
				seqIndex = BitConverter.ToInt32( bytes, 168 ),
				numSeqGroups = BitConverter.ToInt32( bytes, 172 ),
				seqGroupIndex = BitConverter.ToInt32( bytes, 176 ),
				numTextures = BitConverter.ToInt32( bytes, 180 ),
				textureIndex = BitConverter.ToInt32( bytes, 184 ),
				textureDataIndex = BitConverter.ToInt32( bytes, 188 ),
				numSkinRef = BitConverter.ToInt32( bytes, 192 ),//x
				numSkinFamilies = BitConverter.ToInt32( bytes, 196 ),//x
				skinIndex = BitConverter.ToInt32( bytes, 200 ),//x
				numBodyParts = BitConverter.ToInt32( bytes, 204 ),//x
				bodyPartIndex = BitConverter.ToInt32( bytes, 208 ),//x
				numAttachments = BitConverter.ToInt32( bytes, 212 ), //x
				attachmentIndex = BitConverter.ToInt32( bytes, 216 ),//x
				soundTable = BitConverter.ToInt32( bytes, 220 ),
				soundIndex = BitConverter.ToInt32( bytes, 224 ),
				soundGroups = BitConverter.ToInt32( bytes, 228 ),
				soundGroupIndex = BitConverter.ToInt32( bytes, 232 ),
				numTransitions = BitConverter.ToInt32( bytes, 236 ),
				transitionIndex = BitConverter.ToInt32( bytes, 240 )
			};
		}

		/** Parses bones */
		public static Bone[] ParseBones( byte[] buffer, int bonesOffset, int numBones )
		{
			Bone[] result = new Bone[numBones];
			var boneOffset = bonesOffset;
			for ( var i = 0; i < numBones; i++ )
			{
				result[i] = new Bone()
				{
					name = Util.ReadString( ref buffer, boneOffset + 0, 32 ),
					parent = BitConverter.ToInt32( buffer, boneOffset + 32 ),
					flags = BitConverter.ToInt32( buffer, boneOffset + 36 ),
					boneController = Enumerable.Range( 0, MAX_PER_BONE_CONTROLLERS ).Select( j => BitConverter.ToInt32( buffer, boneOffset + 40 + j * sizeof( Int32 ) ) ).ToArray(),
					value = Enumerable.Range( 0, MAX_PER_BONE_CONTROLLERS ).Select( j => BitConverter.ToSingle( buffer, (boneOffset + 40 + j * sizeof( float )) + (MAX_PER_BONE_CONTROLLERS * sizeof( float )) ) ).ToArray(),
					scale = Enumerable.Range( 0, MAX_PER_BONE_CONTROLLERS ).Select( j => BitConverter.ToSingle( buffer, (boneOffset + 40 + j * sizeof( float )) + (MAX_PER_BONE_CONTROLLERS * sizeof( float )) + (MAX_PER_BONE_CONTROLLERS * sizeof( float )) ) ).ToArray(),

				};

				boneOffset += (40 + MAX_PER_BONE_CONTROLLERS * sizeof( float )) + (MAX_PER_BONE_CONTROLLERS * sizeof( float )) + (MAX_PER_BONE_CONTROLLERS * sizeof( float ));
			}

			return result;
		}

		/** Parses bone controllers */
		public static BoneController[] ParseBoneControllers( byte[] buffer, int boneControllersOffset, int numControllers )
		{
			BoneController[] result = new BoneController[numControllers];
			var boneControllerOffset = boneControllersOffset;
			for ( var i = 0; i < numControllers; i++ )
			{
				result[i] = new BoneController()
				{
					bone = BitConverter.ToInt32( buffer, boneControllerOffset + 0 ),
					type = BitConverter.ToInt32( buffer, boneControllerOffset + 4 ),
					start = BitConverter.ToSingle( buffer, boneControllerOffset + 8 ),
					end = BitConverter.ToSingle( buffer, boneControllerOffset + 12 ),
					rest = BitConverter.ToInt32( buffer, boneControllerOffset + 16 ),
					index = BitConverter.ToInt32( buffer, boneControllerOffset + 20 ),
				};
				boneControllerOffset += 24;
			}

			return result;
		}

		/** Parses attachments */
		public static Attachment[] ParseAttachments( byte[] buffer, int attachmentsOffset, int numAttachments )
		{
			Attachment[] result = new Attachment[numAttachments];
			var attachmentOffset = attachmentsOffset;
			for ( var i = 0; i < numAttachments; i++ )
			{
				result[i] = new Attachment()
				{
					name = Util.ReadString( ref buffer, attachmentOffset + 0, 32 ),
					type = BitConverter.ToInt32( buffer, attachmentOffset + 32 ),
					bone = BitConverter.ToInt32( buffer, attachmentOffset + 36 ),
					org = new Vector3( BitConverter.ToSingle( buffer, attachmentOffset + 40 ), BitConverter.ToSingle( buffer, attachmentOffset + 44 ), BitConverter.ToSingle( buffer, attachmentOffset + 48 ) ),
					vectors = Enumerable.Range( 0, 3 ).Select( i =>
					{
						int offset = attachmentOffset + 52 + i * (sizeof( float ) * 3);
						return new Vector3(
							BitConverter.ToSingle( buffer, offset ),
							BitConverter.ToSingle( buffer, offset + sizeof( float ) ),
							BitConverter.ToSingle( buffer, offset + sizeof( float ) * 2 )
						);
					} ).ToArray()
				};
				attachmentOffset += 52 + 3 * (sizeof( float ) * 3);
			}

			return result;
		}

		/** Parses bounding boxes */
		public static BoundingBox[] ParseHitboxes( byte[] buffer, int hitboxesOffset, int numHitboxes )
		{
			BoundingBox[] result = new BoundingBox[numHitboxes];
			var hitboxOffset = hitboxesOffset;
			for ( var i = 0; i < numHitboxes; i++ )
			{
				result[i] = new BoundingBox()
				{
					bone = BitConverter.ToInt32( buffer, hitboxOffset + 0 ),
					group = BitConverter.ToInt32( buffer, hitboxOffset + 4 ),
					bbmin = new Vector3( BitConverter.ToSingle( buffer, hitboxOffset + 8 ), BitConverter.ToSingle( buffer, hitboxOffset + 12 ), BitConverter.ToSingle( buffer, hitboxOffset + 16 ) ),
					bbmax = new Vector3( BitConverter.ToSingle( buffer, hitboxOffset + 20 ), BitConverter.ToSingle( buffer, hitboxOffset + 24 ), BitConverter.ToSingle( buffer, hitboxOffset + 28 ) ),
				};
				hitboxOffset += 32;
			}

			return result;
		}

		/** Parses sequences */
		public static SeqDesc[] ParseSequences( byte[] buffer, int sequencesOffset, int numSequences )
		{
			SeqDesc[] result = new SeqDesc[numSequences];
			var sequenceOffset = sequencesOffset;
			for ( var i = 0; i < numSequences; i++ )
			{
				result[i] = new SeqDesc()
				{
					label = Util.ReadString( ref buffer, sequenceOffset + 0, 32 ),
					fps = BitConverter.ToSingle( buffer, sequenceOffset + 32 ),
					flags = BitConverter.ToInt32( buffer, sequenceOffset + 36 ),
					activity = BitConverter.ToInt32( buffer, sequenceOffset + 40 ),
					actWeight = BitConverter.ToInt32( buffer, sequenceOffset + 44 ),
					numEvents = BitConverter.ToInt32( buffer, sequenceOffset + 48 ),
					eventIndex = BitConverter.ToInt32( buffer, sequenceOffset + 52 ),
					numFrames = BitConverter.ToInt32( buffer, sequenceOffset + 56 ),
					numPivots = BitConverter.ToInt32( buffer, sequenceOffset + 60 ),
					pivotIndex = BitConverter.ToInt32( buffer, sequenceOffset + 64 ),
					motionType = BitConverter.ToInt32( buffer, sequenceOffset + 68 ),
					motionBone = BitConverter.ToInt32( buffer, sequenceOffset + 72 ),
					linearMovement = new Vector3( BitConverter.ToSingle( buffer, sequenceOffset + 76 ), BitConverter.ToSingle( buffer, sequenceOffset + 80 ), BitConverter.ToSingle( buffer, sequenceOffset + 84 ) ),
					autoMovePosIndex = BitConverter.ToInt32( buffer, sequenceOffset + 88 ),
					autoMoveAngleIndex = BitConverter.ToInt32( buffer, sequenceOffset + 92 ),
					bbmin = new Vector3( BitConverter.ToSingle( buffer, sequenceOffset + 96 ), BitConverter.ToSingle( buffer, sequenceOffset + 100 ), BitConverter.ToSingle( buffer, sequenceOffset + 104 ) ),
					bbmax = new Vector3( BitConverter.ToSingle( buffer, sequenceOffset + 108 ), BitConverter.ToSingle( buffer, sequenceOffset + 112 ), BitConverter.ToSingle( buffer, sequenceOffset + 116 ) ),
					numBlends = BitConverter.ToInt32( buffer, sequenceOffset + 120 ),
					animIndex = BitConverter.ToInt32( buffer, sequenceOffset + 124 ),
					blendType = Enumerable.Range( 0, 2 ).Select( j => BitConverter.ToInt32( buffer, sequenceOffset + 128 + j * sizeof( Int32 ) ) ).ToArray(),
					blendStart = Enumerable.Range( 0, 2 ).Select( j => BitConverter.ToSingle( buffer, sequenceOffset + 136 + j * sizeof( float ) ) ).ToArray(),
					blendEnd = Enumerable.Range( 0, 2 ).Select( j => BitConverter.ToSingle( buffer, sequenceOffset + 144 + j * sizeof( float ) ) ).ToArray(),
					blendParent = BitConverter.ToInt32( buffer, sequenceOffset + 152 ),
					seqGroup = BitConverter.ToInt32( buffer, sequenceOffset + 156 ),
					entryNode = BitConverter.ToInt32( buffer, sequenceOffset + 160 ),
					exitNode = BitConverter.ToInt32( buffer, sequenceOffset + 164 ),
					nodeFlags = BitConverter.ToInt32( buffer, sequenceOffset + 168 ),
					nextSeq = BitConverter.ToInt32( buffer, sequenceOffset + 172 ),
				};
				sequenceOffset += 176;
			}

			return result;
		}

		/** Parses sequence groups */
		public static SeqGroup[] ParseSequenceGroups( byte[] buffer, int sequenceGroupsOffset, int numSequenceGroups )
		{
			SeqGroup[] result = new SeqGroup[numSequenceGroups];
			var sequenceGroupOffset = sequenceGroupsOffset;
			for ( var i = 0; i < numSequenceGroups; i++ )
			{
				result[i] = new SeqGroup()
				{
					label = Util.ReadString( ref buffer, sequenceGroupOffset + 0, 32 ),
					name = Util.ReadString( ref buffer, sequenceGroupOffset + 32, 64 ),
					unused1 = BitConverter.ToInt32( buffer, sequenceGroupOffset + 96 ),
					unused2 = BitConverter.ToInt32( buffer, sequenceGroupOffset + 100 ),
				};
				sequenceGroupOffset += 104;
			}

			return result;
		}

		/** Parses body parts */
		public static BodyPart[] ParseBodyParts( byte[] buffer, int bodyPartsOffset, int numBodyParts )
		{
			BodyPart[] result = new BodyPart[numBodyParts];
			var bodyPartOffset = bodyPartsOffset;
			for ( var i = 0; i < numBodyParts; i++ )
			{
				result[i] = new BodyPart()
				{
					name = Util.ReadString( ref buffer, bodyPartOffset + 0, 64 ),
					numModels = BitConverter.ToInt32( buffer, bodyPartOffset + 64 ),
					@base = BitConverter.ToInt32( buffer, bodyPartOffset + 68 ),
					modelIndex = BitConverter.ToInt32( buffer, bodyPartOffset + 72 ),
				};
				bodyPartOffset += 76;
			}

			return result;
		}

		/** Parses textures info */
		public static Texture[] ParseTextures( byte[] buffer, int texturesOffset, int numTextures )
		{
			Texture[] result = new Texture[numTextures];
			var textureOffset = texturesOffset;
			for ( var i = 0; i < numTextures; i++ )
			{
				result[i] = new Texture()
				{
					name = Util.ReadString( ref buffer, textureOffset + 0, 64 ),
					flags = BitConverter.ToInt32( buffer, textureOffset + 64 ),
					width = BitConverter.ToInt32( buffer, textureOffset + 68 ),
					height = BitConverter.ToInt32( buffer, textureOffset + 72 ),
					index = BitConverter.ToInt32( buffer, textureOffset + 76 ),
				};

				textureOffset += 80;

			}

			return result;
		}

		/** Parses skin references */
		public static short[] ParseSkinRef( byte[] buffer, int skinRefOffset, int numSkinRef )
		{
			var shortArray = new short[numSkinRef];

			for ( int i = 0; i < numSkinRef; i++ )
			{
				shortArray[i] = BitConverter.ToInt16( buffer, skinRefOffset + (i * sizeof( short )) );
			}
			return shortArray;
		}


		/**
		 * Parses sub model
		 */
		public static SubModel[][] parseSubModel( byte[] buffer, BodyPart[] bodyParts )
		{

			SubModel[][] result = new SubModel[bodyParts.Count()][];
			for ( var i = 0; i < bodyParts.Count(); i++ )
			{
				var bodyPart = bodyParts[i];

				int offset = bodyPart.modelIndex;

				SubModel[] structResult = new SubModel[bodyPart.numModels];

				for ( int j = 0; j < bodyPart.numModels; j++ )
				{
					SubModel subModel = new SubModel()
					{
						name = Util.ReadString( ref buffer, offset + 0, 64 ),
						type = BitConverter.ToInt32( buffer, offset + 64 ),
						boundingRadius = BitConverter.ToSingle( buffer, offset + 68 ),
						numMesh = BitConverter.ToInt32( buffer, offset + 72 ),
						meshIndex = BitConverter.ToInt32( buffer, offset + 76 ),
						numVerts = BitConverter.ToInt32( buffer, offset + 80 ),
						vertInfoIndex = BitConverter.ToInt32( buffer, offset + 84 ),
						vertIndex = BitConverter.ToInt32( buffer, offset + 88 ),
						numNorms = BitConverter.ToInt32( buffer, offset + 92 ),
						normInfoIndex = BitConverter.ToInt32( buffer, offset + 96 ),
						normIndex = BitConverter.ToInt32( buffer, offset + 100 ),
						numGroups = BitConverter.ToInt32( buffer, offset + 104 ),
						groupIndex = BitConverter.ToInt32( buffer, offset + 108 ),
					};

					offset += 112;

					structResult[j] = subModel;
				}
				result[i] = structResult;
			}

			return result;
		}
		public struct PhysicsHull
		{
			public Vector3[] Vertices;
			public int[] Indices;
		}

		/**
		 * Parses meshes
		 */
		public static async Task<Mesh[][][]> parseMeshes( byte[] buffer, SubModel[][] subModels )
		{
			Mesh[][][] result = new Mesh[subModels.Count()][][];
			for ( var i = 0; i < subModels.Count(); i++ )
			{
				var bodyParts = subModels[i];
				Mesh[][] meshes = new Mesh[bodyParts.Count()][];
				for ( int y = 0; y < bodyParts.Count(); y++ )
				{
					var subModel = bodyParts[y];
					int offset = subModel.meshIndex;

					Mesh[] meshResult = new Mesh[subModel.numMesh];

					for ( int j = 0; j < subModel.numMesh; j++ )
					{
						Mesh mesh = new Mesh()
						{
							numTris = BitConverter.ToInt32( buffer, offset ),
							triIndex = BitConverter.ToInt32( buffer, offset + 4 ),
							skinRef = BitConverter.ToInt32( buffer, offset + 8 ),
							numNorms = BitConverter.ToInt32( buffer, offset + 12 ),
							normIndex = BitConverter.ToInt32( buffer, offset + 16 ),
						};

						offset += 20;

						meshResult[j] = mesh;
					}
					meshes[y] = meshResult;
				}
				await Task.Yield();
				result[i] = meshes;
			}

			return result;
		}

		/**
		 * Parses submodels vertices.
		 * Path: vertices[bodyPartIndex][subModelIndex]
		 */
		/*	public static float[][][] ParseVertices( byte[] buffer, SubModel[][] subModels )
		{
			return subModels.Select( bodyPart =>
				bodyPart.Select( subModel => buffer
					.Skip( subModel.vertIndex )
					.Select( ( b, i ) => new { Byte = b, Index = i } )
					.Where( x => x.Index % sizeof( float ) == 0 )
					.Select( x => BitConverter.ToSingle( buffer, subModel.vertIndex + x.Index ) )
					.ToArray() )
					.ToArray() )
				.ToArray();
		}*/
		/*public static float[][] parseVertices( byte[] buffer, SubModel[][] subModels )
		{

			float[][] result = new float[subModels.Count()][];
			for ( var i = 0; i < subModels.Count(); i++ )
			{
				var bodyPart = subModels[i];

				int offset = bodyPart.index;

				float[] structResult = new float[bodyPart.numModels];

				for ( int j = 0; j < bodyPart.numModels; j++ )
				{
					SubModel subModel = new SubModel()
					{
						name = BitConverter.ToString( buffer, offset + 0, 64 ).TrimEnd( '\0' ),
						type = BitConverter.ToInt32( buffer, offset + 64 ),
						boundingRadius = BitConverter.ToSingle( buffer, offset + 68 ),
						numMesh = BitConverter.ToInt32( buffer, offset + 72 ),
						meshIndex = BitConverter.ToInt32( buffer, offset + 76 ),
						numVerts = BitConverter.ToInt32( buffer, offset + 80 ),
						vertInfoIndex = BitConverter.ToInt32( buffer, offset + 84 ),
						vertIndex = BitConverter.ToInt32( buffer, offset + 88 ),
						numNorms = BitConverter.ToInt32( buffer, offset + 92 ),
						normInfoIndex = BitConverter.ToInt32( buffer, offset + 96 ),
						normIndex = BitConverter.ToInt32( buffer, offset + 100 ),
						numGroups = BitConverter.ToInt32( buffer, offset + 104 ),
						groupIndex = BitConverter.ToInt32( buffer, offset + 108 ),
					};

					offset += 108;

					structResult[j] = subModel;
				}
				result[i] = structResult;
			}

			return result;
		}*/

		public static float[][][] ParseVertices( byte[] buffer, SubModel[][] subModels )
		{
			return subModels.Select( bodyPart =>
				bodyPart.Select( subModel =>
						  //buffer.Skip( subModel.vertIndex )
						  Enumerable.Range(0, subModel.numVerts * 3).Select( index =>
						  BitConverter.ToSingle( buffer, subModel.vertIndex + (index * sizeof( float )) ) ) // buffer.Select( ( _, index ) =>
						  .ToArray()
				).ToArray()
			).ToArray();

			//return subModels.Select( bodyPart =>
			//	bodyPart.Select( subModel => new ArraySegment<byte>( buffer, subModel.vertIndex, subModel.numVerts ).ToArray() ).ToArray() ).ToArray();
		}

		/**
		 * Parses ones vertices buffer.
		 * Path: vertBoneBuffer[bodyPartIndex][subModelIndex]
		 */
		public static byte[][][] ParseVertBoneBuffer( byte[] buffer, SubModel[][] subModels )
		{
			return subModels.Select( bodyPart =>
				bodyPart.Select( subModel => new ArraySegment<byte>( buffer, subModel.vertInfoIndex, subModel.numVerts ).ToArray() ).ToArray() ).ToArray();
		}

		/*public static Int16[][][] ParseTriangles( byte[] buffer, Mesh[][][] meshes, int headerLength )
		{
			var result = new Int16[meshes.Length][][];

			for ( int i = 0; i < meshes.Length; i++ )
			{
				var bodyPart = meshes[i];
				result[i] = new Int16[bodyPart.Length][];

				for ( int j = 0; j < bodyPart.Length; j++ )
				{
					var subModel = bodyPart[j];
					result[i][j] = new Int16[subModel.Length];

					for ( int k = 0; k < subModel.Length; k++ )
					{
						var mesh = subModel[k];
						var start = mesh.triIndex;
						var count = (int)MathF.Floor((headerLength - start) / 2);
						result[i][j][k] = BitConverter.ToInt16( buffer, start );

						for ( int l = 1; l < count; l++ )
						{
							result[i][j][k + l] = BitConverter.ToInt16( buffer, start + l * 2 );
						}
					}
				}
			}

			return result;
		}*/
		public static short[][][][] ParseTriangles( byte[] buffer, Mesh[][][] meshes, int headerLength )
		{
			return meshes.Select( bodyPart =>
				bodyPart.Select( subModel =>
					subModel.Select( mesh => {
						int triIndex = mesh.triIndex;
						int numTriangles = (headerLength - triIndex) / 2;
						short[] triangles = new short[numTriangles];
						for ( int i = 0; i < numTriangles; i++ )
						{
							triangles[i] = BitConverter.ToInt16( buffer, triIndex + i * 2 );
						}
						return triangles;
					} ).ToArray()
				).ToArray()
			).ToArray();
		}

		public static async Task<Animation[][]> ParseAnimations( byte[] buffer, SeqDesc[] sequences, int numBones )
		{
			Animation[][] result = new Animation[sequences.Count()][];
			var buffCount = buffer.Count();
			for ( var i = 0; i < sequences.Count(); i++ )
			{
				var animation = sequences[i];

				int offset = animation.animIndex;

				Animation[] structResult = new Animation[numBones];

				for ( int j = 0; j < numBones; j++ )
				{
					Animation anim = new Animation()
					{
						offset = Enumerable.Range( 0, 6 ).Select( r => {

						var _offset = offset + r * sizeof( ushort );
						return _offset >= buffCount ? (ushort)0 : BitConverter.ToUInt16( buffer, _offset );
						
					}
						).ToArray(),
					};

					offset += sizeof( ushort ) * 6;

					structResult[j] = anim;
				}
				result[i] = structResult;
				await Task.Yield();
			}

			return result;
		}

		/*public static short[][][][][] ParseAnimValues(
	BinaryReader reader,
	SeqDesc[] sequences,
	Animation[][] animations,
	int numBones )
		{
			const int AXLES_NUM = 6;
			const int MAX_SRCBONES = 192;

			var animStructLength = 2*6;//BinaryReaderExtensions.StructLength<Structs.Animation>();

			// Create frames values array
			var animValues = new short[sequences.Count()][][][][];

			for ( int i = 0; i < sequences.Count(); i++ )
			{
				animValues[i] = new short[numBones][][][];

				for ( int j = 0; j < numBones; j++ )
				{
					animValues[i][j] = new short[AXLES_NUM][][];

					var animationIndex = sequences[i].animIndex + j * animStructLength;

					for ( int axis = 0; axis < AXLES_NUM; axis++ )
					{
						animValues[i][j][axis] = new short[MAX_SRCBONES][];

						for ( int v = 0; v < MAX_SRCBONES; v++ )
						{
							var offset = animationIndex + animations[i][j].offset[axis + AXLES_NUM] + v * sizeof( short );

							var value = reader.ReadInt16( offset );
							var valid = reader.ReadByte( offset );
							var total = reader.ReadByte( offset + sizeof( byte ) );

							animValues[i][j][axis][v] = new short[] { value, valid, total };
						}
					}
				}
			}

			return animValues;
		}*/

		public static async Task<short[][][][][]> ParseAnimValues( byte[] data, SeqDesc[] sequences, Animation[][] animations, int numBones )
		{
			var animStructLength = 2 * 6; // sizeof(short) * AXLES_NUM
										  // Create frames values array
			var dataCount = data.Count();

			var animValues = new short[sequences.Length][][][][];

			for ( int i = 0; i < sequences.Length; i++ )
			{
				animValues[i] = new short[numBones][][][];

				for ( int j = 0; j < numBones; j++ )
				{
					animValues[i][j] = new short[AXLES_NUM][][];

					var animationIndex = /* seqGroup.data + */ sequences[i].animIndex + j * animStructLength;

					for ( int axis = 0; axis < AXLES_NUM; axis++ )
					{
						animValues[i][j][axis] = new short[MAX_SRCBONES][];

						for ( int v = 0; v < MAX_SRCBONES; v++ )
						{
							var offset = animationIndex + animations[i][j].offset[axis + AXLES_NUM] + (v * (sizeof( short ) ));

							var value = offset+2 >= dataCount ? (short)0 : BitConverter.ToInt16( data, offset );
							var valid = offset >= dataCount ? (byte)0 : data[offset];
							var total = offset + sizeof( byte ) >= dataCount ? (byte)0 : data[offset + sizeof( byte )];

							animValues[i][j][axis][v] = new short[] { value, valid, total };
						}
					}
				}
				await Task.Yield();
			}

			return animValues;
		}

		public struct ModelParser
		{
			public Header header;
			public Bone[] bones;
			public BoneController[] boneControllers;
			public Attachment[] attachments;
			public BoundingBox[] hitBoxes;
			public SeqDesc[] sequences;
			public SeqGroup[] sequenceGroups;
			public BodyPart[] bodyParts;
			public Texture[] textures;
			public short[] skinRef;
			public SubModel[][] subModels;
			public Mesh[][][] meshes;
			public float[][][] vertices;
			public byte[][][] vertBoneBuffer;
			public short[][][][] triangles;
			public Animation[][] animations;
			public short[][][][][] animValues;
		}

		/**
		 * Returns parsed data of MDL file. A MDL file is a binary buffer divided in
		 * two part: header and data. Information about the data and their position is
		 * in the header.
		 * @param modelBuffer The MDL file buffer
		 * @returns {ModelDataParser}
		 */
		public async static Task<ModelParser> ParseModel( byte[] modelBuffer )
		{
			PreparingIndicator.Update( "Models" );

			// Reading header of the model
			var header = ParseHeader( ref modelBuffer );

			/*Log.Info( header.attachmentIndex );
			Log.Info( header.bbmax );
			Log.Info( header.bbmin );
			Log.Info( header.bodyPartIndex );
			Log.Info( header.boneControllerIndex );
			Log.Info( header.boneIndex );
			Log.Info( header.eyePosition );
			Log.Info( header.flags );
			Log.Info( header.hitBoxIndex );
			Log.Info( header.id );
			Log.Info( header.length );
			Log.Info( header.max );
			Log.Info( header.min );
			Log.Info( header.name );
			Log.Info( header.numAttachments );
			Log.Info( header.numBodyParts );
			Log.Info( header.numBoneControllers );
			Log.Info( header.numBones );
			Log.Info( header.numHitboxes );
			Log.Info( header.numSeq );
			Log.Info( header.numSeqGroups );
			Log.Info( header.numSkinFamilies );
			Log.Info( header.numSkinRef );
			Log.Info( header.numTextures );
			Log.Info( header.numTransitions );
			Log.Info( header.seqGroupIndex );
			Log.Info( header.seqIndex );
			Log.Info( header.skinIndex );
			Log.Info( header.soundGroupIndex );
			Log.Info( header.soundGroups );
			Log.Info( header.soundIndex );
			Log.Info( header.soundTable );
			Log.Info( header.textureDataIndex );
			Log.Info( header.textureIndex );
			Log.Info( header.transitionIndex );
			Log.Info( header.version );*/


			// Checking version of MDL file
			if ( header.version != VERSION )
			{
				throw new Exception( "Unsupported version of the MDL file" );
			}

			// Checking textures of the model
			// Some models don't have any texture
			if ( header.textureIndex == 0 || header.numTextures == 0 )
				Notify.CreateCL( $"No textures in the MDL file ({header.name})" );

			// The data below will be used to obtain another data

			// Body parts info
			var bodyParts = ParseBodyParts( modelBuffer, header.bodyPartIndex, header.numBodyParts );
			// Submodels info
			var subModels = parseSubModel( modelBuffer, bodyParts );

			await Task.Yield();

			PreparingIndicator.Update( "Models" );

			// Meshes info
			var meshes = await parseMeshes( modelBuffer, subModels );

			PreparingIndicator.Update( "Models" );

			//  Model sequences info
			var sequences = ParseSequences( modelBuffer, header.seqIndex, header.numSeq );

			await Task.Yield();

			PreparingIndicator.Update( "Models" );

			// Bones animations
			var animations = await ParseAnimations( modelBuffer, sequences, header.numBones );

			PreparingIndicator.Update( "Models" );

			// Animation frames
			var animValues = await ParseAnimValues( modelBuffer, sequences, animations, header.numBones );

			await Task.Yield();

			PreparingIndicator.Update( "Models" );

			var modelData = new ModelParser()
			{
				// The header of the MDL file
				header = header,

				// Main data that was obtained directly from the MDL file header

				// Bones info
				bones = ParseBones( modelBuffer, header.boneIndex, header.numBones ),
				// Bone controllers
				boneControllers = ParseBoneControllers( modelBuffer, header.boneControllerIndex, header.numBoneControllers ),
				// Model attachments
				attachments = ParseAttachments( modelBuffer, header.attachmentIndex, header.numAttachments ),
				// Model hitboxes
				hitBoxes = ParseHitboxes( modelBuffer, header.hitBoxIndex, header.numHitboxes ),
				// Model sequences info
				sequences = sequences,
				// Sequences groups
				sequenceGroups = ParseSequenceGroups( modelBuffer, header.seqGroupIndex, header.numSeqGroups ),
				// Body parts info
				bodyParts = bodyParts,
				// Textures info
				textures = ParseTextures( modelBuffer, header.textureIndex, header.numTextures ),
				// Skins references
				skinRef = ParseSkinRef( modelBuffer, header.skinIndex, header.numSkinRef ),

				// Sub models data. This data was obtained by parsing data from body parts

				// Submodels info
				subModels = subModels,
				// Meshes info. Path: meshes[bodyPartIndex][subModelIndex][meshIndex]
				meshes = meshes,
				// Submodels vertices. Path: vertices[bodyPartIndex][subModelIndex]
				vertices = ParseVertices( modelBuffer, subModels ),
				// Bones vertices buffer. Path: vertBoneBuffer[bodyPartIndex][subModelIndex]
				vertBoneBuffer = ParseVertBoneBuffer( modelBuffer, subModels ),
				// Mesh triangles. Path: meshes[bodyPartIndex][subModelIndex][meshIndex]
				triangles = ParseTriangles( modelBuffer, meshes, header.length ),

				// Sequences data

				// Bones animations
				animations = animations,
				// Animation frames
				animValues = animValues
			};

			return modelData;
		}
	}


}
