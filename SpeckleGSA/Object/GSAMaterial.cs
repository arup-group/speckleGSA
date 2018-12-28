using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA
{
    public class GSAMaterial : GSAObject
    {
        public string Type { get; set; }
        public string Grade { get; set; }

        public int LocalReference { get; set; }

        public GSAMaterial()
        {
            Type = "CONCRETE";
            Grade = "35MPa";

            LocalReference = 0;
        }

        #region GSAObject Functions
        public override void ParseGWACommand(string command, GSAObject[] children = null)
        {
            string[] pieces = command.ListSplit(",");

            int counter = 0;
            string identifier = pieces[counter++];
            LocalReference = Convert.ToInt32(pieces[counter++]);

            if (identifier.Contains("STEEL"))
            {
                Type = "STEEL";
                counter++; // Move to name field of basic MAT definition
            }
            else if (identifier.Contains("CONCRETE"))
            {
                Type = "CONCRETE";
                counter++; // Move to name field of basic MAT definition
            }
            else
                Type = "GENERAL";

            Grade = pieces[counter++].Trim(new char[] { '"' }); // Use name as grade

            // TODO: Skip all other properties for now
        }

        public override string GetGWACommand(GSAObject[] children = null)
        {
            List<string> ls = new List<string>();

            ls.Add("SET");
            switch (Type)
            {
                case "STEEL":
                    ls.Add("MAT_STEEL.3");
                    ls.Add(Reference.ToString());
                    ls.Add("MAT.6");
                    ls.Add(Grade);
                    break;
                case "CONCRETE":
                    ls.Add("MAT_CONCRETE.16");
                    ls.Add(Reference.ToString());
                    ls.Add("MAT.6");
                    ls.Add(Grade);
                    break;
                default:
                    ls.Add("MAT.6");
                    ls.Add(Reference.ToString());
                    ls.Add(Grade);
                    break;
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

        public override List<GSAObject> GetChildren()
        {
            throw new NotImplementedException();
        }
        #endregion

    }
}
