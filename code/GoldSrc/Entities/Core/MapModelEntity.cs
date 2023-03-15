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
		private static Material renderMat = Material.FromShader( "shaders/goldsrc_static_model.shader" );
		private Texture lightmap;
		private List<(VertexBuffer, Texture, int)> vertexBuffer = new(); // int: faceindex
		private int vertexBufferCount;
		public bool render = false;
		public EntityParser.EntityData entity;
		//public int meshCount = 0; // debug purposes
		float opacity = 1f;
		public Map.Map_CL parent;
		Vector3 renderColor = Vector3.One;
		int renderMode = 0;
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

			// Mins and Maxs were coming from Lump.Models, but I guess there is a problem. For now, mins and maxs finding manually with on the vertices
			//Bounds = new BBox( tf.TransformVector( Mins ), tf.TransformVector( Maxs ) );

			if ( entity.data.TryGetValue( "rendermode", out var _rendermode ) )
				renderMode = int.Parse( _rendermode );

			if ( renderMode == 6 )
			{
				Delete();
				return;
			}

			if ( renderMode != 0 && renderMode != 4 && entity.data.TryGetValue( "renderamt", out var renderamt ) )
			{
				var val = int.Parse( renderamt );
				opacity = val / 255f;

				// There is problem with AlphaToCoverageEnable caused from shader
				/*if ( opacity > 0f )
					opacity = MathX.Remap( opacity, 0f, 1f, 0.25f, 1f );*/
				if ( opacity > 0f && opacity < 0.25f )
					opacity = 0.25f;
			}

			if ( ( renderMode == 1 || renderMode == 3 ) && entity.data.TryGetValue( "rendercolor", out var rendercolor ) )
			{
				var color = Vector3.Parse( rendercolor );
				renderColor = new Vector3( color.x / 255f, color.y / 255f, color.z / 255f );

				// i don't sure it is correct way, we are trying to get if they are default values, if so, don't calculate opacity and rendercolor..
				/*if ( renderColor == Vector3.Zero && opacity == 0f )
				{
					opacity = 1;
					renderColor = Color.White;
				}*/
			}

			if ( opacity < 1f )
				Flags.IsTranslucent = true;

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

					// The last meshes, they are breaks the mins and maxs, idk why, maybe releated leaf position? for visibility.
					// We are already manually binding to leaf as inside leaf of this entity. When we use Mins and Maxs from the parameter, it working on properly generally, but for some model entities are not work.
					// Verify and review..

					//if ( vertexBufferIndex != meshInfo.Count - 1 ) { 
						mins = Vector3.Min( mins, vec );
						maxs = Vector3.Max( maxs, vec );
					//}

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

			vertexBufferCount = vertexBuffer.Count;

			createTextures( texturesNeedLoaded, settings, this );

			Bounds = new BBox( mins, maxs );

			//meshCount = meshInfo.Count;
		}
		public async void createTextures( Dictionary<int, string> texturesNeedLoaded, SpawnParameter settings, MapModelEntity mapEntity ) => await GameTask.RunInThreadAsync( () => TextureCache.addTextures( texturesNeedLoaded, settings, mapEntity: mapEntity ) );

		public override void RenderSceneObject()
		{
			if ( !render )
				return;

			if ( Graphics.LayerType != SceneLayerType.Opaque && Graphics.LayerType != SceneLayerType.Translucent )
				return;

			Graphics.Attributes.Set( "RenderColor", renderColor );
			Graphics.Attributes.Set( "TextureLightmap", lightmap );
			Graphics.Attributes.Set( "Opacity", opacity );
			Graphics.Attributes.Set( "Pixelation", clientSettings.pixelation );

			for ( var i = 0; i < vertexBufferCount; i++ )
			{
				var vertices = vertexBuffer[i];
				Graphics.Attributes.Set( "TextureDiffuse", vertices.Item2 );
				vertices.Item1.Draw( renderMat );
			}

			base.RenderSceneObject();

			//DebugOverlay.Text( $"{entity.classname}",Bounds.Center, 0f );
			//DebugOverlay.Box( Bounds.Mins, Bounds.Maxs, Color.Red );
		}
		public void updateTexture( int key, Texture newTex )
		{
			PreparingIndicator.Update( "Texture" );

			for ( var i = 0; i < vertexBuffer.Count; i++ )
			{
				var vb = vertexBuffer[i];

				if ( key == i )
					vb.Item2 = newTex;

				vertexBuffer[i] = vb;
			}
		}
	}
}
