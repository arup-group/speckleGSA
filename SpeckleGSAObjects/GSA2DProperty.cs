using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Interop.Gsa_9_0;

namespace SpeckleGSA
{
    public class GSA2DProperty : GSAObject
    {
        public static readonly string GSAKeyword = "PROP_2D";
        public static readonly string Stream = "properties";
        public static readonly int ReadPriority = 1;
        public static readonly int WritePriority = 3;

        public double Thickness { get; set; }
        public int Material { get; set; }

        public GSA2DProperty()
        {
            Thickness = 0;
            Material = 1;
        }

        #region GSAObject Functions
        public static void GetObjects(ComAuto gsa, Dictionary<Type, object> dict)
        {
            List<GSAObject> materials = dict[typeof(GSAMaterial)] as List<GSAObject>;
            List<GSAObject> props = new List<GSAObject>();

            string res = gsa.GwaCommand("GET_ALL,PROP_2D");

            if (res == "")
                return;

            string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (string p in pieces)
            {
                GSA2DProperty prop = new GSA2DProperty().AttachGSA(gsa);
                prop.ParseGWACommand(p, materials.ToArray());

                props.Add(prop);
            }

            dict[typeof(GSA2DProperty)] = props;
        }

        public static void WriteObjects(ComAuto gsa, Dictionary<Type, object> dict)
        {
            if (!dict.ContainsKey(typeof(GSA2DProperty))) return;

            List<GSAObject> props = dict[typeof(GSA2DProperty)] as List<GSAObject>;

            foreach (GSAObject p in props)
            {
                p.AttachGSA(gsa);

                GSARefCounters.RefObject(p);
                
                p.RunGWACommand(p.GetGWACommand(dict));
            }

            dict.Remove(typeof(GSA2DProperty));
        }

        public override void ParseGWACommand(string command, GSAObject[] children = null)
        {
            string[] pieces = command.ListSplit(",");
            int counter = 1; // Skip identifier
            Reference = Convert.ToInt32(pieces[counter++]);
            Name = pieces[counter++].Trim(new char[] { '"' });
            Color = pieces[counter++].ParseGSAColor();
            counter++; // Type
            counter++; // Axis
            counter++; // Analysis material

            string materialType = pieces[counter++];
            int materialGrade = Convert.ToInt32(pieces[counter++]);
            
            GSAObject matchingMaterial = children.Cast<GSAMaterial>().Where(m => m.LocalReference == materialGrade & m.Type == materialType).FirstOrDefault();

            Material = matchingMaterial == null ? 1 : matchingMaterial.Reference;

            counter++; // Design property
            Thickness = Convert.ToDouble(pieces[counter++]);

            // Ignore the rest
        }

        public override string GetGWACommand(Dictionary<Type, object> dict = null)
        {
            List<string> ls = new List<string>();

            ls.Add("SET");
            ls.Add(GSAKeyword);
            ls.Add(Reference.ToNumString());
            ls.Add(Name);
            if (Color == null)
                ls.Add("NO_RGB");
            else
                ls.Add(Color.ToNumString());
            ls.Add("SHELL");
            ls.Add("GLOBAL");
            ls.Add("0"); // Analysis material

            GSAMaterial matchingMaterial = (dict[typeof(GSAMaterial)] as List<GSAObject>).Cast<GSAMaterial>().Where(m => m.Reference == Material).FirstOrDefault();
            ls.Add(matchingMaterial == null ? "" : matchingMaterial.Type);

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

        public override List<GSAObject> GetChildren()
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
