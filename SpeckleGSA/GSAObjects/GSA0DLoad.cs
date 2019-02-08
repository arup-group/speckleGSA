using Interop.Gsa_10_0;
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
    public class GSA0DLoad : Structural0DLoad
    {
        public static readonly string GSAKeyword = "LOAD_NODE";
        public static readonly string Stream = "loads";

        public static readonly Type[] ReadPrerequisite = new Type[1] { typeof(GSANode) };
        public static readonly Type[] WritePrerequisite = new Type[1] { typeof(GSANode) };
        public static readonly bool AnalysisLayer = true;
        public static readonly bool DesignLayer = true;

        public int Axis;

        #region Contructors and Converters
        public GSA0DLoad()
        {
            Axis = 0;
        }

        public GSA0DLoad(Structural0DLoad baseClass)
        {
            Axis = 0;

            foreach (FieldInfo f in baseClass.GetType().GetFields())
                f.SetValue(this, f.GetValue(baseClass));

            foreach (PropertyInfo p in baseClass.GetType().GetProperties())
                p.SetValue(this, p.GetValue(baseClass));
        }
        
        public StructuralObject GetBase()
        {
            StructuralObject baseClass = (StructuralObject)Activator.CreateInstance(this.GetType().BaseType);

            foreach (FieldInfo f in baseClass.GetType().GetFields())
                f.SetValue(baseClass, f.GetValue(this));

            foreach (PropertyInfo p in baseClass.GetType().GetProperties())
                p.SetValue(baseClass, p.GetValue(this));

            return baseClass;
        }
        #endregion

        #region GSA Functions
        public static void GetObjects(Dictionary<Type, List<StructuralObject>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType))
                dict[MethodBase.GetCurrentMethod().DeclaringType] = new List<StructuralObject>();
            
            List<StructuralObject> loads = new List<StructuralObject>();

            string res = (string)GSA.RunGWACommand("GET_ALL,LOAD_NODE");

            if (res == "")
                return;

            string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
            
            List<StructuralObject> nodes = dict[typeof(GSANode)];
            
            double counter = 1;
            foreach (string p in pieces)
            {
                List<GSA0DLoad> loadSubList = new List<GSA0DLoad>();

                // Placeholder load object to get list of nodes and load values
                // Need to transform to axis so one load definition may be transformed to many
                GSA0DLoad initLoad = new GSA0DLoad();
                initLoad.ParseGWACommand(p,dict);

                // Raise node flag to make sure it gets sent
                foreach(GSANode n in nodes.Where(n => initLoad.Nodes.Contains(n.Reference)).Cast<GSANode>())
                    n.ForceSend = true;

                // Create load for each node applied
                foreach (int nRef in initLoad.Nodes)
                {
                    GSA0DLoad load = new GSA0DLoad();
                    load.Name = initLoad.Name;
                    load.LoadCase = initLoad.LoadCase;
                    
                    // Transform load to defined axis
                    GSANode node = nodes.Where(n => n.Reference == nRef).First() as GSANode;
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

                Status.ChangeStatus("Reading 0D loads", counter++ / pieces.Length * 100);
            }

            dict[typeof(GSA0DLoad)].AddRange(loads);
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

        public void ParseGWACommand(string command, Dictionary<Type, List<StructuralObject>> dict = null)
        {
            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier
            Name = pieces[counter++].Trim(new char[] { '"' });

            int[] targetNodes = pieces[counter++].ParseGSAList(GsaEntity.NODE);

            if (dict.ContainsKey(typeof(GSANode)))
            {
                Nodes = dict[typeof(GSANode)].Cast<GSANode>()
                    .Where(n => targetNodes.Contains(n.Reference))
                    .Select(n => n.Reference).ToList();
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
                subLs.Add(GSAKeyword);
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
