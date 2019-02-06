using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using Interop.Gsa_10_0;
using SpeckleStructures;

namespace SpeckleGSA
{
    public class GSANode : StructuralNode
    {
        public static readonly string GSAKeyword = "NODE";
        public static readonly string Stream = "nodes";

        public static readonly Type[] ReadPrerequisite = new Type[0];
        public static readonly Type[] WritePrerequisite = new Type[4] { typeof(GSA1DElement), typeof(GSA1DMember), typeof(GSA2DElement), typeof(GSA2DMember) };

        public bool ForceSend;

        public GSANode()
        {
            ForceSend = false;
        }

        public GSANode(StructuralNode baseClass)
        {
            ForceSend = false;

            foreach (FieldInfo f in baseClass.GetType().GetFields())
                f.SetValue(this, f.GetValue(baseClass));

            foreach (PropertyInfo p in baseClass.GetType().GetProperties())
                p.SetValue(this, p.GetValue(baseClass));
        }

        #region GSAObject Functions
        public static void GetObjects(Dictionary<Type, List<StructuralObject>> dict)
        {
            List<StructuralObject> nodes = new List<StructuralObject>();

            string res = (string)GSA.RunGWACommand("GET_ALL,NODE");

            if (res == "")
                return;

            string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            double counter = 1;
            foreach (string p in pieces)
            {
                GSANode n = new GSANode();
                n.ParseGWACommand(p);

                nodes.Add(n);

                Status.ChangeStatus("Reading nodes", counter++ / pieces.Length * 100);
            }

            // Read 0D elements here
            res = (string)GSA.RunGWACommand("GET_ALL,EL");

            if (res != "")
            {
                pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

                counter = 1;
                foreach (string p in pieces)
                {
                    string[] pPieces = p.ListSplit(",");
                    if (pPieces[4].ParseElementNumNodes() == 1)
                    {
                        GSA0DElement e0D = new GSA0DElement();
                        e0D.ParseGWACommand(p, dict);

                        nodes.Cast<GSANode>()
                            .Where(n => n.Reference == e0D.Connectivity).First()
                            .Mass = e0D.Mass;
                    }

                    Status.ChangeStatus("Reading 0D elements", counter++ / pieces.Length * 100);
                }
                
            }

            dict[typeof(GSANode)] = nodes;
        }

        public static void WriteObjects(Dictionary<Type, List<StructuralObject>> dict)
        {
            if (!dict.ContainsKey(typeof(GSANode))) return;

            List<StructuralObject> nodes = dict[typeof(GSANode)];

            for (int i = 0; i < nodes.Count(); i++)
            {
                GSARefCounters.RefObject(nodes[i]);

                List<StructuralObject> matches = nodes.Where(
                    (n, j) => j != i & n.Reference == nodes[i].Reference)
                    .ToList();

                foreach (StructuralObject m in matches)
                {
                    (nodes[i] as GSANode).Merge(m as GSANode);
                    nodes.Remove(m);
                }
                
                GSA.RunGWACommand(((GSANode)nodes[i]).GetGWACommand());
                
                // Write 0D Elements
                if (((GSANode)nodes[i]).Mass < 0)
                {
                    GSA0DElement e0D = GSARefCounters.RefObject(new GSA0DElement()) as GSA0DElement;
                    e0D.Type = "MASS";
                    e0D.Mass = ((GSANode)nodes[i]).Mass;
                    e0D.Connectivity = nodes[i].Reference;
                    GSA.RunGWACommand(e0D.GetGWACommand());
                }

                Status.ChangeStatus("Writing nodes and 0D elements", (double)(i+1) / nodes.Count() * 100);
            }
            
            dict.Remove(typeof(GSANode));
        }

