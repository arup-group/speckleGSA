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

		Dictionary<string, IGSAResultParams> NodalResults { get; }
		Dictionary<string, IGSAResultParams> Element1DResults { get; }
		Dictionary<string, IGSAResultParams> Element2DResults { get; }
		Dictionary<string, IGSAResultParams> MiscResults { get; } 

		bool SendResults { get; }
		List<string> ResultCases { get; }
		bool ResultInLocalAxis { get; }
		int Result1DNumPosition { get; }
		bool EmbedResults { get; }
	}
}
