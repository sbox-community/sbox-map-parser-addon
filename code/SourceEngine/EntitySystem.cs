using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using static MapParser.SourceEngine.BSPFile;
using static MapParser.SourceEngine.Main;
using static MapParser.SourceEngine.Materials;

namespace MapParser.SourceEngine
{
	public partial class EntitySystem
	{

		/*
		public class BaseEntity
		{
			public BSPModelRenderer modelBSP = null;
			public StudioModelInstance modelStudio = null;
			public SpriteInstance modelSprite = null;

			public Vector3 localOrigin = Vector3.Zero;
			public Vector3 localAngles = Vector3.Zero;
			public Color rendercolor = Color.White;
			public float renderamt = 1.0f;
			public int rendermode = 0;
			public bool visible = true;
			public EntityMaterialParameters materialParams = null;
			public int skin = 0;
			public Vector3 lightingOrigin = Vector3.Zero;

			public string targetName = null;
			public BaseEntity parentEntity = null;
			public int? parentAttachment = null;
			public Matrix4x4 modelMatrix = new Matrix4x4();
			public bool alive = true;
			public bool enabled = true;
			public SpawnState spawnState = SpawnState.ReadyForSpawn;

			public Dictionary<string, EntityInputFunc> inputs = new Dictionary<string, EntityInputFunc>();

			private EntityOutput output_onuser1 = new EntityOutput();
			private EntityOutput output_onuser2 = new EntityOutput();
			private EntityOutput output_onuser3 = new EntityOutput();
			private EntityOutput output_onuser4 = new EntityOutput();

			// Animation Playback (should probably be split out to a different class)
			private int seqdefaultindex = -1;
			private int seqindex = 0;
			private float seqtime = 0;
			private bool seqplay = false;
			private float seqrate = 1;
			private bool holdAnimation = false;



			public BaseEntity( EntitySystem entitySystem, SourceRenderContext renderContext, BSPEntity entity ) //BSPRenderer bspRenderer,
			{
				this.entitySystem = entitySystem;
				this.renderContext = renderContext;
				this.bspRenderer = bspRenderer;
				this.entity = entity;

				if ( entity.model != null )
				{
					this.setModelName( renderContext, entity.model );
				}

				if ( entity.origin != null )
				{
					float[] origin = vmtParseVector( entity.origin );
					this.localOrigin = new Vector3( origin[0], origin[1], origin[2] );
				}

				if ( entity.angles != null )
				{
					float[] angles = vmtParseVector( entity.angles );
					this.localAngles = new Vector3( angles[0], angles[1], angles[2] );
				}

				if ( entity.rendercolor != null )
				{
					vmtParseColor( this.rendercolor, entity.rendercolor );
				}

				this.renderamt = vmtParseNumber( entity.renderamt, 255.0f ) / 255.0f;
				this.rendermode = vmtParseNumber( entity.rendermode, 0 );

				if ( entity.targetname != null )
				{
					this.targetName = entity.targetname.ToLower();
				}

				this.skin = vmtParseNumber( entity.skin, 0 );

				if ( entity.startdisabled != null )
				{
					this.enabled = !(bool)entity.startdisabled;
				}
				else if ( entity.start_disabled != null )
				{
					this.enabled = !(bool)entity.start_disabled;
				}

				this.holdAnimation = (bool)fallbackUndefined( this.entity.holdanimation, '0' );

				this.registerInput( "enable", input_enable );
				this.registerInput( "disable", input_disable );
				this.registerInput( "enabledraw", input_enabledraw );
				this.registerInput( "disabledraw", input_disabledraw );
				this.registerInput( "kill", input_kill );
				this.registerInput( "skin", input_skin );
				this.registerInput( "use", input_use );

				this.output_onuser1.parse( this.entity.onuser1 );
				this.output_onuser2.parse( this.entity.onuser1 );
				this.output_onuser3.parse( this.entity.onuser1 );
				this.output_onuser4.parse( this.entity.onuser1 );

				this.registerInput( "fireuser1", input_fireuser1 );
				this.registerInput( "fireuser2", input_fireuser2 );
				this.registerInput( "fireuser3", input_fireuser3 );
				this.registerInput( "fireuser4", input_fireuser4 );

				this.registerInput( "setparent", input_setparent );
				this.registerInput( "clearparent", input_clearparent );
				this.registerInput( "setparentattachment", input_setparentattachment );
				this.registerInput( "setparentattachmentmaintainoffset", input_setparentattachmentmaintainoffset );

				// TODO(jstpierre): This should be on baseanimation / prop_dynamic
				this.registerInput( "setanimation", input_setanimation );
				this.registerInput( "setdefaultanimation", input_setdefaultanimation );
				this.registerInput( "setplaybackrate", input_setplaybackrate );

				if ( shouldHideEntityFallback( this.entity.classname ) )
				{
					this.visible = false;
				}
			}

			public bool ShouldDraw()
			{
				return visible && enabled && alive && spawnState == SpawnState.Spawned;
			}

			public bool CheckFrustum( SourceRenderContext renderContext )
			{
				if ( modelStudio != null )
				{
					return modelStudio.CheckFrustum( renderContext );
				}
				else if ( modelBSP != null )
				{
					return modelBSP.CheckFrustum( renderContext );
				}
				else
				{
					// TODO: Implement this part
					return false;
				}
			}

			private int FindSequenceLabel( string label )
			{
				label = label.ToLower();
				return modelStudio!.ModelData.Seq.FindIndex( seq => seq.Label == label );
			}

			private void PlaySeqIndex( int index )
			{
				if ( index < 0 )
				{
					index = 0;
				}

				seqindex = index;
				seqplay = true;
				seqtime = 0;
			}

			public void ResetSequence( string label )
			{
				PlaySeqIndex( FindSequenceLabel( label ) );
			}

			public void Spawn( EntitySystem entitySystem )
			{
				if ( entity.parentname != null )
				{
					SetParentEntity( entitySystem.FindEntityByTargetName( entity.parentname ) );
				}

				if ( entity.defaultanim != null )
				{
					seqdefaultindex = FindSequenceLabel( entity.defaultanim );
					PlaySeqIndex( seqdefaultindex );
				}

				spawnState = SpawnState.Spawned;
			}

			protected EntityMaterialParameters EnsureMaterialParams()
			{
				if ( materialParams == null )
					materialParams = new EntityMaterialParameters();

				return materialParams;
			}
			public bool shouldDraw()
			{
				return visible && enabled && alive && spawnState == SpawnState.Spawned;
			}

			public bool checkFrustum( SourceRenderContext renderContext )
			{
				if ( modelStudio != null )
				{
					return modelStudio.checkFrustum( renderContext );
				}
				else if ( modelBSP != null )
				{
					return modelBSP.checkFrustum( renderContext );
				}
				else
				{
					// TODO(jstpierre): Do what here?
					return false;
				}
			}

			private int findSequenceLabel( string label )
			{
				label = label.ToLower();
				return modelStudio!.modelData.seq.FindIndex( ( seq ) => seq.label == label );
			}

			private void playseqindex( int index )
			{
				if ( index < 0 )
				{
					index = 0;
				}

				seqindex = index;
				seqplay = true;
				seqtime = 0;
			}

			public void resetSequence( string label )
			{
				playseqindex( findSequenceLabel( label ) );
			}

			public void spawn( EntitySystem entitySystem )
			{
				if ( !string.IsNullOrEmpty( entity.parentname ) )
				{
					setParentEntity( entitySystem.findEntityByTargetName( entity.parentname ) );
				}

				if ( !string.IsNullOrEmpty( entity.defaultanim ) )
				{
					seqdefaultindex = findSequenceLabel( entity.defaultanim );
					playseqindex( seqdefaultindex );
				}

				spawnState = SpawnState.Spawned;
			}

			protected EntityMaterialParameters ensureMaterialParams()
			{
				if ( materialParams == null )
				{
					materialParams = new EntityMaterialParameters();
				}

				return materialParams;
			}

			public void setModelName( SourceRenderContext renderContext, string modelName )
			{
				ensureMaterialParams();

				if ( modelName.StartsWith( "*" ) )
				{
					int index = int.Parse( modelName.Substring( 1 ) );
					modelBSP = bspRenderer.models[index];
					modelBSP.modelMatrix = modelMatrix;
					modelBSP.setEntity( this );
				}
				else if ( modelName.EndsWith( ".mdl" ) )
				{
					fetchStudioModel( renderContext, modelName );
				}
				else if ( modelName.EndsWith( ".vmt" ) || modelName.EndsWith( ".spr" ) )
				{
					fetchSpriteModel( renderContext, modelName );
				}
			}

			protected void remove()
			{
				alive = false;
			}

			protected void registerInput( string inputName, EntityInputFunc func )
			{
				Debug.Assert( !inputs.ContainsKey( inputName ) );
				inputs[inputName] = func;
			}

			public void fireInput( EntitySystem entitySystem, string inputName, EntityMessageValue value )
			{
				if ( !alive )
				{
					return;
				}

				if ( !inputs.TryGetValue( inputName, out EntityInputFunc func ) )
				{
					Debug.LogWarning( $"Unknown input: {targetName} ({entity.classname}) {inputName} {value}" );
					return;
				}

				func( entitySystem, value );
			}

			private void updateLightingData()
			{
				EntityMaterialParameters materialParams = ensureMaterialParams();

				Matrix4x4 modelMatrix = updateModelMatrix();
				materialParams.position = modelMatrix.GetColumn( 3 );

				if ( modelStudio != null )
				{
					lightingOrigin = transformVec3Mat4w1( modelMatrix, modelStudio.modelData.illumPosition );
				}
				else
				{
					lightingOrigin = materialParams.position;
				}

				materialParams.lightCache = new LightCache( bspRenderer, lightingOrigin );
			}

			private async Task FetchStudioModel( SourceRenderContext renderContext, string modelName )
			{
				Debug.Assert( this.spawnState == SpawnState.ReadyForSpawn );
				this.spawnState = SpawnState.FetchingResources;
				var modelData = await renderContext.studioModelCache.FetchStudioModelData( modelName );
				if ( modelData.bodyPartData.Length != 0 )
				{
					this.modelStudio = new StudioModelInstance( renderContext, modelData, this.EnsureMaterialParams() );
					this.modelStudio.SetSkin( renderContext, this.skin );
					this.UpdateLightingData();
				}
				this.spawnState = SpawnState.ReadyForSpawn;
			}

			private async Task FetchSpriteModel( SourceRenderContext renderContext, string modelName )
			{
				Debug.Assert( this.spawnState == SpawnState.ReadyForSpawn );
				this.spawnState = SpawnState.FetchingResources;
				var materialName = modelName.Replace( ".spr", ".vmt" );
				var materialCache = renderContext.materialCache;
				var materialInstance = await materialCache.CreateMaterialInstance( materialName );
				materialInstance.ParamSetNumber( "$rendermode", this.rendermode );
				materialInstance.EntityParams = this.EnsureMaterialParams();
				await materialInstance.Init( renderContext );
				this.modelSprite = new SpriteInstance( renderContext, materialInstance );
				this.spawnState = SpawnState.ReadyForSpawn;
			}

			private void UpdateStudioPose()
			{
				if ( this.modelStudio == null )
					throw new Exception( "whoops" );

				Matrix4x4.Copy( this.modelMatrix, this.modelStudio.ModelMatrix );
				this.modelStudio.SetupPoseFromSequence( this.seqindex, this.seqtime );
			}

			public void PrepareToRender( SourceRenderContext renderContext, GfxRenderInstManager renderInstManager )
			{
				if ( !this.ShouldDraw() )
					return;

				if ( this.materialParams != null )
					ColorUtils.ColorCopy( this.materialParams.BlendColor, this.rendercolor, this.renderamt );

				if ( this.modelStudio != null )
				{
					this.UpdateStudioPose();
					this.modelStudio.SetSkin( renderContext, this.skin );
					this.modelStudio.PrepareToRender( renderContext, renderInstManager );
				}
				else if ( this.modelSprite != null )
				{
					this.modelSprite.PrepareToRender( renderContext, renderInstManager );
				}

				if ( this.debug )
					this.materialParams.LightCache.DebugDrawLights( renderContext.CurrentView );
			}

			private Matrix4x4 getParentModelMatrix()
			{
				if ( parentAttachment != null )
				{
					return parentEntity.GetAttachmentMatrix( parentAttachment );
				}
				else
				{
					return parentEntity.UpdateModelMatrix();
				}
			}

			public void SetAbsOrigin( Vector3 origin )
			{
				if ( parentEntity != null )
				{
					Matrix4x4.Invert( getParentModelMatrix(), out Matrix4x4 scratchMat4a );
					TransformVec3Mat4w1( localOrigin, scratchMat4a, origin );
				}
				else
				{
					localOrigin = origin;
				}

				UpdateModelMatrix();
			}

			public void SetAbsOriginAndAngles( Vector3 origin, Vector3 angles )
			{
				if ( parentEntity != null )
				{
					Matrix4x4.Invert( getParentModelMatrix(), out Matrix4x4 scratchMat4a );
					ComputeModelMatrixPosQAngle( out Matrix4x4 scratchMat4b, origin, angles );
					Matrix4x4.Multiply( scratchMat4a, scratchMat4b, out scratchMat4b );
					ComputePosQAngleModelMatrix( localOrigin, localAngles, scratchMat4b );
				}
				else
				{
					localOrigin = origin;
					localAngles = angles;
				}

				UpdateModelMatrix();
			}

			public void GetAbsOrigin( out Vector3 dstOrigin )
			{
				if ( parentEntity != null )
				{
					ComputePosQAngleModelMatrix( out dstOrigin, null, UpdateModelMatrix() );
				}
				else
				{
					dstOrigin = localOrigin;
				}
			}

			public void GetAbsOriginAndAngles( out Vector3 dstOrigin, out Vector3 dstAngles )
			{
				if ( parentEntity != null )
				{
					ComputePosQAngleModelMatrix( out dstOrigin, out dstAngles, UpdateModelMatrix() );
				}
				else
				{
					dstOrigin = localOrigin;
					dstAngles = localAngles;
				}
			}
			public void setParentEntity( BaseEntity parentEntity, int? parentAttachment = null )
			{
				// TODO(jstpierre): How is this supposed to work? Happens in infra_c4_m2_furnace...
				if ( parentEntity == this )
				{
					parentEntity = null;
					parentAttachment = null;
				}

				if ( parentEntity == this.parentEntity && parentAttachment == this.parentAttachment )
					return;

				// Transform origin into absolute world-space.
				getAbsOriginAndAngles( localOrigin, localAngles );

				this.parentEntity = parentEntity;
				this.parentAttachment = parentAttachment;

				// Transform origin from world-space into entity space.
				setAbsOriginAndAngles( localOrigin, localAngles );
			}

			public void setParentAttachment( string attachmentName, bool maintainOffset )
			{
				if ( parentEntity == null )
					return;

				if ( parentEntity.modelStudio == null )
					return;

				int? parentAttachment = parentEntity.getAttachmentIndex( attachmentName );
				setParentEntity( parentEntity, parentAttachment );

				if ( !maintainOffset )
				{
					localOrigin = vec3.zero;
					localAngles = vec3.zero;
				}
			}

			public int? getAttachmentIndex( string attachmentName )
			{
				if ( modelStudio == null )
					return null;

				int attachmentIndex = modelStudio.modelData.attachment.FindIndex( ( attachment ) => attachment.name == attachmentName );
				if ( attachmentIndex < 0 )
					return null;

				return attachmentIndex;
			}

			public Matrix4x4 getAttachmentMatrix( int attachmentIndex )
			{
				if ( modelStudio == null )
					throw new Exception( "whoops" );

				updateModelMatrix();
				updateStudioPose();
				return modelStudio.attachmentMatrix[attachmentIndex];
			}

			private Matrix4x4 getParentModelMatrix()
			{
				if ( parentAttachment != null )
					return parentEntity.getAttachmentMatrix( parentAttachment.Value );
				else
					return parentEntity.updateModelMatrix();
			}

			public void setAbsOrigin( Vector3 origin )
			{
				if ( parentEntity != null )
				{
					Matrix4x4.Invert( getParentModelMatrix(), out scratchMat4a );
					transformVec3Mat4w1( localOrigin, scratchMat4a, origin );
				}
				else
				{
					localOrigin = origin;
				}

				updateModelMatrix();
			}

			public void setAbsOriginAndAngles( Vector3 origin, Vector3 angles )
			{
				if ( parentEntity != null )
				{
					Matrix4x4.Invert( getParentModelMatrix(), out scratchMat4a );
					computeModelMatrixPosQAngle( out scratchMat4b, origin, angles );
					Matrix4x4.Multiply( scratchMat4a, scratchMat4b, out scratchMat4b );
					computePosQAngleModelMatrix( localOrigin, localAngles, scratchMat4b );
				}
				else
				{
					localOrigin = origin;
					localAngles = angles;
				}

				updateModelMatrix();
			}

			public void getAbsOrigin( out Vector3 dstOrigin )
			{
				if ( parentEntity != null )
				{
					computePosQAngleModelMatrix( out dstOrigin, null, updateModelMatrix() );
				}
				else
				{
					dstOrigin = localOrigin;
				}
			}

			public void getAbsOriginAndAngles( out Vector3 dstOrigin, out Vector3 dstAngles )
			{
				if ( parentEntity != null )
				{
					computePosQAngleModelMatrix( out dstOrigin, out dstAngles, updateModelMatrix() );
				}
				else
				{
					dstOrigin = localOrigin;
					dstAngles = localAngles;
				}
			}
			public mat4 UpdateModelMatrix()
			{
				ComputeModelMatrixPosQAngle( this.modelMatrix, this.localOrigin, this.localAngles );

				if ( this.parentEntity != null )
				{
					mat4 parentModelMatrix = this.parentAttachment != null ? this.parentEntity.GetAttachmentMatrix( this.parentAttachment ) : this.parentEntity.UpdateModelMatrix();
					mat4.Multiply( this.modelMatrix, parentModelMatrix, this.modelMatrix );
				}

				return this.modelMatrix;
			}

			public void Movement( EntitySystem entitySystem, SourceRenderContext renderContext )
			{
				if ( this.modelBSP != null || this.modelStudio != null )
				{
					mat4 modelMatrix = this.UpdateModelMatrix();
					GetMatrixTranslation( this.materialParams.position, modelMatrix );

					bool visible = this.ShouldDraw();
					if ( this.renderamt == 0 )
						visible = false;
					if ( this.rendermode == 10 )
						visible = false;

					if ( this.modelBSP != null )
					{
						this.modelBSP.Visible = visible;
					}
					else if ( this.modelStudio != null )
					{
						this.modelStudio.Visible = visible;
						this.modelStudio.Movement( renderContext );
					}
				}

				if ( this.modelStudio != null )
				{
					// Update animation state machine.
					if ( this.seqplay )
					{
						float oldSeqTime = this.seqtime;
						this.seqtime += renderContext.GlobalDeltaTime * this.seqrate;

						if ( this.seqtime < 0 )
							this.seqtime = 0;

						// Pass to default animation if we're through.
						if ( this.seqdefaultindex >= 0 && this.modelStudio.SequenceIsFinished( this.seqindex, this.seqtime ) && !this.holdAnimation )
							this.PlaySeqIndex( this.seqdefaultindex );

						// Handle events.
						Sequence seq = this.modelStudio.ModelData.Seq[this.seqindex];
						Anim anim = this.modelStudio.ModelData.Anim[seq.Anim[0]];
						if ( anim != null )
						{
							float animcyc = anim.Fps / anim.Numframes;
							for ( int i = 0; i < seq.Events.Length; i++ )
							{
								AnimEvent ev = seq.Events[i];
								if ( ev.Cycle > (oldSeqTime * animcyc) && ev.Cycle <= (this.seqtime * animcyc) )
								{
									this.DispatchAnimEvent( entitySystem, ev.Event, ev.Options );
								}
							}
						}
					}
				}
			}
			public void use( EntitySystem entitySystem )
			{
				// Do nothing by default.
			}

			public void destroy( GfxDevice device )
			{
				if ( this.modelStudio != null )
					this.modelStudio.destroy( device );
			}

			protected void dispatchAnimEvent( EntitySystem entitySystem, int @event, string options) {
				if (@event == 1100) { // SCRIPT_EVENT_FIRE_INPUT
				this.fireInput( entitySystem, options, "" );
				}
			}

			public BSPEntity cloneMapData()
			{
				return (BSPEntity)this.entity.Clone();
			}

			private void input_enable()
			{
				this.enabled = true;
			}

			private void input_disable()
			{
				this.enabled = false;
			}

			private void input_enabledraw()
			{
				this.visible = true;
			}

			private void input_disabledraw()
			{
				this.visible = false;
			}

			private void input_kill()
			{
				this.remove();
			}

			private void input_use( EntitySystem entitySystem )
			{
				this.use( entitySystem );
			}

			private void input_fireuser1( EntitySystem entitySystem, string value )
			{
				this.output_onuser1.fire( entitySystem, this, value );
			}

			private void input_fireuser2( EntitySystem entitySystem, string value )
			{
				this.output_onuser2.fire( entitySystem, this, value );
			}

			public void use( EntitySystem entitySystem )
			{
				// Do nothing by default.
			}

			public void destroy( GfxDevice device )
			{
				if ( this.modelStudio != null )
					this.modelStudio.destroy( device );
			}

			protected void dispatchAnimEvent( EntitySystem entitySystem, int @event, string options )
			{
				if ( @event == 1100 )
				{ // SCRIPT_EVENT_FIRE_INPUT
					this.fireInput( entitySystem, options, "" );
				}
			}

			public BSPEntity cloneMapData()
			{
				return new BSPEntity() { entity = this.entity.Clone() };
			}

			private void input_enable()
			{
				this.enabled = true;
			}

			private void input_disable()
			{
				this.enabled = false;
			}

			private void input_enabledraw()
			{
				this.visible = true;
			}

			private void input_disabledraw()
			{
				this.visible = false;
			}

			private void input_kill()
			{
				this.remove();
			}

			private void input_use( EntitySystem entitySystem )
			{
				this.use( entitySystem );
			}

			private void input_fireuser1( EntitySystem entitySystem, string value )
			{
				this.output_onuser1.fire( entitySystem, this, value );
			}

			private void input_fireuser2( EntitySystem entitySystem, string value )
			{
				this.output_onuser2.fire( entitySystem, this, value );
			}

			private void input_fireuser3( EntitySystem entitySystem, string value )
			{
				this.output_onuser3.fire( entitySystem, this, value );
			}

			private void input_fireuser4( EntitySystem entitySystem, string value )
			{
				this.output_onuser4.fire( entitySystem, this, value );
			}

			private void input_skin( EntitySystem entitySystem, string value )
			{
				this.skin = int.Parse( value ) != 0 ? int.Parse( value ) : 0;
			}

			private void input_setparent( EntitySystem entitySystem, string value )
			{
				BSPEntity parentEntity = entitySystem.findEntityByTargetName( value );
				if ( parentEntity != null )
					this.setParentEntity( parentEntity );
			}

			private void input_clearparent( EntitySystem entitySystem )
			{
				this.setParentEntity( null );
			}

			private void input_setparentattachment( EntitySystem entitySystem, string value )
			{
				this.setParentAttachment( value, false );
			}

			private void input_setparentattachmentmaintainoffset( EntitySystem entitySystem, string value )
			{
				this.setParentAttachment( value, true );
			}

			private void input_setanimation( EntitySystem entitySystem, string value )
			{
				if ( this.modelStudio == null )
					return;

				this.playseqindex( this.findSequenceLabel( value ) );
			}

			private void input_setdefaultanimation( EntitySystem entitySystem, string value )
			{
				if ( this.modelStudio == null )
					return;

				this.seqdefaultindex = this.findSequenceLabel( value );
			}

			private void input_setplaybackrate( EntitySystem entitySystem, string value )
			{
				this.seqrate = float.Parse( value );
			}


		}


		public interface EntityFactory<T> where T : BaseEntity
		{
			T Create( EntitySystem entitySystem, SourceRenderContext renderContext, BSPEntity entity ); //BSPRenderer bspRenderer,
			string classname { get; }
		}


		public class EntityFactoryRegistry
		{
			public Dictionary<string, EntityFactory> classname = new Dictionary<string, EntityFactory>();

			public EntityFactoryRegistry()
			{
				RegisterDefaultFactories();
			}

			private void RegisterDefaultFactories()
			{
				RegisterFactory( worldspawn );
				RegisterFactory( sky_camera );
				RegisterFactory( water_lod_control );
				RegisterFactory( func_movelinear );
				RegisterFactory( func_door );
				RegisterFactory( func_door_rotating );
				RegisterFactory( func_rotating );
				RegisterFactory( func_areaportalwindow );
				RegisterFactory( func_instance_io_proxy );
				RegisterFactory( logic_auto );
				RegisterFactory( logic_relay );
				RegisterFactory( logic_branch );
				RegisterFactory( logic_case );
				RegisterFactory( logic_timer );
				RegisterFactory( logic_compare );
				RegisterFactory( math_counter );
				RegisterFactory( math_remap );
				RegisterFactory( math_colorblend );
				RegisterFactory( trigger_multiple );
				RegisterFactory( trigger_once );
				RegisterFactory( trigger_look );
				RegisterFactory( env_fog_controller );
				RegisterFactory( env_texturetoggle );
				RegisterFactory( material_modify_control );
				RegisterFactory( info_overlay_accessor );
				RegisterFactory( color_correction );
				RegisterFactory( light );
				RegisterFactory( light_spot );
				RegisterFactory( light_glspot );
				RegisterFactory( light_environment );
				RegisterFactory( point_template );
				RegisterFactory( env_entity_maker );
				RegisterFactory( env_steam );
				RegisterFactory( env_sprite );
				RegisterFactory( env_glow );
				RegisterFactory( env_sprite_clientside );
				RegisterFactory( env_tonemap_controller );
				RegisterFactory( env_projectedtexture );
				RegisterFactory( env_shake );
				RegisterFactory( point_camera );
				RegisterFactory( func_monitor );
				RegisterFactory( info_camera_link );
				RegisterFactory( info_player_start );
				// RegisterFactory(info_particle_system);
			}

			private void RegisterFactory( EntityFactory entityFactory )
			{
				classname.Add( entityFactory.ClassName, entityFactory );
			}
			
			public void RegisterFactory( EntityFactory factory )
			{
				classname[factory.classname] = factory;
			}
			
			public BaseEntity CreateEntity( EntitySystem entitySystem, SourceRenderContext renderContext, BSPEntity bspEntity ) //BSPRenderer renderer, 
			{
				if ( classname.TryGetValue( bspEntity.classname, out EntityFactory factory ) )
				{
					return factory( entitySystem, renderContext, bspEntity ); //renderer, 
				}
				else
				{
					// Fallback
					return new BaseEntity( entitySystem, renderContext, bspEntity ); //renderer, 
				}
			}

		}*/

	}
}
