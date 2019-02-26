using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SpeckleStructures;

namespace SpeckleGSA
{
    [GSAObject("LOAD_TITLE", "loads", true, true, new Type[] { }, new Type[] { })]
    public class GSALoadCase : StructuralLoadCase
    {
        #region Contructors and Converters
        public GSALoadCase() { }

        public GSALoadCase(StructuralLoadCase baseClass)
        {
            foreach (FieldInfo f in baseClass.GetType().GetFields())
                f.SetValue(this, f.GetValue(baseClass));

            foreach (PropertyInfo p in baseClass.GetType().GetProperties())
                p.SetValue(this, p.GetValue(baseClass));
        }
        #endregion

        #region GSA Functions
        public static void GetObjects(Dictionary<Type, List<StructuralObject>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType))
                dict[MethodBase.GetCurrentMethod().DeclaringType] = new List<StructuralObject>();

            List<StructuralObject> loadCases = new List<StructuralObject>();

            string res = (string)GSA.RunGWACommand("GET_ALL,LOAD_TITLE");

            if (res == "")
                return;

            string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            double counter = 1;
            foreach (string p in pieces)
            {
                GSALoadCase lc = new GSALoadCase();
                lc.ParseGWACommand(p, dict);

                loadCases.Add(lc);

                Status.ChangeStatus("Reading load cases", counter++ / pieces.Length * 100);
            }

            dict[typeof(GSALoadCase)] = loadCases;
        }

        public static void WriteObjects(Dictionary<Type, List<StructuralObject>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType)) return;

            List<StructuralObject> loadCases = dict[typeof(GSALoadCase)];

            foreach (StructuralObject lc in loadCases)
                GSA.RunGWACommand(((GSALoadCase)lc).GetGWACommand());
        }

        public void ParseGWACommand(string command, Dictionary<Type, List<StructuralObject>> dict = null)
        {
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
