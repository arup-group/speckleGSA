using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA
{
    public class GSA2DProperty : GSAObject
    {
        public double Thickness { get; set; }
        public int Material { get; set; }

        public GSA2DProperty()
        {
            Thickness = 0;
            Material = 1;
        }

        #region GSAObject Functions
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

            GSAObject matchingMaterial = (children as GSAMaterial[]).Where(m => m.LocalReference == materialGrade & m.Type == materialType).FirstOrDefault();

            Material = matchingMaterial == null ? 1 : matchingMaterial.Reference;

            counter++; // Design property
            Thickness = Convert.ToDouble(pieces[counter++]);

            // Ignore the rest
        }

        public override string GetGWACommand(GSAObject[] children = null)
        {
            List<string> ls = new List<string>();

            ls.Add("SET");
            ls.Add("PROP_2D.5");
            ls.Add(Reference.ToNumString());
            ls.Add(Name);
            if (Color == null)
                ls.Add("NO_RGB");
            else
                ls.Add(Color.ToNumString());
            ls.Add("SHELL");
            ls.Add("GLOBAL");
            ls.Add("0"); // Analysis material
            ls.Add((children as GSAMaterial[]).Where(m => m.Reference == Material).FirstOrDefault().Type);
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

        public override void WritetoGSA(Dictionary<Type, object> dict)
        {
            RunGWACommand(GetGWACommand((dict[typeof(GSAMaterial)] as IList).Cast<GSAMaterial>().ToArray()));
        }
        #endregion
    }
}
