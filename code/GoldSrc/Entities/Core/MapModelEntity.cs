// sbox.Community © 2023-2024

using Sandbox;
using System.Collections.Generic;
using System.Linq;
using static MapParser.Manager;

namespace MapParser.GoldSrc.Entities
{
	// This entity from the lump of Models of BSP. I didn't seperate physics of these from the map physics for now, so only creating clside entity.
	public partial class MapModelEntity : SceneCustomObject
	{
		private static Material renderMat = Material.FromShader( "shaders/goldsrc_render.shader" );
		private Texture lightmap;
		private List<(VertexBuffer, Texture, int)> vertexBuffer = new(); // int: faceindex
		private int vertexBufferCount;
		public bool render = false;
		public EntityParser.EntityData entity;
		//public int meshCount = 0; // debug purposes
		float opacity = 1f;
		public Map.Map_CL parent;

		public MapModelEntity( SpawnParameter settings, Texture lightmap, Map.Map_CL parent, EntityParser.EntityData entity, List<(List<Vertex>, List<int>, string, int)> meshInfo, Vector3 Mins, Vector3 Maxs, Vector3 vOrigin ) : base( settings.sceneWorld )
		{
			Flags.IsOpaque = true;
			Flags.IsTranslucent = false;
			Flags.IsDecal = false;
			Flags.OverlayLayer = false;
			Flags.BloomLayer = false;
			Flags.ViewModelLayer = false;
			Flags.SkyBoxLayer = false;
			Flags.NeedsLightProbe = true;

			this.lightmap = lightmap;
			this.entity = entity;
			this.parent = parent;

			Position = settings.position + vOrigin;

			if ( entity.data.TryGetValue( "origin", out var origin ) )
				Position += Vector3.Parse( origin );

			if ( entity.data.TryGetValue( "angles", out var angles ) )
				Rotation = Rotation.From( Angles.Parse( angles ) );

			Transform tf = new();
			tf.Position = Position;
			tf.Rotation = Rotation;

			if ( entity.data.TryGetValue( "renderamt", out var renderamt ) )
				opacity = int.Parse( renderamt ) / 255f;

			if ( opacity < 1f )
				Flags.IsTranslucent = true;

			// TODO: Add RenderColor, should we get mins and maxs from entData?

			// Mins and Maxs were coming from Lump.Models, but I guess there is a problem. For now, mins and maxs finding manually with on the vertices
			/*Mins += Position;
			Maxs += Position;
			Bounds = new BBox( Mins, Maxs );*/

			var vertexBufferIndex = 0;
			Dictionary<int, string> texturesNeedLoaded = new();
			bool initializeMinsMaxs = true;
			Vector3 mins = new();
			Vector3 maxs = new();

			foreach ( var mesh in meshInfo )
			{
				PreparingIndicator.Update( "Static Models" );

				VertexBuffer buffer = new();
				buffer.Init( true );

				foreach ( var vertex in mesh.Item1 )
				{
					var vec = tf.TransformVector( vertex.Position );

					// Finding mins and maxs
					if ( initializeMinsMaxs )
					{
						mins = new Vector3( vec.x, vec.y, vec.z );
						maxs = new Vector3( vec.x, vec.y, vec.z );
						initializeMinsMaxs = false;
					}
					if ( vec.x < mins.x )
						mins.x = vec.x;

					if ( vec.y < mins.y )
						mins.y = vec.y;

					if ( vec.z < mins.z )
						mins.z = vec.z;

					if ( vec.x > maxs.x )
						maxs.x = vec.x;

					if ( vec.y > maxs.y )
						maxs.y = vec.y;

					if ( vec.z > maxs.z )
						maxs.z = vec.z;

					buffer.Add( new( vec, vertex.Normal, vertex.Tangent, vertex.TexCoord0 ) );
				}

				foreach ( var indice in mesh.Item2 )
					buffer.AddRawIndex( indice );

				var findTexture = MapParser.Render.TextureCache.textureData.TryGetValue( mesh.Item3, out var texCacheData );
				if ( !findTexture )
					texturesNeedLoaded.Add( vertexBufferIndex, mesh.Item3 );

				vertexBuffer.Add( (buffer, findTexture ? texCacheData.texture : Texture.Invalid, mesh.Item4) );

				vertexBufferIndex++;
			}

			vertexBufferCount = vertexBuffer.Count();

			createTextures( texturesNeedLoaded, settings, this );

			Bounds = new BBox( mins, maxs );

			//meshCount = meshInfo.Count();
		}
		public async void createTextures( Dictionary<int, string> texturesNeedLoaded, SpawnParameter settings, MapModelEntity mapEntity ) => await GameTask.RunInThreadAsync( () => TextureCache.addTextures( texturesNeedLoaded, settings, mapEntity: mapEntity ) );

		public override void RenderSceneObject()
		{
			if ( !render )
				return;

			if ( Graphics.LayerType != SceneLayerType.Opaque && Graphics.LayerType != SceneLayerType.Translucent )
				return;

			Graphics.Attributes.Set( "TextureLightmap", lightmap );
			Graphics.Attributes.Set( "Opacity", opacity );

			for ( var i = 0; i < vertexBufferCount; i++ )
			{
				var vertices = vertexBuffer[i];
				Graphics.Attributes.Set( "TextureDiffuse", vertices.Item2 );
				vertices.Item1.Draw( renderMat );
			}

			base.RenderSceneObject();

			//DebugOverlay.Text( $"{entity.classname}",Position , 0f );
			//DebugOverlay.Box( Bounds.Mins, Bounds.Maxs, Color.Red );
		}
		public void updateTexture( int key, Texture newTex )
		{
			PreparingIndicator.Update( "Texture" );

			for ( var i = 0; i < vertexBuffer.Count(); i++ )
			{
				var vb = vertexBuffer[i];

				if ( key == i )
					vb.Item2 = newTex;

				vertexBuffer[i] = vb;
			}
		}
	}
}
