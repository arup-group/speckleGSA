using SpeckleStructuresClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA
{
    public static class Indexer
    {
        private static Dictionary<string, int> indexMap = new Dictionary<string, int>();
        private static Dictionary<string, int> counter = new Dictionary<string, int>();
        private static Dictionary<string, List<int>> indexUsed = new Dictionary<string, List<int>>();
        private static Dictionary<string, List<int>> baseLine = new Dictionary<string, List<int>>();

        public static void Clear()
        {
            indexMap.Clear();
            counter.Clear();
            indexUsed.Clear();
            baseLine.Clear();
        }

        #region Indexer
        private static int NextIndex(string keywordGSA)
        {
            if (!counter.ContainsKey(keywordGSA))
                counter[keywordGSA] = 1;

            if (indexUsed.ContainsKey(keywordGSA))
                while (indexUsed[keywordGSA].Contains(counter[keywordGSA]))
                    counter[keywordGSA]++;

            return counter[keywordGSA]++;
        }

        public static int ResolveIndex(Type type)
        {
            return ResolveIndex(type.GetGSAKeyword(), string.Empty, type.Name);
        }

        public static int ResolveIndex(Type type, IStructural obj)
        {
            return ResolveIndex(type.GetGSAKeyword(), obj.StructuralID, type.Name);
        }

        public static int ResolveIndex(Type type, string structuralID)
        {
            return ResolveIndex(type.GetGSAKeyword(), structuralID, type.Name);
        }

        public static int ResolveIndex(string keywordGSA, string type = "")
        {
            return ResolveIndex(keywordGSA, string.Empty, type);
        }

        public static int ResolveIndex(string keywordGSA, IStructural obj, string type = "")
        {
            return ResolveIndex(keywordGSA, obj.StructuralID, type);
        }

        public static int ResolveIndex(string keywordGSA, string structuralID, string type = "")
        {
            // If no ID set, return next one but do not store.
            if (structuralID == null || structuralID == string.Empty)
                return NextIndex(keywordGSA);

            string key = keywordGSA + ":" + type + ":" + structuralID;

            if (!indexMap.ContainsKey(key))
                indexMap[key] = NextIndex(keywordGSA);

            return indexMap[key];
        }

        public static List<int> ResolveIndices(Type type, List<IStructural> objects)
        {
            return objects.Select(o => ResolveIndex(type, o)).ToList();
        }

        public static List<int> ResolveIndices(Type type, List<string> structuralID)
        {
            return structuralID.Select(s => ResolveIndex(type, s)).ToList();
        }

        public static List<int> ResolveIndices(string keywordGSA, List<IStructural> objects, string type = "")
        {
            return objects.Select(o => ResolveIndex(keywordGSA, o, type)).ToList();
        }

        public static List<int> ResolveIndices(string keywordGSA, List<string> structuralID, string type = "")
        {
            return structuralID.Select(s => ResolveIndex(keywordGSA, s, type)).ToList();
        }

        public static void ReserveIndices(string keywordGSA, List<int> refs)
        {
            if (!indexUsed.ContainsKey(keywordGSA))
                indexUsed[keywordGSA] = refs;
            else
                indexUsed[keywordGSA].AddRange(refs);

            indexUsed[keywordGSA] = indexUsed[keywordGSA].Distinct().ToList();
        }
        #endregion

        #region Base Line
        public static void SetBaseline()
        {
            baseLine.Clear();
            foreach (KeyValuePair<string, List<int>> kvp in indexUsed)
                baseLine[kvp.Key] = new List<int>(kvp.Value);
        }

        public static void ResetToBaseline()
        {
            indexUsed.Clear();
            foreach (KeyValuePair<string, List<int>> kvp in baseLine)
                indexUsed[kvp.Key] = new List<int>(kvp.Value);
        }

        public static bool InBaseline(string keywordGSA, int index)
        {
            if (baseLine.ContainsKey(keywordGSA))
                if (baseLine[keywordGSA].Contains(index))
                    return true;

            return false;
        }
        #endregion
    }
}
