using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using Interop.Gsa_10_0;

namespace SpeckleGSA
{
    public class GSA2DElement : GSAObject
    {
        public override string Entity { get => "2D Element"; set { } }
        
        public static readonly string GSAKeyword = "EL";
        public static readonly string Stream = "elements";
        public static readonly int ReadPriority = 3;
        public static readonly int WritePriority = 1;

        public string Type { get; set; }
        public int Property { get; set; }
        public Dictionary<string, object> Axis { get; set; }
        public double Offset { get; set; }

        public int MeshReference;

        public GSA2DElement()
        {
            Type = "QUAD4";
            Property = 1;
            Axis = new Dictionary<string, object>()
            {
                { "X", new Dictionary<string, object> { { "x", 1 }, { "y", 0 },{ "z", 0 }  } },
                { "Y", new Dictionary<string, object> { { "x", 0 }, { "y", 1 },{ "z", 0 }  } },
                { "Z", new Dictionary<string, object> { { "x", 0 }, { "y", 0 },{ "z", 1 }  } },
            };
            Offset = 0;

            MeshReference = 1;
        }

        #region GSAObject Functions
        public static void GetObjects(Dictionary<Type, object> dict)
        {
            if (!dict.ContainsKey(typeof(GSANode))) return;

            List<GSAObject> nodes = dict[typeof(GSANode)] as List<GSAObject>;
            List<GSAObject> e2Ds = new List<GSAObject>();

            string res = (string)GSA.RunGWACommand("GET_ALL,EL");

            if (res == "")
                return;

            string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            double counter = 1;
            foreach (string p in pieces)
            {
                string[] pPieces = p.ListSplit(",");
                int numConnectivity = pPieces[4].ParseElementNumNodes();
                if (pPieces[4].ParseElementNumNodes() >= 3)
                {
                    GSA2DElement e2D = new GSA2DElement();
                    e2D.ParseGWACommand(p, dict);

                    e2Ds.Add(e2D);
                }
                Status.ChangeStatus("Reading 2D elements", counter++ / pieces.Length * 100);
            }

            dict[typeof(GSA2DElement)] = e2Ds;
        }

        public static void WriteObjects(Dictionary<Type, object> dict)
        {
            if (!dict.ContainsKey(typeof(GSA2DElement))) return;

            List<GSAObject> e2Ds = dict[typeof(GSA2DElement)] as List<GSAObject>;

            double counter = 1;
            foreach (GSAObject e in e2Ds)
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

                e.Connectivity = nodes.Select(n => n.Reference).ToList();

                GSA.RunGWACommand(e.GetGWACommand());
                Status.ChangeStatus("Writing 2D elements", counter++ / e2Ds.Count() * 100);
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
            Property = Convert.ToInt32(pieces[counter++]);
            counter++; // Group

            Connectivity.Clear();
            Coor.Clear();

            List<GSAObject> nodes = dict[typeof(GSANode)] as List<GSAObject>;
            for (int i = 0; i < Type.ParseElementNumNodes(); i++)
            { 
                Connectivity.Add(Convert.ToInt32(pieces[counter++]));
                Coor.AddRange(nodes.Where(n => n.Reference == Connectivity[i]).FirstOrDefault().Coor);
            }

            counter++; // Orientation node

            if (dict.ContainsKey(typeof(GSA2DProperty)))
            { 
                List<GSAObject> props = dict[typeof(GSA2DProperty)] as List<GSAObject>;
                GSAObject prop = props.Where(p => p.Reference == Property).FirstOrDefault();
                Axis = HelperFunctions.Parse2DAxis(Coor.ToArray(),
                    Convert.ToDouble(pieces[counter++]),
                    prop == null ? false : (prop as GSA2DProperty).IsAxisLocal);
            }
            else
            {
                Axis = HelperFunctions.Parse2DAxis(Coor.ToArray(),
                    Convert.ToDouble(pieces[counter++]),
                    false);
            }

            if (pieces[counter++] != "NO_RLS")
            {
                string start = pieces[counter++];
                string end = pieces[counter++];

                counter += start.Split('K').Length - 1 + end.Split('K').Length - 1;
            }
            
            counter++; //Ofsset x-start
            counter++; //Ofsset x-end
            counter++; //Ofsset y

            Offset = GetGSATotalElementOffset(Property,Convert.ToDouble(pieces[counter++]));

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
            ls.Add("0"); //Orientation node
            ls.Add(HelperFunctions.Get2DAngle(Coor.ToArray(),Axis).ToNumString());
            ls.Add("NO_RLS");

            ls.Add("0"); // Offset x-start
            ls.Add("0"); // Offset x-end
            ls.Add("0"); // Offset y
            ls.Add(Offset.ToNumString());

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
                if (Connectivity.Count() > i)
                    n.Reference = Connectivity[i];
                else
                    n.Reference = 0;
                children.Add(n);
            }

            return children;
        }
        #endregion

        #region Offset
        private double GetGSATotalElementOffset(int prop, double insertionPointOffset)
        {
            double materialInsertionPointOffset = 0;
            double zMaterialOffset = 0;
            double materialThickness = 0;

            string res = (string)GSA.RunGWACommand("GET,PROP_2D," + prop);

            if (res == null || res == "")
                return insertionPointOffset;

            string[] pieces = res.ListSplit(",");

            materialThickness = Convert.ToDouble(pieces[10]);
            switch (pieces[11])
            {
                case "TOP_CENTRE":
                    materialInsertionPointOffset = -materialThickness / 2;
                    break;
                case "BOT_CENTRE":
                    materialInsertionPointOffset = materialThickness / 2;
                    break;
                default:
                    materialInsertionPointOffset = 0;
                    break;
            }

            zMaterialOffset = -Convert.ToDouble(pieces[12]);
            return insertionPointOffset + zMaterialOffset + materialInsertionPointOffset;
        }
        #endregion
    }
}
