// sbox.Community © 2023-2024

using System.Collections.Generic;
using System.Linq;
using Sandbox;
using static MapParser.Manager;

namespace MapParser.GoldSrc.Entities
{
	public partial class Map
	{
		public Map_SV SV;
		public Map_CL CL;

		public static Map Create( ref SpawnParameter settings, ref BSPFile bspData, ref List<Texture> skyTextures, ref List<(List<Vertex>, List<int>, string, int)> meshInfo, ref ModelBuilder modelBuilder )
		{
			Map ent = new Map();

			if ( Game.IsServer )
				ent.SV = new Map_SV( ref settings, ref modelBuilder );
			else
				ent.CL = new Map_CL( ref settings, ref bspData, ref skyTextures, ref meshInfo );

			return ent;
		}

		public void Delete()
		{
			if ( SV != null && SV.IsValid() )
				SV.Delete();

			if ( CL != null && CL.IsValid() )
			{
				CL.removed = true;
				CL.Delete();
			}
		}

		// No link required
		public bool Link()
		{
			return true;
		}

		public partial class Map_CL : SceneCustomObject
		{
			private List<Vector3> AABB;
			private Vector3 Mins;
			private Vector3 Maxs;
			private Vector3 Center;
			private List<Texture> skyTextures = new();
			private VertexBuffer sky_vb;
			private List<Vector3> skyAABB;
			private static Material renderMat = Material.FromShader( "shaders/goldsrc_render.shader" );
			private static Material skyMat = Material.FromShader( "shaders/goldsrc_sky.shader" );
			private Texture lightmap;
			private List<(VertexBuffer, Texture, int)> vertexBuffer = new(); // int: faceindex
			private List<Vector3> vertexBufferNormals = new(); // for backface culling
			private List<BSPFile.Leaf> leaves;
			bool[] PVS;
			int PVSCount;
			int leavesCount;
			int vertexBufferCount;
			public List<MapModelEntity> entities = new();
			List<List<int>> entitiesPVS = new();
			public List<MDLEntity> models = new();
			List<List<int>> modelsPVS = new();
			List<BBox> freeze = new();
			private bool insideMap = false;
			private bool insideMapChanged = false;
			public bool removed = false;
			public bool hasModelEntity = false;

			//int entityMeshCount = 0; // debug purposes
			//int currentEntityMeshCount = 0; // debug purposes
			private static int[] sky_faceIndices = new int[]
			{
				0, 1, 2, 3,
				7, 6, 5, 4,
				4, 5, 1, 0,
				5, 6, 2, 1,
				6, 7, 3, 2,
				7, 4, 0, 3,
			};

			//Texture Coordinates.
			private static Vector2[] sky_uv = new Vector2[]
			{
				new Vector2( 0, 0 ),
				new Vector2( 1, 0 ),
				new Vector2( 1, 1 ),
				new Vector2( 0, 1 )
			};

			/*private static Vector3[] sky_uAxis = new Vector3[]
			{
				Vector3.Forward,
				Vector3.Left,
				Vector3.Left,
				Vector3.Forward,
				Vector3.Right,
				Vector3.Backward,
			};

			private static Vector3[] sky_vAxis = new Vector3[]
			{
				Vector3.Left,
				Vector3.Forward,
				Vector3.Down,
				Vector3.Down,
				Vector3.Down,
				Vector3.Down,
			};*/

