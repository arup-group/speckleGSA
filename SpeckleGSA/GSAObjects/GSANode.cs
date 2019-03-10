using SpeckleStructuresClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Media.Media3D;

namespace SpeckleGSA
{
    [GSAObject("NODE.2", "nodes", true, true, new Type[] { }, new Type[] { typeof(GSA1DElement), typeof(GSA1DMember), typeof(GSA2DElement), typeof(GSA2DMember) })]
    public class GSANode : StructuralNode, IGSAObject
    {
        public bool ForceSend;

        public string GWACommand { get; set; }
        public List<string> SubGWACommand { get; set; }

        #region Contructors and Converters
        public GSANode()
        {
            GWACommand = "";
            SubGWACommand = new List<string>();
            ForceSend = false;
        }

        public GSANode(StructuralNode baseClass)
        {
            GWACommand = "";
            SubGWACommand = new List<string>();
            ForceSend = false;

            foreach (FieldInfo f in baseClass.GetType().GetFields())
                f.SetValue(this, f.GetValue(baseClass));

            foreach (PropertyInfo p in baseClass.GetType().GetProperties())
                p.SetValue(this, p.GetValue(baseClass));
        }
        #endregion

        #region GSA Functions
        public static bool GetObjects(Dictionary<Type, List<object>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType))
                dict[MethodBase.GetCurrentMethod().DeclaringType] = new List<object>();

            List<object> nodes = new List<object>();

            string[] lines = GSA.GetGWAGetCommands("GET_ALL,NODE");
            string[] deletedLines = GSA.GetDeletedGWAGetCommands("GET_ALL,NODE");

            // Remove deleted lines
            dict[typeof(GSANode)].RemoveAll(l => deletedLines.Contains(((IGSAObject)l).GWACommand));
            foreach (KeyValuePair<Type, List<object>> kvp in dict)
                kvp.Value.RemoveAll(l => ((IGSAObject)l).SubGWACommand.Any(x => deletedLines.Contains(x)));

            // Filter only new lines
            string[] prevLines = dict[typeof(GSANode)].Select(l => ((GSANode)l).GWACommand).ToArray();
            string[] newLines = lines.Where(l => !prevLines.Contains(l)).ToArray();
            
            foreach (string p in newLines)
            {
                GSANode n = new GSANode();
                n.ParseGWACommand(p, dict);

                nodes.Add(n);
            }

            // Read 0D elements here
            string[] e0DLines = GSA.GetGWAGetCommands("GET_ALL,EL");
            string[] e0DDeletedLines = GSA.GetDeletedGWAGetCommands("GET_ALL,EL");

            // Filter only new lines
            string[] e0DPrevLines = dict[typeof(GSANode)].SelectMany(l => ((GSANode)l).SubGWACommand).ToArray();
            string[] e0DNewLines = e0DLines.Where(l => !e0DPrevLines.Contains(l)).ToArray();

            bool e0dChanged = false;

            foreach (string p in e0DDeletedLines)
            {
                string[] pPieces = p.ListSplit(",");
                if (pPieces[4].ParseElementNumNodes() == 1)
                {
                    GSA0DElement e0D = new GSA0DElement();
                    e0D.ParseGWACommand(p, dict);

                    nodes.Cast<GSANode>()
                        .Where(n => n.Reference == e0D.Connectivity).First()
                        .Mass = 0;

                    e0dChanged = true;
                }
            }
            
            foreach (string p in e0DNewLines)
            {
                string[] pPieces = p.ListSplit(",");
                if (pPieces[4].ParseElementNumNodes() == 1)
                {
                    GSA0DElement e0D = new GSA0DElement();
                    e0D.ParseGWACommand(p, dict);

                    nodes.Cast<GSANode>()
                        .Where(n => n.Reference == e0D.Connectivity).First()
                        .Mass = e0D.Mass;

                    nodes.Cast<GSANode>()
                        .Where(n => n.Reference == e0D.Connectivity).First()
                        .SubGWACommand.Add(e0D.GWACommand);

                    e0dChanged = true;
                }
            }

            dict[typeof(GSANode)].AddRange(nodes);

            if (nodes.Count() > 0 || deletedLines.Length > 0 || e0dChanged) return true;

            return false;
        }

        public static void WriteObjects(Dictionary<Type, List<StructuralObject>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType)) return;
            
            List<StructuralObject> nodes = dict[typeof(GSANode)];
            
            // Need iterator to make sure that we don't match with the same node in LINQ
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
                if (((GSANode)nodes[i]).Mass > 0)
                {
                    GSA0DElement e0D =  new GSA0DElement();
                    GSARefCounters.RefObject(e0D);
                    e0D.Type = "MASS";
                    e0D.Mass = ((GSANode)nodes[i]).Mass;
                    e0D.Connectivity = nodes[i].Reference;
                    GSA.RunGWACommand(e0D.GetGWACommand());
                }

                Status.ChangeStatus("Writing nodes and 0D elements", (double)(i+1) / nodes.Count() * 100);
            }
        }

        public void ParseGWACommand(string command, Dictionary<Type, List<object>> dict = null)
        {
            GWACommand = command;

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

            //counter += 3; // TODO: Skip unknown fields in NODE.3

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
            ls.Add((string)this.GetAttribute("GSAKeyword"));
            ls.Add(Reference.ToString());
            ls.Add(Name);
            ls.Add("NO_RGB");
            ls.Add(string.Join(",", Coordinates.ToArray()));

            //ls.Add("0"); // TODO: Skip unknown fields in NODE.3
            //ls.Add("0"); // TODO: Skip unknown fields in NODE.3
            //ls.Add("0"); // TODO: Skip unknown fields in NODE.3

            ls.Add("NO_GRID");

            ls.Add(AddAxistoGSA().ToString());

            if (!Restraint.X & !Restraint.Y & Restraint.Z & !Restraint.XX & !Restraint.YY & !Restraint.ZZ)
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
}
