﻿// sbox.Community © 2023-2024

using Sandbox;
using Sandbox.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using static MapParser.GoldSrc.Entities.ModelRenderer;
using static MapParser.Manager;

namespace MapParser.GoldSrc.Entities
{
	// Static model entity
	public class MDLEntity
	{
		public MDLEntity_SV SV;
		public MDLEntity_CL CL;

		public static MDLEntity Create( ref List<(BufferAttribute<float>[][], BufferAttribute<float>, Sandbox.Texture, List<float[]>)> subModels, ref GoldSrc.EntityParser.EntityData entData, ref SpawnParameter settings, ref List<GoldSrc.EntityParser.EntityData> lightEntities )
		{
			MDLEntity ent = new MDLEntity();

			if ( Game.IsServer )
				ent.SV = new MDLEntity_SV( ref subModels, ref entData, ref settings );
			else
				ent.CL = new MDLEntity_CL( ref subModels, ref entData, ref settings, ref lightEntities );

			return ent;
		}

		// Not good approach, temporary
		public static bool TryLink( MDLEntity_CL self )
		{
			foreach ( var ent in Entity.All.OfType<MDLEntity_SV>() )
				if ( ent.Position.Distance( self.Position ) < 1e01 )
				{
					self.parent = ent;
					self.linked = true;
				}
			return false;
		}

		public void Delete()
		{
			if ( SV != null && SV.IsValid() )
				SV.Delete();

			if ( CL != null && CL.IsValid() )
				CL.Delete();
		}


		public class MDLEntity_CL : SceneCustomObject
		{
			// Submodels, animations and frames respectively. Frames are segmented as list because of the limit of vertexBuffer
			[SkipHotload]
			List<List<List<List<VertexBuffer>>>> vertexBuffers = new();
			private static Material renderMat = Material.FromShader( "shaders/goldsrc_model_render.shader" );
			List<Sandbox.Texture> textures = new();
			List<bool> enabledSubmodels = new();
			int activeAnimation = 0;
			float frameRate = 60.0f;  //60fps, added fps from entity but is fps from sequences (or from header) needed?, is it needed bbmax bbmin for physics?
			double frameState = 0.0f;
			double timeTaken = 0;
			float opacity = 1f;
			Vector4 renderColor = new Vector4( 1f, 1f, 1f, 1f );
			public bool render = false;
			public bool linked;
			public MDLEntity_SV parent;
			//private Texture lightmap;

