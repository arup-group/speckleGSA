using Interop.Gsa_10_0;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using SpeckleStructuresClasses;
using System.Reflection;

namespace SpeckleGSA
{
    [GSAObject("LOAD_NODE.2", "loads", true, true, new Type[] { typeof(GSANode) }, new Type[] { typeof(GSANode) })]
    public class GSA0DLoad : Structural0DLoad, IGSAObject
    {
        public int Axis; // Store this temporarily to generate other loads

        public string GWACommand { get; set; }
        public List<string> SubGWACommand { get; set; }
        
        #region Sending Functions
        public static bool GetObjects(Dictionary<Type, List<IGSAObject>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType))
                dict[MethodBase.GetCurrentMethod().DeclaringType] = new List<IGSAObject>();

            List<GSA0DLoad> loads = new List<GSA0DLoad>();

            List<GSANode> nodes = dict[typeof(GSANode)].Cast<GSANode>().ToList();

            string[] lines = GSA.GetGWAGetCommands("GET_ALL,LOAD_NODE");
            string[] deletedLines = GSA.GetDeletedGWAGetCommands("GET_ALL,LOAD_NODE");

            // Remove deleted lines
            dict[typeof(GSA0DLoad)].RemoveAll(l => deletedLines.Contains(l.GWACommand));
            foreach (KeyValuePair<Type, List<IGSAObject>> kvp in dict)
                kvp.Value.RemoveAll(l => l.SubGWACommand.Any(x => deletedLines.Contains(x)));

            // Filter only new lines
            string[] prevLines = dict[typeof(GSA0DLoad)].Select(l => l.GWACommand).ToArray();
            string[] newLines = lines.Where(l => !prevLines.Contains(l)).ToArray();

            foreach (string p in newLines)
            {
                List<GSA0DLoad> loadSubList = new List<GSA0DLoad>();

                // Placeholder load object to get list of nodes and load values
                // Need to transform to axis so one load definition may be transformed to many
                GSA0DLoad initLoad = ParseGWACommand(p, nodes);

                // Raise node flag to make sure it gets sent
                foreach (GSANode n in nodes.Where(n => initLoad.NodeRefs.Contains(n.StructuralID)))
                    n.ForceSend = true;

                // Create load for each node applied
                foreach (string nRef in initLoad.NodeRefs)
                {
                    GSA0DLoad load = new GSA0DLoad();
                    load.GWACommand = initLoad.GWACommand;
                    load.SubGWACommand = new List<string>(initLoad.SubGWACommand);
                    load.Name = initLoad.Name;
                    load.LoadCaseRef = initLoad.LoadCaseRef;

                    // Transform load to defined axis
                    GSANode node = nodes.Where(n => (n.StructuralID == nRef)).First();
                    StructuralAxis loadAxis = HelperFunctions.Parse0DAxis(initLoad.Axis, node.Value.ToArray());
                    load.Loading = initLoad.Loading;
                    load.Loading.TransformOntoAxis(loadAxis);

                    // If the loading already exists, add node ref to list
                    GSA0DLoad match = loadSubList.Count() > 0 ? loadSubList.Where(l => l.Loading.Value.SequenceEqual(load.Loading.Value)).First() : null;
                    if (match != null)
                        match.NodeRefs.Add(nRef);
                    else
                    {
                        load.NodeRefs.Add(nRef);
                        loadSubList.Add(load);
                    }
                }

                loads.AddRange(loadSubList);
            }

            dict[typeof(GSA0DLoad)].AddRange(loads);

            if (loads.Count() > 0 || deletedLines.Length > 0) return true;

            return false;
        }

        public static GSA0DLoad ParseGWACommand(string command, List<GSANode> nodes)
        {
            GSA0DLoad ret = new Structural0DLoad() as GSA0DLoad;

            ret.GWACommand = command;

            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier
            ret.Name = pieces[counter++].Trim(new char[] { '"' });

            int[] targetNodeRefs = pieces[counter++].ParseGSAList(GsaEntity.NODE);

            if (nodes != null)
            {
                List<GSANode> targetNodes = nodes
                    .Where(n => targetNodeRefs.Contains(Convert.ToInt32(n.StructuralID))).ToList();

                ret.NodeRefs = nodes.Select(n => n.StructuralID).ToList();
                ret.SubGWACommand.AddRange(nodes.Select(n => n.GWACommand));
            }

            ret.LoadCaseRef = pieces[counter++];

            string axis = pieces[counter++];
            ret.Axis = axis == "GLOBAL" ? 0 : Convert.ToInt32(axis);

            ret.Loading = new StructuralVectorSix(new double[6]);
            string direction = pieces[counter++].ToLower();
            switch (direction.ToUpper())
            {
                case "X":
                    ret.Loading.Value[0] = Convert.ToDouble(pieces[counter++]);
                    break;
                case "Y":
                    ret.Loading.Value[1] = Convert.ToDouble(pieces[counter++]);
                    break;
                case "Z":
                    ret.Loading.Value[2] = Convert.ToDouble(pieces[counter++]);
                    break;
                case "XX":
                    ret.Loading.Value[3] = Convert.ToDouble(pieces[counter++]);
                    break;
                case "YY":
                    ret.Loading.Value[4] = Convert.ToDouble(pieces[counter++]);
                    break;
                case "ZZ":
                    ret.Loading.Value[5] = Convert.ToDouble(pieces[counter++]);
                    break;
                default:
                    // TODO: Error case maybe?
                    break;
            }
            return ret;
        }
        #endregion

        #region Receiving Functions
        public static void SetObjects(Dictionary<Type, List<IStructural>> dict)
        {
            if (!dict.ContainsKey(typeof(Structural0DLoad))) return;

            foreach (IStructural obj in dict[typeof(Structural0DLoad)])
            {
                Set(obj as Structural0DLoad);
            }
        }

        public static void Set(Structural0DLoad load)
        {
            if (load == null)
                return;

            if (load.Loading == null)
                return;

            string keyword = MethodBase.GetCurrentMethod().DeclaringType.GetGSAKeyword();

            int index = Indexer.ResolveIndex(keyword, load);
            List<int> nodeRefs = Indexer.ResolveIndices(typeof(GSANode).GetGSAKeyword(), load.NodeRefs);
            int loadCaseRef = Indexer.ResolveIndex(typeof(GSALoadCase).GetGSAKeyword(), load.LoadCaseRef);
            
            string[] direction = new string[6] { "X", "Y", "Z", "X", "Y", "Z" };

            for (int i = 0; i < load.Loading.Value.Count(); i++)
            {
                List<string> ls = new List<string>();

                if (load.Loading.Value[i] == 0) continue;

                ls.Add("SET_AT");
                ls.Add(index.ToString());
                ls.Add(keyword);
                ls.Add(load.Name == null || load.Name == "" ? " " : load.Name);
                ls.Add(string.Join(" ", nodeRefs));
                ls.Add(loadCaseRef.ToString());
                ls.Add("GLOBAL"); // Axis
                ls.Add(direction[i]);
                ls.Add(load.Loading.Value[i].ToString());

                GSA.RunGWACommand(string.Join(",", ls));
            }
        }
        #endregion
    }
}
