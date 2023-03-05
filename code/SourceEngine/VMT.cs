// sbox.Community © 2023-2024

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static MapParser.SourceEngine.VmtParser;

namespace MapParser.SourceEngine
{





	public static class VmtParser
	{

		private static readonly Regex unquoteRegex = new Regex( @"[0-9a-zA-Z$%<>=/\\_]" );
		private static readonly Regex numRegex1 = new Regex( @"[0-9.]" );
		private static readonly Regex numRegex2 = new Regex( @"[-0-9.]" );
		private static readonly Regex numRegex3 = new Regex( @"[a-zA-Z]" );
		private static readonly Regex numRegex4 = new Regex( @"[a-zA-Z$%<>=/\\_]" );

		public delegate string VKFPairUnit(); //T VKFPairUnit<T>();
											  //public delegate Dictionary<string, string> VKFParamMap();//<VKFPairType>

		public class VKFParamMap
		{
			public Dictionary<string, string> val;
		}

		public class VKFPair//<T>
		{
			public string Key { get; set; }
			public object Value { get; set; } //T

		public VKFPair( string item1, object item2 ) //T
		{
			Key = item1;
			Value = item2;
		}
	}

	public struct VMT
	{
		public string _Root { get; set; }
		public string _Patch { get; set; }
		public string _Filename { get; set; }

			// patch
		public string include { get; set; }
		public object replace { get; set; }
		public object insert { get; set; }

			// proxies
		public object proxies { get; set; }

			// generic
		public Dictionary<string, object> Items { get; set; } //public Dictionary<string, VKFPairUnit<VKFPair<string>>> Items { get; set; }
		}




		public  class ValveKeyValueParser
		{
			private int pos = 0;
			private readonly string S;

			public ValveKeyValueParser( string s )
			{
				S = s;
			}

			public bool HasToken()
			{
				return (pos < S.Length);
			}

			public void SkipWhite()
			{
				while ( HasToken() )
				{
					var tok = S[pos];
					if ( char.IsWhiteSpace( tok ) || tok == '`' || tok == '\0' )
						pos++;
					else
						return;
				}
			}

			private bool SkipComment2()
			{
				if ( Chew() == '/' )
				{
					var ch = Chew( true );
					if ( ch == '/' )
					{
						while ( Chew( true ) != '\n' )
							;
						return true;
					}
					else if ( ch == '*' )
					{
						pos = S.IndexOf( "*/", pos ) + 2;
						return true;
					}
					else
					{
						throw new Exception( "whoops" );
					}
				}
				else
				{
					Spit();
					return false;
				}
			}

			private char Chew( bool flag = false )
			{
				if ( !flag )
					SkipWhite();

				return S[pos++];
			}

			private void Spit()
			{
				pos--;
			}

			//private VKFPair<T>[] Obj<T>() where T : VKFPair<T>
			private VKFPair[] Obj()
			{
				var val = new List<VKFPair>();
				// already consumed "{"

				while ( this.HasToken() )
				{
					this.SkipComment();
					var tok = this.Chew();
					if ( tok == '}' || string.IsNullOrEmpty(tok.ToString()) )//tok == ""
					{
						return val.ToArray();
					}
					else
					{
						this.Spit();
					}

					val.Add( (VKFPair) this.Pair() );
				}

				return val.ToArray();
			}



			private string Quote( string delim )
			{
				var val = "";
				// already consumed delim
				while ( this.HasToken()  )
				{
					var tok = this.Chew( true );
					//Log.Info( this.HasToken() + " " +  tok + " "+ delim + " "  + (tok.ToString() == delim) + " " + val );
					if ( tok.ToString() == delim )
						return val;
					else
						val += tok;
				}

				throw new Exception( "whoops" );
			}

			private string Run( Regex t, string start )
			{
				var val = start;
				while ( this.HasToken() )
				{
					var tok = this.Chew( true );
					if ( t.IsMatch( tok.ToString() ) )
					{
						val += tok;
					}
					else
					{
						this.Spit();
						break;
					}
				}

				return val;
			}

			private string Num( string start )
			{
				var num = this.Run( numRegex1, start );
				// numbers can have garbage at the end of them. this is ugly...
				// shoutouts to materials/models/props_lab/printout_sheet.vmt which has a random letter "y" after a number
				this.Run( numRegex3, "" );
				return num;
			}

			private string Unquote( string start )
			{
				return this.Run( unquoteRegex, start );
			}

			//public object Unit<T>() where T : VKFPair<T>
			public object Unit()
			{
				SkipComment();

