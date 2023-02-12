using Sandbox;
using System.Collections.Generic;

namespace MapParser
{
	public static class Render
	{
		public enum TextureCacheType
		{
			MIPTEX,
		}
		public struct TextureCacheData
		{
			public string name { get; set; }
			public int width { get; set; }
			public int height { get; set; }
			public string WADname { get; set; }
			public TextureCacheType type { get; set; }
			public Texture texture { get; set; }
			//public byte[] textureData { get; set; }
		}

		public static class TextureCache
		{
			public static Dictionary<string, TextureCacheData> textureData = new();

			public static void clearTexture( string texname = "" )
			{
				var empty = string.IsNullOrEmpty( texname );
				foreach ( var kvp in textureData )
				{
					if ( (!empty && kvp.Key == texname) || empty )
					{
						if ( kvp.Key != null && textureData.TryGetValue( kvp.Key, out var texData ) && texData.texture != null )
							textureData[kvp.Key].texture.Dispose();

						if ( !empty )
							break;
					}
				}

				if ( empty )
					textureData.Clear();
				else
					textureData.Remove( texname );
			}
		}
	}
}