			public Map_CL( ref SpawnParameter settings, ref BSPFile bspData, ref List<Texture> skyTextures, ref List<(List<Vertex>, List<int>, string, int)> meshInfo ) : base( settings.sceneWorld )
			{
				Flags.IsOpaque = true;
				Flags.IsTranslucent = false;
				Flags.IsDecal = false;
				Flags.OverlayLayer = false;
				Flags.BloomLayer = false;
				Flags.ViewModelLayer = false;
				Flags.SkyBoxLayer = false;
				Flags.NeedsLightProbe = true;

				lightmap = bspData.lightmap;
				leaves = bspData.leaves;
				this.skyTextures = skyTextures;

				Position = Vector3.Zero;
				sky_vb = new();
				sky_vb.Init( true );

				var vertexBufferIndex = 0;
				Dictionary<int, string> texturesNeedLoaded = new();

				foreach ( var mesh in meshInfo )
				{
					PreparingIndicator.Update( "Map" );

					Vertex[] vertexs = new Vertex[mesh.Item1.Count()];

					VertexBuffer buffer = new();
					buffer.Init( true );

					Vector3 Normal = Vector3.Zero;
					foreach ( var vertex in mesh.Item1 )
					{
						buffer.Add( new( vertex.Position + settings.position, vertex.Normal, vertex.Tangent, vertex.TexCoord0 ) );
						Normal = vertex.Normal;
					}

					foreach ( var indices in mesh.Item2 )
						buffer.AddRawIndex( indices );

					var findTexture = Render.TextureCache.textureData.TryGetValue( mesh.Item3, out var texCacheData );
					if ( !findTexture )
						texturesNeedLoaded.Add( vertexBufferIndex, mesh.Item3 );

					vertexBuffer.Add( (buffer, findTexture ? texCacheData.texture : Texture.Invalid, mesh.Item4) );

					vertexBufferNormals.Add( Normal );
					vertexBufferIndex++;
				}

				vertexBufferCount = vertexBuffer.Count();

				AABB = settings.AABB;

				for ( var i = 0; i < AABB.Count; i++ )
				{
					AABB[i] -= settings.center;

					// Skybox resizing, but not efficiently, need revision
					if ( i == 0 )
						Mins = AABB[i] * 1.1f + settings.center;
					else if ( i == 6 )
						Maxs = AABB[i] * 1.1f + settings.center;

					AABB[i] *= 3;
					AABB[i] *= new Vector3( 1, 1, 3 );
					AABB[i] += settings.center;
				}

				skyAABB = AABB.ToList(); // TODO: This is should be square, we are using map's AABB

				Center = Position - settings.center;

				Bounds = new BBox( Mins, Maxs ); // TODO: fix, it causes invisible problem

				PVS = new bool[vertexBuffer.LastOrDefault().Item3 + 1];
				PVSCount = PVS.Count();
				leavesCount = leaves.Count();

				if ( leavesCount == 0 ) // If VIS not compiled, 
				{
					for ( var indx = 0; indx < PVSCount; indx++ )
						PVS[indx] = true;
					foreach ( var ent in entities )
						ent.render = true;
				}

				createTextures( texturesNeedLoaded, settings, this );
			}

			~Map_CL()
			{
				Event.Unregister( this );
			}

			public async void createTextures( Dictionary<int, string> texturesNeedLoaded, SpawnParameter settings, Map_CL map ) => await GameTask.RunInThreadAsync( () => TextureCache.addTextures( texturesNeedLoaded, settings, map: map ) );

			// Finding parent leaf of entities, idk is it correct way to PVS, couldn't find info about it, that is my implementation..
			public void findPVSForEntities()
			{
				foreach ( var leaf in leaves )
					entitiesPVS.Add( new() );

				var entIndex = 0;
				foreach ( var ent in entities )
				{
					var entBBox = ent.Bounds;
					var leafindex = 0;
					foreach ( var leaf in leaves )
					{
						if ( entBBox.Overlaps( leaf.BBox ) )
							entitiesPVS[leafindex].Add( entIndex );

						leafindex++;
					}
					entIndex++;
				}
			}

			public void findPVSForModelEntities()
			{
				foreach ( var leaf in leaves )
					modelsPVS.Add( new() );

				var modelIndex = 0;
				foreach ( var model in models )
				{
					var modelBBox = model.CL.Bounds;
					var leafindex = 0;
					foreach ( var leaf in leaves )
					{
						if ( modelBBox.Overlaps( leaf.BBox ) )
							modelsPVS[leafindex].Add( modelIndex );
						leafindex++;
					}
					modelIndex++;
				}
				hasModelEntity = true;
			}

			public void RegisterEvent() => Event.Register( this );

			/*public void updateEntityRenderBounds()
			{
				foreach ( var ent in entities )
				{
					ent.Bounds = Bounds;
					//entityMeshCount += ent.meshCount;
				}
			}*/

