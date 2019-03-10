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
    [GSAObject("PROP_2D.5", "properties", true, true, new Type[] { typeof(GSAMaterial) }, new Type[] { typeof(GSAMaterial) })]
    public class GSA2DProperty : Structural2DProperty, IGSAObject
    {
        public bool IsAxisLocal;

        public string GWACommand { get; set; }
        public List<string> SubGWACommand { get; set; }

        #region Contructors and Converters
        public GSA2DProperty()
        {
            GWACommand = "";
            SubGWACommand = new List<string>();
            IsAxisLocal = false;
        }

        public GSA2DProperty(Structural2DProperty baseClass)
        {
            GWACommand = "";
            SubGWACommand = new List<string>();
            IsAxisLocal = false;

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

            List<object> props = new List<object>();

            string[] lines = GSA.GetGWAGetCommands("GET_ALL,PROP_2D");
            string[] deletedLines = GSA.GetDeletedGWAGetCommands("GET_ALL,PROP_2D");

            // Remove deleted lines
            dict[typeof(GSA2DProperty)].RemoveAll(l => deletedLines.Contains(((IGSAObject)l).GWACommand));
            foreach (KeyValuePair<Type, List<object>> kvp in dict)
                kvp.Value.RemoveAll(l => ((IGSAObject)l).SubGWACommand.Any(x => deletedLines.Contains(x)));

            // Filter only new lines
            string[] prevLines = dict[typeof(GSA2DProperty)].Select(l => ((GSA2DProperty)l).GWACommand).ToArray();
            string[] newLines = lines.Where(l => !prevLines.Contains(l)).ToArray();
            
            foreach (string p in newLines)
            {
                GSA2DProperty prop = new GSA2DProperty();
                prop.ParseGWACommand(p, dict);

                props.Add(prop);
            }
            
            dict[typeof(GSA2DProperty)].AddRange(props);

            if (props.Count() > 0 || deletedLines.Length > 0) return true;

            return false;
        }

        public static void WriteObjects(Dictionary<Type, List<StructuralObject>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType)) return;

            List<StructuralObject> props = dict[typeof(GSA2DProperty)];
            
            double counter = 1;
            foreach (StructuralObject p in props)
            {
                GSARefCounters.RefObject(p);
                
                GSA.RunGWACommand((p as GSA2DProperty).GetGWACommand(dict));
                Status.ChangeStatus("Writing 2D properties", counter++ / props.Count() * 100);
            }
        }

        public void ParseGWACommand(string command, Dictionary<Type, List<object>> dict = null)
        {
            GWACommand = command;

            string[] pieces = command.ListSplit(",");
            int counter = 1; // Skip identifier
            Reference = Convert.ToInt32(pieces[counter++]);
            Name = pieces[counter++].Trim(new char[] { '"' });
            counter++; // Color
            counter++; // Type
            IsAxisLocal = pieces[counter++] == "LOCAL"; // Axis
            counter++; // Analysis material

            string materialType = pieces[counter++];
            string materialTypeEnum;
            if (materialType == "STEEL")
                materialTypeEnum = StructuralMaterialType.STEEL;
            else if (materialType == "CONCRETE")
                materialTypeEnum = StructuralMaterialType.CONCRETE;
            else
                materialTypeEnum = StructuralMaterialType.GENERIC;
            int materialGrade = Convert.ToInt32(pieces[counter++]);

            if (dict.ContainsKey(typeof(GSAMaterial)))
            {
                List<object> materials = dict[typeof(GSAMaterial)];
                GSAMaterial matchingMaterial = materials.Cast<GSAMaterial>().Where(m => m.LocalReference == materialGrade & m.Type == materialTypeEnum).FirstOrDefault();
                Material = matchingMaterial == null ? 1 : matchingMaterial.Reference;
                if (matchingMaterial != null)
                    SubGWACommand.Add(matchingMaterial.GWACommand);
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
            ls.Add((string)this.GetAttribute("GSAKeyword"));
            ls.Add(Reference.ToString());
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

            ls.Add(Material.ToString());
            ls.Add("1"); // Design
            ls.Add(Thickness.ToString());
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
