// sbox.Community © 2023-2024

using Sandbox;
using Sandbox.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static MapParser.SourceEngine.Gfx;
using static MapParser.SourceEngine.Materials;

namespace MapParser.SourceEngine
{

	public class VTF
	{
		public enum ImageFormat
		{
			RGBA8888 = 0x00,
			ABGR8888 = 0x01,
			RGB888 = 0x02,
			BGR888 = 0x03,
			I8 = 0x05,
			ARGB8888 = 0x0B,
			BGRA8888 = 0x0C,
			DXT1 = 0x0D,
			DXT3 = 0x0E,
			DXT5 = 0x0F,
			BGRX8888 = 0x10,
			BGRA5551 = 0x15,
			UV88 = 0x16,
			RGBA16161616F = 0x18
		}

		public static bool ImageFormatIsBlockCompressed( ImageFormat fmt )
		{
			return fmt switch
			{
				ImageFormat.DXT1 => true,
				ImageFormat.DXT3 => true,
				ImageFormat.DXT5 => true,
				_ => false,
			};
		}

		public static int ImageFormatGetBPP( ImageFormat fmt )
		{
			return fmt switch
			{
				ImageFormat.RGBA16161616F => 8,
				ImageFormat.RGBA8888 => 4,
				ImageFormat.ABGR8888 => 4,
				ImageFormat.ARGB8888 => 4,
				ImageFormat.BGRA8888 => 4,
				ImageFormat.BGRX8888 => 4,
				ImageFormat.RGB888 => 3,
				ImageFormat.BGR888 => 3,
				ImageFormat.BGRA5551 => 2,
				ImageFormat.UV88 => 2,
				ImageFormat.I8 => 1,
				_ => throw new Exception( "whoops" ),
			};
		}

		public static int ImageFormatCalcLevelSize( ImageFormat fmt, int width, int height, int depth )
		{
			if ( ImageFormatIsBlockCompressed( fmt ) )
			{
				width = Math.Max( width, 4 );
				height = Math.Max( height, 4 );
				var count = ((width * height) / 16) * depth;
				if ( fmt == ImageFormat.DXT1 )
					return count * 8;
				else if ( fmt == ImageFormat.DXT3 )
					return count * 16;
				else if ( fmt == ImageFormat.DXT5 )
					return count * 16;
				else
					throw new Exception( "whoops" );
			}
			else
			{
				return (width * height * depth) * ImageFormatGetBPP( fmt );
			}
		}

		public static Sandbox.ImageFormat ImageFormatToGfxFormat( ImageFormat fmt, bool srgb ) //GfxDevice device,
		{

			// TODO(jstpierre): Software decode BC1 if necessary.
			if ( fmt == ImageFormat.DXT1 )
				return srgb ? Sandbox.ImageFormat.BC6H : Sandbox.ImageFormat.BC7;//GfxFormat.BC1_SRGB : GfxFormat.BC1;
			/*else if ( fmt == ImageFormat.DXT3 )
				return srgb ? GfxFormat.BC2_SRGB : GfxFormat.BC2;
			else if ( fmt == ImageFormat.DXT5 )
				return srgb ? GfxFormat.BC3_SRGB : GfxFormat.BC3;
			else if ( fmt == ImageFormat.RGBA8888 )
				return srgb ? GfxFormat.U8_RGBA_SRGB : GfxFormat.U8_RGBA_NORM;
			else if ( fmt == ImageFormat.RGB888 )
				return srgb ? GfxFormat.U8_RGBA_SRGB : GfxFormat.U8_RGBA_NORM;
			else if ( fmt == ImageFormat.BGR888 )
				return srgb ? GfxFormat.U8_RGBA_SRGB : GfxFormat.U8_RGBA_NORM;
			else if ( fmt == ImageFormat.BGRA8888 )
				return srgb ? GfxFormat.U8_RGBA_SRGB : GfxFormat.U8_RGBA_NORM;
			else if ( fmt == ImageFormat.ABGR8888 )
				return srgb ? GfxFormat.U8_RGBA_SRGB : GfxFormat.U8_RGBA_NORM;
			else if ( fmt == ImageFormat.BGRX8888 )
				return srgb ? GfxFormat.U8_RGBA_SRGB : GfxFormat.U8_RGBA_NORM;
			else if ( fmt == ImageFormat.BGRA5551 )
				return GfxFormat.U16_RGBA_5551; // TODO(jstpierre): sRGB?
			else if ( fmt == ImageFormat.UV88 )
				return GfxFormat.S8_RG_NORM;
			else if ( fmt == ImageFormat.I8 )
				return GfxFormat.U8_RGBA_NORM;
			else if ( fmt == ImageFormat.RGBA16161616F )
				return GfxFormat.F16_RGBA;
			else*/
				throw new Exception( "whoops" );
		}

