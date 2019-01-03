using Interop.Gsa_9_0;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace SpeckleGSA
{
    public class GSA0DLoad : GSAObject
    {
        public static readonly string GSAKeyword = "LOAD_NODE";
        public static readonly string Stream = "loads";
        public static readonly int ReadPriority = 3;
        public static readonly int WritePriority = 9999;
        
        public List<int> Nodes { get; set; }
        public int Case { get; set; }
        public Dictionary<string, object> Loading { get; set; }

        public int Axis;

        public GSA0DLoad()
        {
            Nodes = new List<int>();
            Case = 1;

            Loading = new Dictionary<string, object>()
            {
                { "x", 0.0 },
                { "y", 0.0 },
                { "z", 0.0 },
                { "xx", 0.0 },
                { "yy", 0.0 },
                { "zz", 0.0 },
            };
            Axis = 0;
        }

        #region GSAObject Functions
        public static void GetObjects(ComAuto gsa, Dictionary<Type, object> dict)
        {
            List<GSAObject> nodes = dict[typeof(GSANode)] as List<GSAObject>;
            List<int> nodeRefs = nodes.Select(n => n.Reference).ToList(); 

            List<GSAObject> loads = new List<GSAObject>();

            string res = gsa.GwaCommand("GET_ALL,LOAD_NODE");

            if (res == "")
                return;

            string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string p in pieces)
            {
                List<GSA0DLoad> loadSubList = new List<GSA0DLoad>();

                // Placeholder load object to get list of nodes and load values
                // Need to transform to axis
                GSA0DLoad initLoad = new GSA0DLoad().AttachGSA(gsa);
                initLoad.ParseGWACommand(p);

                // Only send those where the nodes actually exists
                List<int> nodesApplied = initLoad.Nodes
                    .Where(nRef => nodeRefs.Contains(nRef)).ToList();

                foreach (int nRef in nodesApplied)
                {
                    GSA0DLoad load = new GSA0DLoad();
                    load.Name = initLoad.Name;
                    load.Case = initLoad.Case;
                    
                    // Transform load to defined axis
                    GSANode node = nodes.Where(n => n.Reference == nRef).First() as GSANode;
                    Dictionary<string, object> loadAxis = node.ParseGSANodeAxis(initLoad.Axis, node.Coor.ToArray());
                    load.Loading = load.TransformLoading(initLoad.Loading, loadAxis);

                    // If the loading already exists, add node ref to list
                    List<GSA0DLoad> matches = loadSubList.Where(l => l.Loading.IsAxisEqual(load.Loading)).ToList();
                    if (matches.Count() > 0)
                        matches[0].Nodes.Add(nRef);
                    else
                    {
                        load.Nodes.Add(nRef);
                        loadSubList.Add(load);
                    }
                }

                loads.AddRange(loadSubList);
            }

            dict[typeof(GSA0DLoad)] = loads;
        }

        public static void WriteObjects(ComAuto gsa, Dictionary<Type, object> dict)
        {
            if (!dict.ContainsKey(typeof(GSA0DLoad))) return;

            List<GSAObject> loads = dict[typeof(GSA0DLoad)] as List<GSAObject>;

            foreach (GSAObject l in loads)
            {
                l.AttachGSA(gsa);

                GSARefCounters.RefObject(l);
                
                string[] commands = l.GetGWACommand().Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string c in commands)
                    l.RunGWACommand(c);
            }
        }

        public override void ParseGWACommand(string command, Dictionary<Type, object> dict = null)
        {
            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier
            Name = pieces[counter++].Trim(new char[] { '"' });
            Nodes = pieces[counter++].ParseGSAList(gsa).ToList();
            Case = Convert.ToInt32(pieces[counter++]);

            string axis = pieces[counter++];
            Axis = axis == "GLOBAL" ? 0 : Convert.ToInt32(axis);

            string direction = pieces[counter++].ToLower();
            if (Loading.ContainsKey(direction))
                Loading[direction] = Convert.ToDouble(pieces[counter++]);
            else
                Loading["x"] = Convert.ToDouble(pieces[counter++]);
        }

        public override string GetGWACommand(Dictionary<Type, object> dict = null)
        {
            List<string> ls = new List<string>();

            foreach (string key in Loading.Keys)
            {
                List<string> subLs = new List<string>(); 

                double value = Convert.ToDouble(Loading[key]);
                if (value == 0) continue;

                subLs.Add("SET");
                subLs.Add(GSAKeyword);
                subLs.Add(Name == ""? " ": "");
                subLs.Add(string.Join(" ", Nodes));
                subLs.Add(Case.ToString());
                subLs.Add("GLOBAL"); // Axis
                subLs.Add(key);
                subLs.Add(value.ToString());

                ls.Add(string.Join(",", subLs));
            }

            return string.Join("\n", ls);
        }

        public override List<GSAObject> GetChildren()
        {
            throw new NotImplementedException();
        }

        #endregion
        public Dictionary<string, object> TransformLoading(Dictionary<string, object> loading, Dictionary<string, object> axis)
        {
            Dictionary<string, object> transformed = new Dictionary<string, object>()
            {
                { "x", 0.0 },
                { "y", 0.0 },
                { "z", 0.0 },
                { "xx", 0.0 },
                { "yy", 0.0 },
                { "zz", 0.0 },
            };

            Dictionary<string, object> X = axis["X"] as Dictionary<string, object>;
            Dictionary<string, object> Y = axis["Y"] as Dictionary<string, object>;
            Dictionary<string, object> Z = axis["Z"] as Dictionary<string, object>;

            Dictionary<string, Vector3D> axisVectors = new Dictionary<string, Vector3D>()
            {
                {"x", new Vector3D(
                    Convert.ToDouble(X["x"]),
                    Convert.ToDouble(X["y"]),
                    Convert.ToDouble(X["z"])) },
                {"y", new Vector3D(
                    Convert.ToDouble(Y["x"]),
                    Convert.ToDouble(Y["y"]),
                    Convert.ToDouble(Y["z"])) },
                {"z", new Vector3D(
                    Convert.ToDouble(Z["x"]),
                    Convert.ToDouble(Z["y"]),
                    Convert.ToDouble(Z["z"])) },
                {"xx", new Vector3D(
                    Convert.ToDouble(X["x"]),
                    Convert.ToDouble(X["y"]),
                    Convert.ToDouble(X["z"])) },
                {"yy", new Vector3D(
                    Convert.ToDouble(Y["x"]),
                    Convert.ToDouble(Y["y"]),
                    Convert.ToDouble(Y["z"])) },
                {"zz", new Vector3D(
                    Convert.ToDouble(Z["x"]),
                    Convert.ToDouble(Z["y"]),
                    Convert.ToDouble(Z["z"])) },
            };
            
            foreach(string k in new string[] { "x", "y", "z" })
            {
                if (!loading.ContainsKey(k)) continue;

                double load = Convert.ToDouble(loading[k]);

                transformed["x"] = (double)transformed["x"] + axisVectors[k].X * load;
                transformed["y"] = (double)transformed["y"] + axisVectors[k].Y * load;
                transformed["z"] = (double)transformed["z"] + axisVectors[k].Z * load;
            }

            foreach (string k in new string[] { "xx", "yy", "zz" })
            {
                if (!loading.ContainsKey(k)) continue;

                double load = Convert.ToDouble(loading[k]);

                transformed["xx"] = (double)transformed["xx"] + axisVectors[k].X * load;
                transformed["yy"] = (double)transformed["yy"] + axisVectors[k].Y * load;
                transformed["zz"] = (double)transformed["zz"] + axisVectors[k].Z * load;
            }

            return transformed;
        }
    }
}
