using System;
using System.Collections.Generic;

namespace SpeckleGSAInterfaces
{
	public interface IGSAIndexer
	{
		int ResolveIndex(string keyword, string applicationId = "", string type = "");
		List<int> ResolveIndices(string keyword, IEnumerable<string> applicationIds);
		int? LookupIndex(string keyword, string applicationId, string type = "");
		List<int?> LookupIndices(string keyword, IEnumerable<string> applicationIds);

		void ReserveIndices(string keyword, IEnumerable<int> indices);
		void ReserveIndicesAndMap(string keyword, string typeName, IList<int> indices, IList<string> applicationIds);

		void SetBaseline();
		void ResetToBaseline();
		bool InBaseline(string keywordGSA, int index);
	}
}
