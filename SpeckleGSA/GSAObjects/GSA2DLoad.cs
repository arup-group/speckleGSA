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
    [GSAObject("LOAD_2D_FACE.2", "loads", true, true, new Type[] { typeof(GSA2DElement), typeof(GSA2DMember) }, new Type[] { typeof(GSA2DElement), typeof(GSA2DMember) })]
    public class GSA2DLoad : Structural2DLoad, IGSAObject
    {
        public int Axis;
        public bool Projected;

        public string GWACommand { get; set; } = "";
        public List<string> SubGWACommand { get; set; } = new List<string>();

        #region Sending Functions
        public static bool GetObjects(Dictionary<Type, List<IGSAObject>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType))
                dict[MethodBase.GetCurrentMethod().DeclaringType] = new List<IGSAObject>();

            List<GSA2DLoad> loads = new List<GSA2DLoad>();
            List<GSA2DElement> elements = GSA.TargetAnalysisLayer ? dict[typeof(GSA2DElement)].Cast<GSA2DElement>().ToList() : new List<GSA2DElement>();
            List<GSA2DMember> members = GSA.TargetDesignLayer ? dict[typeof(GSA2DMember)].Cast<GSA2DMember>().ToList() : new List<GSA2DMember>();

            string keyword = MethodBase.GetCurrentMethod().DeclaringType.GetGSAKeyword();

            string[] lines = GSA.GetGWAGetCommands("GET_ALL," + keyword);
            string[] deletedLines = GSA.GetDeletedGWAGetCommands("GET_ALL," + keyword);

            // Remove deleted lines
            dict[typeof(GSA2DLoad)].RemoveAll(l => deletedLines.Contains(l.GWACommand));
            foreach (KeyValuePair<Type, List<IGSAObject>> kvp in dict)
                kvp.Value.RemoveAll(l => l.SubGWACommand.Any(x => deletedLines.Contains(x)));

            // Filter only new lines
            string[] prevLines = dict[typeof(GSA2DLoad)].Select(l => l.GWACommand).ToArray();
            string[] newLines = lines.Where(l => !prevLines.Contains(l)).ToArray();

            foreach (string p in newLines)
            {
                List<GSA2DLoad> loadSubList = new List<GSA2DLoad>();

                // Placeholder load object to get list of elements and load values
                // Need to transform to axis so one load definition may be transformed to many
                GSA2DLoad initLoad = ParseGWACommand(p, elements, members);

                if (GSA.TargetAnalysisLayer)
                {
                    // Create load for each element applied
                    foreach (string nRef in initLoad.ElementRefs)
                    {
                        GSA2DLoad load = new GSA2DLoad();
                        load.GWACommand = initLoad.GWACommand;
                        load.SubGWACommand = new List<string>(initLoad.SubGWACommand);
                        load.Name = initLoad.Name;
                        load.LoadCaseRef = initLoad.LoadCaseRef;

                        // Transform load to defined axis
                        GSA2DElement elem = elements.Where(e => e.StructuralId == nRef).First();
                        StructuralAxis loadAxis = HelperFunctions.Parse2DAxis(elem.Vertices.ToArray(), 0, load.Axis != 0); // Assumes if not global, local
                        load.Loading = initLoad.Loading;

                        // Perform projection
                        if (load.Projected)
                        {
                            load.Loading.Value[0] = 0;
                            load.Loading.Value[1] = 0;
                        }
                        load.Loading.TransformOntoAxis(loadAxis);

                        // If the loading already exists, add element ref to list
                        GSA2DLoad match = loadSubList.Count() > 0 ? loadSubList.Where(l => l.Loading.Equals(load.Loading)).First() : null;
                        if (match != null)
                            match.ElementRefs.Add(nRef);
                        else
                        {
                            load.ElementRefs = new List<string>() { nRef };
                            loadSubList.Add(load);
                        }
                    }
                }
                else if (GSA.TargetDesignLayer)
                {
                    // Create load for each element applied
                    foreach (string nRef in initLoad.ElementRefs)
                    {
                        GSA2DLoad load = new GSA2DLoad();
                        load.GWACommand = initLoad.GWACommand;
                        load.SubGWACommand = new List<string>(initLoad.SubGWACommand);
                        load.Name = initLoad.Name;
                        load.LoadCaseRef = initLoad.LoadCaseRef;

                        // Transform load to defined axis
                        GSA2DMember memb = members.Where(e => e.StructuralId == nRef).First();
                        StructuralAxis loadAxis = HelperFunctions.Parse2DAxis(memb.Vertices.ToArray(), 0, load.Axis != 0); // Assumes if not global, local
                        load.Loading = initLoad.Loading;
                        load.Loading.TransformOntoAxis(loadAxis);

                        // Perform projection
                        if (load.Projected)
                        {
                            load.Loading.Value[0] = 0;
                            load.Loading.Value[1] = 0;
                        }
                        load.Loading.TransformOntoAxis(loadAxis);

                        // If the loading already exists, add element ref to list
                        GSA2DLoad match = loadSubList.Count() > 0 ? loadSubList.Where(l => l.Loading.Equals(load.Loading)).First() : null;
                        if (match != null)
                            match.ElementRefs.Add(nRef);
                        else
                        {
                            load.ElementRefs = new List<string>() { nRef };
                            loadSubList.Add(load);
                        }
                    }
                }

                loads.AddRange(loadSubList);
            }

            dict[typeof(GSA2DLoad)].AddRange(loads);

            if (loads.Count() > 0 || deletedLines.Length > 0) return true;

            return false;
        }

        public static GSA2DLoad ParseGWACommand(string command, List<GSA2DElement> elements, List<GSA2DMember> members)
        {
            GSA2DLoad ret = new GSA2DLoad();

            ret.GWACommand = command;

            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier

            ret.Name = pieces[counter++].Trim(new char[] { '"' });

            if (GSA.TargetAnalysisLayer)
            {
                int[] targetElements = pieces[counter++].ParseGSAList(GsaEntity.ELEMENT);

                if (elements != null)
                {
                    List<GSA2DElement> elems = elements.Where(n => targetElements.Contains(Convert.ToInt32(n.StructuralId))).ToList();

                    ret.ElementRefs = elems.Select(n => n.StructuralId).ToList();
                    ret.SubGWACommand.AddRange(elems.Select(n => n.GWACommand));
                }
            }
            else
            {
                int[] targetGroups = pieces[counter++].GetGroupsFromGSAList();

                if (members != null)
                {
                    List<GSA2DMember> membs = members.Where(m => targetGroups.Contains(m.Group)).ToList();

                    ret.ElementRefs = membs.Select(m => m.StructuralId).ToList();
                    ret.SubGWACommand.AddRange(membs.Select(n => n.GWACommand));
                }
            }

            ret.LoadCaseRef = pieces[counter++];

            string axis = pieces[counter++];
            ret.Axis = axis == "GLOBAL" ? 0 : Convert.ToInt32(axis);

            counter++; // Type. TODO: Skipping since we're taking the average

            ret.Projected = pieces[counter++] == "YES";

            ret.Loading = new StructuralVectorThree(new double[3]);
            string direction = pieces[counter++].ToLower();

            double[] values = pieces.Skip(counter).Select(p => Convert.ToDouble(p)).ToArray();

            switch (direction.ToUpper())
            {
                case "X":
                    ret.Loading.Value[0] = values.Average();
                    break;
                case "Y":
                    ret.Loading.Value[1] = values.Average();
                    break;
                case "Z":
                    ret.Loading.Value[2] = values.Average();
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
            if (!dict.ContainsKey(typeof(Structural2DLoad))) return;

            foreach (IStructural obj in dict[typeof(Structural2DLoad)])
            {
                Set(obj as Structural2DLoad);
            }
        }

        public static void Set(Structural2DLoad load)
        {
            if (load == null)
                return;

            if (load.Loading == null)
                return;

            string keyword = MethodBase.GetCurrentMethod().DeclaringType.GetGSAKeyword();

            int index = Indexer.ResolveIndex(keyword, load);
            List<int> elementRefs;
            List<int> groupRefs;
            if (GSA.TargetAnalysisLayer)
            { 
                elementRefs = Indexer.LookupIndices(typeof(GSA2DElement), load.ElementRefs).Where(x => x.HasValue).Select(x => x.Value).ToList();
                groupRefs = Indexer.LookupIndices(typeof(GSA2DElementMesh), load.ElementRefs).Where(x => x.HasValue).Select(x => x.Value).ToList();
            }
            else
            {
                elementRefs = new List<int>();
                groupRefs = Indexer.LookupIndices(typeof(GSA2DMember), load.ElementRefs).Where(x => x.HasValue).Select(x => x.Value).ToList();
            }
            int loadCaseRef = 0;
            try
            {
                loadCaseRef = Indexer.LookupIndex(typeof(GSALoadCase), load.LoadCaseRef).Value;
            }
            catch { }

            string[] direction = new string[3] { "X", "Y", "Z" };

            for (int i = 0; i < load.Loading.Value.Count(); i++)
            {
                List<string> ls = new List<string>();

                if (load.Loading.Value[i] == 0) continue;

                ls.Add("SET_AT");
                ls.Add(index.ToString());
                ls.Add(keyword);
                ls.Add(load.Name == null || load.Name == "" ? " " : load.Name);
                // TODO: This is a hack.
                ls.Add(string.Join(
                    " ",
                    elementRefs.Select(x => x.ToString())
                        .Concat(groupRefs.Select(x => "G" + x.ToString()))
                ));
                ls.Add(loadCaseRef.ToString());
                ls.Add("GLOBAL"); // Axis
                ls.Add("CONS"); // Type
                ls.Add("NO"); // Projected
                ls.Add(direction[i]);
                ls.Add(load.Loading.Value[i].ToString());

                GSA.RunGWACommand(string.Join(",", ls));
            }
        }
        #endregion
    }
}
