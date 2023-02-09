using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace MapParser.GoldSrc
{
	public static class EntityParser
	{
		[Serializable]
		public struct EntityData
		{
			public string classname { get; set; }
			public Dictionary<string, string> data { get; set; }
		}

		public static List<EntityData> parseEntities( byte[] data )
		{
			List<EntityData> entDataList = new();
			var stringData = Encoding.ASCII.GetString( data );

			//"\\{(?:[^{}]|(R))*\\}"
			var regex = new Regex( @"\{(?:[^{}]|(?<Depth>\{)|(?<-Depth>\}))*(?(Depth)(?!))\}" ); 

			var match = regex.Match( stringData );

			if ( match.Success )
			{
				while( match.Success )
				{
					var value = match.Value;
					var entData = new EntityData();
					Dictionary<string, string> entAnotherData = new();
					string classname = string.Empty;

					foreach ( var line in parseLine( value ) )
					{
						if ( line.Item1 == "classname" )
							classname = line.Item2;
						else if( !string.IsNullOrEmpty( line.Item1 ) )
						{
							// If is there any duplicated entity data. (there was)
							// Is the most recent data valid for engine? idk
							entAnotherData.Remove( line.Item1 );
							entAnotherData.Add( line.Item1, line.Item2 ); 
						}
					}

					if( !string.IsNullOrEmpty(classname))
					{
						entData.classname = classname;
						entData.data = entAnotherData;
						entDataList.Add( entData );
					}
					
					match = match.NextMatch();
				}
			}
			else
				Notify.Create( "Entity parsing has been failed.", Notify.NotifyType.Error );

			return entDataList;
		}

		public static IEnumerable<(string,string)> parseLine( string input )
		{
			MatchCollection matches = Regex.Matches( input, @"""(?<key>[^""]+)""\s*""(?<value>[^""]+)""" );

			foreach ( Match match in matches )
				if ( match.Success )
					yield return (match.Groups["key"].Value, match.Groups["value"].Value);
			yield return (string.Empty, string.Empty);

		}
	}
}
