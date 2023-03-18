using Sandbox;
using System;
namespace MapParser.GoldSrc.Entities
{
	public partial class ambient_Generic : IGoldSrcEntity
	{
		public ambient_generic_CL CL;
		//public ambient_generic_SV SV;
		public EntityParser.EntityData entData { get; set; }

		public static ambient_Generic Create( EntityParser.EntityData entData, Manager.SpawnParameter settings )
		{
			ambient_Generic ent = new ambient_Generic();
			ent.entData = entData;

				ent.CL = new ambient_generic_CL( ref entData, ref settings );

			return ent;
		}

		public static void TryLink(){}

		public void Delete()
		{
			if ( CL != null && CL.IsValid() )
				CL.Remove();
		}

		public class ambient_generic_CL : SceneCustomObject
		{
			[Flags]
			public enum SpawnFlags
			{
				PlayEverywhere = 1,
				SmallRadius = 2, // Radius are not working
				MediumRadius = 4,
				LargeRadius = 8,
				StartSilent = 16,
				NotToggled = 32,
			}
			public string targetName;
			public string message; // File
			public ushort volume = 10;
			public ushort preset;
			public ushort volstart;
			public ushort fadein;
			public ushort fadeout;
			public ushort pitch = 100;
			public ushort pitchStart = 100;
			public ushort spinUp;
			public ushort spinDown;
			public ushort lfoType;
			public ushort lfoRate;
			public ushort lfoModPitch;
			public ushort lfoModVOl;
			public ushort cspinup;

			public bool Toggle = false;
			public bool Finished = false;
			bool looped = false;
			bool notToggled = false;

			bool playEverywhere = false;
			bool playOnStartup = true;
			private Sound Sound = default;
			private SoundStream? SoundStream;
			private SoundData? SoundData;
			private int SampleRate;
			private float Duration = 1f;
			public ambient_generic_CL( ref EntityParser.EntityData entData, ref Manager.SpawnParameter settings ) : base( settings.sceneWorld )
			{
				Position = settings.position;

				if ( entData.data.TryGetValue( "origin", out var origin ) )
					Position += Vector3.Parse( origin );

				if ( entData.data.TryGetValue( "spawnflags", out var spawnflags ) )
				{
					var flag = (SpawnFlags)ushort.Parse( spawnflags );
				
					playEverywhere = flag.HasFlag( SpawnFlags.PlayEverywhere );
					notToggled = flag.HasFlag( SpawnFlags.NotToggled );
					playOnStartup = !flag.HasFlag( SpawnFlags.StartSilent );
				}

				message = entData.data["message"];

				LoadSound( message, settings );

				Event.Register( this );
			}
			~ambient_generic_CL()
			{
				Remove();
			}

			public void Remove()
			{
				Toggle = false;
				playOnStartup = false;

				if ( SoundStream is not null )
					SoundStream.Delete();

				SoundStream = null;
				SoundData = null;

				if ( Sound.IsPlaying )
					Sound.Stop();

				Event.Unregister( this );

				Delete();
			}
			void LoadSound( string file, Manager.SpawnParameter settings )
			{
				if ( !file.EndsWith( ".wav" ) )
					file = $"{file}.wav";

				var path = settings.assetparty_version ? $"sound/{file}.txt" : $"{Manager.downloadPath}{settings.saveFolder}sound/{file}";
				
				if ( !settings.fileSystem.FileExists( path ) )
				{
					Notify.Create( $"Wav file not found ({path})" );
					Remove();
					return;
				}

				SoundData ??= SoundLoader.LoadSamples( path, settings.fileSystem );
				var SoundSpeed = 1;
				SampleRate = (int)SoundData.SampleRate * SoundSpeed;
				Duration = SoundData.Duration;
				looped = SoundData.LoopEnd == 0; // != 0, marked every sounds are looped, we must determine sound is looped or not
			}

			public void Trigger()
			{
				if( notToggled )
				{
					if ( !Toggle || Sound.ElapsedTime > Duration )
						StartSound();
				}
				else
				{
					if ( Toggle )
						StopSound();
					else
						StartSound();
				}
			}

			void StartSound()
			{
				Toggle = true;
				playOnStartup = false;

				if ( Sound.IsPlaying || Sound.ElapsedTime < Duration )
					Sound.Stop();

				Sound = playEverywhere ? Sound.FromScreen( "audiostream.default" ).SetVolume( volume * 0.1f ) : Sound.FromWorld( "audiostream.default", Position ).SetVolume( volume * 0.1f );
				SoundStream = Sound.CreateStream( SampleRate );
				SoundStream.WriteData( SoundData.Samples );
			}

			void StopSound()
			{
				Toggle = false;
				Sound.Stop();
			}

			[Event.Tick]
			public void Tick()
			{
				if ( (looped && Toggle && Sound.ElapsedTime > Duration ) || playOnStartup ) //notToggled &&
					StartSound();

				if ( Sound.IsPlaying && playEverywhere ) 
					Sound.SetPosition( Game.LocalPawn.Position ); // There are noise because of steam audio?
			}
		}
	}
}