		public static byte[] ImageFormatConvertData(  ImageFormat fmt, byte[] data, int width, int height, int depth ) //GfxDevice device,
		{
			if ( fmt == ImageFormat.BGR888 )
			{
				// BGR888 => RGBA8888
				byte[] src = data.ToArray();
				int n = width * height * depth * 4;
				byte[] dst = new byte[n];
				int p = 0;
				using ( BinaryReader reader = new BinaryReader( new MemoryStream( src ) ) )
				{
					for ( int i = 0; i < n; )
					{
						dst[i++] = reader.ReadByte();
						dst[i++] = reader.ReadByte();
						dst[i++] = reader.ReadByte();
						dst[i++] = 255;
						p += 3;
					}
				}
				return dst;
			}
			else if ( fmt == ImageFormat.RGB888 )
			{
				// RGB888 => RGBA8888
				byte[] src = data.ToArray();
				int n = width * height * depth * 4;
				byte[] dst = new byte[n];
				int p = 0;
				using ( BinaryReader reader = new BinaryReader( new MemoryStream( src ) ) )
				{
					for ( int i = 0; i < n; )
					{
						dst[i++] = reader.ReadByte();
						dst[i++] = reader.ReadByte();
						dst[i++] = reader.ReadByte();
						dst[i++] = 255;
						p += 3;
					}
				}
				return dst;
			}
			else if ( fmt == ImageFormat.ABGR8888 )
			{
				// ABGR8888 => RGBA8888
				byte[] src = data.ToArray();
				int n = width * height * depth * 4;
				byte[] dst = new byte[n];
				int p = 0;
				using ( BinaryReader reader = new BinaryReader( new MemoryStream( src ) ) )
				{
					for ( int i = 0; i < n; )
					{
						dst[i++] = reader.ReadByte();
						dst[i++] = reader.ReadByte();
						dst[i++] = reader.ReadByte();
						dst[i++] = reader.ReadByte();
						p += 4;
					}
				}
				return dst;
			}
			else if ( fmt == ImageFormat.BGRA8888 )
			{
				// BGRA8888 => RGBA8888
				byte[] src = data.ToArray();
				int n = width * height * depth * 4;
				byte[] dst = new byte[n];
				int p = 0;
				using ( BinaryReader reader = new BinaryReader( new MemoryStream( src ) ) )
				{
					for ( int i = 0; i < n; )
					{
						dst[i++] = reader.ReadByte();
						dst[i++] = reader.ReadByte();
						dst[i++] = reader.ReadByte();
						dst[i++] = reader.ReadByte();
						p += 4;
					}
				}
				return dst;
			}
			else if ( fmt == ImageFormat.BGRX8888 )
			{
				// BGRX8888 => RGBA8888
				byte[] src = data.ToArray();
				int n = width * height * depth * 4;
				byte[] dst = new byte[n];
				int p = 0;
				using ( BinaryReader reader = new BinaryReader( new MemoryStream( src ) ) )
				{
					for ( int i = 0; i < n; )
					{
						dst[i++] = reader.ReadByte();
						dst[i++] = reader.ReadByte();
						dst[i++] = reader.ReadByte();
						dst[i++] = 0xFF;
						p += 4;
					}
				}
				return dst;
			}
			else if ( fmt == ImageFormat.UV88 )
			{
				return data;
			}
			return new byte[0];
		}

	public enum VTFFlags
	{
		POINTSAMPLE = 0x00000001,
		TRILINEAR = 0x00000002,
		CLAMPS = 0x00000004,
		CLAMPT = 0x00000008,
		SRGB = 0x00000040,
		NOMIP = 0x00000100,
		ONEBITALPHA = 0x00001000,
		EIGHTBITALPHA = 0x00002000,
		ENVMAP = 0x00004000,
	}

	public class VTFResourceEntry
	{
		public int rsrcID;
		public byte[] data;
	}


		//public GfxTexture[] gfxTextures = new GfxTexture[0];
		public Texture[] gfxTextures = new Texture[0];
		//public GfxSampler gfxSampler = null;

		public ImageFormat format;
	public VTFFlags flags = 0;
	public int width = 0;
	public int height = 0;
	public int depth = 1;
	public int numFrames = 1;
	public int numLevels = 1;

	public List<VTFResourceEntry> resources = new();

	private int versionMajor;
	private int versionMinor;

		public string name;
		public bool srgb;
		public LateBindingTexture lateBinding;