			public MDLEntity_CL( ref List<(BufferAttribute<float>[][], BufferAttribute<float>, Sandbox.Texture, List<float[]>)> subModels, ref GoldSrc.EntityParser.EntityData entData, ref SpawnParameter settings, ref List<GoldSrc.EntityParser.EntityData> lightEntities ) : base( settings.sceneWorld )
			{
				Flags.IsOpaque = true;
				Flags.IsTranslucent = false;
				Flags.IsDecal = false;
				Flags.OverlayLayer = false;
				Flags.BloomLayer = false;
				Flags.ViewModelLayer = false;
				Flags.SkyBoxLayer = false;
				Flags.NeedsLightProbe = true;

				var origin = Vector3.Parse( entData.data["origin"] );
				Position = settings.position + origin;

				if ( entData.data.TryGetValue( "angle", out var angle ) )
					Rotation = Rotation.From( new Angles( 0, 0, float.Parse( angle ) ) );

				if ( entData.data.TryGetValue( "angles", out var angles ) )
					Rotation = Rotation.From( Angles.Parse( angles ) );

				// Fixed to 60 fps
				//if ( entData.data.TryGetValue( "framerate", out var framerate ) )
				//	frameRate = float.Parse( framerate );

				if ( entData.data.TryGetValue( "renderamt", out var renderamt ) )
					opacity = int.Parse( renderamt ) / 255f;

				if ( opacity < 1f )
					Flags.IsTranslucent = true;

				EntityParser.EntityData? closestLight = null;
				float closestLightDistance = 0f;

				// Find closest light to apply lightcolor to the model
				foreach ( var light in lightEntities )
				{
					// Initial assign
					if ( closestLight is null )
					{
						closestLight = light;
						closestLightDistance = Vector3.Parse( closestLight.Value.data["origin"] ).Distance( origin );
						continue;
					}

					if ( Vector3.Parse( closestLight.Value.data["origin"] ).Distance( origin ) < closestLightDistance )
					{
						closestLight = light;
						closestLightDistance = Vector3.Parse( closestLight.Value.data["origin"] ).Distance( origin );
					}
				}

				// _diffuse_light? https://developer.valvesoftware.com/wiki/Light_(GoldSource_Engine) & http://zhlt.info/entity-changes.html
				if ( closestLight is not null )
				{
					var color = Color.Parse( closestLight.Value.data.TryGetValue( "_diffuse_light", out var diffuse_color ) ? diffuse_color : closestLight.Value.data["_light"] ).Value;
					renderColor = new Vector4( color.r, color.g, color.b, color.a );
				}

				// TODO: Add RenderColor

				//this.lightmap = lightmap;

				Vector3 mins = new();
				Vector3 maxs = new();

				bool initializeMinsMaxs = true;
				ushort meshBatchSize = 612;

				foreach ( var submodel in subModels )
				{
					var uvArray = submodel.Item2.Array.ToArray();
					//var lightDataArray = submodel.Item3.Array.ToArray();

					List<List<List<VertexBuffer>>> animationList = new();

					foreach ( var animations in submodel.Item1 )
					{
						List<List<VertexBuffer>> frameList = new();

						foreach ( var frames in animations )
						{
							var meshArray = frames.Array.ToArray();

							// Flipping mesh and uv's
							float[] vertices = meshArray;
							float[] uvs = uvArray.ToArray();
							//float[] lightData = uvArray.ToArray();
							int numVertices = vertices.Length / 3;
							int numUvs = uvs.Length / 2;

							for ( int i = 0; i < numVertices / 2; i++ )
							{
								// Swap the ith vertex with the (n-i-1)th vertex
								int j = numVertices - i - 1;
								float x = vertices[i * 3];
								float y = vertices[i * 3 + 1];
								float z = vertices[i * 3 + 2];
								vertices[i * 3] = vertices[j * 3];
								vertices[i * 3 + 1] = vertices[j * 3 + 1];
								vertices[i * 3 + 2] = vertices[j * 3 + 2];
								vertices[j * 3] = x;
								vertices[j * 3 + 1] = y;
								vertices[j * 3 + 2] = z;

								int k = numUvs - i - 1;
								float ux = uvs[i * 2];
								float uy = uvs[i * 2 + 1];
								uvs[i * 2] = uvs[k * 2];
								uvs[i * 2 + 1] = uvs[k * 2 + 1];
								uvs[k * 2] = ux;
								uvs[k * 2 + 1] = uy;

								/*float lx = lightData[i * 2];
								float ly = lightData[i * 2 + 1];
								lightData[i * 2] = lightData[k * 2];
								lightData[i * 2 + 1] = lightData[k * 2 + 1];
								lightData[k * 2] = ux;
								lightData[k * 2 + 1] = uy;*/
							}

							meshArray = vertices;

							List<VertexBuffer> vbList = new();

							// VertexBuffer has limitation (65536), I think max ~862 vertices can be added but coming too much, so need segmentation
							float[][] subArrays = Enumerable.Range( 0, (int)Math.Ceiling( (float)meshArray.Length / meshBatchSize ) )
							.Select( i => meshArray.Skip( i * meshBatchSize ).Take( meshBatchSize ).ToArray() ).ToArray(); //.AsParallel()

							var uvIndex = 0;
							//var lightsIndex = 0;

							for ( var i = 0; i < subArrays.Count(); i++ )
							{
								var arr = subArrays[i];

								VertexBuffer vb = new();
								vb.Init( false );

								for ( var j = 0; j < arr.Count() / 3; j++ )
								{
									Transform tf = new();
									tf.Rotation = Rotation;
									tf.Position = Position;

									var vec = tf.TransformVector( new Vector3( arr[j * 3], arr[j * 3 + 1], arr[j * 3 + 2] ) );

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

									// idk what is lightdata for model, therefore, I was only passed them as Vector4 component, but didn't used anywhere ( lightData[lightsIndex++], lightData[lightsIndex++] )
									vb.Add( new Vertex( vec, Vector3.Zero, Vector3.Right, new Vector2( uvs[uvIndex++], uvs[uvIndex++] ) ) );
								}

								vbList.Add( vb );
							}

							frameList.Add( vbList );
						}
						animationList.Add( frameList );
					}
					vertexBuffers.Add( animationList );
					textures.Add( submodel.Item3 );
					enabledSubmodels.Add( true );
				}

				Bounds = new BBox( mins, maxs );
			}

