using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA
{
    public class GSAElement : GSAObject
    {
        public string Type { get; set; }
        public int Property { get; set; }
        public int Group { get; set; }
        public double Beta { get; set; }
        public int OrientNode { get; set; }
        public Dictionary<string, object> EndStiffness { get; set; }
        public Dictionary<string, object> EndOffset { get; set; }
        public bool Dummy { get; set; }

        public GSAElement() : base("ELEMENT")
        {
            Type = "BEAM";
            Property = 1;
            Group = 0;
            Beta = 0;
            OrientNode = 0;
            EndStiffness = new Dictionary<string, object>()
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
            EndOffset = new Dictionary<string, object>()
            {
                { "x-start", 0.0 },
                { "x-end", 0.0 },
                { "y", 0.0 },
                { "z", 0.0 },
            };
            Dummy = false;
        }

        public override void ParseGWACommand(string command)
        {
            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier
            Ref = Convert.ToInt32(pieces[counter++]);
            Name = pieces[counter++].Trim(new char[] { '"' });
            SpeckleID = Name;
            Color = pieces[counter++].ParseGSAColor();
            Type = pieces[counter++];
            Property = Convert.ToInt32(pieces[counter++]);
            Group = Convert.ToInt32(pieces[counter++]);
            Connectivity = new int[(int)Enum.Parse(typeof(ElementNumNodes), Type)];
            for (int i = 0; i < Connectivity.Length; i++)
                Connectivity[i] = Convert.ToInt32(pieces[counter++]);
            OrientNode = Convert.ToInt32(pieces[counter++]);
            Beta = Convert.ToDouble(pieces[counter++]);
            if (pieces[counter++] != "NO_RLS")
            {
                string start = pieces[counter++];
                string end = pieces[counter++];
                (EndStiffness["start"] as Dictionary<string, object>)["x"] = ParseEndStiffness(start[0], pieces, ref counter);
                (EndStiffness["start"] as Dictionary<string, object>)["y"] = ParseEndStiffness(start[1], pieces, ref counter);
                (EndStiffness["start"] as Dictionary<string, object>)["z"] = ParseEndStiffness(start[2], pieces, ref counter);
                (EndStiffness["start"] as Dictionary<string, object>)["xx"] = ParseEndStiffness(start[3], pieces, ref counter);
                (EndStiffness["start"] as Dictionary<string, object>)["yy"] = ParseEndStiffness(start[4], pieces, ref counter);
                (EndStiffness["start"] as Dictionary<string, object>)["zz"] = ParseEndStiffness(start[5], pieces, ref counter);
                (EndStiffness["end"] as Dictionary<string, object>)["x"] = ParseEndStiffness(end[0], pieces, ref counter);
                (EndStiffness["end"] as Dictionary<string, object>)["y"] = ParseEndStiffness(end[1], pieces, ref counter);
                (EndStiffness["end"] as Dictionary<string, object>)["z"] = ParseEndStiffness(end[2], pieces, ref counter);
                (EndStiffness["end"] as Dictionary<string, object>)["xx"] = ParseEndStiffness(end[3], pieces, ref counter);
                (EndStiffness["end"] as Dictionary<string, object>)["yy"] = ParseEndStiffness(end[4], pieces, ref counter);
                (EndStiffness["end"] as Dictionary<string, object>)["zz"] = ParseEndStiffness(end[5], pieces, ref counter);
            }
            EndOffset["x-start"]= Convert.ToDouble(pieces[counter++]);
            EndOffset["x-end"] = Convert.ToDouble(pieces[counter++]);
            EndOffset["y"] = Convert.ToDouble(pieces[counter++]);
            EndOffset["z"] = Convert.ToDouble(pieces[counter++]);
            Dummy = pieces[counter++] == "DUMMY";
        }

        public override string GetGWACommand()
        {
            List<string> ls = new List<string>();

            ls.Add("SET");
            ls.Add("EL.3");
            ls.Add(Ref.ToString());
            ls.Add(Name);
            if (Color == null)
                ls.Add("NO_RGB");
            else
                ls.Add(((int)Color).ToString());
            ls.Add(Type);
            ls.Add(Property.ToString());
            ls.Add(Group.ToString());
            foreach (int c in Connectivity)
                ls.Add(c.ToString());
            ls.Add(OrientNode.ToString());
            ls.Add(Beta.ToString());
            if (Coor.Length / 3 == 2)
            {
                ls.Add("RLS");

                string start = "";
                string end = "";
                List<double> stiffness = new List<double>();

                start += GetEndStiffness((EndStiffness["start"] as Dictionary<string, object>)["x"], ref stiffness);
                start += GetEndStiffness((EndStiffness["start"] as Dictionary<string, object>)["y"], ref stiffness);
                start += GetEndStiffness((EndStiffness["start"] as Dictionary<string, object>)["z"], ref stiffness);
                start += GetEndStiffness((EndStiffness["start"] as Dictionary<string, object>)["xx"], ref stiffness);
                start += GetEndStiffness((EndStiffness["start"] as Dictionary<string, object>)["yy"], ref stiffness);
                start += GetEndStiffness((EndStiffness["start"] as Dictionary<string, object>)["zz"], ref stiffness);

                end += GetEndStiffness((EndStiffness["end"] as Dictionary<string, object>)["x"], ref stiffness);
                end += GetEndStiffness((EndStiffness["end"] as Dictionary<string, object>)["y"], ref stiffness);
                end += GetEndStiffness((EndStiffness["end"] as Dictionary<string, object>)["z"], ref stiffness);
                end += GetEndStiffness((EndStiffness["end"] as Dictionary<string, object>)["xx"], ref stiffness);
                end += GetEndStiffness((EndStiffness["end"] as Dictionary<string, object>)["yy"], ref stiffness);
                end += GetEndStiffness((EndStiffness["end"] as Dictionary<string, object>)["zz"], ref stiffness);

                ls.Add(start);
                ls.Add(end);

                foreach (double d in stiffness)
                    ls.Add(d.ToString());
            }
            else
                ls.Add("NO_RLS");

            ls.Add(((double)EndOffset["x-start"]).ToString());
            ls.Add(((double)EndOffset["x-end"]).ToString());
            ls.Add(((double)EndOffset["y"]).ToString());
            ls.Add(((double)EndOffset["z"]).ToString());

            ls.Add(Dummy ? "DUMMY" : "");
            Console.WriteLine(string.Join(",", ls));
            return string.Join(",", ls);
        }

        public override List<GSAObject> GetChildren()
        {
            List<GSAObject> children = new List<GSAObject>();

            for (int i = 0; i < Coor.Length/3; i++)
            {
                GSANode n = new GSANode();
                n.Coor = Coor.Skip(i * 3).Take(3).ToArray();
                children.Add(n);
            }

            return children;
        }

        private object ParseEndStiffness(char code, string[] pieces, ref int counter)
        {
            switch(code)
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
