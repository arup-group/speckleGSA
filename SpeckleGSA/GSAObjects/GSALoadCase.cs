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

        #region Contructors and Converters
        public GSALoadCase()
        {
            GWACommand = "";
            SubGWACommand = new List<string>();
        }

        public GSALoadCase(StructuralLoadCase baseClass)
        {
            GWACommand = "";
            SubGWACommand = new List<string>();

            foreach (FieldInfo f in baseClass.GetType().GetFields())
                f.SetValue(this, f.GetValue(baseClass));

            foreach (PropertyInfo p in baseClass.GetType().GetProperties())
                p.SetValue(this, p.GetValue(baseClass));
        }
        #endregion

        #region GSA Functions
        public static bool GetObjects(Dictionary<Type, List<object>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType))
                dict[MethodBase.GetCurrentMethod().DeclaringType] = new List<object>();

            List<object> loadCases = new List<object>();

            string[] lines = GSA.GetGWAGetCommands("GET_ALL,LOAD_TITLE");
            string[] deletedLines = GSA.GetDeletedGWAGetCommands("GET_ALL,LOAD_TITLE");

            // Remove deleted lines
            dict[typeof(GSALoadCase)].RemoveAll(l => deletedLines.Contains(((IGSAObject)l).GWACommand));
            foreach (KeyValuePair<Type, List<object>> kvp in dict)
                kvp.Value.RemoveAll(l => ((IGSAObject)l).SubGWACommand.Any(x => deletedLines.Contains(x)));

            // Filter only new lines
            string[] prevLines = dict[typeof(GSALoadCase)].Select(l => ((GSALoadCase)l).GWACommand).ToArray();
            string[] newLines = lines.Where(l => !prevLines.Contains(l)).ToArray();
            
            foreach (string p in newLines)
            {
                GSALoadCase lc = new GSALoadCase();
                lc.ParseGWACommand(p, dict);

                loadCases.Add(lc);
            }

            dict[typeof(GSALoadCase)].AddRange(loadCases);

            if (loadCases.Count() > 0 || deletedLines.Length > 0) return true;

            return false;
        }

        public static void WriteObjects(Dictionary<Type, List<StructuralObject>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType)) return;

            List<StructuralObject> loadCases = dict[typeof(GSALoadCase)];

            foreach (StructuralObject lc in loadCases)
                GSA.RunGWACommand(((GSALoadCase)lc).GetGWACommand());
        }

        public void ParseGWACommand(string command, Dictionary<Type, List<object>> dict = null)
        {
            GWACommand = command;

            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier

            Reference = Convert.ToInt32(pieces[counter++]);
            Name = pieces[counter++];

            string type = pieces[counter++];
            switch(type)
            {
                case "DEAD":
                    Type = StructuralLoadCaseType.DEAD;
                    break;
                case "LC_VAR_IMP":
                    Type = StructuralLoadCaseType.LIVE;
                    break;
                case "WIND":
                    Type = StructuralLoadCaseType.WIND;
                    break;
                case "SNOW":
                    Type = StructuralLoadCaseType.SNOW;
                    break;
                case "SEISMIC":
                    Type = StructuralLoadCaseType.EARTHQUAKE;
                    break;
                case "LC_PERM_SOIL":
                    Type = StructuralLoadCaseType.SOIL;
                    break;
                default:
                    Type = StructuralLoadCaseType.GENERIC;
                    break;
            }

            // Rest is unimportant

            return;
        }

        public string GetGWACommand(Dictionary<Type, List<StructuralObject>> dict = null)
        {
            List<string> ls = new List<string>();

            ls.Add("SET");
            ls.Add((string)this.GetAttribute("GSAKeyword"));
            ls.Add(Reference.ToString());
            ls.Add(Name); // Name
            if (Type == StructuralLoadCaseType.DEAD)
                ls.Add("DEAD");
            else if (Type == StructuralLoadCaseType.LIVE)
                ls.Add("LC_VAR_IMP");
            else if (Type == StructuralLoadCaseType.WIND)
                ls.Add("WIND");
            else if (Type == StructuralLoadCaseType.SNOW)
                ls.Add("SNOW");
            else if (Type == StructuralLoadCaseType.EARTHQUAKE)
                ls.Add("SEISMIC");
            else if (Type == StructuralLoadCaseType.SOIL)
                ls.Add("LC_PERM_SOIL");
            else
                ls.Add("UNDEF");
            ls.Add("1"); // Source
            ls.Add("~"); // Category
            ls.Add("NONE"); // Direction
            ls.Add("INC_BOTH"); // Include

            return string.Join(",", ls);
        }
        #endregion
    }
}