			/*public Model createModelForMV()
			{
				ModelBuilder mb = new();
				List<Mesh> meshList = new();

				for ( int i = 0; i < enabledSubmodels.Count(); i++ )
				{
					if ( enabledSubmodels[i] )
					{
						Material mat = Material.Create( textures[i].ResourceName, "simple" );
						Mesh mesh = new( mat );
						
						var animation = vertexBuffers[i][activeAnimation];

						if ( frameState > animation.Count - 1 )
							frameState = 0;

						var vb = animation[(int)frameState];
						for ( var l = 0; l < vb.Count; l++ )
						{
							mesh.CreateBuffers( vb[l] );
						}
						meshList.Add( mesh );
					}
				}
				return mb.AddMeshes( meshList.ToArray() ).Create();
			}*/

			public override void RenderSceneObject()
			{
				if ( !render )
					return;

				if ( Graphics.LayerType != SceneLayerType.Opaque && Graphics.LayerType != SceneLayerType.Translucent )
					return;

				// Might be identified as will not be linked for only clside entities
				if ( linked )
				{
					if ( !parent.IsValid() )
						Delete();
					else
						Position = parent.Position;
				}
				else
					TryLink( this );

				timeTaken += PerformanceStats.FrameTime * frameRate;

				if ( timeTaken > 1f )
				{
					timeTaken = 0f;
					frameState++;
				}

				Graphics.Attributes.Set( "Pixelation", clientSettings.pixelation );
				Graphics.Attributes.Set( "Opacity", opacity );
				//Graphics.Attributes.Set( "TextureLightmap", Texture.Transparent );
				Graphics.Attributes.Set( "Color", renderColor );

				for ( int i = 0; i < enabledSubmodels.Count(); i++ )
				{
					if ( enabledSubmodels[i] )
					{
						Graphics.Attributes.Set( "TextureDiffuse", textures[i] );

						if ( vertexBuffers == null ) // remove
						{
							Delete();
							return;
						}
						var animation = vertexBuffers[i][activeAnimation];

						if ( frameState > animation.Count - 1 )
							frameState = 0;

						var vb = animation[(int)frameState];
						for ( var l = 0; l < vb.Count; l++ )
						{
							//DebugOverlay.Box( Bounds.Mins, Bounds.Maxs, Color.Blue );
							vb[l].Draw( renderMat );
						}
					}
				}
				base.RenderSceneObject();
			}


		}

		public class MDLEntity_SV : ModelEntity //KeyframeEntity
		{
			public MDLEntity_SV() { }
			public MDLEntity_SV( ref List<(BufferAttribute<float>[][], BufferAttribute<float>, Sandbox.Texture, List<float[]>)> subModels, ref GoldSrc.EntityParser.EntityData entData, ref SpawnParameter settings )
			{
				// We are using first animation frame of models as physics, in the future.
				// Because complex models will be trouble for server.
				// Find out way to get (or parse?) physics mesh or use optimisation method like decimination etc..

				List<Vector3> vectorList = new();
				ModelBuilder mb = new();

				foreach ( var submodel in subModels )
				{
					foreach ( var animations in submodel.Item1 )
					{
						foreach ( var frames in animations )
						{
							var meshArray = frames.Array.ToArray();

							for ( var i = 0; i < meshArray.Count() / 3; i++ )
							{
								vectorList.Add( new Vector3( meshArray[i * 3], meshArray[i * 3 + 1], meshArray[i * 3 + 2] ) + Position );
							}
							break;
						}
						break;
					}
				}

				List<int> indexList = new List<int>();

				for ( int i = 2; i < vectorList.Count(); i++ )
				{
					if ( i % 2 == 0 )
					{
						indexList.Add( i );
						indexList.Add( i - 1 );
						indexList.Add( i - 2 );
					}
					else
					{
						indexList.Add( i - 2 );
						indexList.Add( i - 1 );
						indexList.Add( i );
					}
				}
				mb.AddCollisionMesh( vectorList.ToArray(), indexList.ToArray() );

				/*List<Vector3> hitboxes = new();
				foreach ( var hitbox in modelData.hitBoxes )
				{
					mb.AddCollisionBox( hitbox.bbmax- hitbox.bbmin, (hitbox.bbmax + hitbox.bbmin)/2f );
				}*/

				Model = mb.Create();
				SetupPhysicsFromModel( PhysicsMotionType.Static ); //true
				Position = settings.position + Vector3.Parse( entData.data["origin"] );
				if ( entData.data.TryGetValue( "angle", out var angle ) )
					Rotation = Rotation.From( new Angles( 0, 0, float.Parse( angle ) ) );
				if ( entData.data.TryGetValue( "angles", out var angles ) )
					Rotation = Rotation.From( Angles.Parse( angles ) );
				PhysicsEnabled = false;
				EnableDrawing = false;
				Tags.Add( "solid" );
				EnableTraceAndQueries = true;
				Predictable = false;
			}
		}
	}
}