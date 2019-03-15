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
    [GSAObject("LOAD_BEAM", "loads", true, true, new Type[] { typeof(GSA1DElement), typeof(GSA1DMember) }, new Type[] { typeof(GSA1DElement), typeof(GSA1DMember) })]
    public class GSA1DLoad : Structural1DLoad, IGSAObject
    {
        public int Axis;
        public bool Projected;

        public string GWACommand { get; set; }
        public List<string> SubGWACommand { get; set; }

        #region Contructors and Converters
        public GSA1DLoad()
        {
            GWACommand = "";
            SubGWACommand = new List<string>();
            Axis = 0;
            Projected = false;
        }

        public GSA1DLoad(Structural1DLoad baseClass)
        {
            GWACommand = "";
            SubGWACommand = new List<string>();
            Axis = 0;
            Projected = false;

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

            string[] lines = GSA.GetGWAGetCommands("GET_ALL,LOAD_BEAM");
            string[] deletedLines = GSA.GetDeletedGWAGetCommands("GET_ALL,LOAD_BEAM");

            // Remove deleted lines
            dict[typeof(GSA1DLoad)].RemoveAll(l => deletedLines.Contains(((IGSAObject)l).GWACommand));
            foreach (KeyValuePair<Type, List<object>> kvp in dict)
                kvp.Value.RemoveAll(l => ((IGSAObject)l).SubGWACommand.Any(x => deletedLines.Contains(x)));

            // Filter only new lines
            string[] prevLines = dict[typeof(GSA1DLoad)].Select(l => ((GSA1DLoad)l).GWACommand).ToArray();
            string[] newLines = lines.Where(l => !prevLines.Contains(l)).ToArray();

            List<object> elements = GSA.TargetAnalysisLayer ? dict[typeof(GSA1DElement)] : new List<object>();
            List<object> members = GSA.TargetDesignLayer ? dict[typeof(GSA1DMember)] : new List<object>();
            
            foreach (string p in newLines)
            {
                List<GSA1DLoad> loadSubList = new List<GSA1DLoad>();

                // Placeholder load object to get list of elements and load values
                // Need to transform to axis so one load definition may be transformed to many
                GSA1DLoad initLoad = new GSA1DLoad();
                initLoad.ParseGWACommand(p,dict);

                if (GSA.TargetAnalysisLayer)
                { 
                    // Create load for each element applied
                    foreach (int nRef in initLoad.Elements)
                    {
                        GSA1DLoad load = new GSA1DLoad();
                        load.GWACommand = initLoad.GWACommand;
                        load.SubGWACommand = new List<string>(initLoad.SubGWACommand);
                        load.Name = initLoad.Name;
                        load.LoadCase = initLoad.LoadCase;

                        // Transform load to defined axis
                        GSA1DElement elem = elements.Where(e => ((GSA1DElement)e).Reference == nRef).First() as GSA1DElement;
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
                        load.GWACommand = initLoad.GWACommand;
                        load.SubGWACommand = new List<string>(initLoad.SubGWACommand);
                        load.Name = initLoad.Name;
                        load.LoadCase = initLoad.LoadCase;

                        // Transform load to defined axis
                        GSA1DMember memb = members.Where(e => ((GSA1DMember)e).Reference == mRef).First() as GSA1DMember;
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
            }

            dict[typeof(GSA1DLoad)].AddRange(loads);

            if (loads.Count() > 0 || deletedLines.Length > 0) return true;

            return false;
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

        public void ParseGWACommand(string command, Dictionary<Type, List<object>> dict = null)
        {
            GWACommand = command;

            string[] pieces = command.ListSplit(",");

            int counter = 0;
            string identifier = pieces[counter++];

            Name = pieces[counter++].Trim(new char[] { '"' });
            
            if (GSA.TargetAnalysisLayer)
            {
                int[] targetElements = pieces[counter++].ParseGSAList(GsaEntity.ELEMENT);

                if (dict.ContainsKey(typeof(GSA1DElement)))
                {
                    List<GSA1DElement> elems = dict[typeof(GSA1DElement)].Cast<GSA1DElement>()
                        .Where(n => targetElements.Contains(n.Reference)).ToList();

                    Elements = elems.Select(n => n.Reference).ToList();
                    SubGWACommand.AddRange(elems.Select(n => n.GWACommand));
                }
            }
            else
            {
                int[] targetGroups = pieces[counter++].GetGroupsFromGSAList();

                if (dict.ContainsKey(typeof(GSA1DMember)))
                {
                    List<GSA1DMember> membs = dict[typeof(GSA1DMember)].Cast<GSA1DMember>()
                        .Where(m => targetGroups.Contains(m.Group)).ToList();

                    Elements = membs.Select(m => m.Reference).ToList();
                    SubGWACommand.AddRange(membs.Select(n => n.GWACommand));
                }
            }

            LoadCase = Convert.ToInt32(pieces[counter++]);

            string axis = pieces[counter++];
            Axis = axis == "GLOBAL" ? 0 : Convert.ToInt32(axis);

            Projected = pieces[counter++] == "YES";

            string direction = pieces[counter++].ToLower();
            double value = 0;

            // TODO: Only reads UDL load properly
            if (identifier.Contains("LOAD_BEAM_POINT.2"))
            {
                Status.AddError("Beam point load not supported.");
                counter++; // Position
                counter++; // Value
                value = 0;
            }
            else if (identifier.Contains("LOAD_BEAM_UDL.2"))
                value = Convert.ToDouble(pieces[counter++]);
            else if (identifier.Contains("LOAD_BEAM_LINE.2"))
            {
                Status.AddError("Beam line load not supported. Average will be taken.");
                value = Convert.ToDouble(pieces[counter++]);
                value += Convert.ToDouble(pieces[counter++]);
                value /= 2;
            }
            else if (identifier.Contains("LOAD_BEAM_PATCH.2"))
            {
                Status.AddError("Beam patch load not supported. Average of values will be taken.");
                counter++; // Position
                value = Convert.ToDouble(pieces[counter++]);
                counter++; // Position
                value += Convert.ToDouble(pieces[counter++]);
                value /= 2;
            }
            else if (identifier.Contains("LOAD_BEAM_TRILIN.2"))
            {
                Status.AddError("Beam trilinier load not supported. Average of values will be taken.");
                counter++; // Position
                value = Convert.ToDouble(pieces[counter++]);
                counter++; // Position
                value += Convert.ToDouble(pieces[counter++]);
                value /= 2;
            }
            else
            {
                Status.AddError("Unable to parse beam load " + identifier);
                value = 0;
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

                subLs.Add("SET_AT");
                subLs.Add(Reference.ToString());
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
