// From https://github.com/Small-Fish-Dev/xoxoxo/

using System;
using System.Collections.Generic;
using System.IO;
using Sandbox;

/// <summary>
/// Sample loader that buffers via filename.
/// Unique sound files only ever get loaded once, they're then handed out again and again to voices that request them
/// </summary>
public static partial class SoundLoader
{
	private static bool Debug { get; set; } = false;

	private static Dictionary<string, SoundData> LoadedSoundData = new(); // Need to be cleared

	[Event.Hotload]
	public static void OnHotload()
	{
		if ( Debug )
			LoadedSoundData.Clear();
	}

	/// <summary>
	/// Get samples from a sound file.
	/// </summary>
	/// <param name="file">Path to a sound file</param>
	public static SoundData LoadSamples( string file, BaseFileSystem filesystem )
	{

		SoundData soundData = null;

		var soundName = System.IO.Path.GetFileNameWithoutExtension( file ).ToLower();

		if ( LoadedSoundData.TryGetValue( soundName, out soundData ) )
		{
			return soundData;
		}

		Stream stream = filesystem.OpenRead( file );
		soundData = LoadFromWav( stream );

		if ( soundData is null )
			throw new InvalidSoundDataException( "No sound data was loaded" );

		if ( Debug )
		{

			Log.Info( $"File: {file}" );
			Log.Info( $"Size: {soundData.Size}" );
			Log.Info( $"SampleSize: {soundData.SampleSize}" );
			Log.Info( $"SampleRate: {soundData.SampleRate}" );
			Log.Info( $"SampleCount: {soundData.SampleCount}" );
			Log.Info( $"BitsPerSample: {soundData.BitsPerSample}" );
			Log.Info( $"Channels: {soundData.Channels}" );
			Log.Info( $"LoopStart: {soundData.LoopStart}" );
			Log.Info( $"LoopEnd: {soundData.LoopEnd}" );

		}

		soundData.File = file;
		LoadedSoundData.Add( soundName, soundData );
		return soundData;

	}

	public class InvalidSoundDataException : Exception
	{

		public InvalidSoundDataException() : base() { }
		public InvalidSoundDataException( string message ) : base( message ) { }
		public InvalidSoundDataException( string message, Exception inner ) : base( message, inner ) { }

	}
}

public class SoundData
{

	public const int NoLoop = -1;

	public string File;
	public uint Size;
	public uint BitsPerSample;
	public uint SampleSize;
	public float Duration;
	public uint SampleRate;
	public uint SampleCount;
	public uint Channels;
	public int LoopStart = NoLoop;
	public int LoopEnd;

	public short[] Samples;

}
