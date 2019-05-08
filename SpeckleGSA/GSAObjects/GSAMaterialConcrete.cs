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
    [GSAObject("MAT_CONCRETE.16", new string[] { }, "properties", true, true, new Type[] { }, new Type[] { })]
    public class GSAMaterialConcrete : StructuralMaterialConcrete, IGSAObject
    {
        public string GWACommand { get; set; } = "";
        public List<string> SubGWACommand { get; set; } = new List<string>();
        
        #region Sending Functions
        public static bool GetObjects(Dictionary<Type, List<IGSAObject>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType))
                dict[MethodBase.GetCurrentMethod().DeclaringType] = new List<IGSAObject>();

            List<GSAMaterialConcrete> materials = new List<GSAMaterialConcrete>();
            
            string keyword = MethodBase.GetCurrentMethod().DeclaringType.GetGSAKeyword();
            string[] subKeywords = MethodBase.GetCurrentMethod().DeclaringType.GetSubGSAKeyword();

            string[] lines = GSA.GetGWARecords("GET_ALL," + keyword);
            List<string> deletedLines = GSA.GetDeletedGWARecords("GET_ALL," + keyword).ToList();
            foreach (string k in subKeywords)
                deletedLines.AddRange(GSA.GetDeletedGWARecords("GET_ALL," + k));

            // Remove deleted lines
            dict[typeof(GSAMaterialConcrete)].RemoveAll(l => deletedLines.Contains(l.GWACommand));
            foreach (KeyValuePair<Type, List<IGSAObject>> kvp in dict)
                kvp.Value.RemoveAll(l => l.SubGWACommand.Any(x => deletedLines.Contains(x)));

            // Filter only new lines
            string[] prevLines = dict[typeof(GSAMaterialConcrete)].Select(l => l.GWACommand).ToArray();
            string[] newLines = lines.Where(l => !prevLines.Contains(l)).ToArray();

            foreach (string p in newLines)
            {
                GSAMaterialConcrete mat = ParseGWACommand(p);
                materials.Add(mat);
            }

            dict[typeof(GSAMaterialConcrete)].AddRange(materials);

            if (materials.Count() > 0 || deletedLines.Count() > 0) return true;

            return false;
        }

        public static GSAMaterialConcrete ParseGWACommand(string command)
        {
            GSAMaterialConcrete ret = new GSAMaterialConcrete();

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
            
            // Skip to last 27th to last
            counter = pieces.Count() - 27;
            ret.CompressiveStrength = Convert.ToDouble(pieces[counter++]);

            // Skip to last 15th to last
            counter = pieces.Count() - 15;
            ret.MaxStrain = Convert.ToDouble(pieces[counter++]);

            // Skip to last 10th to last
            counter = pieces.Count() - 10;
            ret.AggragateSize = Convert.ToDouble(pieces[counter++]);
            
            return ret;
        }
        #endregion

        #region Receiving Functions
        public static void SetObjects(Dictionary<Type, List<IStructural>> dict)
        {
            if (!dict.ContainsKey(typeof(StructuralMaterialConcrete))) return;

            foreach (IStructural obj in dict[typeof(StructuralMaterialConcrete)])
            {
                Set(obj as StructuralMaterialConcrete);
            }
        }

        public static void Set(StructuralMaterialConcrete mat)
        {
            if (mat == null)
                return;

            string keyword = MethodBase.GetCurrentMethod().DeclaringType.GetGSAKeyword();

            int index = Indexer.ResolveIndex(MethodBase.GetCurrentMethod().DeclaringType, mat);

            // TODO: This function barely works.
            List<string> ls = new List<string>();

            ls.Add("SET");
            ls.Add("MAT_CONCRETE.16");
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
            ls.Add("Concrete");
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
            ls.Add("0"); // Ultimate strain
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
            ls.Add("CYLINDER"); // Strength type
            ls.Add("N"); // Cement class
            ls.Add(mat.CompressiveStrength.ToString()); // Concrete strength
            ls.Add("0"); //ls.Add("27912500"); // Uncracked strength
            ls.Add("0"); //ls.Add("17500000"); // Cracked strength
            ls.Add("0"); //ls.Add("2366431"); // Tensile strength
            ls.Add("0"); //ls.Add("2366431"); // Peak strength for curves
            ls.Add("0"); // TODO: What is this?
            ls.Add("1"); // Ratio of initial elastic modulus to secant modulus
            ls.Add("2"); // Parabolic coefficient
            ls.Add("1"); // Modifier on elastic stiffness
            ls.Add("0.00218389285990043"); // SLS strain at peak stress
            ls.Add("0.0035"); // SLS max strain
            ls.Add("0.00041125"); // ULS strain at plateau stress
            ls.Add(mat.MaxStrain.ToString()); // ULS max compressive strain
            ls.Add("0.0035"); // TODO: What is this?
            ls.Add("0.002"); // Plateau strain
            ls.Add("0.0035"); // Max axial strain
            ls.Add("NO"); // Lightweight?
            ls.Add(mat.AggragateSize.ToString()); // Aggragate size
            ls.Add("0"); // TODO: What is this?
            ls.Add("0"); // TODO: What is this?
            ls.Add("1"); // TODO: What is this?
            ls.Add("0.8825"); // Constant stress depth
            ls.Add("0"); // TODO: What is this?
            ls.Add("0"); // TODO: What is this?
            ls.Add("0"); // TODO: What is this?
            ls.Add("0"); // TODO: What is this?
            ls.Add("0"); // TODO: What is this?

            GSA.RunGWACommand(string.Join("\t", ls));
        }
        #endregion
    }
}