        public void ParseGWACommand(string command, Dictionary<Type, List<StructuralObject>> dict = null)
        {
            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier
            Reference = Convert.ToInt32(pieces[counter++]);
            Name = pieces[counter++].Trim(new char[] { '"' });
            counter++; // Color
            List<double> coor = new List<double>();
            coor.Add(Convert.ToDouble(pieces[counter++]));
            coor.Add(Convert.ToDouble(pieces[counter++]));
            coor.Add(Convert.ToDouble(pieces[counter++]));
            Coordinates = new Coordinates(coor.ToArray());

            counter += 3; // TODO: Skip unknown fields in NODE.3

            while (counter < pieces.Length)
            {
                string s = pieces[counter++];
                if (s == "GRID")
                {
                    counter++; // Grid place
                    counter++; // Datum
                    counter++; // Grid line A
                    counter++; // Grid line B
                }
                else if (s == "REST")
                {
                    Restraint = new SixVectorBool();
                    Restraint.X = pieces[counter++] == "0" ? false : true;
                    Restraint.Y = pieces[counter++] == "0" ? false : true;
                    Restraint.Z = pieces[counter++] == "0" ? false : true;
                    Restraint.XX = pieces[counter++] == "0" ? false : true;
                    Restraint.YY = pieces[counter++] == "0" ? false : true;
                    Restraint.ZZ = pieces[counter++] == "0" ? false : true;
                }
                else if (s == "STIFF")
                {
                    Stiffness = new SixVectorDouble();
                    Stiffness.X = Convert.ToDouble(pieces[counter++]);
                    Stiffness.Y = Convert.ToDouble(pieces[counter++]);
                    Stiffness.Z = Convert.ToDouble(pieces[counter++]);
                    Stiffness.XX = Convert.ToDouble(pieces[counter++]);
                    Stiffness.YY = Convert.ToDouble(pieces[counter++]);
                    Stiffness.ZZ = Convert.ToDouble(pieces[counter++]);
                }
                else if (s == "MESH")
                {
                    counter++; // Edge length
                    counter++; // Radius
                    counter++; // Tie to mesh
                    counter++; // Column rigidity
                    counter++; // Column prop
                    counter++; // Column node
                    counter++; // Column angle
                    counter++; // Column factor
                    counter++; // Column slab factor
                }
                else
                    Axis = HelperFunctions.Parse0DAxis(Convert.ToInt32(pieces[counter++]), Coordinates.ToArray());
            }
            return;
        }

        public string GetGWACommand(Dictionary<Type, List<StructuralObject>> dict = null)
        {
            List<string> ls = new List<string>();

            ls.Add("SET");
            ls.Add(GSAKeyword);
            ls.Add(Reference.ToString());
            ls.Add(Name);
            ls.Add("NO_RGB");
            ls.Add(string.Join(",", Coordinates.ToArray()));

            ls.Add("0"); // TODO: Skip unknown fields in NODE.3
            ls.Add("0"); // TODO: Skip unknown fields in NODE.3
            ls.Add("0"); // TODO: Skip unknown fields in NODE.3

            //ls.Add("NO_GRID");

            ls.Add(AddAxistoGSA().ToString());

            if (!Restraint.X | !Restraint.Y | Restraint.Z | !Restraint.XX | !Restraint.YY | !Restraint.ZZ)
                ls.Add("NO_REST");
            else
            {
                ls.Add("REST");
                ls.Add(Restraint.X ? "1" : "0");
                ls.Add(Restraint.Y ? "1" : "0");
                ls.Add(Restraint.Z ? "1" : "0");
                ls.Add(Restraint.XX ? "1" : "0");
                ls.Add(Restraint.YY ? "1" : "0");
                ls.Add(Restraint.ZZ ? "1" : "0");
            }

            if (Stiffness.X == 0 && Stiffness.Y == 0 && Stiffness.Z == 0 && Stiffness.XX == 0 && Stiffness.YY == 0 && Stiffness.ZZ == 0)
                ls.Add("NO_STIFF");
            else
            {
                ls.Add("STIFF");
                ls.Add(Stiffness.X.ToString());
                ls.Add(Stiffness.Y.ToString());
                ls.Add(Stiffness.Z.ToString());
                ls.Add(Stiffness.XX.ToString());
                ls.Add(Stiffness.YY.ToString());
                ls.Add(Stiffness.ZZ.ToString());
            }

            ls.Add("NO_MESH");

            return string.Join(",", ls);
        }
        #endregion
        
        #region Helper Functions
        public void Merge(GSANode mergeNode)
        {
            Restraint.X = Restraint.X | mergeNode.Restraint.X;
            Restraint.Y = Restraint.Y | mergeNode.Restraint.Y;
            Restraint.Z = Restraint.Z | mergeNode.Restraint.Z;
            Restraint.XX = Restraint.XX | mergeNode.Restraint.XX;
            Restraint.YY = Restraint.YY | mergeNode.Restraint.YY;
            Restraint.ZZ = Restraint.ZZ | mergeNode.Restraint.ZZ;

            Stiffness.X = Stiffness.X + mergeNode.Stiffness.X;
            Stiffness.Y = Stiffness.Y + mergeNode.Stiffness.Y;
            Stiffness.Z = Stiffness.Z + mergeNode.Stiffness.Z;
            Stiffness.XX = Stiffness.XX + mergeNode.Stiffness.XX;
            Stiffness.YY = Stiffness.YY + mergeNode.Stiffness.YY;
            Stiffness.ZZ = Stiffness.ZZ + mergeNode.Stiffness.ZZ;

            Mass += mergeNode.Mass;
        }