				var tok = this.Chew();
				if ( tok == '{' )
					return this.Obj(); //return this.Obj<T>();
				else if ( tok == '\"' )
					return this.Quote( tok.ToString() );
				else if ( numRegex4.IsMatch( tok.ToString() ) )
					return this.Unquote( tok.ToString() );
				else if ( numRegex2.IsMatch( tok.ToString() ) )
					return this.Num( tok.ToString() );
				else
					throw new Exception( "whoops" );
			}

			/*public VKFPair<T> Pair<T>() where T : VKFPair<T>
			{
				var kk = this.Unit<T>();
				if ( !(kk is string) )
					throw new Exception( "whoops" );

				Log.Info( typeof( T ) );
				var k = (kk as string).ToLower();
				var v = (T)this.Unit<T>();
				return new VKFPair<T>( k, v );
			}*/

			public object Pair()
			{
				var kk = this.Unit();
				if ( kk is not string )
					throw new Exception( "whoops" );

				//Log.Info( typeof( T ) );
				var k = (kk as string).ToLower();
				var v = this.Unit();
				if ( v is string )
					return new VKFPair( k, (string)v );
				else
					return new VKFPair( k, (VKFPair[]) v );
					//foreach(var vvv in (VKFPair[])vv )
						//v+= vvv.Value;
				
			}


			private void SkipComment()
			{
				while ( SkipComment2() ) { }
			}





		}



































		public static Dictionary<string, object> PairsToObject( VKFPair[] pairs, bool recurse = false )
		{
			var o = new Dictionary<string, object>();
			ConvertPairsToObj( ref o, ref pairs, recurse );
			return o;
		}

		private static void ConvertPairsToObj( ref Dictionary<string, object> o, ref VKFPair[] pairs, bool recurse, bool supportsMultiple = true )
		{
			foreach ( var pair in pairs )
			{
				var k = pair.Key;
				var v = pair.Value;
				var vv = (recurse && v is VKFPair[]) ? PairsToObject( (VKFPair[] ) v, recurse ) : v;

				if ( o.ContainsKey( k ) )
				{
					if ( supportsMultiple )
					{
						if ( !(o[k] is List<object>) )
							o[k] = new List<object> { o[k] };
						((List<object>)o[k]).Add( vv );
					}
					else
					{
						// Take the first one.
						continue;
					}
				}
				else
				{
					o[k] = vv;
				}
			}
		}

		public static void Patch( ref Dictionary<string, object> dst, VKFPair[] srcpair, bool replace )
		{
			if ( srcpair == null )
				return;

			foreach ( var pair in srcpair )
			{
				var key = pair.Key;
				var value = pair.Value;
				if ( dst.ContainsKey( key ) || !replace )
				{
					if ( value is VKFPair[] )
					{
						var _dst = (Dictionary<string, object>)dst[key];
						Patch( ref _dst, (VKFPair[])value, replace );
					}
					else
						dst[key] = value;
				}
			}
		}

		public static KeyValuePair<string, object>? StealPair( ref Dictionary<string, object> pairs, string name )
		{
			var pair = pairs.Where( p => p.Key == name ).FirstOrDefault();
			if ( pair.Value == null )
				return null;

			pairs.Remove( pair.Key );
			return pair;
		}

		private static void arrayRemove<T>( List<T> list, T item )
		{
			list.Remove( item );
		}

