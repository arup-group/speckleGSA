using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SpeckleStructuresClasses;

namespace SpeckleGSA
{
    [GSAObject("EL.3", new string[] { "PROP_MASS.2" }, "elements", true, true, new Type[] { typeof(GSANode) }, new Type[] { typeof(GSANode) })]
    public class GSA0DElement : StructuralNode, IGSAObject
    {
        public string GWACommand { get; set; } = "";
        public List<string> SubGWACommand { get; set; } = new List<string>();

        #region Sending Functions
        public static bool GetObjects(Dictionary<Type, List<IGSAObject>> dict)
        {
            if (!dict.ContainsKey(typeof(GSANode)))
                return false;

            List<GSANode> nodes = dict[typeof(GSANode)].Cast<GSANode>().ToList();

            string keyword = MethodBase.GetCurrentMethod().DeclaringType.GetGSAKeyword();
            string[] subKeywords = MethodBase.GetCurrentMethod().DeclaringType.GetSubGSAKeyword();

            // Read lines here
            string[] lines = GSA.GetGWARecords("GET_ALL," + keyword);
            List<string> deletedLines = GSA.GetDeletedGWARecords("GET_ALL," + keyword).ToList();
            foreach (string k in subKeywords)
                deletedLines.AddRange(GSA.GetDeletedGWARecords("GET_ALL," + k));

            bool changed = false;

            // Remove deleted lines
            foreach (KeyValuePair<Type, List<IGSAObject>> kvp in dict)
                foreach (IGSAObject o in kvp.Value.Where(l => l.SubGWACommand.Any(x => deletedLines.Contains(x))))
                {
                    ((GSANode)o).Mass = 0;
                    o.SubGWACommand.RemoveAll(s => lines.Contains(s));
                    o.SubGWACommand.RemoveAll(s => deletedLines.Contains(s));

                    changed = true;
                }


            // Filter only new lines
            string[] prevLines = dict[typeof(GSANode)].SelectMany(l => l.SubGWACommand).ToArray();
            string[] newLines = lines.Where(l => !prevLines.Contains(l)).ToArray();
            
            foreach (string p in newLines)
            {
                string[] pPieces = p.ListSplit(",");
                if (pPieces[4].ParseElementNumNodes() == 1)
                {
                    GSA0DElement massNode = Get(p);

                    nodes
                        .Where(n => n.StructuralId == massNode.StructuralId).First()
                        .Mass = massNode.Mass;

                    nodes.Cast<GSANode>()
                        .Where(n => n.StructuralId == massNode.StructuralId).First()
                        .SubGWACommand.AddRange(massNode.SubGWACommand.Concat(new string[] { p }));

                    changed = true;
                }
            }
            
            if (changed) return true;
            return false;
        }

        public static GSA0DElement Get(string command)
        {
            GSA0DElement ret = new GSA0DElement();

            ret.GWACommand = command;

            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier
            counter++; // Reference
            counter++; // Name
            counter++; // Color
            counter++; // Type
            var massRet = GetGSAMass(Convert.ToInt32(pieces[counter++]));
            ret.Mass = massRet.Item1;
            ret.SubGWACommand.Add(massRet.Item2);
            counter++; // group
            ret.StructuralId = pieces[counter++];
            // Rest is unimportant for 0D element
            
            return ret;
        }

        private static Tuple<double, string> GetGSAMass(int propertyRef)
        {
            string res = GSA.GetGWARecords("GET,PROP_MASS.2," + propertyRef.ToString()).FirstOrDefault();
            string[] pieces = res.ListSplit(",");

            return new Tuple<double, string>(Convert.ToDouble(pieces[5]), res);
        }
        #endregion

        #region Receiving Functions
        public static void SetObjects(Dictionary<Type, List<IStructural>> receivedObjects)
        {
            if (!receivedObjects.ContainsKey(typeof(StructuralNode))) return;

            foreach (IStructural obj in receivedObjects[typeof(StructuralNode)])
            {
                Set(obj as StructuralNode);
            }
        }

        public static void Set(StructuralNode node)
        {
            if (node == null)
                return;

            if (node.Mass == 0)
                return;

            string keyword = MethodBase.GetCurrentMethod().DeclaringType.GetGSAKeyword();

            int index = Indexer.ResolveIndex(MethodBase.GetCurrentMethod().DeclaringType, node);
            int propIndex = Indexer.ResolveIndex("PROP_MASS.2", node);
            int nodeRef = Indexer.ResolveIndex(typeof(GSANode), node);

            List<string> ls = new List<string>();

            ls.Add("SET");
            ls.Add(keyword);
            ls.Add(index.ToString());
            ls.Add(node.Name == null || node.Name == "" ? " " : node.Name);
            ls.Add("NO_RGB");
            ls.Add("MASS");
            ls.Add(propIndex.ToString());
            ls.Add("0"); // Group
            ls.Add(nodeRef.ToString());
            ls.Add("0"); // Orient Node
            ls.Add("0"); // Beta
            ls.Add("NO_RLS"); // Release
            ls.Add("0"); // Offset x-start
            ls.Add("0"); // Offset y-start
            ls.Add("0"); // Offset y
            ls.Add("0"); // Offset z
            ls.Add(""); //Dummy
            
            GSA.RunGWACommand(string.Join("\t", ls));

            ls.Clear();
            ls.Add("SET");
            ls.Add("PROP_MASS.2");
            ls.Add(propIndex.ToString());
            ls.Add("");
            ls.Add("NO_RGB");
            ls.Add("GLOBAL");
            ls.Add(node.Mass.ToString());
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
            GSA.RunGWACommand(string.Join("\t", ls));
        }
        #endregion
    }
}
