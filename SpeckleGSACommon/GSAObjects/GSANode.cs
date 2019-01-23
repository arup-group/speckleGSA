using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using Interop.Gsa_10_0;

namespace SpeckleGSA
{
    public class GSANode : GSAObject
    {
        public override string Entity { get => "Node"; set { } }

        public static readonly string GSAKeyword = "NODE";
        public static readonly string Stream = "nodes";
        public static readonly int ReadPriority = 2;
        public static readonly int WritePriority = 2;

        public Dictionary<string, object> Axis { get; set; }
        public Dictionary<string, object> Restraint { get; set; }
        public Dictionary<string, object> Stiffness { get; set; }
        public double Mass { get; set; }

        public GSANode()
        {
            Axis = new Dictionary<string, object>()
            {
                { "X", new Dictionary<string, object> { { "x", 1 }, { "y", 0 },{ "z", 0 }  } },
                { "Y", new Dictionary<string, object> { { "x", 0 }, { "y", 1 },{ "z", 0 }  } },
                { "Z", new Dictionary<string, object> { { "x", 0 }, { "y", 0 },{ "z", 1 }  } },
            };
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
            Mass = 0;
        }

        #region GSAObject Functions
        public static void GetObjects(Dictionary<Type, object> dict)
        {
            List<GSAObject> nodes = new List<GSAObject>();

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

            counter = 1;
            if (dict.ContainsKey(typeof(GSA0DElement)))
            {
                foreach(GSAObject e in dict[typeof(GSA0DElement)] as List<GSAObject>)
                { 
                    try { 
                        (nodes.Where(n => (e as GSA0DElement).Connectivity.Contains(n.Reference)).First() as GSANode).Merge0DElement(e as GSA0DElement);
                    }
                    catch { }
                    Status.ChangeStatus("Merging 0D elements", counter++ / pieces.Length * 100);
                }

                dict.Remove(typeof(GSA0DElement));
            }

            dict[typeof(GSANode)] = nodes;
        }

        public static void WriteObjects(Dictionary<Type, object> dict)
        {
            if (!dict.ContainsKey(typeof(GSANode))) return;

            List<GSAObject> nodes = dict[typeof(GSANode)] as List<GSAObject>;

            for (int i = 0; i < nodes.Count(); i++)
            {
                GSARefCounters.RefObject(nodes[i]);

                List<GSAObject> matches = nodes.Where(
                    (n, j) => j != i & n.Reference == nodes[i].Reference)
                    .ToList();

                foreach (GSAObject m in matches)
                {
                    (nodes[i] as GSANode).Merge(m as GSANode);
                    nodes.Remove(m);
                }
                
                List<GSAObject> e0Ds = nodes[i].GetChildren();

                for (int j = 0; j < e0Ds.Count(); j++)
                    GSARefCounters.RefObject(e0Ds[j]);

                if (!dict.ContainsKey(typeof(GSA0DElement)))
                    dict[typeof(GSA0DElement)] = e0Ds;
                else
                    (dict[typeof(GSA0DElement)] as List<GSAObject>).AddRange(e0Ds);

                GSA.RunGWACommand(nodes[i].GetGWACommand());
                Status.ChangeStatus("Writing nodes", (double)(i+1) / nodes.Count() * 100);
            }
            
            dict.Remove(typeof(GSANode));
        }

        public override void ParseGWACommand(string command, Dictionary<Type, object> dict = null)
        {
            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier
            Reference = Convert.ToInt32(pieces[counter++]);
            Name = pieces[counter++].Trim(new char[] { '"' });
            Color = pieces[counter++].ParseGSAColor();
            Coor = new List<double>();
            Coor.Add(Convert.ToDouble(pieces[counter++]));
            Coor.Add(Convert.ToDouble(pieces[counter++]));
            Coor.Add(Convert.ToDouble(pieces[counter++]));

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
                    Restraint["x"] = pieces[counter++] == "0" ? false : true;
                    Restraint["y"] = pieces[counter++] == "0" ? false : true;
                    Restraint["z"] = pieces[counter++] == "0" ? false : true;
                    Restraint["xx"] = pieces[counter++] == "0" ? false : true;
                    Restraint["yy"] = pieces[counter++] == "0" ? false : true;
                    Restraint["zz"] = pieces[counter++] == "0" ? false : true;
                }
                else if (s == "STIFF")
                {
                    Stiffness["x"] = Convert.ToDouble(pieces[counter++]);
                    Stiffness["y"] = Convert.ToDouble(pieces[counter++]);
                    Stiffness["z"] = Convert.ToDouble(pieces[counter++]);
                    Stiffness["xx"] = Convert.ToDouble(pieces[counter++]);
                    Stiffness["yy"] = Convert.ToDouble(pieces[counter++]);
                    Stiffness["zz"] = Convert.ToDouble(pieces[counter++]);
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
                    Axis = HelperFunctions.Parse0DAxis(Convert.ToInt32(pieces[counter++]), Coor.ToArray());
            }
            return;
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
                ls.Add(((int)Color).ToString());
            ls.Add(string.Join(",", Coor));

