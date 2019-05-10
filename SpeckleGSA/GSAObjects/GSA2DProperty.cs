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
    [GSAObject("PROP_2D.5", new string[] { "MAT_STEEL.3", "MAT_CONCRETE.16" }, "properties", true, true, new Type[] { typeof(GSAMaterialSteel), typeof(GSAMaterialConcrete) }, new Type[] { typeof(GSAMaterialSteel), typeof(GSAMaterialConcrete) })]
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
            List<GSAMaterialSteel> steels = dict[typeof(GSAMaterialSteel)].Cast<GSAMaterialSteel>().ToList();
            List<GSAMaterialConcrete> concretes = dict[typeof(GSAMaterialConcrete)].Cast<GSAMaterialConcrete>().ToList();

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
                GSA2DProperty prop = ParseGWACommand(p, steels, concretes);
                props.Add(prop);
            }

            dict[typeof(GSA2DProperty)].AddRange(props);

            if (props.Count() > 0 || deletedLines.Count() > 0) return true;

            return false;
        }

        public static GSA2DProperty ParseGWACommand(string command, List<GSAMaterialSteel> steels, List<GSAMaterialConcrete> concretes)
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
            string materialGrade = pieces[counter++];
            if (materialType == "STEEL")
            {
                if (steels != null)
                {
                    GSAMaterialSteel matchingMaterial = steels.Where(m => m.StructuralId == materialGrade).FirstOrDefault();
                    ret.MaterialRef = matchingMaterial == null ? null : matchingMaterial.StructuralId;
                    if (matchingMaterial != null)
                        ret.SubGWACommand.Add(matchingMaterial.GWACommand);
                }
            }
            else if (materialType == "CONCRETE")
            {
                if (concretes != null)
                {
                    GSAMaterialConcrete matchingMaterial = concretes.Where(m => m.StructuralId == materialGrade).FirstOrDefault();
                    ret.MaterialRef = matchingMaterial == null ? null : matchingMaterial.StructuralId;
                    if (matchingMaterial != null)
                        ret.SubGWACommand.Add(matchingMaterial.GWACommand);
                }
            }

            counter++; // Analysis material
            ret.Thickness = Convert.ToDouble(pieces[counter++]);

            switch(pieces[counter++])
            {
                case "CENTROID":
                    ret.ReferenceSurface = Structural2DPropertyReferenceSurface.Middle;
                    break;
                case "TOP_CENTRE":
                    ret.ReferenceSurface = Structural2DPropertyReferenceSurface.Top;
                    break;
                case "BOT_CENTRE":
                    ret.ReferenceSurface = Structural2DPropertyReferenceSurface.Bottom;
                    break;
                default:
                    ret.ReferenceSurface = Structural2DPropertyReferenceSurface.Middle;
                    break;
            }
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
            string materialType = "UNDEF";

            var res = Indexer.LookupIndex(typeof(GSAMaterialSteel), prop.MaterialRef);
            if (res.HasValue)
            {
                materialRef = res.Value;
                materialType = "STEEL";
            }
            else
            {
                res = Indexer.LookupIndex(typeof(GSAMaterialConcrete), prop.MaterialRef);
                if (res.HasValue)
                {
                    materialRef = res.Value;
                    materialType = "CONCRETE";
                }
            }
            
            List<string> ls = new List<string>();

            ls.Add("SET");
            ls.Add(keyword);
            ls.Add(index.ToString());
            ls.Add(prop.Name == null || prop.Name == "" ? " " : prop.Name);
            ls.Add("NO_RGB");
            ls.Add("SHELL");
            ls.Add("GLOBAL");
            ls.Add("0"); // Analysis material
            ls.Add(materialType);
            ls.Add(materialRef.ToString());
            ls.Add("0"); // Design
            ls.Add(prop.Thickness.ToString());
            switch (prop.ReferenceSurface)
            {
                case Structural2DPropertyReferenceSurface.Middle:
                    ls.Add("CENTROID");
                    break;
                case Structural2DPropertyReferenceSurface.Top:
                    ls.Add("TOP_CENTRE");
                    break;
                case Structural2DPropertyReferenceSurface.Bottom:
                    ls.Add("BOT_CENTRE");
                    break;
                default:
                    ls.Add("CENTROID");
                    break;
            }
            ls.Add("0"); // Ref_z
            ls.Add("0"); // Mass
            ls.Add("100%"); // Flex modifier
            ls.Add("100%"); // Shear modifier
            ls.Add("100%"); // Inplane modifier
            ls.Add("100%"); // Weight modifier
            ls.Add("NO_ENV"); // Environmental data

            GSA.RunGWACommand(string.Join("\t", ls));
        }
        #endregion
    }
}