			public void updateTexture( int key, Texture newTex )
			{
				PreparingIndicator.Update("Texture");

				for ( var i = 0; i < vertexBuffer.Count(); i++ )
				{
					var vb = vertexBuffer[i];
					if ( key == i )
						vb.Item2 = newTex;
					vertexBuffer[i] = vb;
				}
			}
			/*
			public float frustumLength = 100f;
			public float frustumWidth = 100f;

			private Vector3[] frustumCorners = new Vector3[4];
			private Vector3[] rays = new Vector3[4];

			private void FrustumUpdate()
			{
				// Calculate the corners of the frustum
				frustumCorners[0] = Camera.Position + Camera.Rotation.Forward * frustumLength + Camera.Rotation.Right * frustumWidth;
				frustumCorners[1] = Camera.Position + Camera.Rotation.Forward * frustumLength - Camera.Rotation.Right * frustumWidth;
				frustumCorners[2] = Camera.Position - Camera.Rotation.Forward * frustumLength - Camera.Rotation.Right * frustumWidth;
				frustumCorners[3] = Camera.Position - Camera.Rotation.Forward * frustumLength + Camera.Rotation.Right * frustumWidth;

				// Calculate the rays for each corner
				for ( int i = 0; i < 4; i++ )
				{
					rays[i] = (frustumCorners[i] - Camera.Position).Normal;
					DebugOverlay.Line( Camera.Position, frustumCorners[i], Color.Red );
				}
			}

			public bool IsInside(Vector3 point)
			{
				// Check if the point is inside the frustum by checking if it is on the same side of all the rays
				for (int i = 0; i < 4; i++)
				{
					if (Vector3.Dot(rays[i], point - frustumCorners[i]) < 0)
					{
						return false;
					}
				}

				return true;
			}*/

			[Event.Tick]
			public void Tick()
			{
				if ( Game.IsServer || removed )
					return;

				if ( insideMapChanged != insideMap )
				{
					insideMapChanged = insideMap;
					foreach ( var ent in Game.SceneWorld.SceneObjects )
						if ( ent != null && ent.IsValid() && ent.Flags.IsOpaque && !ent.Flags.NeedsLightProbe )
							ent.RenderingEnabled = !insideMap;

					foreach ( var ent in entities )
						ent.render = insideMap;
				}

				//FrustumUpdate();

				// Render map if we are inside it
				var bbox = Game.LocalClient.Pawn.WorldSpaceBounds;

				if ( !bbox.Overlaps( Bounds ) )
				{
					if ( insideMap )
						insideMap = false;
					return;
				}
				else
					if ( !insideMap )
					insideMap = true;

				if ( leavesCount != 0 )
				{
					// If we are standing, do not calculate everything again
					var pass = false;

					var freezeIndex = 0;
					var freezeCount = freeze.Count();
					for ( var i = 0; i < leavesCount; i++ )
					{
						var leaf = leaves[i];

						if ( bbox.Overlaps( leaf.BBox ) && (freezeIndex == freezeCount || freeze[freezeIndex++] != leaf.BBox) )
						{
							pass = true;
							break;
						}
					}

					//DebugOverlay.ScreenText( $"World Meshes: {PVS.Where( x => x == true ).Count()} / {PVSCount}", new Vector2( 10, 30 ), 0f );
					//DebugOverlay.ScreenText( $"Entity Meshes: {currentEntityMeshCount} / {entityMeshCount}", new Vector2( 10, 45 ), 0f );

					if ( !pass )
						return;

					freeze.Clear();

					for ( var j = 0; j < PVSCount; j++ )
						PVS[j] = false;

					foreach ( var ent in entities )
						ent.render = false;

					foreach ( var model in models )
						model.CL.render = false;

					for ( var i = 0; i < leavesCount; i++ )
					{
						var leaf = leaves[i];

						if ( bbox.Overlaps( leaf.BBox ) )
						{
							freeze.Add( leaf.BBox );

							for ( var ix = 0; ix < leavesCount; ix++ )
							{
								if ( leaf.visLeaves[ix] )
								{
									foreach ( var entpvsid in entitiesPVS[ix] )
									{
										//if( !entities[entpvsid].render )
										//currentEntityMeshCount += entities[entpvsid].meshCount;

										entities[entpvsid].render = true;
									}

									if( hasModelEntity ) { 
										foreach ( var modelpvsid in modelsPVS[ix] )
										{
											//if( !entities[entpvsid].render )
											//currentEntityMeshCount += entities[entpvsid].meshCount;

											models[modelpvsid].CL.render = true;
										}
									}

									var leaf2 = leaves[ix];
									foreach ( var ind in leaf2.faceIndex )
									{
										if ( PVSCount <= ind ) //Log.Info( "?" );
											continue; // because of model's face index?

										// Vector3.Dot( vertexBufferNormals[ix], Camera.Rotation.Forward.Normal ) < 0f;
										PVS[ind] = true;//IsInside( leaf2.nMaxs - ((leaf2.nMaxs- leaf2.nMins)/2f) );
									}
								}
							}

							// Add itself
							foreach ( var ind in leaf.faceIndex )
							{
								if ( PVSCount <= ind ) // because of model's face index?
									continue;

								PVS[ind] = true;
							}

							foreach ( var entpvsid in entitiesPVS[i] )
							{
								//if ( !entities[entpvsid].render )
								//currentEntityMeshCount += entities[entpvsid].meshCount;

								entities[entpvsid].render = true;
							}

							if ( hasModelEntity )
							{
								foreach ( var modelpvsid in modelsPVS[i] )
								{
									//if( !entities[entpvsid].render )
									//currentEntityMeshCount += entities[entpvsid].meshCount;

									models[modelpvsid].CL.render = true;
								}
							}

							//break;
						}
						//DebugOverlay.Box( leaf.BBox.Mins, leaf.BBox.Maxs );
					}
				}
			}

