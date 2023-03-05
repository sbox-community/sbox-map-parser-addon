// sbox.Community © 2023-2024

namespace MapParser.GoldSrc.Entities
{
	public static class Structs
	{
		/**
		 * Head of mdl-file
		 */
		public struct Header
		{
			/** Model format ID */
			public int id;
			/** Format version number */
			public int version;
			/** The internal name of the model */
			public string name;
			/** Data size of MDL file in bytes */
			public int length;
			/** Position of player viewpoint relative to model origin */
			public Vector3 eyePosition;
			/** Corner of model hull box with the least X/Y/Z values */
			public Vector3 max;
			/** Opposite corner of model hull box */
			public Vector3 min;
			/** Min position of view bounding box */
			public Vector3 bbmin;
			/** Max position of view bounding box */
			public Vector3 bbmax;
			/**
			 * Binary flags in little-endian order.
			 * ex (00000001, 00000000, 00000000, 11000000) means flags for position
			 * 0, 30, and 31 are set. Set model flags section for more information
			 */
			public int flags;

			// After this point, the header contains many references to offsets
			// within the MDL file and the number of items at those offsets.
			// Offsets are from the very beginning of the file.
			// Note that indexes/counts are not always paired and ordered consistently.

			/** Number of bones */
			public int numBones;
			/** Offset of first data section */
			public int boneIndex;
			/** Number of bone controllers */
			public int numBoneControllers;
			/** Offset of bone controllers */
			public int boneControllerIndex;
			/** Number of complex bounding boxes */
			public int numHitboxes;
			/** Offset of hit boxes */
			public int hitBoxIndex;
			/** Number of sequences */
			public int numSeq;
			/** Offset of sequences */
			public int seqIndex;
			/** Number of demand loaded sequences */
			public int numSeqGroups;
			/** Offset of demand loaded sequences */
			public int seqGroupIndex;
			/** Number of raw textures */
			public int numTextures;
			/** Offset of raw textures */
			public int textureIndex;
			/** Offset of textures data */
			public int textureDataIndex;
			/** Number of replaceable textures */
			public int numSkinRef;
			public int numSkinFamilies;
			public int skinIndex;
			/** Number of body parts */
			public int numBodyParts;
			/** Index of body parts */
			public int bodyPartIndex;
			/** Number queryable attachable points */
			public int numAttachments;
			public int attachmentIndex;
			// This seems to be obsolete.
			// Probably replaced by events that reference external sounds?
			public int soundTable;
			public int soundIndex;
			public int soundGroups;
			public int soundGroupIndex;
			/** Animation node to animation node transition graph */
			public int numTransitions;
			public int transitionIndex;
		}
		/**
	 * Bone description
	 */
		public struct Bone
		{
			/** Bone name for symbolic links */
			public string name;
			/** Parent bone */
			public int parent;
			/** ?? */
			public int flags;
			/** Bone controller index, -1 == none */
			public int[] boneController;
			/** Default DoF values */
			public float[] value;
			/** Scale for delta DoF values */
			public float[] scale;
		}

		/**
	 * Bone controllers
	 */
		public struct BoneController
		{
			public int bone;
			public int type;
			public float start;
			public float end;
			public int rest;
			public int index;
		}
	

	/**
	 * Attachment
	 */
	public struct Attachment
	{
		public string name;
		public int type;
		public int bone;
		/** Attachment point */
		public Vector3 org;
		public Vector3[] vectors;
	}

	/**
	 * Bounding boxes
	 */
	public struct BoundingBox
	{
		public int bone;
		/** Intersection group */
		public int group;
		/** Bounding box */
		public Vector3 bbmin;
		public Vector3 bbmax;
	}


	/**
	 * Sequence description
	 */
	public class SeqDesc
	{
		public string label;    // Sequence label
		public float fps;       // Frames per second
		public int flags;       // Looping/non-looping flags
		public int activity;
		public int actWeight;
		public int numEvents;
		public int eventIndex;
		public int numFrames;   // Number of frames per sequence
		public int numPivots;   // Number of foot pivots
		public int pivotIndex;
		public int motionType;
		public int motionBone;
		public Vector3 linearMovement;
		public int autoMovePosIndex;
		public int autoMoveAngleIndex;
		public Vector3 bbmin;      // Per sequence bounding box
		public Vector3 bbmax;
		public int numBlends;
		public int animIndex;   // "anim" pointer relative to start of sequence group data
		public int[] blendType;// = new int[2, 6];   // [blend][bone][X, Y, Z, XR, YR, ZR]
		public float[] blendStart;// = new float[2, 6];   // Starting value
		public float[] blendEnd;// = new float[2, 6];     // Ending value
		public int blendParent;
		public int seqGroup;    // Sequence group for demand loading
		public int entryNode;   // Transition node at entry
		public int exitNode;    // Transition node at exit
		public int nodeFlags;   // Transition rules
		public int nextSeq;     // Auto advancing sequences
	}

	/**
	 * Demand loaded sequence groups
	 */
	public struct SeqGroup
	{
		/** Textual name */
		public string label;
		/** File name */
		public string name;
		/** Was "cache" - index pointer */
		public int unused1;
		/** Was "data" - hack for group 0 */
		public int unused2;
	}


	/**
	 * Body part index
	 */
	public struct BodyPart
	{
		public string name;
		public int numModels;
		public int @base;
		/** Index into models array */
		public int modelIndex;
	}

	/**
	 * Texture info
	 */

	public struct Texture
	{
		/** Texture name */
		public string name;
		/** Flags */
		public int flags;
		/** Texture width */
		public int width;
		/** Texture height */
		public int height;
		/** Texture data offset */
		public int index;
	}

	/**
	 * Sub models
	 */
	public struct SubModel
	{
		public string name;

		public int type;

		public float boundingRadius;

		public int numMesh;
		public int meshIndex;

		/** Number of unique vertices */
		public int numVerts;
		/** Vertex bone info */
		public int vertInfoIndex;
		/** Vertex vec3 */
		public int vertIndex;
		/** Number of unique surface normals */
		public int numNorms;
		/** Normal bone info */
		public int normInfoIndex;
		/** Normal vec3 */
		public int normIndex;

		/** Deformation groups */
		public int numGroups;
		public int groupIndex;
	}

	/**
	 * Mesh info
	 */
	public struct Mesh
	{
		public int numTris;
		public int triIndex;
		public int skinRef;
		/** Per mesh normals */
		public int numNorms;
		/** Normal vec3_t */
		public int normIndex;
	}

	/**
	 * Animation description
	 */
	public struct Animation
	{
		public ushort[] offset;
	}

	}
}
