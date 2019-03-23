using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SpeckleStructuresClasses;

namespace SpeckleGSA
{
    [GSAObject("LOAD_TITLE.2", "loads", true, true, new Type[] { }, new Type[] { })]
    public class GSALoadCase : StructuralLoadCase, IGSAObject
    {
        public string GWACommand { get; set; }
        public List<string> SubGWACommand { get; set; }
        
        #region Sending Functions
        public static bool GetObjects(Dictionary<Type, List<IGSAObject>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType))
                dict[MethodBase.GetCurrentMethod().DeclaringType] = new List<IGSAObject>();

            List<GSALoadCase> loadCases = new List<GSALoadCase>();

            string[] lines = GSA.GetGWAGetCommands("GET_ALL,LOAD_TITLE");
            string[] deletedLines = GSA.GetDeletedGWAGetCommands("GET_ALL,LOAD_TITLE");

            // Remove deleted lines
            dict[typeof(GSALoadCase)].RemoveAll(l => deletedLines.Contains(l.GWACommand));
            foreach (KeyValuePair<Type, List<IGSAObject>> kvp in dict)
                kvp.Value.RemoveAll(l => l.SubGWACommand.Any(x => deletedLines.Contains(x)));

            // Filter only new lines
            string[] prevLines = dict[typeof(GSALoadCase)].Select(l => l.GWACommand).ToArray();
            string[] newLines = lines.Where(l => !prevLines.Contains(l)).ToArray();

            foreach (string p in newLines)
            {
                GSALoadCase loadCase = ParseGWACommand(p);
                loadCases.Add(loadCase);
            }

            dict[typeof(GSALoadCase)].AddRange(loadCases);

            if (loadCases.Count() > 0 || deletedLines.Length > 0) return true;

            return false;
        }

        public static GSALoadCase ParseGWACommand(string command)
        {
            GSALoadCase ret = new StructuralLoadCase() as GSALoadCase;

            ret.GWACommand = command;
            
            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier

            ret.StructuralID = pieces[counter++];
            ret.Name = pieces[counter++];

            string type = pieces[counter++];
            switch (type)
            {
                case "DEAD":
                    ret.CaseType = StructuralLoadCaseType.Dead;
                    break;
                case "LC_VAR_IMP":
                    ret.CaseType = StructuralLoadCaseType.Live;
                    break;
                case "WIND":
                    ret.CaseType = StructuralLoadCaseType.Wind;
                    break;
                case "SNOW":
                    ret.CaseType = StructuralLoadCaseType.Snow;
                    break;
                case "SEISMIC":
                    ret.CaseType = StructuralLoadCaseType.Earthquake;
                    break;
                case "LC_PERM_SOIL":
                    ret.CaseType = StructuralLoadCaseType.Soil;
                    break;
                default:
                    ret.CaseType = StructuralLoadCaseType.Generic;
                    break;
            }

            // Rest is unimportant

            return ret;
        }
        #endregion

        #region Receiving Functions
        public static void SetObjects(Dictionary<Type, List<IStructural>> dict)
        {
            if (!dict.ContainsKey(typeof(StructuralLoadCase))) return;

            foreach (IStructural obj in dict[typeof(StructuralLoadCase)])
            {
                Set(obj as StructuralLoadCase);
            }
        }

        public static void Set(StructuralLoadCase loadCase)
        {
            if (loadCase == null)
                return;

            string keyword = MethodBase.GetCurrentMethod().DeclaringType.GetGSAKeyword();

            int index = Indexer.ResolveIndex(keyword, loadCase);

            List<string> ls = new List<string>();

            ls.Add("SET");
            ls.Add(keyword);
            ls.Add(index.ToString());
            ls.Add(loadCase.Name); // Name
            switch(loadCase.CaseType)
            {
                case StructuralLoadCaseType.Dead:
                    ls.Add("DEAD");
                    break;
                case StructuralLoadCaseType.Live:
                    ls.Add("LC_VAR_IMP");
                    break;
                case StructuralLoadCaseType.Wind:
                    ls.Add("WIND");
                    break;
                case StructuralLoadCaseType.Snow:
                    ls.Add("SNOW");
                    break;
                case StructuralLoadCaseType.Earthquake:
                    ls.Add("SEISMIC");
                    break;
                case StructuralLoadCaseType.Soil:
                    ls.Add("LC_PERM_SOIL");
                    break;
                default:
                    ls.Add("UNDEF");
                    break;
            }
            ls.Add("1"); // Source
            ls.Add("~"); // Category
            ls.Add("NONE"); // Direction
            ls.Add("INC_BOTH"); // Include
            
            GSA.RunGWACommand(string.Join(",", ls));
        }
        #endregion
    }
}
