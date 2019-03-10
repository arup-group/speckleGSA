﻿using Interop.Gsa_10_0;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using SpeckleStructures;
using System.Reflection;

namespace SpeckleGSA
{
    [GSAObject("MEMB.7", "elements", false, true, new Type[] { typeof(GSANode), typeof(GSA2DProperty) }, new Type[] { typeof(GSA2DElementMesh) })]
    public class GSA2DMember : Structural2DElementMesh, IGSAObject
    {
        public List<int> Connectivity;
        public int Group;

        public string GWACommand { get; set; }
        public List<string> SubGWACommand { get; set; }

        #region Contructors and Converters
        public GSA2DMember()
        {
            GWACommand = "";
            SubGWACommand = new List<string>();
            Connectivity = new List<int>();
            Group = 0;
        }
        
        public GSA2DMember(Structural2DElementMesh baseClass)
        {
            GWACommand = "";
            SubGWACommand = new List<string>();

            Connectivity = new List<int>();
            Group = 0;

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

            if (!GSA.TargetDesignLayer) return false;

            List<object> m2Ds = new List<object>();

            string[] lines = GSA.GetGWAGetCommands("GET_ALL,MEMB");
            string[] deletedLines = GSA.GetDeletedGWAGetCommands("GET_ALL,MEMB");

            // Remove deleted lines
            dict[typeof(GSA2DMember)].RemoveAll(l => deletedLines.Contains(((IGSAObject)l).GWACommand));
            foreach (KeyValuePair<Type, List<object>> kvp in dict)
                kvp.Value.RemoveAll(l => ((IGSAObject)l).SubGWACommand.Any(x => deletedLines.Contains(x)));

            // Filter only new lines
            string[] prevLines = dict[typeof(GSA2DMember)].Select(l => ((GSA2DMember)l).GWACommand).ToArray();
            string[] newLines = lines.Where(l => !prevLines.Contains(l)).ToArray();
            
            foreach (string p in newLines)
            {
                string[] pPieces = p.ListSplit(",");
                if (pPieces[4].MemberIs2D())
                {
                    GSA2DMember m2D = new GSA2DMember();
                    m2D.ParseGWACommand(p, dict);
                    m2Ds.Add(m2D);
                }
            }

            dict[typeof(GSA2DMember)].AddRange(m2Ds);

            if (m2Ds.Count() > 0 || deletedLines.Length > 0) return true;

            return false;
        }

        public static void WriteObjects(Dictionary<Type, List<StructuralObject>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType)) return;

            List<StructuralObject> m2Ds = dict[typeof(GSA2DMember)];

