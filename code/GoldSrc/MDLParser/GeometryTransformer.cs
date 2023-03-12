// sbox.Community © 2023-2024

using System;
using System.Collections.Generic;
using System.Numerics;
using static MapParser.GoldSrc.Entities.Constants;
using static MapParser.GoldSrc.Entities.Structs;

namespace MapParser.GoldSrc.Entities
{
	public static class GeometryTransformer
	{

		/**
		 * Calculates bone angle
		 */
		public static Quaternion CalcBoneQuaternion(
			ref int frame,
			ref Bone bone,
			ref ushort[] animOffset,
			ref short[][][][][] animValues,
			int sequenceIndex,
			int boneIndex,
			ref float s )
		{
			Vector3 angle1 = Vector3.Zero;
			Vector3 angle2 = Vector3.Zero;

			var animValWithBone = animValues[sequenceIndex][boneIndex];

			float getTotal( ref int index, ref short[][] animValBoneWithAxis )
			{
				try
				{
					return animValBoneWithAxis[index][(int)ANIM_VALUE.TOTAL];
				}
				catch ( IndexOutOfRangeException )
				{
					return float.NaN;
				}
			};

			float getValue( int index, ref short[][] animValBoneWithAxis )
			{
				try
				{
					return animValBoneWithAxis[index][(int)ANIM_VALUE.VALUE];
				}
				catch ( IndexOutOfRangeException )
				{
					return float.NaN;
				}
			};

			float getValid( int index, ref short[][] animValBoneWithAxis )
			{
				try
				{
					return animValBoneWithAxis[index][(int)ANIM_VALUE.VALID];
				}
				catch ( IndexOutOfRangeException )
				{
					return float.NaN;
				}
			};

			for ( int axis = 0; axis < 3; axis++ )
			{
				var animValBoneWithAxis = animValWithBone[axis];
				if ( animOffset[axis + 3] == 0 )
				{
					angle2[axis] = angle1[axis] = bone.value[axis + 3]; // default;
				}
				else
				{
				
					int i = 0;
					int k = frame;

					while ( getTotal( ref i, ref animValBoneWithAxis ) <= k )
					{
						k -= (int)getTotal( ref i, ref animValBoneWithAxis ); //MathF.Floor ?
						i += (int)getValid( i, ref animValBoneWithAxis ) + 1;
					}

					// Bah, missing blend!
					if ( getValid( i, ref animValBoneWithAxis ) > k )
					{
						angle1[axis] = getValue( i + k + 1, ref animValBoneWithAxis );

						if ( getValid( i, ref animValBoneWithAxis ) > k + 1 )
						{
							angle2[axis] = getValue( i + k + 2, ref animValBoneWithAxis );
						}
						else
						{
							if ( getTotal( ref i, ref animValBoneWithAxis ) > k + 1 )
							{
								angle2[axis] = angle1[axis];
							}
							else
							{
								angle2[axis] = getValue( i + (int)getValid( i, ref animValBoneWithAxis ) + 2, ref animValBoneWithAxis );
							}
						}
					}
					else
					{
						angle1[axis] = getValue( i + (int)getValid( i, ref animValBoneWithAxis ), ref animValBoneWithAxis );

						if ( getTotal( ref i, ref animValBoneWithAxis ) > k + 1 )
						{
							angle2[axis] = angle1[axis];
						}
						else
						{
							angle2[axis] = getValue( i + (int)getValid( i, ref animValBoneWithAxis ) + 2, ref animValBoneWithAxis );
						}
					}

					angle1[axis] = bone.value[axis + 3] + angle1[axis] * bone.scale[axis + 3];
					angle2[axis] = bone.value[axis + 3] + angle2[axis] * bone.scale[axis + 3];
				}
			}
	
			if ( angle1 == angle2 )
			{
				return AnglesToQuaternion( ref angle1 );
			}

			Quaternion q1 = AnglesToQuaternion( ref angle1 );
			Quaternion q2 = AnglesToQuaternion( ref angle2 );

			return Quaternion.Slerp( q1, q2, s );//Quaternion.Slerp( q1, q2, s );
		}

		/**
		 * Converts Euler angles into a quaternion
		 */
		private static Quaternion AnglesToQuaternion( ref Vector3 angles )
		{
			var pitch = angles.x;
			var roll = angles.y;
			var yaw = angles.z;
			// FIXME: rescale the inputs to 1/2 angle
			float cy = MathF.Cos( yaw * 0.5f );
			float sy = MathF.Sin( yaw * 0.5f );
			float cp = MathF.Cos( roll * 0.5f );
			float sp = MathF.Sin( roll * 0.5f );
			float cr = MathF.Cos( pitch * 0.5f );
			float sr = MathF.Sin( pitch * 0.5f );

			Quaternion result = new();
			result.X = sr * cp * cy - cr * sp * sy;
			result.Y = cr * sp * cy + sr * cp * sy;
			result.Z = cr * cp * sy - sr * sp * cy;
			result.W = cr * cp * cy + sr * sp * sy;

			return result;
		}

