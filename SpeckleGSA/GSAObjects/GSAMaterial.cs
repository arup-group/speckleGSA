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
    [GSAObject("MAT", "properties", true, true, new Type[] { }, new Type[] { })]
    public class GSAMaterial : StructuralMaterial, IGSAObject
    {
        // Need local reference since materials can have same reference if different types
        public int LocalReference;

        public string GWACommand { get; set; } = "";
        public List<string> SubGWACommand { get; set; } = new List<string>();
        
        #region Sending Functions
        public static bool GetObjects(Dictionary<Type, List<IGSAObject>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType))
                dict[MethodBase.GetCurrentMethod().DeclaringType] = new List<IGSAObject>();

            List<GSAMaterial> materials = new List<GSAMaterial>();

            // TODO: Only supports steel and concrete
            string[] materialIdentifier = new string[]
                { "MAT_STEEL.3", "MAT_CONCRETE.16" };

            List<string> pieces = new List<string>();
            bool deleted = false;
            foreach (string id in materialIdentifier)
            {
                string[] lines = GSA.GetGWARecords("GET_ALL," + id);
                string[] deletedLines = GSA.GetDeletedGWARecords("GET_ALL," + id);

                if (deletedLines.Length > 0)
                    deleted = true;

                // Remove deleted lines
                dict[typeof(GSAMaterial)].RemoveAll(l => deletedLines.Contains(l.GWACommand));
                foreach (KeyValuePair<Type, List<IGSAObject>> kvp in dict)
                    kvp.Value.RemoveAll(l => l.SubGWACommand.Any(x => deletedLines.Contains(x)));

                // Filter only new lines
                string[] prevLines = dict[typeof(GSAMaterial)].Select(l => ((GSAMaterial)l).GWACommand).ToArray();
                string[] newLines = lines.Where(l => !prevLines.Contains(l)).ToArray();

                pieces.AddRange(newLines);
            }
            pieces = pieces.Distinct().ToList();

            for (int i = 0; i < pieces.Count(); i++)
            {
                GSAMaterial mat = ParseGWACommand(pieces[i]);
                mat.StructuralId = (i + 1).ToString(); // Offset references
                materials.Add(mat);
            }

            dict[typeof(GSAMaterial)].AddRange(materials);

            if (materials.Count() > 0 || deleted) return true;

            return false;
        }

        public static GSAMaterial ParseGWACommand(string command)
        {
            GSAMaterial ret = new GSAMaterial();

            ret.GWACommand = command;

            string[] pieces = command.ListSplit(",");

            int counter = 0;
            string identifier = pieces[counter++];
            ret.LocalReference = Convert.ToInt32(pieces[counter++]);

            if (identifier.Contains("STEEL"))
            {
                ret.MaterialType = StructuralMaterialType.Steel;
                counter++; // Move to name field of basic MAT definition
            }
            else if (identifier.Contains("CONCRETE"))
            {
                ret.MaterialType = StructuralMaterialType.Concrete;
                counter++; // Move to name field of basic MAT definition
            }
            else
                ret.MaterialType = StructuralMaterialType.Generic;

            ret.Grade = pieces[counter++].Trim(new char[] { '"' }); // TODO: Using name as grade

            // Rest is unimportant

            return ret;
        }
        #endregion

        #region Receiving Functions
        public static void SetObjects(Dictionary<Type, List<IStructural>> dict)
        {
            if (!dict.ContainsKey(typeof(StructuralMaterial))) return;

            foreach (IStructural obj in dict[typeof(StructuralMaterial)])
            {
                Set(obj as StructuralMaterial);
            }
        }

        public static void Set(StructuralMaterial mat)
        {
            if (mat == null)
                return;

            string keyword = MethodBase.GetCurrentMethod().DeclaringType.GetGSAKeyword();

            int index = Indexer.ResolveIndex(MethodBase.GetCurrentMethod().DeclaringType, mat);

            // TODO: This function barely works.
            List<string> ls = new List<string>();

            ls.Add("SET");
            if (mat.MaterialType == StructuralMaterialType.Steel)
            {
                ls.Add("MAT_STEEL.3");
                ls.Add(index.ToString());
                ls.Add("MAT.8");
                ls.Add(mat.Grade == null || mat.Grade == "" ? " " : mat.Grade);
                ls.Add("YES"); // Unlocked
                ls.Add("200000000000"); // E
                ls.Add("0.3"); // nu
                ls.Add("76923076923"); // G
                ls.Add("7850"); // rho
                ls.Add("1.2e-05"); // alpha
                ls.Add("MAT_ANAL.1");
                ls.Add("0"); // TODO: What is this?
                ls.Add("Steel");
                ls.Add("-268435456"); // TODO: What is this?
                ls.Add("MAT_ELAS_ISO");
                ls.Add("6"); // TODO: What is this?
                ls.Add("200000000000"); // E
                ls.Add("0.3"); // nu
                ls.Add("7850"); // rho
                ls.Add("1.2e-05"); // alpha
                ls.Add("76923076923"); // G
                ls.Add("0"); // TODO: What is this?
                ls.Add("0"); // TODO: What is this?
                ls.Add("0"); // TODO: What is this?
                ls.Add("0"); // TODO: What is this?
                ls.Add("0"); // TODO: What is this?
                ls.Add("0"); // TODO: What is this?
                ls.Add("0"); // TODO: What is this?
                ls.Add("0.05"); // Ultimate strain
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
                ls.Add("350000000"); // Yield strength
                ls.Add("450000000"); // Ultimate strength
                ls.Add("0"); // Perfectly plastic strain limit
                ls.Add("0"); // Hardening modulus

            }
            else // if (Type == StructuralMaterialType.CONCRETE) // TODO: Default to concrete
            {
                ls.Add("MAT_CONCRETE.16");
                ls.Add(index.ToString());
                ls.Add("MAT.8");
                ls.Add(mat.Grade == null || mat.Grade == "" ? " " : mat.Grade);
                ls.Add("YES"); // Unlocked
                ls.Add("28000000000"); // E
                ls.Add("0.2"); // nu
                ls.Add("11666666666"); // G
                ls.Add("2400"); // rho
                ls.Add("1e-05"); // alpha
                ls.Add("MAT_ANAL.1");
                ls.Add("0"); // TODO: What is this?
                ls.Add("Concrete");
                ls.Add("-268435456"); // TODO: What is this?
                ls.Add("MAT_ELAS_ISO");
                ls.Add("6"); // TODO: What is this?
                ls.Add("28000000000"); // E
                ls.Add("0.2"); // nu
                ls.Add("2400"); // rho
                ls.Add("1e-05"); // alpha
                ls.Add("11666666666"); // G
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
                ls.Add("35000000"); // Concrete strength
                ls.Add("27912500"); // Uncracked strength
                ls.Add("17500000"); // Cracked strength
                ls.Add("2366431"); // Tensile strength
                ls.Add("2366431"); // Peak strength for curves
                ls.Add("0"); // TODO: What is this?
                ls.Add("1"); // Ratio of initial elastic modulus to secant modulus
                ls.Add("2"); // Parabolic coefficient
                ls.Add("1"); // Modifier on elastic stiffness
                ls.Add("0.00218389285990043"); // SLS strain at peak stress
                ls.Add("0.0035"); // SLS max strain
                ls.Add("0.00041125"); // ULS strain at plateau stress
                ls.Add("0.0035"); // ULS max compressive strain
                ls.Add("0.0035"); // TODO: What is this?
                ls.Add("0.002"); // Plateau strain
                ls.Add("0.0035"); // Max axial strain
                ls.Add("NO"); // Lightweight?
                ls.Add("0.02"); // Aggragate size
                ls.Add("0"); // TODO: What is this?
                ls.Add("0"); // TODO: What is this?
                ls.Add("1"); // TODO: What is this?
                ls.Add("0.8825"); // Constant stress depth
                ls.Add("0"); // TODO: What is this?
                ls.Add("0"); // TODO: What is this?
                ls.Add("0"); // TODO: What is this?
                ls.Add("0"); // TODO: What is this?
                ls.Add("0"); // TODO: What is this?
            }
            
            GSA.RunGWACommand(string.Join(",", ls));
        }
        #endregion
    }
}