            double counter = 1;
            foreach (StructuralObject m in m2Ds)
            {
                int currRef = m.Reference;
                if (GSARefCounters.RefObject(m) != 0)
                {
                    m.Reference = 0;
                    GSARefCounters.RefObject(m);
                    if (dict.ContainsKey(typeof(GSA2DLoad)))
                    {
                        foreach (StructuralObject o in dict[typeof(GSA2DLoad)])
                        {
                            if ((o as GSA2DLoad).Elements.Contains(currRef))
                            { 
                                (o as GSA2DLoad).Elements.Remove(currRef);
                                (o as GSA2DLoad).Elements.Add(m.Reference);
                            }
                        }
                    }
                }

                List<StructuralObject> eNodes = (m as GSA2DMember).GetChildren();

                // Ensure no coincident nodes
                if (!dict.ContainsKey(typeof(GSANode)))
                    dict[typeof(GSANode)] = new List<StructuralObject>();

                dict[typeof(GSANode)] = HelperFunctions.CollapseNodes(dict[typeof(GSANode)].Cast<GSANode>().ToList(), eNodes.Cast<GSANode>().ToList()).Cast<StructuralObject>().ToList();

                (m as GSA2DMember).Connectivity = eNodes.Select(n => n.Reference).ToList();

                GSA.RunGWACommand((m as GSA2DMember).GetGWACommand());
                Status.ChangeStatus("Writing 2D members", counter++ / m2Ds.Count() * 100);
            }
        }

        public void ParseGWACommand(string command, Dictionary<Type, List<object>> dict = null)
        {
            GWACommand = command;

            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier
            Reference = Convert.ToInt32(pieces[counter++]);
            Name = pieces[counter++].Trim(new char[] { '"' });
            Color = pieces[counter++].ParseGSAColor();
            
            string type = pieces[counter++];
            if (type == "SLAB")
                Type = Structural2DElementType.SLAB;
            else if (type == "WALL")
                Type = Structural2DElementType.WALL;
            else
                Type = Structural2DElementType.GENERIC;
            
            Property = Convert.ToInt32(pieces[counter++]);
            Group = Convert.ToInt32(pieces[counter++]); // Keep group for load targetting

            List<double> coordinates = new List<double>();
            string[] nodeRefs = pieces[counter++].ListSplit(" ");

            for (int i = 0; i < nodeRefs.Length; i++)
            {
                GSANode node = dict[typeof(GSANode)].Cast<GSANode>().Where(n => n.Reference == Convert.ToInt32(nodeRefs[i])).FirstOrDefault();
                coordinates.AddRange(node.Coordinates.ToArray());
                SubGWACommand.Add(node.GWACommand);
            }

            ElementsFromEdge(new Coordinates(coordinates.ToArray()));

            counter++; // Orientation node

            if (dict.ContainsKey(typeof(GSA2DProperty)))
            {
                List<object> props = dict[typeof(GSA2DProperty)];
                GSA2DProperty prop = props.Cast<GSA2DProperty>().Where(p => p.Reference == Property).FirstOrDefault();
                Axis = HelperFunctions.Parse2DAxis(coordinates.ToArray(),
                    Convert.ToDouble(pieces[counter++]),
                    prop == null ? false : (prop as GSA2DProperty).IsAxisLocal);
                SubGWACommand.Add(prop.GWACommand);
            }
            else
            {
                Axis = HelperFunctions.Parse2DAxis(coordinates.ToArray(),
                    Convert.ToDouble(pieces[counter++]),
                    false);
            }

            // Skip to offsets at second to last
            counter = pieces.Length - 2;
            Offset = Convert.ToDouble(pieces[counter++]);
        }

        public string GetGWACommand(Dictionary<Type, List<StructuralObject>> dict = null)
        {
            List<string> ls = new List<string>();

            ls.Add("SET");
            ls.Add((string)this.GetAttribute("GSAKeyword"));
            ls.Add(Reference.ToString());
            ls.Add(Name);
            ls.Add(Color == null ? "NO_RGB" : Color.ToString());
            if (Type == Structural2DElementType.SLAB)
                ls.Add("SLAB");
            else if (Type == Structural2DElementType.WALL)
                ls.Add("WALL");
            else
                ls.Add("2D_GENERIC");
            ls.Add(Property.ToString());
            ls.Add(Reference.ToString()); // TODO: This allows for targeting of elements from members group
            string topo = "";
            foreach (int c in Connectivity)
                topo += c.ToString() + " ";
            ls.Add(topo);
            ls.Add("0"); // Orientation node
            ls.Add(HelperFunctions.Get2DAngle(Coordinates().ToArray(), Axis).ToString());
            ls.Add("1"); // Target mesh size
            ls.Add("MESH"); // TODO: What is this?
            ls.Add("LINEAR"); // Element type
            ls.Add("0"); // Fire
            ls.Add("0"); // Time 1
            ls.Add("0"); // Time 2
            ls.Add("0"); // Time 3
            ls.Add("0"); // TODO: What is this?
            ls.Add("ACTIVE"); // Dummy
            ls.Add(Offset.ToString()); // Offset z
            ls.Add("ALL"); // Exposure

            return string.Join(",", ls);
        }
        #endregion

        #region Helper Functions
        public List<StructuralObject> GetChildren()
        {
            List<StructuralObject> children = new List<StructuralObject>();

            List<int[]> connectivities = EdgeConnectivities();
            Coordinates coordinates = Coordinates();

            foreach(int[] conn in connectivities)
            { 
                foreach(int c in conn)
                { 
                    GSANode n = new GSANode();
                    n.Coordinates = new Coordinates(coordinates.Values[c].ToArray());
                    children.Add(n);
                }
            }
            return children;
        }
        #endregion
    }
}
