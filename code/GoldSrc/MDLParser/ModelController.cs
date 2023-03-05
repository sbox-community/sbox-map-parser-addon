// sbox.Community © 2023-2024

using MapParser.GoldSrc.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//using static MapParser.GoldSrc.MDLParser.ModelRenderer;

namespace MapParser.GoldSrc.Entities
{
	public static class ModelController
	{


		/**
		 * Calculates frame index by animation time, fps and frames number
		 */
		//	private static int GetCurrentFrame( float time, float fps, int numFrames )
		//		{
		//			return MathF.FloorToInt( (time % (numFrames / fps)) / (1 / fps) );
		//		}
		//
		//		/**
		//		 * Returns the mesh's animation clips
		//		 */
		//		public static List<AnimationClip> PrepareAnimationClips( MeshRenderData meshData, ModelData modelData )
		//		{
		//			List<AnimationClip> animationClips = new List<AnimationClip>();
		//			for ( int i = 0; i < meshData.GeometryBuffers.Count; i++ )
		//			{
		//				// Sequence level
		//				var sequence = modelData.Sequences[i];
		//				bool isNoLoop = false;
		//				var frameBuffers = new List<MorphTargetFrameBuffer>();
		//				for ( int j = 0; j < sequenceBuffers.Count; j++ )
		//				{
		//					frameBuffers.Add( new MorphTargetFrameBuffer
		//					{
		//						Name = j.ToString(),
		//						Vertices = sequenceBuffers[j].Array
		//					} );
		//				}
		//
		//				var animationClip = AnimationClip.CreateFromMorphTargetSequence(
		//					sequence.Label,
		//					frameBuffers.ToArray(),
		//					sequence.FPS,
		//					isNoLoop
		//				);
		//
		//				animationClips.Add( animationClip );
		//			}
		//
		//			return animationClips;
		//		}
		//
		//		public struct MorphTargetFrameBuffer
		//		{
		//			public string Name;
		//			public Vector3[] Vertices;
		//		}

//		setPlaybackRate: (rate: number) => {
//      mixer.timeScale = rate !== 0 ? 1 / rate : 0
//    },
//
//    /**
//     * Sets visibility of the mesh
//     */
//    setVisibility: (isVisible: boolean) => {
//      mesh.visible = isVisible
//},
//
//    /**
//     * Updates delta tile
//     */
//    update: (deltaTime: number) => mixer.update( deltaTime ),
//
//    /**
//     * Sets animation to play
//     */
//    setAnimation: (sequenceIndex: number) => {
//	const wasPaused = activeAction ? activeAction.paused : false
//
//
//	  previousAction = activeAction
//
//	  activeAction = actions[sequenceIndex]
//
//
//	  if ( previousAction )
//	{
//		previousAction.stop()
//
//	  }
//
//	// Update mesh morph targets
//	const geometry = mesh.geometry
//
//	  if ( geometry instanceof THREE.BufferGeometry) {
//		// mesh.updateMorphTargets()
//
//		geometry.morphAttributes.position = meshRenderData.geometryBuffers[sequenceIndex]
//
//	  }
//
//	activeAction.reset().play()
//
//	  activeAction.paused = wasPaused
//
//	},
//
//    /**
//     * Set pause state of the running animation
//     */
//    setPause: (isPaused: boolean) => {
//	if ( activeAction )
//	{
//		activeAction.paused = isPaused
//
//	  }
//},
//
//    /**
//     * Jump to specific time of the running animation
//     */
//    setTime: (time: number) => {
//	if ( activeAction )
//	{
//		activeAction.time = time
//
//	  }
//},
//
//    /**
//     * Returns current time of the running animation
//     */
//    getCurrentTime: () => (activeAction ? activeAction.time : 0)
//  }
//
//  return meshModelController
//}
//
///**
// * The model state
// */
//export type ModelState = {
//  isPaused: boolean
//  activeAnimationIndex: number
//  showedSubModels: number[]
//  frame: number
//  playbackRate: number
//}
//
///**
// * Creates model controller.
// * @todo refactor this shit
// */
//export const createModelController = (
//  meshes: THREE.Mesh[][][],
//  meshesRenderData: MeshRenderData[][][],
//modelData: ModelData,
//  initialSequence: number = 0
//) => {
//	let playbackRate = 1
//  let isAnimationPaused = false
//
//  // Active sequence
//  let activeSequenceIndex: number = initialSequence
//
//  // List of showed sub models indices
//  let showedSubModels: number[] = modelData.bodyParts.map( () => 0 )
//
//  // Path: [bodyPartIndex][subModelIndex][meshIndex][sequenceIndex]
//  const meshControllers = meshes.map( ( bodyPart, bodyPartIndex ) =>
//	bodyPart.map( ( subModel, subModelIndex ) =>
//	  subModel.map( ( mesh, meshIndex ) =>
//		createMeshController(
//		  mesh,
//		  meshesRenderData[bodyPartIndex][subModelIndex][meshIndex],
//		  modelData,
//		  subModelIndex === 0
//		)
//	  )
//	)
//  )
//
//  // Setting default animation
//  meshControllers.forEach( bodyPart =>
//	bodyPart.forEach( subModel => subModel.forEach( controller => controller.setAnimation( activeSequenceIndex ) ) )
//  )
//
//  const getAnimationTime = () =>
//	R.converge( R.divide, [R.sum, R.length] )(
//	  meshControllers.reduce(
//		( acc, bodyPart ) =>
//		  acc.concat(
//			bodyPart.reduce(
//			  ( acc, subModel ) => acc.concat( subModel.map( controller => controller.getCurrentTime() ) ),
//
//			  [] as number[]
//			)
//		  ),
//
//		[] as number[]
//	  )
//	)
//
//  const areSubModelsPaused = () =>
//	meshControllers.reduce(
//	  ( acc, bodyPart ) =>
//		acc
//		&& bodyPart.reduce(
//		  ( acc, subModel ) => acc && subModel.reduce( ( acc, controller ) => acc && controller.isPaused, true ),
//		  true
//		),
//	  true
//	)
//
//  /** Returns current state of the model */
//  const getCurrentState = (): ModelState => ({
//		isPaused: areSubModelsPaused(),
//    activeAnimationIndex: activeSequenceIndex,
//    showedSubModels,
//    frame: getCurrentFrame(
//	  getAnimationTime(),
//	  modelData.sequences[activeSequenceIndex].fps,
//	  modelData.sequences[activeSequenceIndex].numFrames
//	),
//    playbackRate
//  })
//
//  const modelController = {
//    /**
//     * Updates delta til=me
//     * @param deltaTime
//     */
//    update: (deltaTime: number) =>
//      meshControllers.forEach( bodyPart =>
//		bodyPart.forEach( subModel =>
//		  subModel.forEach( controller => {
//			  controller.update( deltaTime )
//
//		  } )
//	  )
//	  ),
//
//    /** Returns current state of the model */
//    getCurrentState,
//
//    /** Set pause state of the model */
//    setPause: (isPaused: boolean) => {
//		isAnimationPaused = isPaused
//
//
//	  meshControllers.forEach( bodyPart =>
//		bodyPart.forEach( subModel => subModel.forEach( controller => controller.setPause( isAnimationPaused ) ) )
//	  )
//
//
//	  return getCurrentState()
//
//	},
//
//    /**
//     * Sets playback rate (animation speed)
//     * @param rate
//     */
//    setPlaybackRate: (rate: number) => {
//		playbackRate = rate
//
//
//	  meshControllers.forEach( bodyPart =>
//		bodyPart.forEach( subModel => subModel.forEach( controller => controller.setPlaybackRate( playbackRate ) ) )
//	  )
//
//
//	  return getCurrentState()
//
//	},
//    /**
//     * Sets animation to play
//     * @param sequenceIndex
//     */
//    setAnimation: (sequenceIndex: number) => {
//		activeSequenceIndex = sequenceIndex
//
//
//	  meshControllers.forEach( bodyPart =>
//		bodyPart.forEach( subModel => subModel.forEach( controller => controller.setAnimation( sequenceIndex ) ) )
//	  )
//
//
//	  return getCurrentState()
//
//	},
//
//    /**
//     * Shows specified sub model
//     */
//    showSubModel: (bodyPartIndex: number, subModelIndex: number) => {
//		showedSubModels[bodyPartIndex] = subModelIndex
//
//
//	  meshControllers[bodyPartIndex].forEach( ( subModel, i ) => {
//		  const isVisible = i === subModelIndex
//
//
//		subModel.forEach( controller => {
//			controller.setVisibility( isVisible )
//
//		} )
//
//	  } )
//
//
//	  return getCurrentState()
//
//	},
//
//    /**
//     * Sets specific frame of the running animations
//     */
//    setFrame: (frame: number) => {
//		const { numFrames, fps } = modelData.sequences[activeSequenceIndex]
//
//	  const safeFrame = R.clamp( 0, numFrames, frame )
//
//	  const duration = numFrames / fps
//
//	  const specifiedTime = (duration / numFrames) * safeFrame
//
//
//	  meshControllers.forEach( bodyPart =>
//		bodyPart.forEach( subModel => subModel.forEach( controller => controller.setTime( specifiedTime ) ) )
//	  )
//
//
//	  return getCurrentState()
//
//	}
//}
//
//return modelController
//}

	
	}
}
