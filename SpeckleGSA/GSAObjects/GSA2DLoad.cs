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
    public class GSA2DLoad : Structural2DLoad
    {
        public static readonly string GSAKeyword = "LOAD_2D_FACE";
        public static readonly string Stream = "loads";

        public static readonly Type[] ReadPrerequisite = new Type[2] { typeof(GSA2DElement), typeof(GSA2DMember) };
        public static readonly Type[] WritePrerequisite = new Type[2] { typeof(GSA2DElement), typeof(GSA2DMember) };
        public static readonly bool AnalysisLayer = true;
        public static readonly bool DesignLayer = true;

        public int Axis;
        public bool Projected;

        #region Contructors and Converters
        public GSA2DLoad()
        {
            Axis = 0;
            Projected = false;
        }

        public GSA2DLoad(Structural2DLoad baseClass)
        {
            Axis = 0;
            Projected = false;

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

            string res = (string)GSA.RunGWACommand("GET_ALL,LOAD_2D_FACE");

            if (res == "")
                return;

            string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            List<StructuralObject> elements = GSA.TargetAnalysisLayer ? dict[typeof(GSA2DElement)] : new List<StructuralObject>();
            List<StructuralObject> members = GSA.TargetDesignLayer ? dict[typeof(GSA2DMember)] : new List<StructuralObject>();

            double counter = 1;
            foreach (string p in pieces)
            {
                List<GSA2DLoad> loadSubList = new List<GSA2DLoad>();

                // Placeholder load object to get list of elements and load values
                // Need to transform to axis
                GSA2DLoad initLoad = new GSA2DLoad();
                initLoad.ParseGWACommand(p,dict);
                
                if (!Settings.Merge2DElementsIntoMesh & GSA.TargetAnalysisLayer)
                { 
                    // Create load for each element applied
                    foreach (int eRef in initLoad.Elements)
                    {
                        GSA2DLoad load = new GSA2DLoad();
                        load.Name = initLoad.Name;
                        load.LoadCase = initLoad.LoadCase;

                        // Transform load to defined axis
                        GSA2DElement elem = elements.Where(e => e.Reference == eRef).First() as GSA2DElement;
                        Axis loadAxis = HelperFunctions.Parse2DAxis(elem.Coordinates.ToArray(), 0, load.Axis != 0); // Assumes if not global, local
                        load.Loading = initLoad.Loading;
                        if (load.Projected)
                        {
                            load.Loading.X = 0;
                            load.Loading.Y = 0;
                        }
                        load.Loading.TransformOntoAxis(loadAxis);

                        // If the loading already exists, add element ref to list
                        GSA2DLoad match = loadSubList.Count() > 0 ? loadSubList.Where(l => l.Loading.Equals(load.Loading)).First() : null;
                        if (match != null)
                            match.Elements.Add(eRef);
                        else
                        {
                            load.Elements.Add(eRef);
                            loadSubList.Add(load);
                        }
                    }
                }

                if (GSA.TargetDesignLayer)
                {
                    // Create load for each member applied
                    foreach (int mRef in initLoad.Elements)
                    {
                        GSA2DLoad load = new GSA2DLoad();
                        load.Name = initLoad.Name;
                        load.LoadCase = initLoad.LoadCase;

                        // Transform load to defined axis
                        GSA2DMember memb = members.Where(m => m.Reference == mRef).First() as GSA2DMember;
                        Axis loadAxis = HelperFunctions.Parse2DAxis(memb.Coordinates().ToArray(), 0, load.Axis != 0); // Assumes if not global, local
                        load.Loading = initLoad.Loading;
                        if (load.Projected)
                        {
                            load.Loading.X = 0;
                            load.Loading.Y = 0;
                        }
                        load.Loading.TransformOntoAxis(loadAxis);

                        // If the loading already exists, add element ref to list
                        GSA2DLoad match = loadSubList.Count() > 0 ? loadSubList.Where(l => l.Loading.Equals(load.Loading)).First() : null;
                        if (match != null)
                            match.Elements.Add(mRef);
                        else
                        {
                            load.Elements.Add(mRef);
                            loadSubList.Add(load);
                        }
                    }
                }

                loads.AddRange(loadSubList);
                Status.ChangeStatus("Reading 2D face loads", counter++ / pieces.Length * 100);
            }

            dict[typeof(GSA2DLoad)].AddRange(loads);
        }

        public static void WriteObjects(Dictionary<Type, List<StructuralObject>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType)) return;
            
            if (GSA.TargetAnalysisLayer && !dict.ContainsKey(typeof(GSA2DElement))) return;
            if (GSA.TargetDesignLayer && !dict.ContainsKey(typeof(GSA2DMember))) return;

            List<StructuralObject> loads = dict[typeof(GSA2DLoad)];

            double counter = 1;
            foreach (StructuralObject l in loads)
            {
                GSARefCounters.RefObject(l);

                if (GSA.TargetAnalysisLayer)
                {
                    // Add mesh elements to target
                    List<StructuralObject> elements = dict[typeof(GSA2DElement)];

                    List<int> matches = elements.Where(e => (l as GSA2DLoad).Elements.Contains((e as GSA2DElement).MeshReference))
                        .Select(e => e.Reference).ToList();
                    (l as GSA2DLoad).Elements.AddRange(matches);
                    (l as GSA2DLoad).Elements = (l as GSA2DLoad).Elements.Distinct().ToList();
                }

                List<string> commands = (l as GSA2DLoad).GetGWACommand();
                foreach (string c in commands)
                    GSA.RunGWACommand(c);

                Status.ChangeStatus("Writing 2D face loads", counter++ / loads.Count() * 100);
            }
        }

        public void ParseGWACommand(string command, Dictionary<Type, List<StructuralObject>> dict = null)
        {
            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier
            Name = pieces[counter++].Trim(new char[] { '"' });

            if (GSA.TargetAnalysisLayer)
            {
                int[] targetElements = pieces[counter++].ParseGSAList(GsaEntity.ELEMENT);

                if (dict.ContainsKey(typeof(GSA2DElement)))
                { 
                    Elements = dict[typeof(GSA2DElement)].Cast<GSA2DElement>()
                        .Where(n => targetElements.Contains(n.Reference))
                        .Select(n => n.Reference).ToList();
                }
            }
            else
            {
                int[] targetGroups = pieces[counter++].GetGroupsFromGSAList();

                if (dict.ContainsKey(typeof(GSA2DMember)))
                {
                    Elements = dict[typeof(GSA2DMember)].Cast<GSA2DMember>()
                        .Where(m => targetGroups.Contains(m.Group))
                        .Select(m => m.Reference).ToList();
                }
            }

            LoadCase = Convert.ToInt32(pieces[counter++]);

            string axis = pieces[counter++];
            Axis = axis == "GLOBAL" ? 0 : 1; // 1 denotes "LOCAL"

            counter++; // Type. Skipping since we're taking the average

            Projected = pieces[counter++] == "YES";
            
            string direction = pieces[counter++].ToLower();
            double[] values = pieces.Skip(counter).Select(p => Convert.ToDouble(p)).ToArray();

            switch (direction.ToUpper())
            {
                case "X":
                    Loading.X = values.Average();
                    break;
                case "Y":
                    Loading.Y = values.Average();
                    break;
                case "Z":
                    Loading.Z = values.Average();
                    break;
                default:
                    // TODO: Error case maybe?
                    break;
            }
        }

        public List<string> GetGWACommand(Dictionary<Type, List<StructuralObject>> dict = null)
        {
            List<string> ls = new List<string>();

            double[] values = Loading.ToArray();
            string[] direction = new string[3] { "X", "Y", "Z"};

            for (int i = 0; i < 3; i++)
            {
                List<string> subLs = new List<string>();
                
                if (values[i] == 0) continue;

                subLs.Add("SET");
                subLs.Add(GSAKeyword);
                subLs.Add(Name == "" ? " " : Name);

                // TODO: This is a hack.
                List<string> target = new List<string>();
                if (GSA.TargetAnalysisLayer)
                    target.AddRange(Elements.Select(x => x.ToString()));
                else
                    target.AddRange(Elements.Select(x => "G" + x.ToString()));
                subLs.Add(string.Join(" ",target));
                
                subLs.Add(LoadCase.ToString());
                subLs.Add("GLOBAL"); // Axis
                subLs.Add("CONS"); // Type
                subLs.Add("NO"); // Projected
                subLs.Add(direction[i]);
                subLs.Add(values[i].ToString());

                ls.Add(string.Join(",", subLs));
            }

            return ls;
        }
        #endregion
    }
}
