using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SpeckleStructuresClasses;

namespace SpeckleGSA
{
    [GSAObject("EL.3", "elements", false, false, new Type[] { typeof(GSANode) }, new Type[] { })]
    public class GSA0DElement : StructuralNode, IGSAObject
    {
        public string GWACommand { get; set; } = "";
        public List<string> SubGWACommand { get; set; } = new List<string>();

        #region Sending Functions
        public static bool GetObjects(Dictionary<Type, List<IGSAObject>> gsaObjects)
        {
            if (!gsaObjects.ContainsKey(typeof(GSANode)))
                return false;

            List<GSANode> nodes = gsaObjects[typeof(GSANode)].Cast<GSANode>().ToList();

            string keyword = MethodBase.GetCurrentMethod().DeclaringType.GetGSAKeyword();

            // Read lines here
            string[] lines = GSA.GetGWAGetCommands("GET_ALL," + keyword);
            string[] deletedLines = GSA.GetDeletedGWAGetCommands("GET_ALL," + keyword);

            // Filter only new lines
            string[] prevLines = gsaObjects[typeof(GSANode)].SelectMany(l => l.SubGWACommand).ToArray();
            string[] newLines = lines.Where(l => !prevLines.Contains(l)).ToArray();

            bool changed = false;

            foreach (string p in deletedLines)
            {
                string[] pPieces = p.ListSplit(",");
                if (pPieces[4].ParseElementNumNodes() == 1)
                {
                    GSA0DElement massNode = Get(p);

                    nodes
                        .Where(n => n.StructuralID == massNode.StructuralID).First()
                        .Mass = 0;

                    changed = true;
                }
            }

            foreach (string p in newLines)
            {
                string[] pPieces = p.ListSplit(",");
                if (pPieces[4].ParseElementNumNodes() == 1)
                {
                    GSA0DElement massNode = Get(p);

                    nodes
                        .Where(n => n.StructuralID == massNode.StructuralID).First()
                        .Mass = massNode.Mass;

                    nodes.Cast<GSANode>()
                        .Where(n => n.StructuralID == massNode.StructuralID).First()
                        .SubGWACommand.Add(p);

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
            ret.Mass = GetGSAMass(Convert.ToInt32(pieces[counter++]));
            counter++; // group
            ret.StructuralID = pieces[counter++];
            // Rest is unimportant for 0D element
            
            return ret;
        }

        private static double GetGSAMass(int propertyRef)
        {
            string res = (string)GSA.RunGWACommand("GET,PROP_MASS," + propertyRef.ToString());
            string[] pieces = res.ListSplit(",");

            return Convert.ToDouble(pieces[5]);
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

            int index = Indexer.ResolveIndex(MethodBase.GetCurrentMethod().DeclaringType);
            int nodeRef = Indexer.ResolveIndex(typeof(GSANode), node);

            List<string> ls = new List<string>();

            ls.Add("SET");
            ls.Add(keyword);
            ls.Add(index.ToString());
            ls.Add(""); // Name
            ls.Add("NO_RGB");
            ls.Add("MASS");
            ls.Add(SetMassProp(node.Mass).ToString()); // Property
            ls.Add("0"); // Group
            ls.Add(nodeRef.ToString());
            ls.Add("0"); // Orient Node
            ls.Add("0"); // Beta
            ls.Add("NO_RLS"); // Release
            ls.Add("0"); // Offset x-start
            ls.Add("0"); // Offset y-start
            ls.Add("0"); // Offset y
            ls.Add("0"); // Offset z

            //ls.Add("NORMAL"); // Action // TODO: EL.4 SUPPORT
            ls.Add(""); //Dummy

            GSA.RunGWACommand(string.Join(",", ls));
        }

        private static int SetMassProp(double Mass)
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
        #endregion
    }
}
