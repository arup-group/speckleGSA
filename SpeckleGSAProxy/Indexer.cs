using SpeckleGSAInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSAProxy
{
	public class Indexer : IGSAIndexer
	{
		private static Dictionary<string, int> indexMap = new Dictionary<string, int>();
		private static Dictionary<string, int> counter = new Dictionary<string, int>();
		private static Dictionary<string, List<int>> indexUsed = new Dictionary<string, List<int>>();

		private static Dictionary<string, List<int>> baseLine = new Dictionary<string, List<int>>();
		public bool InBaseline(string keywordGSA, int index)
		{
			if (baseLine.ContainsKey(keywordGSA))
				if (baseLine[keywordGSA].Contains(index))
					return true;

			return false;
		}

		public int? LookupIndex(string keywordGSA, string applicationId, string type = "")
		{
			if (applicationId == null || applicationId == string.Empty)
				return null;

			string key = keywordGSA + ":" + type + ":" + applicationId;

			if (!indexMap.ContainsKey(key))
				return null;

			return indexMap[key];
		}

		public List<int?> LookupIndices(string keyword, IEnumerable<string> applicationIds)
		{
			return applicationIds.Select(s => LookupIndex(keyword, s)).ToList();
		}

		public void ReserveIndices(string keyword, IEnumerable<int> indices)
		{
			if (!indexUsed.ContainsKey(keyword))
				indexUsed[keyword] = indices.ToList();
			else
				indexUsed[keyword].AddRange(indices);

			indexUsed[keyword] = indexUsed[keyword].Distinct().ToList();
		}

		public void ReserveIndicesAndMap(string keyword, string typeName, IList<int> indices, IList<string> applicationIds)
		{
			for (int i = 0; i < applicationIds.Count(); i++)
			{
				string key = keyword + ":" + typeName + ":" + applicationIds[i];
				indexMap[key] = indices[i];
			}

			ReserveIndices(keyword, indices);
		}

		public void ResetToBaseline()
		{
			indexUsed.Clear();
			indexMap.Clear();
			counter.Clear();
			foreach (KeyValuePair<string, List<int>> kvp in baseLine)
			{
				indexUsed[kvp.Key] = new List<int>(kvp.Value);
			}
		}
		public int ResolveIndex(string keyword, string applicationId, string type = "")
		{
			// If no ID set, return next one but do not store.
			if (applicationId == null || applicationId == string.Empty)
			{
				return NextIndex(keyword);
			}

			var key = keyword + ":" + type + ":" + applicationId;

			if (!indexMap.ContainsKey(key))
			{
				indexMap[key] = NextIndex(keyword);
			}

			return indexMap[key];
		}

		public List<int> ResolveIndices(string keyword, IEnumerable<string> applicationIds)
		{
			return applicationIds.Select(s => ResolveIndex(keyword, s)).ToList();
		}

		public void SetBaseline()
		{
			baseLine.Clear();
			foreach (KeyValuePair<string, List<int>> kvp in indexUsed)
			{
				baseLine[kvp.Key] = new List<int>(kvp.Value);
			}
		}

		private int NextIndex(string keywordGSA)
		{
			if (!counter.ContainsKey(keywordGSA))
				counter[keywordGSA] = 1;

			if (indexUsed.ContainsKey(keywordGSA))
				while (indexUsed[keywordGSA].Contains(counter[keywordGSA]))
					counter[keywordGSA]++;

			return counter[keywordGSA]++;
		}
	}
}
