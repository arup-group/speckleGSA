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
    //Dictionary of composite keys (keyword + type + application ID) and the GSA record ID
		private static Dictionary<string, int> indexMap = new Dictionary<string, int>();

    //Highest record ID used for each GSA keyword
		private static Dictionary<string, int> counter = new Dictionary<string, int>();

    //Memory of all record IDs used, whether by external actors or by Speckle so ar
		private static Dictionary<string, List<int>> indexUsed = new Dictionary<string, List<int>>();

    //Memory of only the record IDs set by actions outside SpeckleGSA - the world as it was before reception from Speckle
    private static Dictionary<string, List<int>> baseLine = new Dictionary<string, List<int>>();

		public bool InBaseline(string keywordGSA, int index)
		{
			if (baseLine.ContainsKey(keywordGSA))
				if (baseLine[keywordGSA].Contains(index))
					return true;

			return false;
		}

		public int? LookupIndex(string keywordGSA, string type, string applicationId)
		{
			if (applicationId == null || applicationId == string.Empty)
				return null;

			string key = keywordGSA + ":" + type + ":" + applicationId;

			if (!indexMap.ContainsKey(key))
				return null;

			return indexMap[key];
		}

		public List<int?> LookupIndices(string keyword, string type, IEnumerable<string> applicationIds)
		{
			return applicationIds.Select(s => LookupIndex(keyword, type, s)).ToList();
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

    public void Reset()
    {
      indexUsed.Clear();
      indexMap.Clear();
      counter.Clear();
      baseLine.Clear();
    }

    public int ResolveIndex(string keyword, string type, string applicationId = null)
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

		public List<int> ResolveIndices(string keyword, string type, IEnumerable<string> applicationIds = null)
		{
			return applicationIds.Select(s => ResolveIndex(keyword, type, s)).ToList();
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
