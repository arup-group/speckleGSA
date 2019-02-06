using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Interop.Gsa_10_0;
using SpeckleStructures;

namespace SpeckleGSA
{
    public class GSA2DProperty : Structural2DProperty
    {
        public static readonly string GSAKeyword = "PROP_2D";
        public static readonly string Stream = "properties";

        public static readonly Type[] ReadPrerequisite = new Type[1] { typeof(GSAMaterial) };
        public static readonly Type[] WritePrerequisite = new Type[1] { typeof(GSAMaterial) };

        public bool IsAxisLocal;

        public GSA2DProperty()
        {
            IsAxisLocal = false;
        }

        public GSA2DProperty(Structural2DProperty baseClass)
        {
            IsAxisLocal = false;

            foreach (FieldInfo f in baseClass.GetType().GetFields())
                f.SetValue(this, f.GetValue(baseClass));

            foreach (PropertyInfo p in baseClass.GetType().GetProperties())
                p.SetValue(this, p.GetValue(baseClass));
        }

        #region GSAObject Functions
        public static void GetObjects(Dictionary<Type, List<StructuralObject>> dict)
        {
            if (!dict.ContainsKey(typeof(GSAMaterial))) return;

            List<StructuralObject> materials = dict[typeof(GSAMaterial)];
            List<StructuralObject> props = new List<StructuralObject>();

            string res = (string)GSA.RunGWACommand("GET_ALL,PROP_2D");

            if (res == "")
                return;

            string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            double counter = 1;
            foreach (string p in pieces)
            {
                GSA2DProperty prop = new GSA2DProperty();
                prop.ParseGWACommand(p, dict);

                props.Add(prop);
                Status.ChangeStatus("Reading 2D properties", counter++ / pieces.Length * 100);
            }
            
            dict[typeof(GSA2DProperty)] = props;
        }

        public static void WriteObjects(Dictionary<Type, List<StructuralObject>> dict)
        {
            if (!dict.ContainsKey(typeof(GSA2DProperty))) return;

            List<StructuralObject> props = dict[typeof(GSA2DProperty)];
            
            double counter = 1;
            foreach (StructuralObject p in props)
            {
                GSARefCounters.RefObject(p);
                
                GSA.RunGWACommand((p as GSA2DProperty).GetGWACommand(dict));
                Status.ChangeStatus("Writing 2D properties", counter++ / props.Count() * 100);
            }
            
            dict.Remove(typeof(GSA2DProperty));
        }

        public void ParseGWACommand(string command, Dictionary<Type, List<StructuralObject>> dict = null)
        {
            string[] pieces = command.ListSplit(",");
            int counter = 1; // Skip identifier
            Reference = Convert.ToInt32(pieces[counter++]);
            Name = pieces[counter++].Trim(new char[] { '"' });
            counter++; // Color
            counter++; // Type
            IsAxisLocal = pieces[counter++] == "LOCAL"; // Axis
            counter++; // Analysis material

            string materialType = pieces[counter++];
            StructuralMaterialType materialTypeEnum;
            if (materialType == "STEEL")
                materialTypeEnum = StructuralMaterialType.STEEL;
            else if (materialType == "CONCRETE")
                materialTypeEnum = StructuralMaterialType.CONCRETE;
            else
                materialTypeEnum = StructuralMaterialType.GENERIC;
            int materialGrade = Convert.ToInt32(pieces[counter++]);

            if (dict.ContainsKey(typeof(GSAMaterial)))
            {
                List<StructuralObject> materials = dict[typeof(GSAMaterial)];
                GSAMaterial matchingMaterial = materials.Cast<GSAMaterial>().Where(m => m.LocalReference == materialGrade & m.Type == materialTypeEnum).FirstOrDefault();
                Material = matchingMaterial == null ? 1 : matchingMaterial.Reference;
            }
            else
                Material = 1;

            counter++; // Design property
            Thickness = Convert.ToDouble(pieces[counter++]);

            // Ignore the rest
        }

        public string GetGWACommand(Dictionary<Type, List<StructuralObject>> dict = null)
        {
            List<string> ls = new List<string>();

            ls.Add("SET");
            ls.Add(GSAKeyword);
            ls.Add(Reference.ToNumString());
            ls.Add(Name);
            ls.Add("NO_RGB");
            ls.Add("SHELL");
            ls.Add("GLOBAL");
            ls.Add("0"); // Analysis material

            if (dict.ContainsKey(typeof(GSAMaterial)))
            {
                GSAMaterial matchingMaterial = dict[typeof(GSAMaterial)].Cast<GSAMaterial>().Where(m => m.Reference == Material).FirstOrDefault();
                if (matchingMaterial != null)
                {
                    if (matchingMaterial.Type == StructuralMaterialType.STEEL)
                        ls.Add("STEEL");
                    else if (matchingMaterial.Type == StructuralMaterialType.CONCRETE)
                        ls.Add("CONCRETE");
                    else
                        ls.Add("GENERAL");
                }
                else
                    ls.Add("");
            }
            else
                ls.Add("");

            ls.Add(Material.ToNumString());
            ls.Add("1"); // Design
            ls.Add(Thickness.ToNumString());
            ls.Add("CENTROID"); // Reference point
            ls.Add("0"); // Ref_z
            ls.Add("0"); // Mass
            ls.Add("100%"); // Flex modifier
            ls.Add("100%"); // Shear modifier
            ls.Add("100%"); // Inplane modifier
            ls.Add("100%"); // Weight modifier
            ls.Add("NO_ENV"); // Environmental data

            return string.Join(",", ls);
        }
        #endregion
    }
}
