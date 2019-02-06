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

        public static readonly Type[] ReadPrerequisite = new Type[1] { typeof(GSA2DElement) };
        public static readonly Type[] WritePrerequisite = new Type[2] { typeof(GSA2DElement), typeof(GSA2DMember) };

        public int Axis;
        public bool Projected;

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

        #region GSAObject Functions
        public static void GetObjects(Dictionary<Type, List<StructuralObject>> dict)
        {
            if (!dict.ContainsKey(typeof(GSA2DElement))) return;

            List<StructuralObject> elements = dict[typeof(GSA2DElement)];
            List<int> elemRefs = elements.Select(e => e.Reference).ToList();

            List<StructuralObject> loads = new List<StructuralObject>();

            string res = (string)GSA.RunGWACommand("GET_ALL,LOAD_2D_FACE");

            if (res == "")
                return;

            string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            double counter = 1;
            foreach (string p in pieces)
            {
                List<GSA2DLoad> loadSubList = new List<GSA2DLoad>();

                // Placeholder load object to get list of nodes and load values
                // Need to transform to axis
                GSA2DLoad initLoad = new GSA2DLoad();
                initLoad.ParseGWACommand(p);

                // Only send those where the nodes actually exists
                List<int> elemsApplied = initLoad.Elements
                    .Where(eRef => elemRefs.Contains(eRef)).ToList();

                foreach (int eRef in elemsApplied)
                {
                    GSA2DLoad load = new GSA2DLoad();
                    load.Name = initLoad.Name;
                    load.LoadCase = initLoad.LoadCase;

                    // Transform load to defined axis
                    GSA2DElement elem = elements.Where(e => e.Reference == eRef).First() as GSA2DElement;
                    Axis loadAxis = HelperFunctions.Parse2DAxis(
                        elem.Coordinates.ToArray(),
                        0,
                        load.Axis != 0); // Assumes if not global, local
                    load.Loading = initLoad.Loading;
                    if (load.Projected)
                    {
                        load.Loading.X = 0;
                        load.Loading.Y = 0;
                    }
                    load.Loading.TransformOntoAxis(loadAxis);

                    // If the loading already exists, add node ref to list
                    List<GSA2DLoad> matches = loadSubList.Where(l => l.Loading == load.Loading).ToList();

                    if (matches.Count() > 0)
                        matches[0].Elements.Add(eRef);
                    else
                    {
                        load.Elements.Add(eRef);
                        loadSubList.Add(load);
                    }
                }

                loads.AddRange(loadSubList);
                Status.ChangeStatus("Reading 2D face loads", counter++ / pieces.Length * 100);
            }

            dict[typeof(GSA2DLoad)] = loads;
        }

        public static void WriteObjects(Dictionary<Type, List<StructuralObject>> dict)
        {
            if (!dict.ContainsKey(typeof(GSA2DLoad))) return;
            if (!dict.ContainsKey(typeof(GSA2DElement)) & GSA.TargetAnalysisLayer) return;
            if (!dict.ContainsKey(typeof(GSA2DMember)) & GSA.TargetDesignLayer) return;

            List<StructuralObject> loads = dict[typeof(GSA2DLoad)];

            double counter = 1;
            foreach (StructuralObject l in loads)
            {
                GSARefCounters.RefObject(l);

                if (GSA.TargetAnalysisLayer)
                {
                    // Add mesh elements to target
                    List<StructuralObject> elements = dict[typeof(GSA2DElement)];

                    List<int> matches = elements.Where(e => (l as GSA2DLoad).Meshes.Contains((e as GSA2DElement).MeshReference))
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
            Elements = pieces[counter++].ParseGSAList(GsaEntity.ELEMENT).ToList();
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
                subLs.Add(Name == "" ? " " : "");
                // TODO: This is a hack.
                if (GSA.TargetDesignLayer)
                {
                    subLs.Add(string.Join(" ", Meshes.Select(x => "G" + x.ToString())));
                }
                else
                {
                    subLs.Add(string.Join(" ", Elements));
                }
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
