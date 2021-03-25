using System;
using System.Collections.Generic;

namespace SpeckleGSAInterfaces
{
	public interface IGSASettings
	{
		string ObjectUrl(string id);  //The app owns the connection with the server.  This is used for logging in the kits
		GSATargetLayer TargetLayer { get;  }
		string Units { get; }
		double CoincidentNodeAllowance { get; }

		Dictionary<string, Tuple<int, int, List<string>>> NodalResults { get; }
		Dictionary<string, Tuple<int, int, List<string>>> Element1DResults { get; }
		Dictionary<string, Tuple<int, int, List<string>>> Element2DResults { get; }
		Dictionary<string, Tuple<string, int, int, List<string>>> MiscResults { get; } 

		bool SendResults { get; }
		List<string> ResultCases { get; }
		bool ResultInLocalAxis { get; }
		int Result1DNumPosition { get; }
		bool EmbedResults { get; }
	}
}
