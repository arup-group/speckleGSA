using Interop.Gsa_10_0;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace SpeckleGSA
{
    public class GSA2DFaceLoad : GSAObject
    {
        public static readonly string GSAKeyword = "LOAD_2D_FACE";
        public static readonly string Stream = "loads";
        public static readonly int ReadPriority = 4;
        public static readonly int WritePriority = 9999;

        public List<int> Elements { get; set; }
        public List<int> Meshes { get; set; }
        public int Case { get; set; }
        public Dictionary<string, object> Loading { get; set; }

        public int Axis;
        public bool Projected;

        public GSA2DFaceLoad()
        {
            Elements = new List<int>();
            Meshes = new List<int>();

            Case = 1;

            Loading = new Dictionary<string, object>()
            {
                { "x", 0.0 },
                { "y", 0.0 },
                { "z", 0.0 },
            };

            Axis = 0;
            Projected = false;
        }

        #region GSAObject Functions
        public static void GetObjects(Dictionary<Type, object> dict)
        {
            if (!dict.ContainsKey(typeof(GSA2DElement))) return;

            List<GSAObject> elements = dict[typeof(GSA2DElement)] as List<GSAObject>;
            List<int> elemRefs = elements.Select(e => e.Reference).ToList();

            List<GSAObject> loads = new List<GSAObject>();

            string res = (string)GSA.RunGWACommand("GET_ALL,LOAD_2D_FACE");

            if (res == "")
                return;

            string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            double counter = 1;
            foreach (string p in pieces)
            {
                List<GSA2DFaceLoad> loadSubList = new List<GSA2DFaceLoad>();

                // Placeholder load object to get list of nodes and load values
                // Need to transform to axis
                GSA2DFaceLoad initLoad = new GSA2DFaceLoad();
                initLoad.ParseGWACommand(p);

                // Only send those where the nodes actually exists
                List<int> elemsApplied = initLoad.Elements
                    .Where(eRef => elemRefs.Contains(eRef)).ToList();

                foreach (int eRef in elemsApplied)
                {
                    GSA2DFaceLoad load = new GSA2DFaceLoad();
                    load.Name = initLoad.Name;
                    load.Case = initLoad.Case;

                    // Transform load to defined axis
                    GSA2DElement elem = elements.Where(e => e.Reference == eRef).First() as GSA2DElement;
                    Dictionary<string, object> loadAxis = HelperFunctions.Parse2DAxis(
                        elem.Coor.ToArray(),
                        0,
                        load.Axis != 0); // Assumes if not global, local
                    load.Loading = load.TransformLoading(initLoad.Loading, loadAxis, load.Projected);

                    // If the loading already exists, add node ref to list
                    List<GSA2DFaceLoad> matches = loadSubList.Where(l => l.Loading.IsAxisEqual(load.Loading)).ToList();

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

            dict[typeof(GSA2DFaceLoad)] = loads;
        }

        public static void WriteObjects(Dictionary<Type, object> dict)
        {
            if (!dict.ContainsKey(typeof(GSA2DFaceLoad))) return;

            List<GSAObject> elements = dict[typeof(GSA2DElement)] as List<GSAObject>;

            List<GSAObject> loads = dict[typeof(GSA2DFaceLoad)] as List<GSAObject>;

            double counter = 1;
            foreach (GSAObject l in loads)
            {
                GSARefCounters.RefObject(l);

                // Target meshes
                List<int> matches = elements.Where(e => (l as GSA2DFaceLoad).Meshes.Contains((e as GSA2DElement).MeshReference))
                    .Select(e => e.Reference).ToList();
                (l as GSA2DFaceLoad).Elements.AddRange(matches);
                (l as GSA2DFaceLoad).Elements = (l as GSA2DFaceLoad).Elements.Distinct().ToList();

                string[] commands = l.GetGWACommand().Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string c in commands)
                    GSA.RunGWACommand(c);

                Status.ChangeStatus("Writing 2D face loads", counter++ / loads.Count() * 100);
            }
        }

        public override void ParseGWACommand(string command, Dictionary<Type, object> dict = null)
        {
            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier
            Name = pieces[counter++].Trim(new char[] { '"' });
            Elements = pieces[counter++].ParseGSAList(GsaEntity.ELEMENT).ToList();
            Case = Convert.ToInt32(pieces[counter++]);

            string axis = pieces[counter++];
            Axis = axis == "GLOBAL" ? 0 : 1; // 1 denotes "LOCAL"

            counter++; // Type. Skipping since we're taking the average

            Projected = pieces[counter++] == "YES";
            
            string direction = pieces[counter++].ToLower();

            double[] values = pieces.Skip(counter).Select(p => Convert.ToDouble(p)).ToArray();

            if (Loading.ContainsKey(direction))
                Loading[direction] = values.Average();
            else
                Loading["x"] = values.Average();
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
                subLs.Add(Name == "" ? " " : "");
                subLs.Add(string.Join(" ", Elements));
                subLs.Add(Case.ToString());
                subLs.Add("GLOBAL"); // Axis
                subLs.Add("CONS"); // Type
                subLs.Add("NO"); // Projected
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

        public Dictionary<string, object> TransformLoading(Dictionary<string, object> loading, Dictionary<string, object> axis, bool isProjected = false)
        {
            Dictionary<string, object> transformed = new Dictionary<string, object>()
            {
                { "x", 0.0 },
                { "y", 0.0 },
                { "z", 0.0 },
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
            };

            foreach (string k in new string[] { "x", "y", "z" })
            {
                if (isProjected)
                    if (k == "x" | k == "y") continue;

                if (!loading.ContainsKey(k)) continue;

                double load = Convert.ToDouble(loading[k]);

                transformed["x"] = (double)transformed["x"] + axisVectors[k].X * load;
                transformed["y"] = (double)transformed["y"] + axisVectors[k].Y * load;
                transformed["z"] = (double)transformed["z"] + axisVectors[k].Z * load;
            }

            return transformed;
        }
    }
}