			public override void RenderSceneObject()
			{
				if ( !insideMap )
					return;

				if ( Graphics.LayerType != SceneLayerType.Opaque )
					return;

				// Rendering Sky
				Graphics.Attributes.Set( "g_vPosition", Center );

				for ( var i = 0; i < 6; ++i )
				{
					sky_vb.Clear();

					for ( var j = 0; j < 4; ++j )
						sky_vb.Add( new Vertex( skyAABB[sky_faceIndices[i * 4 + j]], Vector3.Left, Vector3.Left, sky_uv[j] ) ); //sky_vb.Add( new Vertex( AABB[sky_faceIndices[(i * 4) + j]], Vector3.Cross( sky_uAxis[i], sky_vAxis[i] ), sky_uAxis[i], sky_uv[j] ) );

					sky_vb.AddRawIndex( 3 );
					sky_vb.AddRawIndex( 0 );
					sky_vb.AddRawIndex( 2 );
					sky_vb.AddRawIndex( 1 );
					sky_vb.AddRawIndex( 2 );
					sky_vb.AddRawIndex( 0 );

					Graphics.Attributes.Set( "g_vSkyTexture", skyTextures[i] );

					sky_vb.Draw( skyMat );
				}

				// Rendering Map
				Graphics.Attributes.Set( "TextureLightmap", lightmap );
				Graphics.Attributes.Set( "Opacity", 1f );
				Graphics.Attributes.Set( "Pixelation", clientSettings.pixelation );

				for ( var i = 0; i < vertexBufferCount; i++ )
				{
					var vertices = vertexBuffer[i];

					if ( !PVS[vertices.Item3] )
						continue;

					Graphics.Attributes.Set( "TextureDiffuse", vertices.Item2 );
					vertices.Item1.Draw( renderMat );
				}

				/*for ( var i = 0; i < leavesCount; i++ )
				{
					var leaf = leaves[i];
					DebugOverlay.Box( leaf.BBox.Mins, leaf.BBox.Maxs );
				}*/

			}

			/*protected static Vector2 Planar( Vector3 pos, Vector3 uAxis, Vector3 vAxis )
			{
				return new Vector2()
				{
					x = Vector3.Dot( uAxis, pos ),
					y = Vector3.Dot( vAxis, pos )
				};
			}
			protected static void GetTangentBinormal( Vector3 normal, out Vector3 tangent, out Vector3 binormal )
			{
				var t1 = Vector3.Cross( normal, Vector3.Forward );
				var t2 = Vector3.Cross( normal, Vector3.Up );
				if ( t1.Length > t2.Length )
				{
					tangent = t1;
				}
				else
				{
					tangent = t2;
				}
				binormal = Vector3.Cross( normal, tangent ).Normal;
			}*/
		}

		public partial class Map_SV : ModelEntity
		{
			public List<MDLEntity> models = new List<MDLEntity>();
			public Map_SV() { }
			public Map_SV( ref SpawnParameter settings, ref ModelBuilder model ) {

				Model = model.Create();
				SetupPhysicsFromModel( PhysicsMotionType.Static );
				Position = settings.position;
				Rotation = Rotation.From( settings.angles );
				PhysicsEnabled = false;
				EnableDrawing = false;
				Tags.Add( "solid" );
				EnableTraceAndQueries = true;
				Predictable = false;
			}

		}
	}
}
