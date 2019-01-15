using Interop.Gsa_10_0;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace SpeckleGSA
{
    public class GSA2DMember : GSAObject
    {
        public static readonly string GSAKeyword = "MEMB";
        public static readonly string Stream = "elements";
        public static readonly int ReadPriority = 3;
        public static readonly int WritePriority = 1;

        public string Type { get; set; }
        public int Property { get; set; }
        public Dictionary<string, object> Axis { get; set; }
        public double Offset { get; set; }

        public GSA2DMember()
        {
            Type = "GENERIC";
            Property = 1;
            Axis = new Dictionary<string, object>()
            {
                { "X", new Dictionary<string, object> { { "x", 1 }, { "y", 0 },{ "z", 0 }  } },
                { "Y", new Dictionary<string, object> { { "x", 0 }, { "y", 1 },{ "z", 0 }  } },
                { "Z", new Dictionary<string, object> { { "x", 0 }, { "y", 0 },{ "z", 1 }  } },
            };
            Offset = 0;
        }

        #region GSAObject Functions
        public static void GetObjects(ComAuto gsa, Dictionary<Type, object> dict)
        {
            if (!dict.ContainsKey(typeof(GSANode))) return;

            List<GSAObject> nodes = dict[typeof(GSANode)] as List<GSAObject>;
            List<GSAObject> m2Ds = new List<GSAObject>();

            // TODO: Workaround for GET_ALL,MEMB bug
            int[] memberRefs = new int[0];
            gsa.EntitiesInList("all", GsaEntity.MEMBER, out memberRefs);

            if (memberRefs.Length == 0)
                return;

            List<string> tempPieces = new List<string>();

            foreach (int r in memberRefs)
            {
                tempPieces.Add(gsa.GwaCommand("GET,MEMB," + r.ToString()));
            }

            string[] pieces = tempPieces.ToArray();

            //string res = gsa.GwaCommand("GET_ALL,MEMB");

            //if (res == "")
            //    return;

            //string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string p in pieces)
            {
                string[] pPieces = p.ListSplit(",");
                if (pPieces[4].MemberIs2D())
                {
                    GSA2DMember m2D = new GSA2DMember().AttachGSA(gsa);
                    m2D.ParseGWACommand(p, dict);
                    m2Ds.Add(m2D);
                }
            }

            dict[typeof(GSA2DMember)] = m2Ds;
        }

        public static void WriteObjects(ComAuto gsa, Dictionary<Type, object> dict)
        {
            if (!dict.ContainsKey(typeof(GSA2DMember))) return;

            List<GSAObject> m2Ds = dict[typeof(GSA2DMember)] as List<GSAObject>;

            foreach (GSAObject m in m2Ds)
            {
                m.AttachGSA(gsa);

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

                m.RunGWACommand(m.GetGWACommand());
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
            Type = Type == "2D_GENERIC" ? "GENERIC" : Type;
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

            counter++; // Orientation node

            if (dict.ContainsKey(typeof(GSA2DProperty)))
            {
                List<GSAObject> props = dict[typeof(GSA2DProperty)] as List<GSAObject>;
                GSAObject prop = props.Where(p => p.Reference == Property).FirstOrDefault();
                Axis = ParseGSA2DMemberAxis(Coor.ToArray(),
                    Convert.ToDouble(pieces[counter++]),
                    prop == null ? false : (prop as GSA2DProperty).IsAxisLocal);
            }
            else
            {
                Axis = ParseGSA2DMemberAxis(Coor.ToArray(),
                    Convert.ToDouble(pieces[counter++]),
                    false);
            }

            // Skip to offsets at second to last
            counter = pieces.Length - 2;
            Offset = Convert.ToDouble(pieces[counter++]);
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
            ls.Add(Type == "GENERIC" ? "2D_GENERIC" : Type);
            ls.Add(Property.ToString());
            ls.Add("0"); // Group
            string topo = "";
            foreach (int c in Connectivity)
                topo += c.ToString() + " ";
            ls.Add(topo);
            ls.Add("0"); // Orientation node
            ls.Add(GetGSA2DMemberAngle(Coor.ToArray(), Axis).ToNumString());
            ls.Add("0"); // Target mesh size
            ls.Add("0"); // Fire
            ls.Add("0"); // Time 1
            ls.Add("0"); // Time 2
            ls.Add("0"); // Time 3
            ls.Add("0"); // TODO: What is this?
            ls.Add("ACTIVE"); // Dummy
            ls.Add("0"); // End 1 condition
            ls.Add("0"); // End 2 condition
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

        #region Axis
        public Dictionary<string, object> ParseGSA2DMemberAxis(double[] coor, double rotationAngle = 0, bool isLocalAxis = false)
        {
            Dictionary<string, object> axisVectors = new Dictionary<string, object>();

            Vector3D x;
            Vector3D y;
            Vector3D z;

            List<Vector3D> nodes = new List<Vector3D>();

            for (int i = 0; i < coor.Length; i += 3)
                nodes.Add(new Vector3D(coor[i], coor[i + 1], coor[i + 2]));

            if (isLocalAxis)
            {
                if (nodes.Count == 3)
                {
                    x = Vector3D.Subtract(nodes[1], nodes[0]);
                    x.Normalize();
                    z = Vector3D.CrossProduct(x, Vector3D.Subtract(nodes[2], nodes[0]));
                    z.Normalize();
                    y = Vector3D.CrossProduct(z, x);
                    y.Normalize();
                }
                else if (nodes.Count == 4)
                {
                    x = Vector3D.Subtract(nodes[2], nodes[0]);
                    x.Normalize();
                    z = Vector3D.CrossProduct(x, Vector3D.Subtract(nodes[3], nodes[1]));
                    z.Normalize();
                    y = Vector3D.CrossProduct(z, x);
                    y.Normalize();
                }
                else
                {
                    // Default to QUAD method
                    x = Vector3D.Subtract(nodes[2], nodes[0]);
                    x.Normalize();
                    z = Vector3D.CrossProduct(x, Vector3D.Subtract(nodes[3], nodes[1]));
                    z.Normalize();
                    y = Vector3D.CrossProduct(z, x);
                    y.Normalize();
                }
            }
            else
            {
                x = Vector3D.Subtract(nodes[1], nodes[0]);
                x.Normalize();
                z = Vector3D.CrossProduct(x, Vector3D.Subtract(nodes[2], nodes[0]));
                z.Normalize();

                x = new Vector3D(1, 0, 0);
                x = Vector3D.Subtract(x, Vector3D.Multiply(Vector3D.DotProduct(x, z), z));

                if (x.Length == 0)
                    x = new Vector3D(0, z.X > 0 ? -1 : 1, 0);

                y = Vector3D.CrossProduct(z, x);

                x.Normalize();
                y.Normalize();
            }

            //Rotation
            Matrix3D rotMat = HelperFunctions.RotationMatrix(z, rotationAngle * (Math.PI / 180));
            x = Vector3D.Multiply(x, rotMat);
            y = Vector3D.Multiply(y, rotMat);

            axisVectors["X"] = new Dictionary<string, object> { { "x", x.X }, { "y", x.Y }, { "z", x.Z } };
            axisVectors["Y"] = new Dictionary<string, object> { { "x", y.X }, { "y", y.Y }, { "z", y.Z } };
            axisVectors["Z"] = new Dictionary<string, object> { { "x", z.X }, { "y", z.Y }, { "z", z.Z } };

            return axisVectors;
        }

        private double GetGSA2DMemberAngle(double[] coor, Dictionary<string, object> axis)
        {
            Dictionary<string, object> X = axis["X"] as Dictionary<string, object>;
            Dictionary<string, object> Y = axis["Y"] as Dictionary<string, object>;
            Dictionary<string, object> Z = axis["Z"] as Dictionary<string, object>;

            Vector3D x = new Vector3D(X["x"].ToDouble(), X["y"].ToDouble(), X["z"].ToDouble());
            Vector3D y = new Vector3D(Y["x"].ToDouble(), Y["y"].ToDouble(), Y["z"].ToDouble());
            Vector3D z = new Vector3D(Z["x"].ToDouble(), Z["y"].ToDouble(), Z["z"].ToDouble());

            Vector3D x0;
            Vector3D z0;

            List<Vector3D> nodes = new List<Vector3D>();

            for (int i = 0; i < coor.Length; i += 3)
                nodes.Add(new Vector3D(coor[i], coor[i + 1], coor[i + 2]));

            // Get 0 angle axis in GLOBAL coordinates
            x0 = Vector3D.Subtract(nodes[1], nodes[0]);
            x0.Normalize();
            z0 = Vector3D.CrossProduct(x0, Vector3D.Subtract(nodes[2], nodes[0]));
            z0.Normalize();

            x0 = new Vector3D(1, 0, 0);
            x0 = Vector3D.Subtract(x0, Vector3D.Multiply(Vector3D.DotProduct(x0, z0), z0));

            if (x0.Length == 0)
                x0 = new Vector3D(0, z0.X > 0 ? -1 : 1, 0);

            x0.Normalize();

            // Find angle
            double angle = Math.Acos(Vector3D.DotProduct(x0, x) / (x0.Length * x.Length)).ToDegrees();
            if (double.IsNaN(angle)) return 0;

            Vector3D signVector = Vector3D.CrossProduct(x0, x);
            double sign = Vector3D.DotProduct(signVector, z);

            return sign >= 0 ? angle : -angle;
        }
        #endregion
    }
}