		public static Vector3 GetBonePositions(
			//int frame,
			ref Bone bone
			/*ushort[] animOffset,
			short[][][][][] animValues,
			int sequenceIndex,
			int boneIndex,
			float s*/ )
		{
			Vector3 position = Vector3.Zero;

			for ( int axis = 0; axis < 3; axis++ )
			{
				position[axis] = bone.value[axis];


				// TOD: fix this part

				// if (animOffset[axis] != 0) {
				//   const getTotal = (index: number) => animValues.get(sequenceIndex, boneIndex, axis, index, ANIM_VALUE.TOTAL)
				//   const getValue = (index: number) => animValues.get(sequenceIndex, boneIndex, axis, index, ANIM_VALUE.VALUE)
				//   const getValid = (index: number) => animValues.get(sequenceIndex, boneIndex, axis, index, ANIM_VALUE.VALID)

				//   let i = 0
				//   let k = frame

				//   // find span of values that includes the frame we want
				//   while (getTotal(i) <= k) {
				//     k -= getTotal(i)
				//     i += getValid(i) + 1
				//   }

				//   // if we're inside the span
				//   if (getValid(i) > k) {
				//     // and there's more data in the span
				//     if (getValid(i) > k + 1) {
				//       position[axis] += (getValue(i + k + 1) * (1.0 - s) + s * getValue(i + k + 2)) * bone.scale[axis]
				//     } else {
				//       position[axis] += getValue(i + k + 1) * bone.scale[axis]
				//     }
				//   } else {
				//     // are we at the end of the repeating values section and there's another section with data?
				//     if (getTotal(i) <= k + 1) {
				//       position[axis]
				//         += (getValue(i + getValid(i)) * (1.0 - s) + s * getValue(i + getValid(i) + 2)) * bone.scale[axis]
				//     } else {
				//       position[axis] += getValue(i + getValid(i)) * bone.scale[axis]
				//     }
				//   }
				// }

			}

			return position;
		}

		public static Matrix4x4[] CalcRotations(
			ref ModelDataParser.ModelParser modelData,
			ref int sequenceIndex,
			ref int frame,
			// TODO: Do something about it
			float s = 0f
		)
		{
			var bonesLength = modelData.bones.Length;
			
			Quaternion[] boneQuaternions = new Quaternion[bonesLength];
			Vector3[] bonesPositions = new Vector3[bonesLength];

			for ( int boneIndex = 0; boneIndex < bonesLength; boneIndex++ )
			{
				var bones = modelData.bones[boneIndex];
				var offsets = modelData.animations[sequenceIndex][boneIndex].offset;

				boneQuaternions[boneIndex] = CalcBoneQuaternion(
					ref frame,
					ref bones,
					ref offsets,
					ref modelData.animValues,
					sequenceIndex,
					boneIndex,
					ref s
				);

				bonesPositions[boneIndex] = GetBonePositions(
					ref bones
				/*frame,
				modelData.bones[boneIndex],
				modelData.animations[sequenceIndex][boneIndex].offset,
				modelData.animValues,
				sequenceIndex,
				boneIndex,
				s*/
				);
			}

			foreach ( int axis in new int[] { MOTION_X, MOTION_Y, MOTION_Z} )
			{

				if ( (modelData.sequences[sequenceIndex].motionType & axis) != 0 )
				{
					var vec = bonesPositions[modelData.sequences[sequenceIndex].motionBone];
					vec.y = 0;
					bonesPositions[modelData.sequences[sequenceIndex].motionBone] = vec;
				}
			}
			return CalcBoneTransforms( ref boneQuaternions, ref bonesPositions, ref modelData.bones );
		}

		private static Matrix4x4[] CalcBoneTransforms(
			ref Quaternion[] quaternions,
			ref Vector3[] positions,
			ref Bone[] bones
		)
		{
			var bonesLength = bones.Length;
			Matrix4x4[] boneTransforms = new Matrix4x4[bonesLength];

			for ( int i = 0; i < bonesLength; i++ )
			{
				Vector3 position = positions[i];

				Matrix4x4 boneMatrix = Matrix4x4.CreateFromQuaternion( quaternions[i] );

				boneMatrix.M41 = position.x;
				boneMatrix.M42 = position.y;
				boneMatrix.M43 = position.z;

				if ( bones[i].parent == -1 )
				{
					// Root bone
					boneTransforms[i] = boneMatrix;
				}
				else
				{
					boneTransforms[i] = boneMatrix * boneTransforms[bones[i].parent]; // Multiplication order is important
				}
			}
			return boneTransforms;
		}
	}
}
