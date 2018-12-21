using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA
{
    public class GSA2DProperty : GSAObject
    {
        public double Thickness { get; set; }
        public Dictionary<string,object> Material { get; set; }

        public GSA2DProperty()
        {
            Thickness = 0;

            Material = new Dictionary<string, object>()
            {
                { "Type", "CONCRETE" },
                { "Grade", "35MPa" }
            };
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
            Material["Type"] = pieces[counter++];
            Material["Grade"] = pieces[counter++].ToString(); // TODO: GRADE REFERENCE
            counter++; // Design property
            Thickness = Convert.ToDouble(pieces[counter++]);

            // Ignore the rest
        }

        public override string GetGWACommand()
        {
            throw new NotImplementedException();
        }

        public override List<GSAObject> GetChildren()
        {
            throw new NotImplementedException();
        }
        #endregion

        //#region Grade
        //private string GetGSAGrade(int mat, string type)
        //{
        //    //string res;

        //    //switch (type)
        //    //{
        //    //    case "STEEL":
        //    //        res = (string)RunGWACommand("GET,MAT_STEEL," + mat);
        //    //        break;
        //    //    case "CONCRETE":
        //    //        res = (string)RunGWACommand("GET,MAT_CONCRETE," + mat);
        //    //        break;
        //    //    case "FRP":
        //    //        res = (string)
        //    //}

        //    //string res = (string)RunGWACommand("GET,PROP_2D," + prop);

        //    //if (res == null || res == "")
        //    //    return null;

        //    //string[] pieces = res.ListSplit(",");

        //    //return pieces[5] == "LOCAL";

        //}
        //#endregion
    }
}
