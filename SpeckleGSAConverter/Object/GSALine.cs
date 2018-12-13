using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA
{
    public class GSALine : GSAObject
    {
        public string Type { get; set; }
        public double Radius { get; set; }
        public string Axis { get; set; }
        public Dictionary<string, object> Restraint { get; set; }
        public Dictionary<string, object> Stiffness { get; set; }
        public Dictionary<string, object> MeshData { get; set; }
        
        public GSALine():base("LINE")
        {
            Type = "LINE";
            Radius = 0;
            Axis = "GLOBAL";
            Restraint = new Dictionary<string, object>()
            {
                { "x", false },
                { "y", false },
                { "z", false },
                { "xx", false },
                { "yy", false },
                { "zz", false },
            };
            Stiffness = new Dictionary<string, object>()
            {
                { "x", 0.0 },
                { "y", 0.0 },
                { "z", 0.0 },
                { "xx", 0.0 },
                { "yy", 0.0 },
                { "zz", 0.0 },
            };
            MeshData = new Dictionary<string, object>()
            {
                {"StepDefinition", "USE_REGION_STEP_SIZE" },
                {"ElemLenStart", 0.0 },
                {"ElemLenEnd", 0.0 },
                {"ElemNum", 0.0 },
                {"TieMesh", false },
            };
        }

        public override void ParseGWACommand(string command)
        {
            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier
            Reference = Convert.ToInt32(pieces[counter++]);
            Name = pieces[counter++].Trim(new char[] { '"' });
            Color = pieces[counter++].ParseGSAColor();
            Type = pieces[counter++];
            Connectivity = new int[(int)Enum.Parse(typeof(LineNumNodes), Type)];
            for (int i = 0; i < Connectivity.Length; i++)
                Connectivity[i] = Convert.ToInt32(pieces[counter++]);
            if ((int)Enum.Parse(typeof(LineNumNodes), Type) == 2) counter++;
            Radius = Convert.ToDouble(pieces[counter++]);
            Axis = pieces[counter++];
            Restraint["x"] = pieces[counter++] == "0" ? false: true;
            Restraint["y"] = pieces[counter++] == "0" ? false : true;
            Restraint["z"] = pieces[counter++] == "0" ? false : true;
            Restraint["xx"] = pieces[counter++] == "0" ? false : true;
            Restraint["yy"] = pieces[counter++] == "0" ? false : true;
            Restraint["zz"] = pieces[counter++] == "0" ? false : true;
            Stiffness["x"] = Convert.ToDouble(pieces[counter++]);
            Stiffness["y"] = Convert.ToDouble(pieces[counter++]);
            Stiffness["z"] = Convert.ToDouble(pieces[counter++]);
            Stiffness["xx"] = Convert.ToDouble(pieces[counter++]);
            Stiffness["yy"] = Convert.ToDouble(pieces[counter++]);
            Stiffness["zz"] = Convert.ToDouble(pieces[counter++]);
            counter++; // Assume CM2_MESHTOOLS
            MeshData["StepDefinition"] = pieces[counter++];
            MeshData["ElemLenStart"] = Convert.ToDouble(pieces[counter++]);
            MeshData["ElemLenEnd"] = Convert.ToDouble(pieces[counter++]);
            MeshData["ElemNum"] = Convert.ToDouble(pieces[counter++]);
            MeshData["TieMesh"] = pieces[counter++] == "NO" ? false : true;
        }

        public override string GetGWACommand()
        {
            List<string> ls = new List<string>();

            ls.Add("SET");
            ls.Add("LINE.3");
            ls.Add(Reference.ToString());
            ls.Add(Name);
            if (Color == null)
                ls.Add("NO_RGB");
            else
                ls.Add(((int)Color).ToString());
            ls.Add(Type);
            ls.Add(Connectivity[0].ToString());
            ls.Add(Connectivity[1].ToString());
            ls.Add(Connectivity.Length == 2 ? "0" : Connectivity[2].ToString());
            ls.Add(Radius.ToString());
            ls.Add(Axis);
            ls.Add(((bool)Restraint["x"])? "1" : "0");
            ls.Add(((bool)Restraint["y"]) ? "1" : "0");
            ls.Add(((bool)Restraint["z"]) ? "1" : "0");
            ls.Add(((bool)Restraint["xx"]) ? "1" : "0");
            ls.Add(((bool)Restraint["yy"]) ? "1" : "0");
            ls.Add(((bool)Restraint["zz"]) ? "1" : "0");
            ls.Add(((double)Stiffness["x"]).ToString());
            ls.Add(((double)Stiffness["y"]).ToString());
            ls.Add(((double)Stiffness["z"]).ToString());
            ls.Add(((double)Stiffness["xx"]).ToString());
            ls.Add(((double)Stiffness["yy"]).ToString());
            ls.Add(((double)Stiffness["zz"]).ToString());
            ls.Add("CM2_MESHTOOLS");

            ls.Add((string)MeshData["StepDefinition"]);
            ls.Add(((double)MeshData["ElemLenStart"]).ToString());
            ls.Add(((double)MeshData["ElemLenEnd"]).ToString());
            ls.Add(((double)MeshData["ElemNum"]).ToString());
            ls.Add(((bool)MeshData["TieMesh"]) == false ? "NO" : "YES");

            return string.Join(",", ls);
        }

        public  override List<GSAObject> GetChildren()
        {
            List<GSAObject> children = new List<GSAObject>();

            for (int i = 0; i < (int)Enum.Parse(typeof(LineNumNodes), Type); i++)
            {
                GSANode n = new GSANode();
                n.Coor = Coor.Skip(i * 3).Take(3).ToArray();
                children.Add(n);
            }

            return children;
        }
    }
}
