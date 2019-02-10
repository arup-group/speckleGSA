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
    public class GSA1DLoad : Structural1DLoad
    {
        public static readonly string GSAKeyword = "LOAD_BEAM";
        public static readonly string Stream = "loads";

        public static readonly Type[] ReadPrerequisite = new Type[2] { typeof(GSA1DElement), typeof(GSA1DMember) };
        public static readonly Type[] WritePrerequisite = new Type[2] { typeof(GSA1DElement), typeof(GSA1DMember)  };
        public static readonly bool AnalysisLayer = true;
        public static readonly bool DesignLayer = true;

        public int Axis;
        public bool Projected;

        #region Contructors and Converters
        public GSA1DLoad()
        {
            Axis = 0;
            Projected = false;
        }

        public GSA1DLoad(Structural1DLoad baseClass)
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

            string res = (string)GSA.RunGWACommand("GET_ALL,LOAD_BEAM");

            if (res == "")
                return;

            string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            List<StructuralObject> elements = GSA.TargetAnalysisLayer ? dict[typeof(GSA1DElement)] : new List<StructuralObject>();
            List<StructuralObject> members = GSA.TargetDesignLayer ? dict[typeof(GSA1DMember)] : new List<StructuralObject>();

            double counter = 1;
            foreach (string p in pieces)
            {
                List<GSA1DLoad> loadSubList = new List<GSA1DLoad>();

                // Placeholder load object to get list of elements and load values
                // Need to transform to axis so one load definition may be transformed to many
                GSA1DLoad initLoad = new GSA1DLoad();
                initLoad.ParseGWACommand(p,dict);

                if (!Settings.Merge1DElementsIntoPolyline && GSA.TargetAnalysisLayer)
                { 
                    // Create load for each element applied
                    foreach (int nRef in initLoad.Elements)
                    {
                        GSA1DLoad load = new GSA1DLoad();
                        load.Name = initLoad.Name;
                        load.LoadCase = initLoad.LoadCase;

                        // Transform load to defined axis
                        GSA1DElement elem = elements.Where(e => e.Reference == nRef).First() as GSA1DElement;
                        Axis loadAxis = load.Axis == 0 ? new Axis() : elem.Axis; // Assumes if not global, local
                        load.Loading = initLoad.Loading;
                        load.Loading.TransformOntoAxis(loadAxis);
                    
                        // Perform projection
                        if (load.Projected)
                        {
                            Vector3D loadDirection = new Vector3D(
                                load.Loading.X,
                                load.Loading.Y,
                                load.Loading.Z);

                            if (loadDirection.Length > 0)
                            {
                                double angle = Vector3D.AngleBetween(loadDirection, elem.Axis.X);
                                double factor = Math.Sin(angle);
                                load.Loading.X *= factor;
                                load.Loading.Y *= factor;
                                load.Loading.Z *= factor;
                            }
                        }

                        // If the loading already exists, add node ref to list
                        GSA1DLoad match = loadSubList.Count() > 0 ? loadSubList.Where(l => l.Loading.Equals(load.Loading)).First() : null;
                        if (match != null)
                            match.Elements.Add(nRef);
                        else
                        {
                            load.Elements.Add(nRef);
                            loadSubList.Add(load);
                        }
                    }
                }

                if (GSA.TargetDesignLayer)
                {
                    // Create load for each member applied
                    foreach (int mRef in initLoad.Elements)
                    {
                        GSA1DLoad load = new GSA1DLoad();
                        load.Name = initLoad.Name;
                        load.LoadCase = initLoad.LoadCase;

                        // Transform load to defined axis
                        GSA1DMember memb = members.Where(e => e.Reference == mRef).First() as GSA1DMember;
                        Axis loadAxis = load.Axis == 0 ? new Axis() : memb.Axis; // Assumes if not global, local
                        load.Loading = initLoad.Loading;
                        load.Loading.TransformOntoAxis(loadAxis);

                        // Perform projection
                        if (load.Projected)
                        {
                            Vector3D loadDirection = new Vector3D(
                                load.Loading.X,
                                load.Loading.Y,
                                load.Loading.Z);

                            if (loadDirection.Length > 0)
                            {
                                double angle = Vector3D.AngleBetween(loadDirection, memb.Axis.X);
                                double factor = Math.Sin(angle);
                                load.Loading.X *= factor;
                                load.Loading.Y *= factor;
                                load.Loading.Z *= factor;
                            }
                        }

                        // If the loading already exists, add node ref to list
                        GSA1DLoad match = loadSubList.Count() > 0 ? loadSubList.Where(l => l.Loading.Equals(load.Loading)).First() : null;
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

                Status.ChangeStatus("Reading 1D loads", counter++ / pieces.Length * 100);
            }

            dict[typeof(GSA0DLoad)].AddRange(loads);
        }

        public static void WriteObjects(Dictionary<Type, List<StructuralObject>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType)) return;

            List<StructuralObject> loads = dict[typeof(GSA1DLoad)];

            double counter = 1;
            foreach (StructuralObject l in loads)
            {
                GSARefCounters.RefObject(l);

                List<string> commands = (l as GSA1DLoad).GetGWACommand();
                foreach (string c in commands)
                    GSA.RunGWACommand(c);

                Status.ChangeStatus("Writing 1D loads", counter++ / loads.Count() * 100);
            }
        }

        public void ParseGWACommand(string command, Dictionary<Type, List<StructuralObject>> dict = null)
        {
            string[] pieces = command.ListSplit(",");

            int counter = 0;
            string identifier = pieces[counter++];

            Name = pieces[counter++].Trim(new char[] { '"' });
            
            if (GSA.TargetAnalysisLayer)
            {
                int[] targetElements = pieces[counter++].ParseGSAList(GsaEntity.ELEMENT);

                if (dict.ContainsKey(typeof(GSA1DElement)))
                {
                    Elements = dict[typeof(GSA1DElement)].Cast<GSA1DElement>()
                        .Where(n => targetElements.Contains(n.Reference))
                        .Select(n => n.Reference).ToList();
                }
            }
            else
            {
                if (dict.ContainsKey(typeof(GSA1DMember)))
                {
                    int[] targetGroups = pieces[counter++].GetGroupsFromGSAList();

                    Elements = dict[typeof(GSA1DMember)].Cast<GSA1DMember>()
                        .Where(m => targetGroups.Contains(m.Group))
                        .Select(m => m.Reference).ToList();
                }
            }

            LoadCase = Convert.ToInt32(pieces[counter++]);

            string axis = pieces[counter++];
            Axis = axis == "GLOBAL" ? 0 : Convert.ToInt32(axis);

            Projected = pieces[counter++] == "YES";

            string direction = pieces[counter++].ToLower();
            double value = 0;

            // TODO: Only reads UDL load properly
            switch (identifier)
            {
                case "LOAD_BEAM_POINT.2":
                    Status.AddError("Beam point load not supported.");
                    counter++; // Position
                    counter++; // Value
                    value = 0;
                    break;
                case "LOAD_BEAM_UDL.2":
                    value = Convert.ToDouble(pieces[counter++]);
                    break;
                case "LOAD_BEAM_LINE.2":
                    Status.AddError("Beam line load not supported. Average will be taken.");
                    value = Convert.ToDouble(pieces[counter++]);
                    value += Convert.ToDouble(pieces[counter++]);
                    value /= 2;
                    break;
                case "LOAD_BEAM_PATCH.2":
                    Status.AddError("Beam patch load not supported. Average of values will be taken.");
                    counter++; // Position
                    value = Convert.ToDouble(pieces[counter++]);
                    counter++; // Position
                    value += Convert.ToDouble(pieces[counter++]);
                    value /= 2;
                    break;
                case "LOAD_BEAM_TRILIN.2":
                    Status.AddError("Beam trilinier load not supported. Average of values will be taken.");
                    counter++; // Position
                    value = Convert.ToDouble(pieces[counter++]);
                    counter++; // Position
                    value += Convert.ToDouble(pieces[counter++]);
                    value /= 2;
                    break;
                default:
                    Status.AddError("Unable to parse beam load " + identifier);
                    value = 0;
                    break;
            }

            switch (direction.ToUpper())
            {
                case "X":
                    Loading.X = value;
                    break;
                case "Y":
                    Loading.Y = value;
                    break;
                case "Z":
                    Loading.Z = value;
                    break;
                case "XX":
                    Loading.XX = value;
                    break;
                case "YY":
                    Loading.YY = value;
                    break;
                case "ZZ":
                    Loading.ZZ = value;
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

            for (int i = 0; i < 6; i++)
            {
                List<string> subLs = new List<string>();

                if (values[i] == 0) continue;

                subLs.Add("SET");
                subLs.Add("LOAD_BEAM_UDL"); // TODO: Only writes to UDL load
                subLs.Add(Name == "" ? " " : Name);

                // TODO: This is a hack.
                List<string> target = new List<string>();
                if (GSA.TargetAnalysisLayer)
                    target.AddRange(Elements.Select(x => x.ToString()));
                else
                    target.AddRange(Elements.Select(x => "G" + x.ToString()));
                subLs.Add(string.Join(" ", target));

                subLs.Add(LoadCase.ToString());
                subLs.Add("GLOBAL"); // Axis
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
