using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpeckleStructures;

namespace SpeckleGSA
{
    [GSAObject("EL.3", "elements", false, false, new Type[] { }, new Type[] { })]
    public class GSA0DElement : StructuralObject
    {
        public string Type;
        public int Property;
        public double Mass;
        public int Connectivity;

        #region Contructors and Converters
        public GSA0DElement()
        {
            Type = "MASS";
            Property = 0;
            Mass = 0;
            Connectivity = 0;
        }
        #endregion

        #region GSA Functions
        public void ParseGWACommand(string command, Dictionary<Type, List<StructuralObject>> dict = null)
        {
            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier
            Reference = Convert.ToInt32(pieces[counter++]);
            counter++; // Name
            counter++; // Color
            Type = pieces[counter++];
            Property = Convert.ToInt32(pieces[counter++]);
            counter++; // group

            Connectivity = Convert.ToInt32(pieces[counter++]);

            Mass = GetGSAMass();
            // Rest is unimportant for 0D element
        }

        public string GetGWACommand(Dictionary<Type, List<StructuralObject>> dict = null)
        {
            List<string> ls = new List<string>();

            ls.Add("SET");
            ls.Add((string)this.GetAttribute("GSAKeyword"));
            ls.Add(Reference.ToString());
            ls.Add(""); // Name
            ls.Add("NO_RGB");
            ls.Add(Type);
            ls.Add(WriteMassProptoGSA().ToString()); // Property
            ls.Add("0"); // Group
            ls.Add(Connectivity.ToString());
            ls.Add("0"); // Orient Node
            ls.Add("0"); // Beta
            ls.Add("NO_RLS"); // Release
            ls.Add("0"); // Offset x-start
            ls.Add("0"); // Offset y-start
            ls.Add("0"); // Offset y
            ls.Add("0"); // Offset z

            //ls.Add("NORMAL"); // Action // TODO: EL.4 SUPPORT
            ls.Add(""); //Dummy

            return string.Join(",", ls);
        }

        private double GetGSAMass()
        {
            string res = (string)GSA.RunGWACommand("GET,PROP_MASS," + Property.ToString());
            string[] pieces = res.ListSplit(",");

            return Convert.ToDouble(pieces[5]);
        }

        private int WriteMassProptoGSA()
        {
            List<string> ls = new List<string>();

            int res = (int)GSA.RunGWACommand("HIGHEST,PROP_MASS");

            ls.Add("SET");
            ls.Add("PROP_MASS.2");
            ls.Add((res + 1).ToString());
            ls.Add("");
            ls.Add("NO_RGB");
            ls.Add("GLOBAL");
            ls.Add(Mass.ToString());
            ls.Add("0");
            ls.Add("0");
            ls.Add("0");
            ls.Add("0");
            ls.Add("0");
            ls.Add("0");

            ls.Add("MOD");
            ls.Add("100%");
            ls.Add("100%");
            ls.Add("100%");

            GSA.RunGWACommand(string.Join(",", ls));

            return res + 1;
        }
        #endregion
    }
}