		public VTF( byte[] buffer, string name, bool srgb, LateBindingTexture lateBinding = 0) //GfxDevice device, GfxRenderCache cache,  //verify, lateBinding default param
		{
			if ( buffer == null )
				return;

			this.name = name;
			this.srgb = srgb;
			this.lateBinding = lateBinding;

			//var view = buffer.CreateDataView();

			//Debug.Assert( ReadString( buffer, 0x00, 0x04, false ) == "VTF\0" );
			if ( Util.ReadString( ref buffer, 0x00, 0x04, false ) != "VTF\0" )
				return; //HATA
			this.versionMajor = (int)BitConverter.ToUInt32( buffer,  0x04 );
			if ( this.versionMajor != 7 )
				return; //HATA
			//Debug.Assert( this.versionMajor == 7 );
			this.versionMinor = (int)BitConverter.ToUInt32( buffer, 0x08 );
			if ( !(this.versionMinor >= 0 && this.versionMinor <= 5) )
				return; //HATA
			
			//Debug.Assert( this.versionMinor >= 0 && this.versionMinor <= 5 );
			var headerSize = BitConverter.ToUInt32( buffer, 0x0C );

			int dataIdx;
			int imageDataIdx = 0;

			if ( this.versionMajor == 0x07 )
			{
				//Debug.Assert( this.versionMinor >= 0x00 );
				if ( !(this.versionMinor >= 0x00) )
					return; //HATA

				this.width = BitConverter.ToUInt16( buffer, 0x10 );
				this.height = BitConverter.ToUInt16( buffer, 0x12 );
				this.flags = (VTFFlags)BitConverter.ToUInt32( buffer, 0x14 );
				this.numFrames = BitConverter.ToUInt16( buffer, 0x18 );
				var startFrame = BitConverter.ToUInt16( buffer, 0x1A );
				var reflectivityR = BitConverter.ToSingle( buffer, 0x20 );
				var reflectivityG = BitConverter.ToSingle( buffer, 0x24 );
				var reflectivityB = BitConverter.ToSingle( buffer, 0x28 );
				var bumpScale = BitConverter.ToSingle( buffer, 0x30 );
				this.format = (ImageFormat)BitConverter.ToUInt32( buffer, 0x34 );
				this.numLevels = buffer[0x38];
				var lowresImageFormat = BitConverter.ToUInt32( buffer, 0x39 ); //uint
				var lowresImageWidth = buffer[0x3D];
				var lowresImageHeight = buffer[0x3E];

				dataIdx = 0x40;

				if ( this.versionMinor >= 0x02 )
				{
					this.depth = Math.Max((int)BitConverter.ToUInt16(buffer, 0x41), 1);
					dataIdx = 0x50;
				}
				else
				{
					this.depth = 1;
				}
				var numResources = this.versionMinor >= 0x03 ? BitConverter.ToUInt32( buffer, 0x44 ) : 0;
				if ( numResources > 0 )
				{
					for ( int i = 0; i < numResources; i++, dataIdx += 0x08 )
					{
						uint rsrcHeader = BitConverter.ToUInt32( buffer, dataIdx + 0x00 );
						int rsrcID = (int)(rsrcHeader & 0xFFFFFF00); //uint
						uint rsrcFlag = (rsrcHeader & 0x000000FF);
						int dataOffs = (int)BitConverter.ToUInt32( buffer, dataIdx + 0x04 ); //uint

						// RSRCFHAS_NO_DATA_CHUNK
						if ( rsrcFlag == 0x02 )
							continue;

						// Legacy resources don't have a size tag.

						if ( rsrcID == 0x01000000 ) // VTF_LEGACY_RSRC_LOW_RES_IMAGE
						{
							// Skip.
							continue;
						}

						if ( rsrcID == 0x30000000 ) // VTF_LEGACY_RSRC_IMAGE
						{
							imageDataIdx = dataOffs;
							continue;
						}

						int dataSize = (int)BitConverter.ToUInt32( buffer, dataOffs + 0x00 );//uint
						byte[] data = new ArraySegment<byte>( buffer, dataOffs + 0x04, dataSize ).ToArray();
						this.resources.Add( new VTFResourceEntry { rsrcID = rsrcID, data = data } );
					}
				}
				else
				{
					if ( lowresImageFormat != 0xFFFFFFFF )
					{
						int lowresDataSize = ImageFormatCalcLevelSize( (ImageFormat)lowresImageFormat, lowresImageWidth, lowresImageHeight, 1 );//uint
						byte[] lowresData = new ArraySegment<byte>( buffer, dataIdx, lowresDataSize ).ToArray();
						dataIdx += lowresDataSize;
					}

					imageDataIdx = dataIdx;
				}

			}
			else
			{
				throw new Exception( "whoops" );
			}

			bool isCube = (this.flags & VTFFlags.ENVMAP) != 0;
			// The srgb flag in the file does nothing :/, we have to know from the material system instead.
			// bool srgb = (this.flags & VTFFlags.SRGB) != 0;
			//var pixelFormat = ImageFormatToGfxFormat(  this.format, srgb );//device,
			GfxTextureDimension dimension = isCube ? GfxTextureDimension.Cube : GfxTextureDimension.n2D;
			int faceCount = isCube ? 6 : 1;
			bool hasSpheremap = this.versionMinor < 5;
			int faceDataCount = isCube ? (6 + (hasSpheremap ? 1 : 0)) : 1;
			//GfxTextureDescriptor descriptor = new GfxTextureDescriptor() { Dimension= dimension, PixelFormat= pixelFormat, Width= this.width, Height= this.height, NumLevels = this.numLevels, Depth=  this.depth * faceCount, Usage= GfxTextureUsage.Sampled };

			for ( int i = 0; i < this.numFrames; i++ )
			{
				//GfxTexture texture = device.createTexture( descriptor );
				//device.setResourceName( texture, $"{this.name} frame {i}" );
				//this.gfxTextures.Add( texture );
			}

			var levelDatas = new byte[this.gfxTextures.Length][][];
			for ( int i = this.numLevels - 1; i >= 0; i-- )
			{
				var mipWidth = Math.Max( this.width >> i, 1 );
				var mipHeight = Math.Max( this.height >> i, 1 );
				var faceSize = this.CalcMipSize( i );
				var size = faceSize * faceCount;
				for ( int j = 0; j < this.gfxTextures.Length; j++ )
				{
					var levelData = ImageFormatConvertData(  this.format, buffer.Skip( imageDataIdx ).Take( size ).ToArray(), mipWidth, mipHeight, this.depth * faceCount );//device,
					imageDataIdx += faceSize * faceDataCount;
					Array.Reverse( levelDatas[j] );
					Array.Resize( ref levelDatas[j], levelDatas[j].Length + 1 );
					levelDatas[j][0] = levelData;
				}
			}


			/*for ( int i = 0; i < this.gfxTextures.Length; i++ )
			{
				device.UploadTextureData( this.gfxTextures[i], 0, levelDatas[i] );
			}*/

			var wrapS = (this.flags & VTFFlags.CLAMPS) != 0 ? GfxWrapMode.Clamp : GfxWrapMode.Repeat;
			var wrapT = (this.flags & VTFFlags.CLAMPT) != 0 ? GfxWrapMode.Clamp : GfxWrapMode.Repeat;

			var texFilter = (this.flags & VTFFlags.POINTSAMPLE) != 0 ? GfxTexFilterMode.Point : GfxTexFilterMode.Bilinear;
			var minFilter = texFilter;
			var magFilter = texFilter;
			var forceTrilinear = true;
			var mipFilter = (this.flags & VTFFlags.NOMIP) != 0 ? GfxMipFilterMode.NoMip :
				(forceTrilinear || (this.flags & VTFFlags.TRILINEAR) != 0) ? GfxMipFilterMode.Linear : GfxMipFilterMode.Nearest; //buralarda unlem vardi sorun cikabilir

			var canSupportAnisotropy = texFilter == GfxTexFilterMode.Bilinear && mipFilter == GfxMipFilterMode.Linear;
			var maxAnisotropy = canSupportAnisotropy ? 16 : 1;
			/*this.gfxSampler = cache.CreateSampler( new SamplerDescriptor
			{
				WrapS = wrapS,
				WrapT = wrapT,
				MinFilter = minFilter,
				MagFilter = magFilter,
				MipFilter = mipFilter,
				MaxAnisotropy = maxAnisotropy
			} );*/

		}

