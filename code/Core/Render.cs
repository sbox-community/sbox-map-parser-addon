using Sandbox;
using Sandbox.Internal;
using System.Collections.Generic;
using System.Linq;

namespace MapParser
{
	public static class Render
	{
		//public static Dictionary<string, Material> pendingMaterials = new Dictionary<string, Material>();
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

		public struct MaterialCacheData
		{
			public string name { get; set; }
			public Material material { get; set; }
			//public Texture texture { get; set; } //we must use shader, be removed
		}

		public static class MaterialCache
		{
			public static Dictionary<string, MaterialCacheData> materialData = new();

			public static void CreateMaterial( string texName ) //, ref SurfaceLightmapData lightmapData, int id
			{
				//var textureFounded = textureData.Find( ( textureName ) => textureName.Name == texName );
				//if ( textureFounded == null )
				var matName = texName;// + "_" + id;
				var matFound = materialData.TryGetValue( matName, out var materialFounded );

				if ( !matFound ) //there also should be materialData comparison
				{
					var found = TextureCache.textureData.TryGetValue( texName, out var infoFromTextureData );
					
					if ( !found )
					{
						Notify.Create( $"Texture not found: {texName}" , Notify.NotifyType.Error );

						Manager.lastTextureErrors.Add( texName );

						//pendingMaterials.TryAdd( texName, mat );
						//return;
					}

					var mat = Material.Create( matName, "simple" ); //goldsrc_render
					mat.Set( "Color", infoFromTextureData.texture );
					mat.Set( "Normal", Texture.Invalid );

					//mat.Set( "TextureDiffuse", infoFromTextureData.texture );
					//mat.Set( "TextureLightmap", lightmap );

					materialData.Add( matName,
						new MaterialCacheData()
						{
							name = texName,
							material = mat,
							//texture = TextureCache.createTexture( texName ),//TextureCache.createTextureWithLightmapped( texName, ref lightmapData ),
						}
					);

					//mat.Set( "g_vTexCoordScale", new Vector2( 6f, 4f ) ); // 0.011385f, 0.00785f
					//mat.Set( "g_vTexCoordOffset", new Vector2( 20.0f, 1.0f ) ); // 0.011385f, 0.00785f

					//var val = materialData[texName];
					//val.material = mat;
					//val.texture = MIPTEXData.ViewerTexture;
					//val.width = MIPTEXData.Width;
					//val.height = MIPTEXData.Height;
					//materialData[texName] = val;
					//materialData[texName + "_" + id] = val;
				}
			}

			public static void clearMaterial( string matname = "" )
			{
				var empty = string.IsNullOrEmpty( matname );
				/*foreach ( var kvp in materialData )
				{
					if ( (!empty && kvp.Key == matname) || empty )
					{
						materialData[kvp.Key].texture.Dispose();

						if ( !empty )
							break;
					}
				}*/
				if ( empty )
					materialData.Clear();
				else
					materialData.Remove( matname );
			}
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
						if ( kvp.Key != null && textureData.TryGetValue( kvp.Key, out var texData) && texData.texture != null )
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