            ls.Add("0"); // TODO: Skip unknown fields in NODE.3
            ls.Add("0"); // TODO: Skip unknown fields in NODE.3
            ls.Add("0"); // TODO: Skip unknown fields in NODE.3
            
            //ls.Add("NO_GRID");

            ls.Add(AddAxistoGSA(Axis).ToString());

            if (Restraint.Count == 0)
                ls.Add("NO_REST");
            else
            {
                ls.Add("REST");
                ls.Add((Convert.ToBoolean(Restraint["x"])) ? "1" : "0");
                ls.Add((Convert.ToBoolean(Restraint["y"])) ? "1" : "0");
                ls.Add((Convert.ToBoolean(Restraint["z"])) ? "1" : "0");
                ls.Add((Convert.ToBoolean(Restraint["xx"])) ? "1" : "0");
                ls.Add((Convert.ToBoolean(Restraint["yy"])) ? "1" : "0");
                ls.Add((Convert.ToBoolean(Restraint["zz"])) ? "1" : "0");
            }

            if (Stiffness.Count == 0)
                ls.Add("NO_STIFF");
            else
            {
                ls.Add("STIFF");
                ls.Add(Convert.ToDouble(Stiffness["x"]).ToNumString());
                ls.Add(Convert.ToDouble(Stiffness["y"]).ToNumString());
                ls.Add(Convert.ToDouble(Stiffness["z"]).ToNumString());
                ls.Add(Convert.ToDouble(Stiffness["xx"]).ToNumString());
                ls.Add(Convert.ToDouble(Stiffness["yy"]).ToNumString());
                ls.Add(Convert.ToDouble(Stiffness["zz"]).ToNumString());
            }

            ls.Add("NO_MESH");

            return string.Join(",", ls);
        }

        public override List<GSAObject> GetChildren()
        {
            List<GSAObject> elements = new List<GSAObject>();

            if (Mass > 0)
            {
                GSA0DElement massElem = new GSA0DElement();
                massElem.Type = "MASS";
                massElem.Connectivity = new List<int>() { Reference };
                massElem.Mass = Mass;
                elements.Add(massElem);
            }

            return elements;
        }
        #endregion

        #region 0D Element Operations
        public void Merge0DElement(GSA0DElement elem)
        {
            if (elem.Type == "MASS")
                Mass = GetGSAMass(elem);
        }

        private double GetGSAMass(GSA0DElement elem)
        {
            string res = (string)GSA.RunGWACommand("GET,PROP_MASS," + elem.Property.ToString());
            string[] pieces = res.ListSplit(",");

            return Convert.ToDouble(pieces[5]);
        }
        
        private int WriteMassProptoGSA(double mass)
        {
            List<string> ls = new List<string>();

            int res = (int)GSA.RunGWACommand("HIGHEST,PROP_MASS");

            ls.Add("SET");
            ls.Add("PROP_MASS.2");
            ls.Add((res + 1).ToString());
            ls.Add("");
            ls.Add("NO_RGB");
            ls.Add("GLOBAL");
            ls.Add(mass.ToString());
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

        public void Merge(GSANode mergeNode)
        {
            Dictionary<string, object> temp = new Dictionary<string, object>();

            foreach (string key in Restraint.Keys)
                temp[key] = Convert.ToBoolean(Restraint[key]) | Convert.ToBoolean(mergeNode.Restraint[key]);
            Restraint = temp;

            temp = new Dictionary<string, object>();
            foreach (string key in Stiffness.Keys)
                temp[key] = Math.Max(Convert.ToDouble(Stiffness[key]), Convert.ToDouble(mergeNode.Stiffness[key]));
            Stiffness = temp;

            Mass += mergeNode.Mass;
        }
        #endregion

        #region Helper Functions
        private int AddAxistoGSA(Dictionary<string, object> axis)
        {
            Dictionary<string, object> X = axis["X"] as Dictionary<string, object>;
            Dictionary<string, object> Y = axis["Y"] as Dictionary<string, object>;
            Dictionary<string, object> Z = axis["Z"] as Dictionary<string, object>;

            if (X["x"].Equal(1) & X["y"].Equal(0) & X["z"].Equal(0) &
                Y["x"].Equal(0) & Y["y"].Equal(1) & Y["z"].Equal(0) &
                Z["x"].Equal(0) & Z["y"].Equal(0) & Z["z"].Equal(1))
            {
                return 0;
            }

            List<string> ls = new List<string>();

            int res = (int)GSA.RunGWACommand("HIGHEST,AXIS");

            ls.Add("AXIS");
            ls.Add((res + 1).ToString());
            ls.Add("");
            ls.Add("CART");

            ls.Add("0");
            ls.Add("0");
            ls.Add("0");

            ls.Add(X["x"].ToNumString());
            ls.Add(X["y"].ToNumString());
            ls.Add(X["z"].ToNumString());

            ls.Add(Y["x"].ToNumString());
            ls.Add(Y["y"].ToNumString());
            ls.Add(Y["z"].ToNumString());

            GSA.RunGWACommand(string.Join(",", ls));

            return res + 1;
        }
        #endregion
    }
}
