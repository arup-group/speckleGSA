using System;
using System.Collections.Generic;

namespace SpeckleGSAInterfaces
{
	public interface IGSASettings
	{
		GSATargetLayer TargetLayer { get; set; }
		string Units { get; set; }
		double CoincidentNodeAllowance { get; set; }

		Dictionary<string, Tuple<int, int, List<string>>> NodalResults { get; set; }
		Dictionary<string, Tuple<int, int, List<string>>> Element1DResults { get; set; }
		Dictionary<string, Tuple<int, int, List<string>>> Element2DResults { get; set; }
		Dictionary<string, Tuple<string, int, int, List<string>>> MiscResults { get; set; } 

		List<string> ResultCases { get; set; }
		bool ResultInLocalAxis { get; set; }
		int Result1DNumPosition { get; set; }
		bool EmbedResults { get; set; }
	}
}
