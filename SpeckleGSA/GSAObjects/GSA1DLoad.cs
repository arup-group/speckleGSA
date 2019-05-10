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
    [GSAObject("LOAD_BEAM", new string[] { "EL.3", "MEMB.7" }, "loads", true, true, new Type[] { typeof(GSA1DElement), typeof(GSA1DMember) }, new Type[] { typeof(GSA1DElement), typeof(GSA1DMember), typeof(GSA1DElementPolyline) })]
    public class GSA1DLoad : Structural1DLoad, IGSAObject
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

            List<GSA1DLoad> loads = new List<GSA1DLoad>();
            List<GSA1DElement> elements = GSA.TargetAnalysisLayer ? dict[typeof(GSA1DElement)].Cast<GSA1DElement>().ToList() : new List<GSA1DElement>();
            List<GSA1DMember> members = GSA.TargetDesignLayer ? dict[typeof(GSA1DMember)].Cast<GSA1DMember>().ToList() : new List<GSA1DMember>();

            string keyword = MethodBase.GetCurrentMethod().DeclaringType.GetGSAKeyword();
            string[] subKeywords = MethodBase.GetCurrentMethod().DeclaringType.GetSubGSAKeyword();

            string[] lines = GSA.GetGWARecords("GET_ALL," + keyword);
            List<string> deletedLines = GSA.GetDeletedGWARecords("GET_ALL," + keyword).ToList();
            foreach (string k in subKeywords)
                deletedLines.AddRange(GSA.GetDeletedGWARecords("GET_ALL," + k));

            // Remove deleted lines
            dict[typeof(GSA1DLoad)].RemoveAll(l => deletedLines.Contains(l.GWACommand));
            foreach (KeyValuePair<Type, List<IGSAObject>> kvp in dict)
                kvp.Value.RemoveAll(l => l.SubGWACommand.Any(x => deletedLines.Contains(x)));

            // Filter only new lines
            string[] prevLines = dict[typeof(GSA1DLoad)].Select(l => l.GWACommand).ToArray();
            string[] newLines = lines.Where(l => !prevLines.Contains(l)).ToArray();

            foreach (string p in newLines)
            {
                List<GSA1DLoad> loadSubList = new List<GSA1DLoad>();

                // Placeholder load object to get list of elements and load values
                // Need to transform to axis so one load definition may be transformed to many
                GSA1DLoad initLoad = ParseGWACommand(p, elements, members);

                if (GSA.TargetAnalysisLayer)
                {
                    // Create load for each element applied
                    foreach (string nRef in initLoad.ElementRefs)
                    {
                        GSA1DLoad load = new GSA1DLoad();
                        load.GWACommand = initLoad.GWACommand;
                        load.SubGWACommand = new List<string>(initLoad.SubGWACommand);
                        load.Name = initLoad.Name;
                        load.LoadCaseRef = initLoad.LoadCaseRef;

                        // Transform load to defined axis
                        GSA1DElement elem = elements.Where(e => e.StructuralId == nRef).First();
                        StructuralAxis loadAxis = load.Axis == 0 ? new StructuralAxis(
                            new StructuralVectorThree(new double[] { 1, 0, 0 }),
                            new StructuralVectorThree(new double[] { 0, 1, 0 }),
                            new StructuralVectorThree(new double[] { 0, 0, 1 })) :
                            HelperFunctions.LocalAxisEntity1D(elem.Value.ToArray(), elem.ZAxis); // Assumes if not global, local
                        load.Loading = initLoad.Loading;
                        load.Loading.TransformOntoAxis(loadAxis);

                        // Perform projection
                        if (load.Projected)
                        {
                            Vector3D loadDirection = new Vector3D(
                                load.Loading.Value[0],
                                load.Loading.Value[1],
                                load.Loading.Value[2]);

                            if (loadDirection.Length > 0)
                            {
                                Vector3D axisX = new Vector3D(elem.Value[5] - elem.Value[0], elem.Value[4] - elem.Value[1], elem.Value[3] - elem.Value[2]);
                                double angle = Vector3D.AngleBetween(loadDirection, axisX);
                                double factor = Math.Sin(angle);
                                load.Loading.Value[0] *= factor;
                                load.Loading.Value[1] *= factor;
                                load.Loading.Value[2] *= factor;
                            }
                        }

                        // If the loading already exists, add element ref to list
                        GSA1DLoad match = loadSubList.Count() > 0 ? loadSubList.Where(l => l.Loading.Equals(load.Loading)).First() : null;
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
                        GSA1DLoad load = new GSA1DLoad();
                        load.GWACommand = initLoad.GWACommand;
                        load.SubGWACommand = new List<string>(initLoad.SubGWACommand);
                        load.Name = initLoad.Name;
                        load.LoadCaseRef = initLoad.LoadCaseRef;

                        // Transform load to defined axis
                        GSA1DMember memb = members.Where(e => e.StructuralId == nRef).First();
                        StructuralAxis loadAxis = load.Axis == 0 ? new StructuralAxis(
                            new StructuralVectorThree(new double[] { 1, 0, 0 }),
                            new StructuralVectorThree(new double[] { 0, 1, 0 }),
                            new StructuralVectorThree(new double[] { 0, 0, 1 })) :
                            HelperFunctions.LocalAxisEntity1D(memb.Value.ToArray(), memb.ZAxis); // Assumes if not global, local
                        load.Loading = initLoad.Loading;
                        load.Loading.TransformOntoAxis(loadAxis);

                        // Perform projection
                        if (load.Projected)
                        {
                            Vector3D loadDirection = new Vector3D(
                                load.Loading.Value[0],
                                load.Loading.Value[1],
                                load.Loading.Value[2]);

                            if (loadDirection.Length > 0)
                            {
                                Vector3D axisX = new Vector3D(memb.Value[5] - memb.Value[0], memb.Value[4] - memb.Value[1], memb.Value[3] - memb.Value[2]);
                                double angle = Vector3D.AngleBetween(loadDirection, axisX);
                                double factor = Math.Sin(angle);
                                load.Loading.Value[0] *= factor;
                                load.Loading.Value[1] *= factor;
                                load.Loading.Value[2] *= factor;
                            }
                        }

                        // If the loading already exists, add element ref to list
                        GSA1DLoad match = loadSubList.Count() > 0 ? loadSubList.Where(l => l.Loading.Equals(load.Loading)).First() : null;
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

            dict[typeof(GSA1DLoad)].AddRange(loads);

            if (loads.Count() > 0 || deletedLines.Count() > 0) return true;

            return false;
        }

        public static GSA1DLoad ParseGWACommand(string command, List<GSA1DElement> elements, List<GSA1DMember> members)
        {
            GSA1DLoad ret = new GSA1DLoad();

            ret.GWACommand = command;

            string[] pieces = command.ListSplit(",");

            int counter = 0; // Skip identifier
            string identifier = pieces[counter++];

            ret.Name = pieces[counter++].Trim(new char[] { '"' });

            if (GSA.TargetAnalysisLayer)
            {
                int[] targetElements = pieces[counter++].ConvertGSAList(GsaEntity.ELEMENT);

                if (elements != null)
                {
                    List<GSA1DElement> elems = elements.Where(n => targetElements.Contains(Convert.ToInt32(n.StructuralId))).ToList();

                    ret.ElementRefs = elems.Select(n => n.StructuralId).ToList();
                    ret.SubGWACommand.AddRange(elems.Select(n => n.GWACommand));
                }
            }
            else
            {
                int[] targetGroups = pieces[counter++].GetGroupsFromGSAList();

                if (members != null)
                {
                    List<GSA1DMember> membs = members.Where(m => targetGroups.Contains(m.Group)).ToList();

                    ret.ElementRefs = membs.Select(m => m.StructuralId).ToList();
                    ret.SubGWACommand.AddRange(membs.Select(n => n.GWACommand));
                }
            }

            ret.LoadCaseRef = pieces[counter++];

            string axis = pieces[counter++];
            ret.Axis = axis == "GLOBAL" ? 0 : -1;// Convert.ToInt32(axis); // TODO: Assume local if not global

            ret.Projected = pieces[counter++] == "YES";

            ret.Loading = new StructuralVectorSix(new double[6]);
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
                    ret.Loading.Value[0] = value;
                    break;
                case "Y":
                    ret.Loading.Value[1] = value;
                    break;
                case "Z":
                    ret.Loading.Value[2] = value;
                    break;
                case "XX":
                    ret.Loading.Value[3] = value;
                    break;
                case "YY":
                    ret.Loading.Value[4] = value;
                    break;
                case "ZZ":
                    ret.Loading.Value[5] = value;
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
            if (!dict.ContainsKey(typeof(Structural1DLoad))) return;

            foreach (IStructural obj in dict[typeof(Structural1DLoad)])
            {
                Set(obj as Structural1DLoad);
            }
        }

        public static void Set(Structural1DLoad load)
        {
            if (load == null)
                return;

            if (load.Loading == null)
                return;

            string keyword = MethodBase.GetCurrentMethod().DeclaringType.GetGSAKeyword();

            List<int> elementRefs;
            List<int> groupRefs;
            if (GSA.TargetAnalysisLayer)
            { 
                elementRefs = Indexer.LookupIndices(typeof(GSA1DElement), load.ElementRefs).Where(x => x.HasValue).Select(x => x.Value).ToList();
                groupRefs = Indexer.LookupIndices(typeof(GSA1DElementPolyline), load.ElementRefs).Where(x => x.HasValue).Select(x => x.Value).ToList();
            }
            else
            {
                elementRefs = new List<int>();
                groupRefs = Indexer.LookupIndices(typeof(GSA1DMember), load.ElementRefs).Where(x => x.HasValue).Select(x => x.Value).ToList();
                groupRefs.AddRange(Indexer.LookupIndices(typeof(GSA1DElementPolyline), load.ElementRefs).Where(x => x.HasValue).Select(x => x.Value).ToList());
            }
            int loadCaseRef = 0;
            try
            {
                loadCaseRef = Indexer.LookupIndex(typeof(GSALoadCase), load.LoadCaseRef).Value;
            }
            catch { loadCaseRef = Indexer.ResolveIndex(typeof(GSALoadCase), load.LoadCaseRef); }

            string[] direction = new string[6] { "X", "Y", "Z", "X", "Y", "Z" };

            for (int i = 0; i < load.Loading.Value.Count(); i++)
            {
                List<string> ls = new List<string>();

                if (load.Loading.Value[i] == 0) continue;

                int index = Indexer.ResolveIndex(MethodBase.GetCurrentMethod().DeclaringType);

                ls.Add("SET_AT");
                ls.Add(index.ToString());
                ls.Add("LOAD_BEAM_UDL"); // TODO: Only writes to UDL load
                ls.Add(load.Name == null || load.Name == "" ? " " : load.Name);
                // TODO: This is a hack.
                ls.Add(string.Join(
                    " ",
                    elementRefs.Select(x => x.ToString())
                        .Concat(groupRefs.Select(x => "G" + x.ToString()))
                ));
                ls.Add(loadCaseRef.ToString());
                ls.Add("GLOBAL"); // Axis
                ls.Add("NO"); // Projected
                ls.Add(direction[i]);
                ls.Add(load.Loading.Value[i].ToString());

                GSA.RunGWACommand(string.Join("\t", ls));
            }
        }
        #endregion
    }
}