		private int CalcMipSize( int i, int? depth = null )
		{
			int mipWidth = Math.Max( this.width >> i, 1 );
			int mipHeight = Math.Max( this.height >> i, 1 );
			int mipDepth = Math.Max( (depth is not null ? depth.Value : this.depth) >> i, 1 );
			return ImageFormatCalcLevelSize( this.format, mipWidth, mipHeight, mipDepth );
		}

		public void FillTextureMapping( TextureMapping m, int frame = 0 )
		{
			if ( this.gfxTextures.Length == 0 )
			{
				m.GfxTexture = null;
			}
			else
			{
				if ( frame < 0 || frame >= this.gfxTextures.Length )
					frame = 0;
				m.GfxTexture = this.gfxTextures[frame] ?? throw new Exception( "GfxTexture not found." );
			}
			//m.gfxSampler = this.gfxSampler;
			m.Width = this.width;
			m.Height = this.height;
			//m.lateBinding = this.lateBinding;
		}

		public bool IsTranslucent()
		{
			return (this.flags & (VTFFlags.ONEBITALPHA | VTFFlags.EIGHTBITALPHA)) != 0;
		}

		/*public void Destroy( GfxDevice device )
		{
			for ( int i = 0; i < this.gfxTextures.Length; i++ )
				device.DestroyTexture( this.gfxTextures[i] );
			this.gfxTextures = Array.Empty<GfxTexture>();
		}*/

	}


























	public class Gfx
	{







		public enum FormatTypeFlags
		{
			U8 = 0x01,
			U16,
			U32,
			S8,
			S16,
			S32,
			F16,
			F32,

