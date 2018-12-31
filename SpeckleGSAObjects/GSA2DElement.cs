using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using Interop.Gsa_9_0;

namespace SpeckleGSA
{
    public class GSA2DElement : GSAObject
    {
        public static readonly string GSAKeyword = "EL";
        public static readonly string Stream = "elements";
        public static readonly int ReadPriority = 3;
        public static readonly int WritePriority = 1;

        public string Type { get; set; }
        public int Property { get; set; }
        public Dictionary<string, object> Axis { get; set; }
        public double InsertionPoint { get; set; }

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
            InsertionPoint = 0;
        }

        #region GSAObject Functions
        public static void GetObjects(ComAuto gsa, Dictionary<Type, object> dict)
        {
            List<GSAObject> nodes = dict[typeof(GSANode)] as List<GSAObject>;
            List<GSAObject> e2Ds = new List<GSAObject>();

            string res = gsa.GwaCommand("GET_ALL,EL");

            if (res == "")
                return;

            string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string p in pieces)
            {
                string[] pPieces = p.ListSplit(",");
                int numConnectivity = pPieces[4].ParseElementNumNodes();
                if (pPieces[4].ParseElementNumNodes() >= 3)
                {
                    GSA2DElement e2D = new GSA2DElement().AttachGSA(gsa);
                    e2D.ParseGWACommand(p, nodes.ToArray());

                    e2Ds.Add(e2D);
                }
            }

            dict[typeof(GSA2DElement)] = e2Ds;
        }

        public static void WriteObjects(ComAuto gsa, Dictionary<Type, object> dict)
        {
            if (!dict.ContainsKey(typeof(GSA2DElement))) return;

            List<GSAObject> e2Ds = dict[typeof(GSA2DElement)] as List<GSAObject>;

            foreach (GSAObject e in e2Ds)
            {
                e.AttachGSA(gsa);

                GSARefCounters.RefObject(e);

                List<GSAObject> nodes = e.GetChildren();
                
                for (int i = 0; i < nodes.Count(); i++)
                    GSARefCounters.RefObject(nodes[i]);

                e.Connectivity = nodes.Select(n => n.Reference).ToList();

                if (dict.ContainsKey(typeof(GSANode)))
                {
                    for (int i = 0; i < nodes.Count(); i++)
                    {
                        List<GSAObject> matches = (dict[typeof(GSANode)] as List<GSAObject>).Where(
                            n => n.Coor[0] == nodes[i].Coor[0] &
                            n.Coor[1] == nodes[i].Coor[1] &
                            n.Coor[2] == nodes[i].Coor[2]).ToList();

                        if (matches.Count() > 0)
                        {
                            e.Connectivity[i] = matches[0].Reference;
                            (matches[0] as GSANode).Merge(nodes[i] as GSANode);
                        }
                        else
                            (dict[typeof(GSANode)] as List<GSAObject>).Add(nodes[i]);
                    }
                }
                else
                    dict[typeof(GSANode)] = nodes;

                e.RunGWACommand(e.GetGWACommand());
            }

            dict.Remove(typeof(GSA2DElement));
        }

        public override void ParseGWACommand(string command, GSAObject[] children = null)
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
            for (int i = 0; i < Type.ParseElementNumNodes(); i++)
            { 
                Connectivity.Add(Convert.ToInt32(pieces[counter++]));
                Coor.AddRange(children.Where(n => n.Reference == Connectivity[i]).FirstOrDefault().Coor);
            }

            counter++; // Orientation node
            
            Axis = ParseGSA2DElementAxis(Coor.ToArray(), Convert.ToDouble(pieces[counter++]), Property);

            if (pieces[counter++] != "NO_RLS")
            {
                string start = pieces[counter++];
                string end = pieces[counter++];

                counter += start.Split('K').Length - 1 + end.Split('K').Length - 1;
            }
            
            counter++; //Ofsset x-start
            counter++; //Ofsset x-end
            counter++; //Ofsset y

            InsertionPoint = GetGSATotalElementOffset(Property,Convert.ToDouble(pieces[counter++]));

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
            ls.Add(GetGSA2DElementAngle(Axis).ToNumString());
            ls.Add("NO_RLS");

            ls.Add("0"); // Offset x-start
            ls.Add("0"); // Offset x-end
            ls.Add("0"); // Offset y
            ls.Add(InsertionPoint.ToNumString());

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

        #region Axis
        private Dictionary<string, object> ParseGSA2DElementAxis(double[] coor, double rotationAngle = 0, int property = 0)
        {
            Dictionary<string, object> axisVectors = new Dictionary<string, object>();

            Vector3D x;
            Vector3D y;
            Vector3D z;

            List<Vector3D> nodes = new List<Vector3D>();

            for (int i = 0; i < coor.Length; i += 3)
                nodes.Add(new Vector3D(coor[i], coor[i + 1], coor[i + 2]));

            if (Is2DElementLocalAxis(property))
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
                y = Vector3D.CrossProduct(z, x);
                y.Normalize();

                x = new Vector3D(1, 0, 0);
                y = new Vector3D(0, 1, 0);
                x = Vector3D.Subtract(x, Vector3D.Multiply(Vector3D.DotProduct(x, z), z));

                y = Vector3D.Subtract(y, Vector3D.Multiply(Vector3D.DotProduct(y, z), z));

                if (x.Length == 0)
                {
                    x = new Vector3D(0, z.X > 0 ? -1 : 1, 0);
                    y = Vector3D.CrossProduct(z, x);
                }

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

        private double GetGSA2DElementAngle(Dictionary<string, object> axis)
        {
            // TODO!!!
            return 0;
        }

        private bool Is2DElementLocalAxis(int prop)
        {
            string res = (string)RunGWACommand("GET,PROP_2D," + prop);

            if (res == null || res == "")
                return false;

            string[] pieces = res.ListSplit(",");

            return pieces[5] == "LOCAL";
        }
        #endregion

        #region Offset
        private double GetGSATotalElementOffset(int prop, double insertionPointOffset)
        {
            double materialInsertionPointOffset = 0;
            double zMaterialOffset = 0;
            double materialThickness = 0;

            string res = (string)RunGWACommand("GET,PROP_2D," + prop);

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
