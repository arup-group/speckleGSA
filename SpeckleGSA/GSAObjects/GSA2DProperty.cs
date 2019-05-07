using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Interop.Gsa_10_0;
using SpeckleStructuresClasses;

namespace SpeckleGSA
{
    [GSAObject("PROP_2D.5", new string[] { "MAT_STEEL.3", "MAT_CONCRETE.16" }, "properties", true, true, new Type[] { typeof(GSAMaterial) }, new Type[] { typeof(GSAMaterial) })]
    public class GSA2DProperty : Structural2DProperty, IGSAObject
    {
        public bool IsAxisLocal;

        public string GWACommand { get; set; } = "";
        public List<string> SubGWACommand { get; set; } = new List<string>();

        #region Sending Functions
        public static bool GetObjects(Dictionary<Type, List<IGSAObject>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType))
                dict[MethodBase.GetCurrentMethod().DeclaringType] = new List<IGSAObject>();

            List<GSA2DProperty> props = new List<GSA2DProperty>();
            List<GSAMaterial> mats = dict[typeof(GSAMaterial)].Cast<GSAMaterial>().ToList();

            string keyword = MethodBase.GetCurrentMethod().DeclaringType.GetGSAKeyword();
            string[] subKeywords = MethodBase.GetCurrentMethod().DeclaringType.GetSubGSAKeyword();

            string[] lines = GSA.GetGWARecords("GET_ALL," + keyword);
            List<string> deletedLines = GSA.GetDeletedGWARecords("GET_ALL," + keyword).ToList();
            foreach (string k in subKeywords)
                deletedLines.AddRange(GSA.GetDeletedGWARecords("GET_ALL," + k));

            // Remove deleted lines
            dict[typeof(GSA2DProperty)].RemoveAll(l => deletedLines.Contains(l.GWACommand));
            foreach (KeyValuePair<Type, List<IGSAObject>> kvp in dict)
                kvp.Value.RemoveAll(l => l.SubGWACommand.Any(x => deletedLines.Contains(x)));

            // Filter only new lines
            string[] prevLines = dict[typeof(GSA2DProperty)].Select(l => l.GWACommand).ToArray();
            string[] newLines = lines.Where(l => !prevLines.Contains(l)).ToArray();

            foreach (string p in newLines)
            {
                GSA2DProperty prop = ParseGWACommand(p, mats);
                props.Add(prop);
            }

            dict[typeof(GSA2DProperty)].AddRange(props);

            if (props.Count() > 0 || deletedLines.Count() > 0) return true;

            return false;
        }

        public static GSA2DProperty ParseGWACommand(string command, List<GSAMaterial> materials)
        {
            GSA2DProperty ret = new GSA2DProperty();

            ret.GWACommand = command;

            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier
            ret.StructuralId = pieces[counter++];
            ret.Name = pieces[counter++].Trim(new char[] { '"' });
            counter++; // Color
            counter++; // Type
            ret.IsAxisLocal = pieces[counter++] == "LOCAL"; // Axis
            counter++; // Analysis material
            string materialType = pieces[counter++];
            StructuralMaterialType materialTypeEnum = StructuralMaterialType.Generic;
            if (materialType == "STEEL")
                materialTypeEnum = StructuralMaterialType.Steel;
            else if (materialType == "CONCRETE")
                materialTypeEnum = StructuralMaterialType.Concrete;
            int materialGrade = Convert.ToInt32(pieces[counter++]);

            if (materials != null)
            {
                GSAMaterial matchingMaterial = materials.Where(m => m.LocalReference == materialGrade & m.MaterialType == materialTypeEnum).FirstOrDefault();
                ret.MaterialRef = matchingMaterial == null ? null : matchingMaterial.StructuralId;
                if (matchingMaterial != null)
                    ret.SubGWACommand.Add(matchingMaterial.GWACommand);
            }
            else
                ret.MaterialRef = null;

            counter++; // Analysis material
            ret.Thickness = Convert.ToDouble(pieces[counter++]);
            // Ignore the rest

            return ret;
        }
        #endregion

        #region Receiving Functions
        public static void SetObjects(Dictionary<Type, List<IStructural>> dict)
        {
            if (!dict.ContainsKey(typeof(Structural2DProperty))) return;

            foreach (IStructural obj in dict[typeof(Structural2DProperty)])
            {
                Set(obj as Structural2DProperty);
            }
        }

        public static void Set(Structural2DProperty prop)
        {
            if (prop == null)
                return;

            string keyword = MethodBase.GetCurrentMethod().DeclaringType.GetGSAKeyword();

            int index = Indexer.ResolveIndex(MethodBase.GetCurrentMethod().DeclaringType, prop);
            int materialRef = 0;
            try
            {
                materialRef = Indexer.LookupIndex(typeof(GSAMaterial), prop.MaterialRef).Value;
            }
            catch { }

            List<string> ls = new List<string>();

            ls.Add("SET");
            ls.Add(keyword);
            ls.Add(index.ToString());
            ls.Add(prop.Name == null || prop.Name == "" ? " " : prop.Name);
            ls.Add("NO_RGB");
            ls.Add("SHELL");
            ls.Add("GLOBAL");
            ls.Add("0"); // Analysis material
            ls.Add(GetMaterialType(materialRef));
            ls.Add(materialRef.ToString());
            ls.Add("1"); // Design
            ls.Add(prop.Thickness.ToString());
            ls.Add("CENTROID"); // Reference point
            ls.Add("0"); // Ref_z
            ls.Add("0"); // Mass
            ls.Add("100%"); // Flex modifier
            ls.Add("100%"); // Shear modifier
            ls.Add("100%"); // Inplane modifier
            ls.Add("100%"); // Weight modifier
            ls.Add("NO_ENV"); // Environmental data

            GSA.RunGWACommand(string.Join(",", ls));
        }

        public static string GetMaterialType(int materialRef)
        {
            // Steel
            if (GSA.GetGWARecords("GET,MAT_STEEL.3," + materialRef.ToString()).FirstOrDefault() != null) return "STEEL";

            // Concrete
            if (GSA.GetGWARecords("GET,MAT_CONCRETE.16," + materialRef.ToString()).FirstOrDefault() != null) return "CONCRETE";

            // Default
            return "GENERIC";
        }
        #endregion
    }
}