			// Compressed texture formats.
			BC1 = 0x41,
			BC2,
			BC3,
			BC4_UNORM,
			BC4_SNORM,
			BC5_UNORM,
			BC5_SNORM,

			// Special-case packed texture formats.
			U16_PACKED_5551 = 0x61,
			U16_PACKED_565,

			// Depth/stencil texture formats.
			D24 = 0x81,
			D32F,
			D24S8,
			D32FS8,
		}

		public enum FormatCompFlags
		{
			R = 0x01,
			RG = 0x02,
			RGB = 0x03,
			RGBA = 0x04,
		}

		public enum FormatFlags
		{
			None = 0b00000000,
			Normalized = 0b00000001,
			sRGB = 0b00000010,
			Depth = 0b00000100,
			Stencil = 0b00001000,
			RenderTarget = 0b00010000,
		}


		public static uint MakeFormat( FormatTypeFlags type, FormatCompFlags comp, FormatFlags flags )
		{
			return ((uint)type << 16) | ((uint)comp << 8) | (uint)flags;
		}

		/*public enum GfxFormat : uint
		{
			F16_R = MakeFormat( (FormatTypeFlags)FormatTypeFlags.F16, (FormatCompFlags)FormatCompFlags.R, (FormatFlags)FormatFlags.None ),
			F16_RG = MakeFormat( (FormatTypeFlags)FormatTypeFlags.F16, (FormatCompFlags)FormatCompFlags.RG, (FormatFlags)FormatFlags.None ),
			F16_RGB = MakeFormat( (FormatTypeFlags)FormatTypeFlags.F16, (FormatCompFlags)FormatCompFlags.RGB, (FormatFlags)FormatFlags.None ),
			F16_RGBA = MakeFormat( (FormatTypeFlags)FormatTypeFlags.F16, (FormatCompFlags)FormatCompFlags.RGBA, (FormatFlags)FormatFlags.None ),
			F32_R = MakeFormat( (FormatTypeFlags)FormatTypeFlags.F32, (FormatCompFlags)FormatCompFlags.R, (FormatFlags)FormatFlags.None ),
			F32_RG = MakeFormat( (FormatTypeFlags)FormatTypeFlags.F32, (FormatCompFlags)FormatCompFlags.RG, (FormatFlags)FormatFlags.None ),
			F32_RGB = MakeFormat( (FormatTypeFlags)FormatTypeFlags.F32, (FormatCompFlags)FormatCompFlags.RGB, (FormatFlags)FormatFlags.None ),
			F32_RGBA = MakeFormat( (FormatTypeFlags)FormatTypeFlags.F32, (FormatCompFlags)FormatCompFlags.RGBA, (FormatFlags)FormatFlags.None ),
			U8_R = MakeFormat( (FormatTypeFlags)FormatTypeFlags.U8, (FormatCompFlags)FormatCompFlags.R, (FormatFlags)FormatFlags.None ),
			U8_R_NORM = MakeFormat( (FormatTypeFlags)FormatTypeFlags.U8, (FormatCompFlags)FormatCompFlags.R, (FormatFlags)FormatFlags.Normalized ),
			U8_RG = MakeFormat( (FormatTypeFlags)FormatTypeFlags.U8, (FormatCompFlags)FormatCompFlags.RG, (FormatFlags)FormatFlags.None ),
			U8_RG_NORM = MakeFormat( (FormatTypeFlags)FormatTypeFlags.U8, (FormatCompFlags)FormatCompFlags.RG, (FormatFlags)FormatFlags.Normalized ),
			U8_RGB = MakeFormat( (FormatTypeFlags)FormatTypeFlags.U8, (FormatCompFlags)FormatCompFlags.RGB, (FormatFlags)FormatFlags.None ),
			U8_RGB_NORM = MakeFormat( (FormatTypeFlags)FormatTypeFlags.U8, (FormatCompFlags)FormatCompFlags.RGB, (FormatFlags)FormatFlags.Normalized ),
			U8_RGB_SRGB = MakeFormat( (FormatTypeFlags)FormatTypeFlags.U8, (FormatCompFlags)FormatCompFlags.RGB, (FormatFlags)FormatFlags.sRGB | (FormatFlags)FormatFlags.Normalized ),
			U8_RGBA = MakeFormat( (FormatTypeFlags)FormatTypeFlags.U8, (FormatCompFlags)FormatCompFlags.RGBA, (FormatFlags)FormatFlags.None ),
			U8_RGBA_NORM = MakeFormat( (FormatTypeFlags)FormatTypeFlags.U8, (FormatCompFlags)FormatCompFlags.RGBA, (FormatFlags)FormatFlags.Normalized ),
			U8_RGBA_SRGB = MakeFormat( (FormatTypeFlags)FormatTypeFlags.U8, (FormatCompFlags)FormatCompFlags.RGBA, (FormatFlags)FormatFlags.sRGB | (FormatFlags)FormatFlags.Normalized ),
			U16_R = MakeFormat( (FormatTypeFlags)FormatTypeFlags.U16, (FormatCompFlags)FormatCompFlags.R, (FormatFlags)FormatFlags.None ),
			U16_R_NORM = MakeFormat( (FormatTypeFlags)FormatTypeFlags.U16, (FormatCompFlags)FormatCompFlags.R, (FormatFlags)FormatFlags.Normalized ),
			U16_RG_NORM = MakeFormat( (FormatTypeFlags)FormatTypeFlags.U16, (FormatCompFlags)FormatCompFlags.RG, (FormatFlags)FormatFlags.Normalized ),
			U16_RGBA_NORM = MakeFormat( (FormatTypeFlags)FormatTypeFlags.U16, (FormatCompFlags)FormatCompFlags.RGBA, (FormatFlags)FormatFlags.Normalized ),
			U16_RGB = MakeFormat( (FormatTypeFlags)FormatTypeFlags.U16, (FormatCompFlags)FormatCompFlags.RGB, (FormatFlags)FormatFlags.None ),
			U32_R = MakeFormat( (FormatTypeFlags)FormatTypeFlags.U32, (FormatCompFlags)FormatCompFlags.R, (FormatFlags)FormatFlags.None ),
			U32_RG = MakeFormat( (FormatTypeFlags)FormatTypeFlags.U32, (FormatCompFlags)FormatCompFlags.RG, (FormatFlags)FormatFlags.None ),
			S8_R = MakeFormat( (FormatTypeFlags)FormatTypeFlags.S8, (FormatCompFlags)FormatCompFlags.R, (FormatFlags)FormatFlags.None ),
			S8_R_NORM = MakeFormat( (FormatTypeFlags)FormatTypeFlags.S8, (FormatCompFlags)FormatCompFlags.R, (FormatFlags)FormatFlags.Normalized ),
			S8_RG_NORM = MakeFormat( (FormatTypeFlags)FormatTypeFlags.S8, (FormatCompFlags)FormatCompFlags.RG, (FormatFlags)FormatFlags.Normalized ),
			S8_RGB_NORM = MakeFormat( (FormatTypeFlags)FormatTypeFlags.S8, (FormatCompFlags)FormatCompFlags.RGB, (FormatFlags)FormatFlags.Normalized ),
			S8_RGBA_NORM = MakeFormat( (FormatTypeFlags)FormatTypeFlags.S8, (FormatCompFlags)FormatCompFlags.RGBA, (FormatFlags)FormatFlags.Normalized ),
			S16_R = MakeFormat( (FormatTypeFlags)FormatTypeFlags.S16, (FormatCompFlags)FormatCompFlags.R, (FormatFlags)FormatFlags.None ),
			S16_RG = MakeFormat( (FormatTypeFlags)FormatTypeFlags.S16, (FormatCompFlags)FormatCompFlags.RG, (FormatFlags)FormatFlags.None ),
			S16_RG_NORM = MakeFormat( (FormatTypeFlags)FormatTypeFlags.S16, (FormatCompFlags)FormatCompFlags.RG, (FormatFlags)FormatFlags.Normalized ),
			S16_RGB_NORM = MakeFormat( (FormatTypeFlags)FormatTypeFlags.S16, (FormatCompFlags)FormatCompFlags.RGB, (FormatFlags)FormatFlags.Normalized ),
			S16_RGBA = MakeFormat( (FormatTypeFlags)FormatTypeFlags.S16, (FormatCompFlags)FormatCompFlags.RGBA, (FormatFlags)FormatFlags.None ),
			S16_RGBA_NORM = MakeFormat( (FormatTypeFlags)FormatTypeFlags.S16, (FormatCompFlags)FormatCompFlags.RGBA, (FormatFlags)FormatFlags.Normalized ),
			S32_R = MakeFormat( (FormatTypeFlags)FormatTypeFlags.S32, (FormatCompFlags)FormatCompFlags.R, (FormatFlags)FormatFlags.None ),

			// Packed texture formats.
			U16_RGBA_5551 = MakeFormat( (FormatTypeFlags)FormatTypeFlags.U16_PACKED_5551, (FormatCompFlags)FormatCompFlags.RGBA, (FormatFlags)FormatFlags.Normalized ),
			U16_RGB_565 = MakeFormat( (FormatTypeFlags)FormatTypeFlags.U16_PACKED_565, (FormatCompFlags)FormatCompFlags.RGB, (FormatFlags)FormatFlags.Normalized ),

			// Compressed
			BC1 = MakeFormat( (FormatTypeFlags)FormatTypeFlags.BC1, (FormatCompFlags)FormatCompFlags.RGBA, (FormatFlags)FormatFlags.Normalized ),
			BC1_SRGB = MakeFormat( (FormatTypeFlags)FormatTypeFlags.BC1, (FormatCompFlags)FormatCompFlags.RGBA, (FormatFlags)FormatFlags.Normalized | (FormatFlags)FormatFlags.sRGB ),
			BC2 = MakeFormat( (FormatTypeFlags)FormatTypeFlags.BC2, (FormatCompFlags)FormatCompFlags.RGBA, (FormatFlags)FormatFlags.Normalized ),
			BC2_SRGB = MakeFormat( (FormatTypeFlags)FormatTypeFlags.BC2, (FormatCompFlags)FormatCompFlags.RGBA, (FormatFlags)FormatFlags.Normalized | (FormatFlags)FormatFlags.sRGB ),
			BC3 = MakeFormat( (FormatTypeFlags)FormatTypeFlags.BC3, (FormatCompFlags)FormatCompFlags.RGBA, (FormatFlags)FormatFlags.Normalized ),
			BC3_SRGB = MakeFormat( (FormatTypeFlags)FormatTypeFlags.BC3, (FormatCompFlags)FormatCompFlags.RGBA, (FormatFlags)FormatFlags.Normalized | (FormatFlags)FormatFlags.sRGB ),
			BC4_UNORM = MakeFormat( (FormatTypeFlags)FormatTypeFlags.BC4_UNORM, (FormatCompFlags)FormatCompFlags.R, (FormatFlags)FormatFlags.Normalized ),
			BC4_SNORM = MakeFormat( (FormatTypeFlags)FormatTypeFlags.BC4_SNORM, (FormatCompFlags)FormatCompFlags.R, (FormatFlags)FormatFlags.Normalized ),
			BC5_UNORM = MakeFormat( (FormatTypeFlags)FormatTypeFlags.BC5_UNORM, (FormatCompFlags)FormatCompFlags.RG, (FormatFlags)FormatFlags.Normalized ),
			BC5_SNORM = MakeFormat( (FormatTypeFlags)FormatTypeFlags.BC5_SNORM, (FormatCompFlags)FormatCompFlags.RG, (FormatFlags)FormatFlags.Normalized ),

			// Depth/Stencil
			D24 = MakeFormat( (FormatTypeFlags)FormatTypeFlags.D24, (FormatCompFlags)FormatCompFlags.R, (FormatFlags)FormatFlags.Depth ),
			D24_S8 = MakeFormat( (FormatTypeFlags)FormatTypeFlags.D24S8, (FormatCompFlags)FormatCompFlags.RG, (FormatFlags)FormatFlags.Depth | (FormatFlags)FormatFlags.Stencil ),
			D32F = MakeFormat( (FormatTypeFlags)FormatTypeFlags.D32F, (FormatCompFlags)FormatCompFlags.R, (FormatFlags)FormatFlags.Depth ),
			D32F_S8 = MakeFormat( (FormatTypeFlags)FormatTypeFlags.D32FS8, (FormatCompFlags)FormatCompFlags.RG, (FormatFlags)FormatFlags.Depth | (FormatFlags)FormatFlags.Stencil ),

			// Special RT formats for preferred backend support.
			U8_RGB_RT = MakeFormat( (FormatTypeFlags)FormatTypeFlags.U8, (FormatCompFlags)FormatCompFlags.RGB, (FormatFlags)FormatFlags.RenderTarget | (FormatFlags)FormatFlags.Normalized ),
			U8_RGBA_RT = MakeFormat( (FormatTypeFlags)FormatTypeFlags.U8, (FormatCompFlags)FormatCompFlags.RGBA, (FormatFlags)FormatFlags.RenderTarget | (FormatFlags)FormatFlags.Normalized ),
			U8_RGBA_RT_SRGB = MakeFormat( (FormatTypeFlags)FormatTypeFlags.U8, (FormatCompFlags)FormatCompFlags.RGBA, (FormatFlags)FormatFlags.RenderTarget | (FormatFlags)FormatFlags.Normalized | (FormatFlags)FormatFlags.sRGB ),

		}*/








