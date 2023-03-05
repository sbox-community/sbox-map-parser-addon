// sbox.Community © 2023-2024

namespace MapParser.GoldSrc.Entities
{
	public static class Constants
	{

		/** Supported model format version */
		public const int VERSION = 10;

		/** Maximum number of bone controllers per bone */
		public const int MAX_PER_BONE_CONTROLLERS = 6;

		/** Flag of texture masking */
		public const int NF_MASKED = 0x0040;

		/** Number of colors */
		public const int PALETTE_ENTRIES = 256;

		/** Number of channels for RGB color. Was "PALETTE_CHANNELS" */
		public const int RGB_SIZE = 3;

		/** Number of channels for RGBA color. Was "PALETTE_CHANNELS_ALPHA" */
		public const int RGBA_SIZE = 4;

		/** Total size of a palette, in bytes. */
		public const int PALETTE_SIZE = PALETTE_ENTRIES * RGB_SIZE;

		/** The index in a palette where the alpha color is stored. Used for transparent textures. */
		public const int PALETTE_ALPHA_INDEX = 255 * RGB_SIZE;

		/** Number of bones allowed at source movement */
		public const int MAX_SRCBONES = 512;

		/** Number of axles in 3d space */
		public const int AXLES_NUM = 3;

		/** Animation value items index constants */
		public enum ANIM_VALUE
		{
			VALUE = 0,
			VALID,
			TOTAL
		}

		/** Triangle fan type */
		public const int TRIANGLE_FAN = 0;

		/** Triangle strip type */
		public const int TRIANGLE_STRIP = 1;

		/** Motion flag X */
		public const int MOTION_X = 0x0001;

		/** Motion flag Y */
		public const int MOTION_Y = 0x0002;

		/** Motion flag Z */
		public const int MOTION_Z = 0x0004;

		/** Controller that wraps shortest distance */
		public const int RLOOP = 0x8000;

		/** Default interface background color */
		public const string INITIAL_UI_BACKGROUND = "#4d7f7e";
	}

}
