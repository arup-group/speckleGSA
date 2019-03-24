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

        public string GWACommand { get; set; } = "";
        public List<string> SubGWACommand { get; set; } = new List<string>();

        #region Sending Functions
        public static bool GetObjects(Dictionary<Type, List<IGSAObject>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType))
                dict[MethodBase.GetCurrentMethod().DeclaringType] = new List<IGSAObject>();

            List<GSANode> nodes = new List<GSANode>();

            string keyword = MethodBase.GetCurrentMethod().DeclaringType.GetGSAKeyword();

            string[] lines = GSA.GetGWAGetCommands("GET_ALL," + keyword);
            string[] deletedLines = GSA.GetDeletedGWAGetCommands("GET_ALL," + keyword);

            // Remove deleted lines
            dict[typeof(GSANode)].RemoveAll(l => deletedLines.Contains(l.GWACommand));
            foreach (KeyValuePair<Type, List<IGSAObject>> kvp in dict)
                kvp.Value.RemoveAll(l => l.SubGWACommand.Any(x => deletedLines.Contains(x)));

            // Filter only new lines
            string[] prevLines = dict[typeof(GSANode)].Select(l => l.GWACommand).ToArray();
            string[] newLines = lines.Where(l => !prevLines.Contains(l)).ToArray();

            foreach (string p in newLines)
            {
                GSANode node = ParseGWACommand(p);
                nodes.Add(node);
            }

            dict[typeof(GSANode)].AddRange(nodes);

            if (nodes.Count() > 0 || deletedLines.Length > 0) return true;

            return false;
        }

        public static GSANode ParseGWACommand(string command)
        {
            GSANode ret = new GSANode();

            ret.GWACommand = command;

            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier
            ret.StructuralID = pieces[counter++];
            ret.Name = pieces[counter++].Trim(new char[] { '"' });
            counter++; // Color
            ret.Value = new List<double>();
            ret.Value.Add(Convert.ToDouble(pieces[counter++]));
            ret.Value.Add(Convert.ToDouble(pieces[counter++]));
            ret.Value.Add(Convert.ToDouble(pieces[counter++]));

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
                    ret.Restraint = new StructuralVectorBoolSix(new bool[6]);
                    for (int i = 0; i < 6; i++)
                        ret.Restraint.Value[i] = pieces[counter++] == "0" ? false : true;
                }
                else if (s == "STIFF")
                {
                    ret.Stiffness = new StructuralVectorSix(new double[6]);
                    for (int i = 0; i < 6; i++)
                        ret.Stiffness.Value[i] = Convert.ToDouble(pieces[counter++]);
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
                    ret.Axis = HelperFunctions.Parse0DAxis(Convert.ToInt32(pieces[counter++]), ret.Value.ToArray());
            }

            return ret;
        }
        #endregion

        #region Receiving Functions
        public static void SetObjects(Dictionary<Type, List<IStructural>> dict)
        {
            if (!dict.ContainsKey(typeof(StructuralNode))) return;

            foreach (IStructural obj in dict[typeof(StructuralNode)])
            {
                Set(obj as StructuralNode);
            }
        }

        public static void Set(StructuralNode node)
        {
            if (node == null)
                return;

            string keyword = MethodBase.GetCurrentMethod().DeclaringType.GetGSAKeyword();

            int index = Indexer.ResolveIndex(MethodBase.GetCurrentMethod().DeclaringType, node);

            List<string> ls = new List<string>();

            ls.Add("SET");
            ls.Add(keyword);
            ls.Add(index.ToString());
            ls.Add(node.Name);
            ls.Add("NO_RGB");
            ls.Add(string.Join(",", node.Value.ToArray()));

            //ls.Add("0"); // TODO: Skip unknown fields in NODE.3
            //ls.Add("0"); // TODO: Skip unknown fields in NODE.3
            //ls.Add("0"); // TODO: Skip unknown fields in NODE.3

            ls.Add("NO_GRID");

            ls.Add(SetAxis(node.Axis).ToString());

            if (!node.Restraint.Value.Any(x =>x))
                ls.Add("NO_REST");
            else
            {
                ls.Add("REST");
                ls.Add(node.Restraint.Value[0] ? "1" : "0");
                ls.Add(node.Restraint.Value[1] ? "1" : "0");
                ls.Add(node.Restraint.Value[2] ? "1" : "0");
                ls.Add(node.Restraint.Value[3] ? "1" : "0");
                ls.Add(node.Restraint.Value[4] ? "1" : "0");
                ls.Add(node.Restraint.Value[5] ? "1" : "0");
            }

            if (!node.Stiffness.Value.Any(x => x == 0))
                ls.Add("NO_STIFF");
            else
            {
                ls.Add("STIFF");
                ls.Add(node.Stiffness.Value[0].ToString());
                ls.Add(node.Stiffness.Value[1].ToString());
                ls.Add(node.Stiffness.Value[2].ToString());
                ls.Add(node.Stiffness.Value[3].ToString());
                ls.Add(node.Stiffness.Value[4].ToString());
                ls.Add(node.Stiffness.Value[5].ToString());
            }

            ls.Add("NO_MESH");

            GSA.RunGWACommand(string.Join(",", ls));
        }
        #endregion

        #region Helper Functions
        private static int SetAxis(StructuralAxis axis)
        {
            if (axis.Xdir.Value.SequenceEqual(new double[] { 1, 0, 0 }) &&
                axis.Ydir.Value.SequenceEqual(new double[] { 0, 1, 0 }) &&
                axis.Normal.Value.SequenceEqual(new double[] { 0, 0, 1 }))
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

            ls.Add(axis.Xdir.Value[0].ToString());
            ls.Add(axis.Xdir.Value[1].ToString());
            ls.Add(axis.Xdir.Value[2].ToString());

            ls.Add(axis.Ydir.Value[0].ToString());
            ls.Add(axis.Ydir.Value[1].ToString());
            ls.Add(axis.Ydir.Value[2].ToString());

            GSA.RunGWACommand(string.Join(",", ls));

            return res + 1;
        }
        #endregion
    }
}
