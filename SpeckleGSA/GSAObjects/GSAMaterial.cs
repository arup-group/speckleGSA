﻿using System;
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
                ls.Add("MAT.6");
                ls.Add(Grade);
            }
            else if (Type == StructuralMaterialType.CONCRETE)
            {
                ls.Add("MAT_CONCRETE.16");
                ls.Add(Reference.ToString());
                ls.Add("MAT.6");
                ls.Add(Grade);
            }
            else
            {
                ls.Add("MAT.6");
                ls.Add(Reference.ToString());
                ls.Add(Grade);
            }

            ls.Add("YES");
            ls.Add("0"); // E
            ls.Add("0"); // nu
            ls.Add("0"); // rho
            ls.Add("0"); // alpha
            ls.Add("0"); // num ULS C curve
            ls.Add("0"); // num SLS C curve
            ls.Add("0"); // num ULS T curve
            ls.Add("0"); // num SLS T curve
            ls.Add("0"); // limit strain
            ls.Add("MAT_CURVE_PARAM.2");
            ls.Add("");
            ls.Add("UNDEF");
            ls.Add("0");
            ls.Add("0");
            ls.Add("MAT_CURVE_PARAM.2");
            ls.Add("");
            ls.Add("UNDEF");
            ls.Add("0");
            ls.Add("0");
            ls.Add("0"); // cost
            
            return string.Join(",", ls);
        }
        #endregion
    }
}
