using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using Interop.Gsa_10_0;

namespace SpeckleGSA
{
    public class GSA1DElement : GSAObject
    {
        public override string Entity { get => "1D Element"; set { } }

        public static readonly string GSAKeyword  = "EL";
        public static readonly string Stream = "elements";
        public static readonly int WritePriority = 1;

        public static readonly Type[] ReadPrerequisite = new Type[1] { typeof(GSANode) };

        public string Type { get; set; }
        public int Property { get; set; }
        public Dictionary<string, object> Axis { get; set; }
        public Dictionary<string, object> EndCondition { get; set; }
        public Dictionary<string, object> Offset { get; set; }

        public List<int> Connectivity;

        public GSA1DElement()
        {
            Type = "BEAM";
            Property = 1;
            Axis = new Dictionary<string, object>()
            {
                { "X", new Dictionary<string, object> { { "x", 1 }, { "y", 0 },{ "z", 0 }  } },
                { "Y", new Dictionary<string, object> { { "x", 0 }, { "y", 1 },{ "z", 0 }  } },
                { "Z", new Dictionary<string, object> { { "x", 0 }, { "y", 0 },{ "z", 1 }  } },
            };
            EndCondition = new Dictionary<string, object>()
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
            Offset = new Dictionary<string, object>()
            {
                { "Vertical", 0 },
                { "Horizontal", 0 },
            };

            Connectivity = new List<int>();
        }

        #region GSAObject Functions
        public static void GetObjects(Dictionary<Type, object> dict)
        {
            if (!GSA.TargetAnalysisLayer) return;

            if (!dict.ContainsKey(typeof(GSANode))) return;

            List<GSAObject> nodes = dict[typeof(GSANode)] as List<GSAObject>;
            List<GSAObject> e1Ds = new List<GSAObject>();

            string res = (string)GSA.RunGWACommand("GET_ALL,EL");

            if (res == "")
                return;

            string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            double counter = 1;
            foreach (string p in pieces)
            {
                string[] pPieces = p.ListSplit(",");
                if (pPieces[4].ParseElementNumNodes() == 2)
                {
                    GSA1DElement e1D = new GSA1DElement();
                    e1D.ParseGWACommand(p, dict);
                    e1Ds.Add(e1D);
                }
                Status.ChangeStatus("Reading 1D elements", counter++ / pieces.Length * 100);
            }

            dict[typeof(GSA1DElement)] = e1Ds;
        }