		/*public enum GfxCompareMode
		{
			Never = WebGLRenderingContext.NEVER,
			Less = WebGLRenderingContext.LESS,
			Equal = WebGLRenderingContext.EQUAL,
			LessEqual = WebGLRenderingContext.LEQUAL,
			Greater = WebGLRenderingContext.GREATER,
			NotEqual = WebGLRenderingContext.NOTEQUAL,
			GreaterEqual = WebGLRenderingContext.GEQUAL,
			Always = WebGLRenderingContext.ALWAYS,
		}

		public enum GfxFrontFaceMode
		{
			CCW = WebGLRenderingContext.CCW,
			CW = WebGLRenderingContext.CW,
		}

		public enum GfxCullMode
		{
			None = 0,
			Front = WebGLRenderingContext.FRONT,
			Back = WebGLRenderingContext.BACK,
			FrontAndBack = WebGLRenderingContext.FRONT_AND_BACK,
		}

		public enum GfxBlendFactor
		{
			Zero = WebGLRenderingContext.ZERO,
			One = WebGLRenderingContext.ONE,
			Src = WebGLRenderingContext.SRC_COLOR,
			OneMinusSrc = WebGLRenderingContext.ONE_MINUS_SRC_COLOR,
			Dst = WebGLRenderingContext.DST_COLOR,
			OneMinusDst = WebGLRenderingContext.ONE_MINUS_DST_COLOR,
			SrcAlpha = WebGLRenderingContext.SRC_ALPHA,
			OneMinusSrcAlpha = WebGLRenderingContext.ONE_MINUS_SRC_ALPHA,
			DstAlpha = WebGLRenderingContext.DST_ALPHA,
			OneMinusDstAlpha = WebGLRenderingContext.ONE_MINUS_DST_ALPHA,
		}

		public enum GfxBlendMode
		{
			Add = WebGLRenderingContext.FUNC_ADD,
			Subtract = WebGLRenderingContext.FUNC_SUBTRACT,
			ReverseSubtract = WebGLRenderingContext.FUNC_REVERSE_SUBTRACT,
		}*/

