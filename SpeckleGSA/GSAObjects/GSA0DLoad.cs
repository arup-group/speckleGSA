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
        public int Axis;

        public string GWACommand { get; set; }
        public List<string> SubGWACommand { get; set; }

        #region Contructors and Converters
        public GSA0DLoad()
        {
            GWACommand = "";
            SubGWACommand = new List<string>();
            Axis = 0;
        }

        public GSA0DLoad(Structural0DLoad baseClass)
        {
            GWACommand = "";
            SubGWACommand = new List<string>();
            Axis = 0;

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
            
            List<object> loads = new List<object>();

            string[] lines = GSA.GetGWAGetCommands("GET_ALL,LOAD_NODE");
            string[] deletedLines = GSA.GetDeletedGWAGetCommands("GET_ALL,LOAD_NODE");

            // Remove deleted lines
            dict[typeof(GSA0DLoad)].RemoveAll(l => deletedLines.Contains(((IGSAObject)l).GWACommand));
            foreach (KeyValuePair<Type, List<object>> kvp in dict)
                kvp.Value.RemoveAll(l => ((IGSAObject)l).SubGWACommand.Any(x => deletedLines.Contains(x)));

            // Filter only new lines
            string[] prevLines = dict[typeof(GSA0DLoad)].Select(l => ((GSA0DLoad)l).GWACommand).ToArray();
            string[] newLines = lines.Where(l => !prevLines.Contains(l)).ToArray();

            List<object> nodes = dict[typeof(GSANode)];
            
            foreach (string p in newLines)
            {
                List<GSA0DLoad> loadSubList = new List<GSA0DLoad>();

                // Placeholder load object to get list of nodes and load values
                // Need to transform to axis so one load definition may be transformed to many
                GSA0DLoad initLoad = new GSA0DLoad();
                initLoad.ParseGWACommand(p,dict);

                // Raise node flag to make sure it gets sent
                foreach(GSANode n in nodes.Where(n => initLoad.Nodes.Contains(((GSANode)n).Reference)).Cast<GSANode>())
                    n.ForceSend = true;

                // Create load for each node applied
                foreach (int nRef in initLoad.Nodes)
                {
                    GSA0DLoad load = new GSA0DLoad();
                    load.GWACommand = initLoad.GWACommand;
                    load.SubGWACommand = new List<string>(initLoad.SubGWACommand);
                    load.Name = initLoad.Name;
                    load.LoadCase = initLoad.LoadCase;
                    
                    // Transform load to defined axis
                    GSANode node = nodes.Where(n => ((GSANode)n).Reference == nRef).First() as GSANode;
                    Axis loadAxis = HelperFunctions.Parse0DAxis(initLoad.Axis, node.Coordinates.ToArray());
                    load.Loading = initLoad.Loading;
                    load.Loading.TransformOntoAxis(loadAxis);

                    // If the loading already exists, add node ref to list
                    GSA0DLoad match = loadSubList.Count() > 0 ? loadSubList.Where(l => l.Loading.Equals(load.Loading)).First() : null;
                    if (match != null)
                        match.Nodes.Add(nRef);
                    else
                    {
                        load.Nodes.Add(nRef);
                        loadSubList.Add(load);
                    }
                }

                loads.AddRange(loadSubList);
            }

            dict[typeof(GSA0DLoad)].AddRange(loads);

            if (loads.Count() > 0 || deletedLines.Length > 0) return true;

            return false;
        }

        public static void WriteObjects(Dictionary<Type, List<StructuralObject>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType)) return;

            List<StructuralObject> loads = dict[typeof(GSA0DLoad)];

            double counter = 1;
            foreach (StructuralObject l in loads)
            {
                GSARefCounters.RefObject(l);
                
                List<string> commands = (l as GSA0DLoad).GetGWACommand();
                foreach (string c in commands)
                    GSA.RunGWACommand(c);

                Status.ChangeStatus("Writing 0D loads", counter++ / loads.Count() * 100);
            }
        }

        public void ParseGWACommand(string command, Dictionary<Type, List<object>> dict = null)
        {
            GWACommand = command;

            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier
            Name = pieces[counter++].Trim(new char[] { '"' });

            int[] targetNodes = pieces[counter++].ParseGSAList(GsaEntity.NODE);

            if (dict.ContainsKey(typeof(GSANode)))
            {
                List<GSANode> nodes = dict[typeof(GSANode)].Cast<GSANode>()
                    .Where(n => targetNodes.Contains(n.Reference)).ToList();

                Nodes = nodes.Select(n => n.Reference).ToList();
                SubGWACommand.AddRange(nodes.Select(n => n.GWACommand));
            }

            LoadCase = Convert.ToInt32(pieces[counter++]);

            string axis = pieces[counter++];
            Axis = axis == "GLOBAL" ? 0 : Convert.ToInt32(axis);

            string direction = pieces[counter++].ToLower();
            switch(direction.ToUpper())
            {
                case "X":
                    Loading.X = Convert.ToDouble(pieces[counter++]);
                    break;
                case "Y":
                    Loading.Y = Convert.ToDouble(pieces[counter++]);
                    break;
                case "Z":
                    Loading.Z = Convert.ToDouble(pieces[counter++]);
                    break;
                case "XX":
                    Loading.XX = Convert.ToDouble(pieces[counter++]);
                    break;
                case "YY":
                    Loading.YY = Convert.ToDouble(pieces[counter++]);
                    break;
                case "ZZ":
                    Loading.ZZ = Convert.ToDouble(pieces[counter++]);
                    break;
                default:
                    // TODO: Error case maybe?
                    break;
            }
        }

        public List<string> GetGWACommand(Dictionary<Type, object> dict = null)
        {
            List<string> ls = new List<string>();

            double[] values = Loading.ToArray();
            string[] direction = new string[6] { "X", "Y", "Z", "X", "Y", "Z" };

            for(int i = 0; i < 6; i++)
            {
                List<string> subLs = new List<string>();
                
                if (values[i] == 0) continue;

                subLs.Add("SET");
                subLs.Add((string)this.GetAttribute("GSAKeyword"));
                subLs.Add(Name == "" ? " " : Name);
                subLs.Add(string.Join(" ", Nodes));
                subLs.Add(LoadCase.ToString());
                subLs.Add("GLOBAL"); // Axis
                subLs.Add(direction[i]);
                subLs.Add(values[i].ToString());

                ls.Add(string.Join(",", subLs));
            }

            return ls;
        }
        #endregion
    }
}
