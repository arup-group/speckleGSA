using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SpeckleStructuresClasses;

namespace SpeckleGSA
{
    [GSAObject("COMBINATION.1", new string[] { }, "loads", true, true, new Type[] { typeof(GSALoadCase), typeof(GSALoadTask) }, new Type[] { typeof(GSALoadCase), typeof(GSALoadTask) })]
    public class GSALoadCombo : StructuralLoadCombo, IGSAObject
    {
        public string GWACommand { get; set; } = "";
        public List<string> SubGWACommand { get; set; } = new List<string>();

        #region Sending Functions
        public static bool GetObjects(Dictionary<Type, List<IGSAObject>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType))
                dict[MethodBase.GetCurrentMethod().DeclaringType] = new List<IGSAObject>();

            List<GSALoadCombo> loadCombos = new List<GSALoadCombo>();

            string keyword = MethodBase.GetCurrentMethod().DeclaringType.GetGSAKeyword();
            string[] subKeywords = MethodBase.GetCurrentMethod().DeclaringType.GetSubGSAKeyword();

            string[] lines = GSA.GetGWARecords("GET_ALL," + keyword);
            List<string> deletedLines = GSA.GetDeletedGWARecords("GET_ALL," + keyword).ToList();
            foreach (string k in subKeywords)
                deletedLines.AddRange(GSA.GetDeletedGWARecords("GET_ALL," + k));

            // Remove deleted lines
            dict[typeof(GSALoadCombo)].RemoveAll(l => deletedLines.Contains(l.GWACommand));
            foreach (KeyValuePair<Type, List<IGSAObject>> kvp in dict)
                kvp.Value.RemoveAll(l => l.SubGWACommand.Any(x => deletedLines.Contains(x)));

            // Filter only new lines
            string[] prevLines = dict[typeof(GSALoadCombo)].Select(l => l.GWACommand).ToArray();
            string[] newLines = lines.Where(l => !prevLines.Contains(l)).ToArray();

            foreach (string p in newLines)
            {
                GSALoadCombo combo = ParseGWACommand(p);
                loadCombos.Add(combo);
            }

            dict[typeof(GSALoadCombo)].AddRange(loadCombos);

            if (loadCombos.Count() > 0 || deletedLines.Count() > 0) return true;

            return false;
        }

        public static GSALoadCombo ParseGWACommand(string command)
        {
            GSALoadCombo ret = new GSALoadCombo();

            ret.GWACommand = command;

            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier

            ret.StructuralId = pieces[counter++];
            ret.Name = pieces[counter++];
            
            // Parse type
            string description = pieces[counter++];
            if (description.Contains("+"))
                ret.ComboType = StructuralLoadComboType.LinearAdd;
            else if (description.Contains("or"))
                ret.ComboType = StructuralLoadComboType.Envelope;
            else
                ret.ComboType = StructuralLoadComboType.LinearAdd;

            ret.LoadTaskRefs = new List<string>();
            ret.LoadTaskFactors = new List<double>();
            ret.LoadComboRefs = new List<string>();
            ret.LoadComboFactors = new List<double>();

            // TODO: this only parses the super simple linear add descriptions
            try
            {
                List<Tuple<string, double>> desc = HelperFunctions.ParseLoadDescription(description);

                foreach(Tuple<string, double> t in desc)
                {
                    switch(t.Item1[0])
                    {
                        case 'A':
                            ret.LoadTaskRefs.Add(t.Item1.Substring(1));
                            ret.LoadTaskFactors.Add(t.Item2);
                            break;
                        case 'C':
                            ret.LoadComboRefs.Add(t.Item1.Substring(1));
                            ret.LoadComboFactors.Add(t.Item2);
                            break;
                    }
                }
            }
            catch
            {
                Status.AddError("Unable to parse description: " + description);
            }

            return ret;
        }
        #endregion

        #region Receiving Functions
        public static void SetObjects(Dictionary<Type, List<IStructural>> dict)
        {
            if (!dict.ContainsKey(typeof(StructuralLoadCombo))) return;

            foreach (IStructural obj in dict[typeof(StructuralLoadCombo)])
            {
                Set(obj as StructuralLoadCombo);
            }
        }

        public static void Set(StructuralLoadCombo loadCombo)
        {
            if (loadCombo == null)
                return;

            string keyword = MethodBase.GetCurrentMethod().DeclaringType.GetGSAKeyword();
            
            int index = Indexer.ResolveIndex(MethodBase.GetCurrentMethod().DeclaringType, loadCombo);

            List<string> ls = new List<string>();
            
            ls.Add("SET");
            ls.Add(keyword);
            ls.Add(index.ToString());
            ls.Add(loadCombo.Name == null || loadCombo.Name == "" ? " " : loadCombo.Name);

            List<string> subLs = new List<string>();
            if (loadCombo.LoadTaskRefs != null)
            {
                for (int i = 0; i < loadCombo.LoadTaskRefs.Count(); i++)
                {
                    int? loadTaskRef = Indexer.LookupIndex(typeof(GSALoadTask), loadCombo.LoadTaskRefs[i]);

                    if (loadTaskRef.HasValue)
                    {
                        if (loadCombo.LoadTaskFactors != null && loadCombo.LoadTaskFactors.Count() > i)
                            subLs.Add(loadCombo.LoadTaskFactors[i].ToString() + "A" + loadTaskRef.Value.ToString());
                        else
                            subLs.Add("A" + loadTaskRef.Value.ToString());
                    }
                }
            }

            if (loadCombo.LoadComboRefs != null)
            {
                for (int i = 0; i < loadCombo.LoadComboRefs.Count(); i++)
                {
                    int? loadComboRef = Indexer.LookupIndex(typeof(GSALoadTask), loadCombo.LoadComboRefs[i]);

                    if (loadComboRef.HasValue)
                    {
                        if (loadCombo.LoadComboFactors != null && loadCombo.LoadComboFactors.Count() > i)
                            subLs.Add(loadCombo.LoadComboFactors[i].ToString() + "C" + loadComboRef.Value.ToString());
                        else
                            subLs.Add("C" + loadComboRef.Value.ToString());
                    }
                }
            }

            switch (loadCombo.ComboType)
            {
                case StructuralLoadComboType.LinearAdd:
                    ls.Add(string.Join(" + ", subLs));
                    break;
                case StructuralLoadComboType.Envelope:
                    ls.Add(string.Join(" or ", subLs));
                    break;
                default:
                    ls.Add(string.Join(" + ", subLs));
                    break;
            }
            
            GSA.RunGWACommand(string.Join(",", ls));
        }
        #endregion
    }
}