		public enum GfxWrapMode { Clamp, Repeat, Mirror }
		public enum GfxTexFilterMode { Point, Bilinear }
		// TODO(jstpierre): remove NoMip
		public enum GfxMipFilterMode { NoMip, Nearest, Linear }
		public enum GfxPrimitiveTopology { Triangles }

		[Flags]
		public enum GfxBufferUsage
		{
			Index = 0b00001,
			Vertex = 0b00010,
			Uniform = 0b00100,
			Storage = 0b01000,
			CopySrc = 0b10000,
			// All buffers are implicitly CopyDst so they can be filled by the CPU... maybe they shouldn't be...
		}

		[Flags]
		public enum GfxBufferFrequencyHint
		{
			Static = 0x01,
			Dynamic = 0x02,
		}

		[Flags]
		public enum GfxVertexBufferFrequency
		{
			PerVertex = 0x01,
			PerInstance = 0x02,
		}

		public enum GfxTextureDimension
		{
			n2D, n2DArray, n3D, Cube,
		}

		[Flags]
		public enum GfxTextureUsage
		{
			Sampled = 0x01,
			RenderTarget = 0x02,
		}

		[Flags]
		public enum GfxChannelWriteMask
		{
			None = 0x00,
			Red = 0x01,
			Green = 0x02,
			Blue = 0x04,
			Alpha = 0x08,