        public static void WriteObjects(Dictionary<Type, object> dict)
        {
            if (!dict.ContainsKey(typeof(GSA1DElement))) return;

            List<GSAObject> e1Ds = dict[typeof(GSA1DElement)] as List<GSAObject>;

            double counter = 1;
            foreach (GSAObject e in e1Ds)
            {
                GSARefCounters.RefObject(e);

                List<GSAObject> nodes = e.GetChildren();

                if (dict.ContainsKey(typeof(GSANode)))
                { 
                    for (int i = 0; i < nodes.Count(); i++)
                    {
                        List<GSAObject> matches = (dict[typeof(GSANode)] as List<GSAObject>).Where(
                            n => (n as GSANode).IsCoincident(nodes[i] as GSANode)).ToList();

                        if (matches.Count() > 0)
                        { 
                            if (matches[0].Reference == 0)
                                GSARefCounters.RefObject(matches[0]);

                            nodes[i].Reference = matches[0].Reference;
                            (matches[0] as GSANode).Merge(nodes[i] as GSANode);
                        }
                        else
                        {
                            GSARefCounters.RefObject(nodes[i]);
                            (dict[typeof(GSANode)] as List<GSAObject>).Add(nodes[i]);
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < nodes.Count(); i++)
                        GSARefCounters.RefObject(nodes[i]);

                    dict[typeof(GSANode)] = nodes;
                }

                (e as GSA1DElement).Connectivity = nodes.Select(n => n.Reference).ToList();

                GSA.RunGWACommand(e.GetGWACommand());

                Status.ChangeStatus("Writing 1D elements", counter++ / e1Ds.Count() * 100);
            }

            dict.Remove(typeof(GSA1DElement));
        }

        public override void ParseGWACommand(string command, Dictionary<Type, object> dict = null)
        {
            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier
            Reference = Convert.ToInt32(pieces[counter++]);
            Name = pieces[counter++].Trim(new char[] { '"' });
            Color = pieces[counter++].ParseGSAColor();
            Type = pieces[counter++];
            Property = Convert.ToInt32(pieces[counter++]);
            counter++; // Group
            
            Coor.Clear();

            List<GSAObject> nodes = dict[typeof(GSANode)] as List<GSAObject>;
            for (int i = 0; i < 2; i++)
            {
                int key = Convert.ToInt32(pieces[counter++]);
                Coor.AddRange(nodes.Where(n => n.Reference == key).FirstOrDefault().Coor);
            }

            int orientationNodeRef = Convert.ToInt32(pieces[counter++]);
            double rotationAngle = Convert.ToDouble(pieces[counter++]);

            if (orientationNodeRef != 0)
                Axis = HelperFunctions.Parse1DAxis(Coor.ToArray(),
                    rotationAngle,
                    nodes.Where(n => n.Reference == orientationNodeRef).FirstOrDefault().Coor.ToArray());
            else
                Axis = HelperFunctions.Parse1DAxis(Coor.ToArray(), rotationAngle);


            if (pieces[counter++] != "NO_RLS")
            {
                string start = pieces[counter++];
                string end = pieces[counter++];
                (EndCondition["start"] as Dictionary<string, object>)["x"] = ParseEndStiffness(start[0], pieces, ref counter);
                (EndCondition["start"] as Dictionary<string, object>)["y"] = ParseEndStiffness(start[1], pieces, ref counter);
                (EndCondition["start"] as Dictionary<string, object>)["z"] = ParseEndStiffness(start[2], pieces, ref counter);
                (EndCondition["start"] as Dictionary<string, object>)["xx"] = ParseEndStiffness(start[3], pieces, ref counter);
                (EndCondition["start"] as Dictionary<string, object>)["yy"] = ParseEndStiffness(start[4], pieces, ref counter);
                (EndCondition["start"] as Dictionary<string, object>)["zz"] = ParseEndStiffness(start[5], pieces, ref counter);
                (EndCondition["end"] as Dictionary<string, object>)["x"] = ParseEndStiffness(end[0], pieces, ref counter);
                (EndCondition["end"] as Dictionary<string, object>)["y"] = ParseEndStiffness(end[1], pieces, ref counter);
                (EndCondition["end"] as Dictionary<string, object>)["z"] = ParseEndStiffness(end[2], pieces, ref counter);
                (EndCondition["end"] as Dictionary<string, object>)["xx"] = ParseEndStiffness(end[3], pieces, ref counter);
                (EndCondition["end"] as Dictionary<string, object>)["yy"] = ParseEndStiffness(end[4], pieces, ref counter);
                (EndCondition["end"] as Dictionary<string, object>)["zz"] = ParseEndStiffness(end[5], pieces, ref counter);
            }

            counter++; // offset x-start
            counter++; // offset x-end

            Offset["Horizontal"] = Convert.ToDouble(pieces[counter++]);
            Offset["Vertical"] = Convert.ToDouble(pieces[counter++]);

            //counter++; // Action // TODO: EL.4 SUPPORT
            counter++; // Dummy
        }

        public override string GetGWACommand(Dictionary<Type, object> dict = null)
        {
            List<string> ls = new List<string>();

            ls.Add("SET");
            ls.Add(GSAKeyword);
            ls.Add(Reference.ToString());
            ls.Add(Name);
            if (Color == null)
                ls.Add("NO_RGB");
            else
                ls.Add(Color.ToNumString());
            ls.Add(Type);
            ls.Add(Property.ToString());
            ls.Add("0"); // Group
            foreach (int c in Connectivity)
                ls.Add(c.ToString());

            ls.Add("0"); // Orientation Node
            ls.Add(HelperFunctions.Get1DAngle(Axis).ToString());

            if (Coor.Count() / 3 == 2)
            {
                ls.Add("RLS");

                string start = "";
                string end = "";
                List<double> stiffness = new List<double>();

                start += GetEndStiffness((EndCondition["start"] as Dictionary<string, object>)["x"], ref stiffness);
                start += GetEndStiffness((EndCondition["start"] as Dictionary<string, object>)["y"], ref stiffness);
                start += GetEndStiffness((EndCondition["start"] as Dictionary<string, object>)["z"], ref stiffness);
                start += GetEndStiffness((EndCondition["start"] as Dictionary<string, object>)["xx"], ref stiffness);
                start += GetEndStiffness((EndCondition["start"] as Dictionary<string, object>)["yy"], ref stiffness);
                start += GetEndStiffness((EndCondition["start"] as Dictionary<string, object>)["zz"], ref stiffness);

                end += GetEndStiffness((EndCondition["end"] as Dictionary<string, object>)["x"], ref stiffness);
                end += GetEndStiffness((EndCondition["end"] as Dictionary<string, object>)["y"], ref stiffness);
                end += GetEndStiffness((EndCondition["end"] as Dictionary<string, object>)["z"], ref stiffness);
                end += GetEndStiffness((EndCondition["end"] as Dictionary<string, object>)["xx"], ref stiffness);
                end += GetEndStiffness((EndCondition["end"] as Dictionary<string, object>)["yy"], ref stiffness);
                end += GetEndStiffness((EndCondition["end"] as Dictionary<string, object>)["zz"], ref stiffness);

                ls.Add(start);
                ls.Add(end);

                foreach (double d in stiffness)
                    ls.Add(d.ToString());
            }
            else
                ls.Add("NO_RLS");

            ls.Add("0"); // Offset x-start
            ls.Add("0"); // Offset x-end

            ls.Add(Offset["Horizontal"].ToNumString());
            ls.Add(Offset["Vertical"].ToNumString());

            //ls.Add("NORMAL"); // Action // TODO: EL.4 SUPPORT
            ls.Add(""); // Dummy

            return string.Join(",", ls);
        }

        public override List<GSAObject> GetChildren()
        {
            List<GSAObject> children = new List<GSAObject>();

            for (int i = 0; i < Coor.Count() / 3; i++)
            {
                GSANode n = new GSANode();
                n.Coor = Coor.Skip(i * 3).Take(3).ToList();
                n.Reference = 0;
                children.Add(n);
            }

            return children;
        }
        #endregion

        #region Helper Functions
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
        #endregion

    }
}
