using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Interop.Gsa_10_0;
using SpeckleStructures;

namespace SpeckleGSA
{
    public class GSAMaterial : StructuralMaterial
    {
        public static readonly string GSAKeyword  = "MAT";
        public static readonly string Stream = "properties";

        public static readonly Type[] ReadPrerequisite = new Type[0];
        public static readonly Type[] WritePrerequisite = new Type[0];
        public static readonly bool AnalysisLayer = true;
        public static readonly bool DesignLayer = true;

        // Need local reference since materials can have same reference if different types
        public int LocalReference;

        #region Contructors and Converters
        public GSAMaterial()
        {
            LocalReference = 0;
        }

        public GSAMaterial(StructuralMaterial baseClass)
        {
            LocalReference = 0;

            foreach (FieldInfo f in baseClass.GetType().GetFields())
                f.SetValue(this, f.GetValue(baseClass));

            foreach (PropertyInfo p in baseClass.GetType().GetProperties())
                p.SetValue(this, p.GetValue(baseClass));
        }

        public StructuralObject GetBase()
        {
            StructuralObject baseClass = (StructuralObject)Activator.CreateInstance(this.GetType().BaseType);

            foreach (FieldInfo f in baseClass.GetType().GetFields())
                f.SetValue(baseClass, f.GetValue(this));

            foreach (PropertyInfo p in baseClass.GetType().GetProperties())
                p.SetValue(baseClass, p.GetValue(this));

            return baseClass;
        }
        #endregion

        #region GSA Functions
        public static void GetObjects(Dictionary<Type, List<StructuralObject>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType))
                dict[MethodBase.GetCurrentMethod().DeclaringType] = new List<StructuralObject>();

            // TODO: Only supports steel and concrete
            string[] materialIdentifier = new string[]
                { "MAT_STEEL", "MAT_CONCRETE" };

            List<StructuralObject> materials = new List<StructuralObject>();

            List<string> pieces = new List<string>();
            foreach (string id in materialIdentifier)
            {
                string res = (string)GSA.RunGWACommand("GET_ALL," + id);

                if (res == "")
                    continue;

                pieces.AddRange(res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries));
            }
            pieces = pieces.Distinct().ToList();
            
            for (int i = 0; i < pieces.Count(); i++)
            {
                GSAMaterial mat = new GSAMaterial();
                mat.ParseGWACommand(pieces[i], dict);
                mat.Reference = i + 1; // Offset references
                materials.Add(mat);

                Status.ChangeStatus("Reading materials", (double)(i+1) / pieces.Count() * 100);
            }

            dict[typeof(GSAMaterial)].AddRange(materials);
        }

        public static void WriteObjects(Dictionary<Type, List<StructuralObject>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType)) return;

            List<StructuralObject> materials = dict[typeof(GSAMaterial)];

            double counter = 1;
            foreach (StructuralObject m in materials)
            {
                GSARefCounters.RefObject(m);

                GSA.RunGWACommand((m as GSAMaterial).GetGWACommand());
                Status.ChangeStatus("Writing materials", counter++ / materials.Count() * 100);
            }
        }

        public void ParseGWACommand(string command, Dictionary<Type, List<StructuralObject>> dict = null)
        {
            string[] pieces = command.ListSplit(",");

            int counter = 0;
            string identifier = pieces[counter++];
            LocalReference = Convert.ToInt32(pieces[counter++]);

            if (identifier.Contains("STEEL"))
            {
                Type = StructuralMaterialType.STEEL;
                counter++; // Move to name field of basic MAT definition
            }
            else if (identifier.Contains("CONCRETE"))
            {
                Type = StructuralMaterialType.CONCRETE;
                counter++; // Move to name field of basic MAT definition
            }
            else
                Type = StructuralMaterialType.GENERIC;

            Grade = pieces[counter++].Trim(new char[] { '"' }); // TODO: Using name as grade

            // TODO: Skip all other properties for now
        }

        public string GetGWACommand(Dictionary<Type, List<StructuralObject>> dict = null)
        {
            // TODO: This function barely works.
            List<string> ls = new List<string>();

            ls.Add("SET");
            if (Type == StructuralMaterialType.STEEL)
            {
                ls.Add("MAT_STEEL.3");
                ls.Add(Reference.ToString());
                ls.Add("MAT.8");
                ls.Add(Grade);
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
                ls.Add(Reference.ToString());
                ls.Add("MAT.8");
                ls.Add(Grade);
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
            
            return string.Join(",", ls);
        }
        #endregion
    }
}
