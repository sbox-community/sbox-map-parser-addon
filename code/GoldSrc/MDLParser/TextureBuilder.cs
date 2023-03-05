// sbox.Community © 2023-2024

using System.Linq;
using static MapParser.GoldSrc.Entities.Constants;
using static MapParser.GoldSrc.Entities.Structs;

namespace MapParser.GoldSrc.Entities
{
	public static class TextureBuilder
	{

		public static ushort[] BuildTexture( ref byte[] buffer, Texture texture )
		{
			int textureArea = texture.width * texture.height;
			int isTextureMasked = texture.flags & NF_MASKED;

			var textureData = buffer.Skip( texture.index ).Take( textureArea ).ToArray();
			var palette = buffer.Skip( texture.index + textureArea ).Take( PALETTE_SIZE ).ToArray();
			var alphaColor = palette.Skip( PALETTE_ALPHA_INDEX ).Take( RGB_SIZE ).ToArray();

			var imageBuffer = new ushort[textureArea * RGBA_SIZE];

			for ( int i = 0; i < textureData.Length; i++ )
			{
				ushort item = textureData[i];
				int paletteOffset = item * RGB_SIZE;
				int pixelOffset = i * RGBA_SIZE;

				bool isAlphaColor = palette[paletteOffset + 0] == alphaColor[0] &&
									palette[paletteOffset + 1] == alphaColor[1] &&
									palette[paletteOffset + 2] == alphaColor[2];

				if ( isTextureMasked != 0 && isAlphaColor )
				{
					// This modifies the model's data. Sets the mask color to black.
					// This is also done by Jed's model viewer (export texture has black)
					imageBuffer[pixelOffset + 0] = 0; // red
					imageBuffer[pixelOffset + 1] = 0; // green
					imageBuffer[pixelOffset + 2] = 0; // blue
					imageBuffer[pixelOffset + 3] = 0; // alpha
				}
				else
				{
					// Just applying to texture image data
					imageBuffer[pixelOffset + 0] = palette[paletteOffset + 0]; // red
					imageBuffer[pixelOffset + 1] = palette[paletteOffset + 1]; // green
					imageBuffer[pixelOffset + 2] = palette[paletteOffset + 2]; // blue
					imageBuffer[pixelOffset + 3] = 255; // alpha
				}
			}

			return imageBuffer;
		}

	}
}