		static async Task<VMT> ParsePath( Main.SourceFileSystem filesystem, string path )
		{
			path = filesystem.ResolvePath( path, ".vmt" );
			if ( !filesystem.HasEntry( path ) )
			{
					// Amazingly, the material could be in materials/materials/, like is
					//    materials/materials/nature/2/blenddirttojunglegrass002b.vmt
					// from cp_mossrock
					path = $"materials/{path}";
			}

			if ( !filesystem.HasEntry( path ) )
			{
				path = "materials/editor/obsolete.vmt";
			}

			byte[] buffer = (await filesystem.FetchFileData( path )).ToArray(); //AssertExists()
			string str = Encoding.UTF8.GetString( buffer );

			var parser = new ValveKeyValueParser( str );

				var pair = (VKFPair) parser.Pair();


				// The data that comes out of the parser is a nested series of VKFPairs.
				var rootK = pair.Key;
				Dictionary<string, object> rootObj = new();// (VKFPair[]) pair.Value;
				foreach(var val in (VKFPair[])pair.Value)
					rootObj.Add(val.Key, val.Value);

				Log.Info( rootK );
				Log.Info( rootObj );

				// Start building our VMT.
				var vmt = new VMT();
			vmt._Root = rootK;
			vmt._Filename = path;


				// First, handle proxies if they exist as special, since there can be multiple keys with the same name.
				var proxiesPairs = StealPair(ref rootObj, "proxies" );
			Log.Info( proxiesPairs is null );
				if ( proxiesPairs != null )
			{
					Log.Info( "ASD2" );

					var proxies = ((VKFPair[])proxiesPairs.Value.Value).Select( pair =>
					{
						var name = pair.Key;
						var value = PairsToObject( new VKFPair[] { pair }, true );
						return new KeyValuePair<string, object>( name, value );
					} ).ToList();
					vmt.proxies = proxies;

					Log.Info( rootK + " " + proxies );
					foreach ( var asd in proxies )
					{ 
						//Log.Info( asd.Key + " " + (asd.Value == null) + " "+  ((asd.Value as string is string tew ) && tew != null ? tew : "no") + " " + ((asd.Value as VKFPair is VKFPair sdfg) && sdfg != null ? sdfg.Value : "no") + " " + ((asd.Value as VKFPair[] is VKFPair[] sdfg2) && sdfg2 != null ? sdfg2.Count() : "no") + " " + ((asd.Value as Dictionary<string, object> is Dictionary<string, object> sdfg3) && sdfg3 != null ? sdfg3.Count() : "no") + " " + ((asd.Value as List<object> is List<object> sdfg4) && sdfg4 != null ? sdfg4.Count() : "no") );
						
						
						// CALISIYOR
						if( (asd.Value as Dictionary<string, object> is Dictionary<string, object> g) )
						{
							foreach ( var d in g.Values )
								foreach ( var sd in ((Dictionary<string, object>)d) )
									Log.Info( asd.Key + " " + sd );
						}


					}

				}

				// Pull out replace / insert patching.
				var replace = StealPair(ref rootObj, "replace" );
			var insert = StealPair( ref rootObj, "insert" );


				// Now go through and convert all the other pairs. Note that if we encounter duplicates, we drop, rather
				// than convert to a list.
				var recurse = true;
				var supportsMultiple = false;
				Dictionary<string, object> Items = new();
				var rootObjAsArray = (VKFPair[])pair.Value;
				ConvertPairsToObj( ref Items, ref rootObjAsArray, recurse, supportsMultiple );
				vmt.Items = Items;

			vmt.replace = replace?.Value;
			vmt.insert = insert?.Value;

			return vmt;
		}

		public static async Task<VMT> ParseVMT( Main.SourceFileSystem filesystem, string path, int depth = 0 )
	{
		

		var vmt = await ParsePath( filesystem, path );
		if ( vmt._Root == "patch" )
		{
			var baseVmt = await ParseVMT( filesystem, (string)vmt.Items["include"], depth + 1 ); //vmt["include"] burayi duzenledim enson
			var baseVmtItems = baseVmt.Items;
			Patch( ref baseVmtItems, (VKFPair[])vmt.replace, true );
			Patch( ref baseVmtItems, (VKFPair[])vmt.insert, false );
			baseVmt._Patch = baseVmt._Filename;
			baseVmt._Filename = vmt._Filename;
			baseVmt.Items = baseVmtItems;
			return baseVmt;
		}
		else
		{
			return vmt;
		}
			
		}
	

		public static float[] VmtParseVector( string s )
	{
		// There are two syntaxes for vectors: [1.0 1.0 1.0] and {255 255 255}. These should both represent white.
		// In practice, combine_tower01b.vmt has "[.25 .25 .25}", so the starting delimeter is all that matters.
		// And factory_metal_floor001a.vmt has ".125 .125 .125" so I guess the square brackets are just decoration??

		float scale = s.StartsWith( "{" ) ? 1 / 255.0f : 1.0f;
		s = Regex.Replace( s, @"[\[\]{}]", "" ).Trim(); // Trim off all the brackets.
		return s.Split( new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries ).Select( item => float.Parse( item ) * scale ).ToArray();
	}

	public static void VmtParseColor( Color dst, string s )
	{
		float[] v = VmtParseVector( s );
		dst.r = v[0] / 255.0f;
		dst.g = v[1] / 255.0f;
		dst.b = v[2] / 255.0f;
		dst.a = (v.Length >= 4) ? v[3] / 255.0f : 1.0f;
	}

	public static float VmtParseNumber( string s, float fallback )
	{
		if ( !String.IsNullOrEmpty( s ) )
		{
			float[] v = VmtParseVector( s );
			if ( !float.IsNaN( v[0] ) )
				return v[0];
		}
		return fallback;
	}
	}
}
