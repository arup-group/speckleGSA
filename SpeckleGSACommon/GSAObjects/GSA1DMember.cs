using Interop.Gsa_10_0;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace SpeckleGSA
{
    public class GSA1DMember : GSAObject
    {
        public static readonly string GSAKeyword = "MEMB";
        public static readonly string Stream = "elements";
        public static readonly int ReadPriority = 3;
        public static readonly int WritePriority = 1;

        public string Type { get; set; }
        public int Property { get; set; }
        public Dictionary<string, object> Axis { get; set; }
        public Dictionary<string, object> EndCondition { get; set; }
        public Dictionary<string, object> Offset { get; set; }
        
        public GSA1DMember()
        {
            Type = "GENERIC";
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
                    { "xx", 0 },
                    { "yy", 0 },
                    { "zz", 0 },
                }
                },
                { "end", new Dictionary<string,object>()
                {
                    { "x", "FIXED" },
                    { "y", "FIXED" },
                    { "z", "FIXED" },
                    { "xx", 0 },
                    { "yy", 0 },
                    { "zz", 0 },
                }
                },
            };
            Offset = new Dictionary<string, object>()
            {
                { "Vertical", 0 },
                { "Horizontal", 0 },
            };
        }

        #region GSAObject Functions
        public static void GetObjects(Dictionary<Type, object> dict)
        {
            if (!dict.ContainsKey(typeof(GSANode))) return;

            List<GSAObject> nodes = dict[typeof(GSANode)] as List<GSAObject>;
            List<GSAObject> m1Ds = new List<GSAObject>();

            // TODO: Workaround for GET_ALL,MEMB bug
            int[] memberRefs = new int[0];
            GSA.GSAObject.EntitiesInList("all", GsaEntity.MEMBER, out memberRefs);

            if (memberRefs.Length == 0)
                return;

            List<string> tempPieces = new List<string>();

            foreach (int r in memberRefs)
            {
                tempPieces.Add((string)GSA.RunGWACommand("GET,MEMB," + r.ToString()));
            }

            string[] pieces = tempPieces.ToArray();

            //string res = gsa.GwaCommand("GET_ALL,MEMB");

            //if (res == "")
            //    return;

            //string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            double counter = 1;
            foreach (string p in pieces)
            {
                string[] pPieces = p.ListSplit(",");
                if (pPieces[4].MemberIs1D())
                {
                    GSA1DMember m1D = new GSA1DMember();
                    m1D.ParseGWACommand(p, dict);
                    m1Ds.Add(m1D);
                }

                Status.ChangeStatus("Reading 1D members", counter++ / pieces.Length * 100);
            }

            dict[typeof(GSA1DMember)] = m1Ds;
        }

        public static void WriteObjects(Dictionary<Type, object> dict)
        {
            if (!dict.ContainsKey(typeof(GSA1DMember))) return;

            List<GSAObject> m1Ds = dict[typeof(GSA1DMember)] as List<GSAObject>;

            double counter = 1;
            foreach (GSAObject m in m1Ds)
            {
                GSARefCounters.RefObject(m);

                List<GSAObject> nodes = m.GetChildren();

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

                m.Connectivity = nodes.Select(n => n.Reference).ToList();

                GSA.RunGWACommand(m.GetGWACommand());
                Status.ChangeStatus("Writing 1D members", counter++ / m1Ds.Count() * 100);
            }
        }

        public override void ParseGWACommand(string command, Dictionary<Type, object> dict = null)
        {
            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier
            Reference = Convert.ToInt32(pieces[counter++]);
            Name = pieces[counter++].Trim(new char[] { '"' });
            Color = pieces[counter++].ParseGSAColor();
            Type = pieces[counter++];
            Type = Type == "1D_GENERIC" ? "GENERIC" : Type;
            Property = Convert.ToInt32(pieces[counter++]);
            counter++; // Group

            Connectivity.Clear();
            Coor.Clear();

            string[] nodeRefs = pieces[counter++].ListSplit(" ");
            List<GSAObject> nodes = dict[typeof(GSANode)] as List<GSAObject>;

            for (int i = 0; i < nodeRefs.Length; i++)
            {
                Connectivity.Add(Convert.ToInt32(nodeRefs[i]));
                Coor.AddRange(nodes.Where(n => n.Reference == Connectivity[i]).FirstOrDefault().Coor);
            }

            int orientationNodeRef = Convert.ToInt32(pieces[counter++]);
            double rotationAngle = Convert.ToDouble(pieces[counter++]);

            if (orientationNodeRef != 0)
                Axis = HelperFunctions.Parse1DAxis(Coor.ToArray(),
                    rotationAngle,
                    nodes.Where(n => n.Reference == orientationNodeRef).FirstOrDefault().Coor.ToArray());
            else
                Axis = HelperFunctions.Parse1DAxis(Coor.ToArray(), rotationAngle);
            
            // Skip to offsets at third to last
            counter = pieces.Length - 3;
            Offset["Horizontal"] = Convert.ToDouble(pieces[counter++]);
            Offset["Vertical"] = Convert.ToDouble(pieces[counter++]);
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
            ls.Add(Type == "GENERIC" ? "1D_GENERIC" : Type);
            ls.Add(Property.ToString());
            ls.Add("1"); // Group
            string topo = "";
            foreach (int c in Connectivity)
                topo += c.ToString() + " ";
            ls.Add(topo);
            ls.Add("0"); // Orientation node
            ls.Add(HelperFunctions.Get1DAngle(Axis).ToNumString());
            ls.Add("0"); // Target mesh size
            ls.Add("0"); // Fire
            ls.Add("0"); // Time 1
            ls.Add("0"); // Time 2
            ls.Add("0"); // Time 3
            ls.Add("0"); // TODO: What is this?
            ls.Add("ACTIVE"); // Dummy
            ls.Add("1"); // End 1 condition
            ls.Add("1"); // End 2 condition
            ls.Add("AUTOMATIC"); // Effective length option
            ls.Add("0"); // Pool
            ls.Add("0"); // Height
            ls.Add("MAN"); // Auto offset 1
            ls.Add("MAN"); // Auto offset 2
            ls.Add("0"); // Offset x 1
            ls.Add("0"); // Offset x 2
            ls.Add("0"); // Offset y
            ls.Add(Offset.ToNumString()); // Offset z
            ls.Add("ALL"); // Exposure

            return string.Join(",", ls);
        }

        public override List<GSAObject> GetChildren()
        {
            List<GSAObject> children = new List<GSAObject>();

            for (int i = 0; i < Coor.Count() / 3; i++)
            {
                GSANode n = new GSANode();
                n.Coor = Coor.Skip(i * 3).Take(3).ToList();
                if (Connectivity.Count() > i)
                    n.Reference = Connectivity[i];
                else
                    n.Reference = 0;
                children.Add(n);
            }

            return children;
        }
        #endregion
    }
}
