using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Interop.Gsa_10_0;
using SpeckleStructuresClasses;

namespace SpeckleGSA
{
    [GSAObject("MAT_STEEL.3", new string[] { }, "properties", true, true, new Type[] { }, new Type[] { })]
    public class GSAMaterialSteel : StructuralMaterialSteel, IGSAObject
    {
        public string GWACommand { get; set; } = "";
        public List<string> SubGWACommand { get; set; } = new List<string>();
        
        #region Sending Functions
        public static bool GetObjects(Dictionary<Type, List<IGSAObject>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType))
                dict[MethodBase.GetCurrentMethod().DeclaringType] = new List<IGSAObject>();

            List<GSAMaterialSteel> materials = new List<GSAMaterialSteel>();
            
            string keyword = MethodBase.GetCurrentMethod().DeclaringType.GetGSAKeyword();
            string[] subKeywords = MethodBase.GetCurrentMethod().DeclaringType.GetSubGSAKeyword();

            string[] lines = GSA.GetGWARecords("GET_ALL," + keyword);
            List<string> deletedLines = GSA.GetDeletedGWARecords("GET_ALL," + keyword).ToList();
            foreach (string k in subKeywords)
                deletedLines.AddRange(GSA.GetDeletedGWARecords("GET_ALL," + k));

            // Remove deleted lines
            dict[typeof(GSAMaterialSteel)].RemoveAll(l => deletedLines.Contains(l.GWACommand));
            foreach (KeyValuePair<Type, List<IGSAObject>> kvp in dict)
                kvp.Value.RemoveAll(l => l.SubGWACommand.Any(x => deletedLines.Contains(x)));

            // Filter only new lines
            string[] prevLines = dict[typeof(GSAMaterialSteel)].Select(l => l.GWACommand).ToArray();
            string[] newLines = lines.Where(l => !prevLines.Contains(l)).ToArray();

            foreach (string p in newLines)
            {
                GSAMaterialSteel mat = ParseGWACommand(p);
                materials.Add(mat);
            }

            dict[typeof(GSAMaterialSteel)].AddRange(materials);

            if (materials.Count() > 0 || deletedLines.Count() > 0) return true;

            return false;
        }

        public static GSAMaterialSteel ParseGWACommand(string command)
        {
            GSAMaterialSteel ret = new GSAMaterialSteel();

            ret.GWACommand = command;

            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier
            ret.StructuralId = pieces[counter++];
            counter++; // MAT.8
            ret.Name = pieces[counter++].Trim(new char[] { '"' });
            counter++; // Unlocked
            ret.YoungsModulus = Convert.ToDouble(pieces[counter++]);
            ret.PoissonsRatio = Convert.ToDouble(pieces[counter++]);
            ret.ShearModulus = Convert.ToDouble(pieces[counter++]);
            ret.Density = Convert.ToDouble(pieces[counter++]);
            ret.CoeffThermalExpansion = Convert.ToDouble(pieces[counter++]);

            // Failure strain is found before MAT_CURVE_PARAM.2
            int strainIndex = Array.FindIndex(pieces, x => x.StartsWith("MAT_CURVE_PARAM"));
            if (strainIndex > 0)
                ret.MaxStrain = Convert.ToDouble(pieces[strainIndex - 1]);

            // Skip to last fourth to last
            counter = pieces.Count() - 4;
            ret.YieldStrength = Convert.ToDouble(pieces[counter++]);
            ret.UltimateStrength = Convert.ToDouble(pieces[counter++]);
            
            return ret;
        }
        #endregion

        #region Receiving Functions
        public static void SetObjects(Dictionary<Type, List<IStructural>> dict)
        {
            if (!dict.ContainsKey(typeof(StructuralMaterialSteel))) return;

            foreach (IStructural obj in dict[typeof(StructuralMaterialSteel)])
            {
                Set(obj as StructuralMaterialSteel);
            }
        }

        public static void Set(StructuralMaterialSteel mat)
        {
            if (mat == null)
                return;

            string keyword = MethodBase.GetCurrentMethod().DeclaringType.GetGSAKeyword();

            int index = Indexer.ResolveIndex(MethodBase.GetCurrentMethod().DeclaringType, mat);

            // TODO: This function barely works.
            List<string> ls = new List<string>();

            ls.Add("SET");
            ls.Add("MAT_STEEL.3");
            ls.Add(index.ToString());
            ls.Add("MAT.8");
            ls.Add(mat.Name == null || mat.Name == "" ? " " : mat.Name);
            ls.Add("YES"); // Unlocked
            ls.Add(mat.YoungsModulus.ToString()); // E
            ls.Add(mat.PoissonsRatio.ToString()); // nu
            ls.Add(mat.ShearModulus.ToString()); // G
            ls.Add(mat.Density.ToString()); // rho
            ls.Add(mat.CoeffThermalExpansion.ToString()); // alpha
            ls.Add("MAT_ANAL.1");
            ls.Add("0"); // TODO: What is this?
            ls.Add("Steel");
            ls.Add("-268435456"); // TODO: What is this?
            ls.Add("MAT_ELAS_ISO");
            ls.Add("6"); // TODO: What is this?
            ls.Add(mat.YoungsModulus.ToString()); // E
            ls.Add(mat.PoissonsRatio.ToString()); // nu
            ls.Add(mat.Density.ToString()); // rho
            ls.Add(mat.CoeffThermalExpansion.ToString()); // alpha
            ls.Add(mat.ShearModulus.ToString()); // G
            ls.Add("0"); // TODO: What is this?
            ls.Add("0"); // TODO: What is this?
            ls.Add("0"); // TODO: What is this?
            ls.Add("0"); // TODO: What is this?
            ls.Add("0"); // TODO: What is this?
            ls.Add("0"); // TODO: What is this?
            ls.Add("0"); // TODO: What is this?
            ls.Add(mat.MaxStrain.ToString()); // Ultimate strain
            ls.Add("MAT_CURVE_PARAM.2");
            ls.Add("");
            ls.Add("UNDEF");
            ls.Add("1"); // Material factor on strength
            ls.Add("1"); // Material factor on elastic modulus
            ls.Add("MAT_CURVE_PARAM.2");
            ls.Add("");
            ls.Add("UNDEF");
            ls.Add("1"); // Material factor on strength
            ls.Add("1"); // Material factor on elastic modulus
            ls.Add("0"); // Cost
            ls.Add(mat.YieldStrength.ToString()); // Yield strength
            ls.Add(mat.UltimateStrength.ToString()); // Ultimate strength
            ls.Add("0"); // Perfectly plastic strain limit
            ls.Add("0"); // Hardening modulus
            
            GSA.RunGWACommand(string.Join("\t", ls));
        }
        #endregion
    }
}
