using System.Collections.Generic;

namespace SpeckleGSAInterfaces
{
  public interface IGSASettings
	{
		string ObjectUrl(string id);  //The app owns the connection with the server.  This is used for logging in the kits
		GSATargetLayer TargetLayer { get; set; }
		string Units { get; set; }
		double CoincidentNodeAllowance { get; set; }

		Dictionary<string, IGSAResultParams> NodalResults { get; set; }
		Dictionary<string, IGSAResultParams> Element1DResults { get; set; }
		Dictionary<string, IGSAResultParams> Element2DResults { get; set; }
		Dictionary<string, IGSAResultParams> MiscResults { get; set; } 

		bool SendResults { get; set; }
		List<string> ResultCases { get; set; }
		bool ResultInLocalAxis { get; set; }
		int Result1DNumPosition { get; set; }
		bool EmbedResults { get; set; }
	}
}
