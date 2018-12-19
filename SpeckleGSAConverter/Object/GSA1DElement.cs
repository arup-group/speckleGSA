using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA
{
    public class GSA1DElement : GSAObject
    {
        public string Type { get; set; }
        public int Property { get; set; }
        public Dictionary<string, object> Axis { get; set; }
        public Dictionary<string, object> Stiffness { get; set; }
        public Dictionary<string, object> InsertionPoint { get; set; }

        public int Group;
        public string Action;
        public bool Dummy;
        public double[] EndOffsetX;

        public GSA1DElement() : base("ELEMENT")
        {
            Type = "BEAM";
            Property = 1;
            Axis = new Dictionary<string, object>()
            {
                { "X", new Dictionary<string, object> { { "x", 1 }, { "y", 0 },{ "z", 0 }  } },
                { "Y", new Dictionary<string, object> { { "x", 0 }, { "y", 1 },{ "z", 0 }  } },
                { "Z", new Dictionary<string, object> { { "x", 0 }, { "y", 0 },{ "z", 1 }  } },
            };
            Stiffness = new Dictionary<string, object>()
            {
                { "start", new Dictionary<string,object>()
                {
                    { "x", "FIXED" },
                    { "y", "FIXED" },
                    { "z", "FIXED" },
                    { "xx", "FIXED" },
                    { "yy", "FIXED" },
                    { "zz", "FIXED" },
                }
                },
                { "end", new Dictionary<string,object>()
                {
                    { "x", "FIXED" },
                    { "y", "FIXED" },
                    { "z", "FIXED" },
                    { "xx", "FIXED" },
                    { "yy", "FIXED" },
                    { "zz", "FIXED" },
                }
                },
            };
            InsertionPoint = new Dictionary<string, object>()
            {
                { "Vertical", "MID" },
                { "Horizontal", "MID" },
            };

            Group = 0;
            Action = "NORMAL";
            Dummy = false;
            EndOffsetX = new double[2];
        }

        public override void ParseGWACommand(string command)
        {
            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier
            Reference = Convert.ToInt32(pieces[counter++]);
            Name = pieces[counter++].Trim(new char[] { '"' });
            Color = pieces[counter++].ParseGSAColor();
            Type = pieces[counter++];
            Property = Convert.ToInt32(pieces[counter++]);
            Group = Convert.ToInt32(pieces[counter++]);

            Connectivity = new int[2];
            List<double> tempCoor = new List<double>();
            for (int i = 0; i < Connectivity.Length; i++)
            {
                Connectivity[i] = Convert.ToInt32(pieces[counter++]);
                tempCoor.AddRange(Connectivity[i].NodeCoor(gsa));
            }
            Coor = tempCoor.ToArray();

            double[] orientationNode = Convert.ToInt32(pieces[counter++]).NodeCoor(gsa);
            double rotationAngle = Convert.ToDouble(pieces[counter++]);
            Axis = Coor.EvaluateGSA1DElementAxis(gsa, rotationAngle, orientationNode);

            if (pieces[counter++] != "NO_RLS")
            {
                string start = pieces[counter++];
                string end = pieces[counter++];
                (Stiffness["start"] as Dictionary<string, object>)["x"] = ParseEndStiffness(start[0], pieces, ref counter);
                (Stiffness["start"] as Dictionary<string, object>)["y"] = ParseEndStiffness(start[1], pieces, ref counter);
                (Stiffness["start"] as Dictionary<string, object>)["z"] = ParseEndStiffness(start[2], pieces, ref counter);
                (Stiffness["start"] as Dictionary<string, object>)["xx"] = ParseEndStiffness(start[3], pieces, ref counter);
                (Stiffness["start"] as Dictionary<string, object>)["yy"] = ParseEndStiffness(start[4], pieces, ref counter);
                (Stiffness["start"] as Dictionary<string, object>)["zz"] = ParseEndStiffness(start[5], pieces, ref counter);
                (Stiffness["end"] as Dictionary<string, object>)["x"] = ParseEndStiffness(end[0], pieces, ref counter);
                (Stiffness["end"] as Dictionary<string, object>)["y"] = ParseEndStiffness(end[1], pieces, ref counter);
                (Stiffness["end"] as Dictionary<string, object>)["z"] = ParseEndStiffness(end[2], pieces, ref counter);
                (Stiffness["end"] as Dictionary<string, object>)["xx"] = ParseEndStiffness(end[3], pieces, ref counter);
                (Stiffness["end"] as Dictionary<string, object>)["yy"] = ParseEndStiffness(end[4], pieces, ref counter);
                (Stiffness["end"] as Dictionary<string, object>)["zz"] = ParseEndStiffness(end[5], pieces, ref counter);
            }

            EndOffsetX = new double[2];

            EndOffsetX[0] = Convert.ToDouble(pieces[counter++]);
            EndOffsetX[1] = Convert.ToDouble(pieces[counter++]);

            InsertionPoint["Horizontal"] = Convert.ToDouble(pieces[counter++]);
            InsertionPoint["Vertical"] = Convert.ToDouble(pieces[counter++]);

            Action = pieces[counter++];
            Dummy = pieces[counter++] == "DUMMY";
        }

        public override string GetGWACommand()
        {
            List<string> ls = new List<string>();

            ls.Add("SET");
            ls.Add("EL.4");
            ls.Add(Reference.ToString());
            ls.Add(Name);
            if (Color == null)
                ls.Add("NO_RGB");
            else
                ls.Add(Color.ToNumString());
            ls.Add(Type);
            ls.Add(Property.ToString());
            ls.Add(Group.ToString());
            foreach (int c in Connectivity)
                ls.Add(c.ToString());

            ls.Add("0"); // Orientation Node
            ls.Add(Axis.GetGSA1DElementAngle(gsa).ToString());

            if (Coor.Length / 3 == 2)
            {
                ls.Add("RLS");

                string start = "";
                string end = "";
                List<double> stiffness = new List<double>();

                start += GetEndStiffness((Stiffness["start"] as Dictionary<string, object>)["x"], ref stiffness);
                start += GetEndStiffness((Stiffness["start"] as Dictionary<string, object>)["y"], ref stiffness);
                start += GetEndStiffness((Stiffness["start"] as Dictionary<string, object>)["z"], ref stiffness);
                start += GetEndStiffness((Stiffness["start"] as Dictionary<string, object>)["xx"], ref stiffness);
                start += GetEndStiffness((Stiffness["start"] as Dictionary<string, object>)["yy"], ref stiffness);
                start += GetEndStiffness((Stiffness["start"] as Dictionary<string, object>)["zz"], ref stiffness);

                end += GetEndStiffness((Stiffness["end"] as Dictionary<string, object>)["x"], ref stiffness);
                end += GetEndStiffness((Stiffness["end"] as Dictionary<string, object>)["y"], ref stiffness);
                end += GetEndStiffness((Stiffness["end"] as Dictionary<string, object>)["z"], ref stiffness);
                end += GetEndStiffness((Stiffness["end"] as Dictionary<string, object>)["xx"], ref stiffness);
                end += GetEndStiffness((Stiffness["end"] as Dictionary<string, object>)["yy"], ref stiffness);
                end += GetEndStiffness((Stiffness["end"] as Dictionary<string, object>)["zz"], ref stiffness);

                ls.Add(start);
                ls.Add(end);

                foreach (double d in stiffness)
                    ls.Add(d.ToString());
            }
            else
                ls.Add("NO_RLS");

            ls.Add(EndOffsetX[0].ToNumString());
            ls.Add(EndOffsetX[1].ToNumString());

            ls.Add(InsertionPoint["Horizontal"].ToNumString());
            ls.Add(InsertionPoint["Vertical"].ToNumString());

            ls.Add(Action);
            ls.Add(Dummy ? "DUMMY" : "");

            return string.Join(",", ls);
        }

        public override List<GSAObject> GetChildren()
        {
            List<GSAObject> children = new List<GSAObject>();

            for (int i = 0; i < Coor.Length / 3; i++)
            {
                GSANode n = new GSANode();
                n.Coor = Coor.Skip(i * 3).Take(3).ToArray();
                children.Add(n);
            }

            return children;
        }

        private object ParseEndStiffness(char code, string[] pieces, ref int counter)
        {
            switch (code)
            {
                case 'F':
                    return "FIXED";
                case 'R':
                    return 0;
                default:
                    return Convert.ToDouble(pieces[counter++]);
            }
        }

        private string GetEndStiffness(object code, ref List<double> stiffness)
        {
            if (code.GetType() == typeof(string)) return "F";
            else if ((double)code == 0) return "R";
            else
            {
                stiffness.Add((double)code);
                return "K";
            }
        }
    }
}