			RGB = 0x07,
			AllChannels = 0x0F,
		}


		public class GfxTextureDescriptor
		{
			public GfxTextureDimension Dimension { get; set; }
			//public GfxFormat PixelFormat { get; set; }
			public int Width { get; set; }
			public int Height { get; set; }
			public int Depth { get; set; }
			public int NumLevels { get; set; }
			public GfxTextureUsage Usage { get; set; }
		}

	}

















	public interface TextureOverride
	{
		Texture gfxTexture { get; set; } //GfxTexture
		//GfxSampler gfxSampler { get; set; }
		int width { get; set; }
		int height { get; set; }
		bool flipY { get; set; }
		string lateBinding { get; set; }
	}

	public class TextureMapping
	{
		public Texture GfxTexture { get; set; } = null; //GfxTexture
		//public GfxSampler GfxSampler { get; set; } = null;
		public string LateBinding { get; set; } = null;
		public int Width { get; set; } = 0;
		public int Height { get; set; } = 0;
		public float LodBias { get; set; } = 0;
		public bool FlipY { get; set; } = false;

		public void Reset()
		{
			GfxTexture = null;
			//GfxSampler = null;
			LateBinding = null;
			Width = 0;
			Height = 0;
			LodBias = 0;
			FlipY = false;
		}

		public bool FillFromTextureOverride( TextureOverride textureOverride )
		{
			GfxTexture = textureOverride.gfxTexture;
			/*if ( textureOverride.GfxSampler != null )
			{
				GfxSampler = textureOverride.GfxSampler;
			}*/
			Width = textureOverride.width;
			Height = textureOverride.height;
			FlipY = textureOverride.flipY;
			if ( textureOverride.lateBinding != null )
			{
				LateBinding = textureOverride.lateBinding;
			}
			return true;
		}

		public void Copy( TextureMapping other )
		{
			GfxTexture = other.GfxTexture;
			//GfxSampler = other.GfxSampler;
			LateBinding = other.LateBinding;
			Width = other.Width;
			Height = other.Height;
			LodBias = other.LodBias;
			FlipY = other.FlipY;
		}
	}


}