        private int AddAxistoGSA()
        {
            if (Axis.X == new Vector3D(1,0,0) && Axis.Y == new Vector3D(0,1,0) && Axis.Z == new Vector3D(0,0,1))
                return 0;

            List<string> ls = new List<string>();

            int res = (int)GSA.RunGWACommand("HIGHEST,AXIS");

            ls.Add("AXIS");
            ls.Add((res + 1).ToString());
            ls.Add("");
            ls.Add("CART");

            ls.Add("0");
            ls.Add("0");
            ls.Add("0");

            ls.Add(Axis.X.X.ToString());
            ls.Add(Axis.X.Y.ToString());
            ls.Add(Axis.X.Z.ToString());

            ls.Add(Axis.Y.X.ToString());
            ls.Add(Axis.Y.Y.ToString());
            ls.Add(Axis.Y.Z.ToString());

            GSA.RunGWACommand(string.Join(",", ls));

            return res + 1;
        }
        #endregion
    }
    
    public class GSA0DElement : StructuralObject
    {
        public static readonly string GSAKeyword = "EL";
        public static readonly string Stream = "elements";

        public string Type;
        public int Property;
        public double Mass;
        public int Connectivity;

        public GSA0DElement()
        {
            Type = "MASS";
            Property = 0;
            Mass = 0;
            Connectivity = 0;
        }

        #region GSAObject Functions
        public void ParseGWACommand(string command, Dictionary<Type, List<StructuralObject>> dict = null)
        {
            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier
            Reference = Convert.ToInt32(pieces[counter++]);
            counter++; // Name
            counter++; // Color
            Type = pieces[counter++];
            Property = Convert.ToInt32(pieces[counter++]);
            counter++; // group

            Connectivity = Convert.ToInt32(pieces[counter++]);

            Mass = GetGSAMass();

            // Rest is unimportant for 0D element
        }

        public string GetGWACommand(Dictionary<Type, List<StructuralObject>> dict = null)
        {
            List<string> ls = new List<string>();

            ls.Add("SET");
            ls.Add(GSAKeyword);
            ls.Add(Reference.ToString());
            ls.Add(""); // Name
            ls.Add("NO_RGB");
            ls.Add(Type);
            ls.Add(WriteMassProptoGSA().ToString()); // Property
            ls.Add("0"); // Group
            ls.Add(Connectivity.ToString());
            ls.Add("0"); // Orient Node
            ls.Add("0"); // Beta
            ls.Add("NO_RLS"); // Release
            ls.Add("0"); // Offset x-start
            ls.Add("0"); // Offset y-start
            ls.Add("0"); // Offset y
            ls.Add("0"); // Offset z

            //ls.Add("NORMAL"); // Action // TODO: EL.4 SUPPORT
            ls.Add(""); //Dummy

            return string.Join(",", ls);
        }
        #endregion

        private double GetGSAMass()
        {
            string res = (string)GSA.RunGWACommand("GET,PROP_MASS," + Property.ToString());
            string[] pieces = res.ListSplit(",");

            return Convert.ToDouble(pieces[5]);
        }

        private int WriteMassProptoGSA()
        {
            List<string> ls = new List<string>();

            int res = (int)GSA.RunGWACommand("HIGHEST,PROP_MASS");

            ls.Add("SET");
            ls.Add("PROP_MASS.2");
            ls.Add((res + 1).ToString());
            ls.Add("");
            ls.Add("NO_RGB");
            ls.Add("GLOBAL");
            ls.Add(Mass.ToString());
            ls.Add("0");
            ls.Add("0");
            ls.Add("0");
            ls.Add("0");
            ls.Add("0");
            ls.Add("0");

            ls.Add("MOD");
            ls.Add("100%");
            ls.Add("100%");
            ls.Add("100%");

            GSA.RunGWACommand(string.Join(",", ls));

            return res + 1;
        }
    }
}
