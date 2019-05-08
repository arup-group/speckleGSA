using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SpeckleStructuresClasses;

namespace SpeckleGSA
{
    [GSAObject("ANAL.1", new string[] { }, "loads", true, true, new Type[] { typeof(GSALoadCase) }, new Type[] { typeof(GSALoadCase) })]
    public class GSALoadTask : StructuralLoadTask, IGSAObject
    {
        public string GWACommand { get; set; } = "";
        public List<string> SubGWACommand { get; set; } = new List<string>();

        #region Sending Functions
        public static bool GetObjects(Dictionary<Type, List<IGSAObject>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType))
                dict[MethodBase.GetCurrentMethod().DeclaringType] = new List<IGSAObject>();

            List<GSALoadTask> loadTasks = new List<GSALoadTask>();

            string keyword = MethodBase.GetCurrentMethod().DeclaringType.GetGSAKeyword();
            string[] subKeywords = MethodBase.GetCurrentMethod().DeclaringType.GetSubGSAKeyword();

            string[] lines = GSA.GetGWARecords("GET_ALL," + keyword);
            List<string> deletedLines = GSA.GetDeletedGWARecords("GET_ALL," + keyword).ToList();
            foreach (string k in subKeywords)
                deletedLines.AddRange(GSA.GetDeletedGWARecords("GET_ALL," + k));

            // Remove deleted lines
            dict[typeof(GSALoadTask)].RemoveAll(l => deletedLines.Contains(l.GWACommand));
            foreach (KeyValuePair<Type, List<IGSAObject>> kvp in dict)
                kvp.Value.RemoveAll(l => l.SubGWACommand.Any(x => deletedLines.Contains(x)));

            // Filter only new lines
            string[] prevLines = dict[typeof(GSALoadTask)].Select(l => l.GWACommand).ToArray();
            string[] newLines = lines.Where(l => !prevLines.Contains(l)).ToArray();

            foreach (string p in newLines)
            {
                GSALoadTask task = ParseGWACommand(p);
                loadTasks.Add(task);
            }

            dict[typeof(GSALoadTask)].AddRange(loadTasks);

            if (loadTasks.Count() > 0 || deletedLines.Count() > 0) return true;

            return false;
        }

        public static GSALoadTask ParseGWACommand(string command)
        {
            GSALoadTask ret = new GSALoadTask();

            ret.GWACommand = command;

            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier

            ret.StructuralId = pieces[counter++];
            ret.Name = pieces[counter++];

            //Find task type
            string taskRef = pieces[counter++];
            ret.TaskType = GetLoadTaskType(taskRef);
           
            // Parse description
            string description = pieces[counter++];
            ret.LoadCaseRefs = new List<string>();
            ret.LoadFactors = new List<double>();

            // TODO: this only parses the super simple linear add descriptions
            try
            {
                List<Tuple<string, double>> desc = HelperFunctions.ParseLoadDescription(description);

                foreach(Tuple<string, double> t in desc)
                {
                    switch(t.Item1[0])
                    {
                        case 'L':
                            ret.LoadCaseRefs.Add(t.Item1.Substring(1));
                            ret.LoadFactors.Add(t.Item2);
                            break;
                    }
                }
            }
            catch
            {
                Status.AddError("Unable to parse description: " + description);
            }

            return ret;
        }
        #endregion

        #region Receiving Functions
        public static void SetObjects(Dictionary<Type, List<IStructural>> dict)
        {
            if (!dict.ContainsKey(typeof(StructuralLoadTask))) return;

            foreach (IStructural obj in dict[typeof(StructuralLoadTask)])
            {
                Set(obj as StructuralLoadTask);
            }
        }

        public static void Set(StructuralLoadTask loadTask)
        {
            if (loadTask == null)
                return;

            string keyword = MethodBase.GetCurrentMethod().DeclaringType.GetGSAKeyword();

            int taskIndex = (int)GSA.RunGWACommand("HIGHEST,TASK.1") + 1;
            int index = Indexer.ResolveIndex(MethodBase.GetCurrentMethod().DeclaringType, loadTask);

            List<string> ls = new List<string>();

            // Set TASK
            ls.Add("SET");
            ls.Add("TASK.1");
            ls.Add(taskIndex.ToString());
            ls.Add(""); // Name
            ls.Add("0"); // Stage
            switch(loadTask.TaskType)
            {
                case StructuralLoadTaskType.LinearStatic:
                    ls.Add("GSS");
                    ls.Add("STATIC");
                    // Defaults:
                    ls.Add("1");
                    ls.Add("0");
                    ls.Add("128");
                    ls.Add("SELF");
                    ls.Add("none");
                    ls.Add("none");
                    ls.Add("DRCMEFNSQBHU*");
                    ls.Add("MIN");
                    ls.Add("AUTO");
                    ls.Add("0");
                    ls.Add("0");
                    ls.Add("0");
                    ls.Add("NONE");
                    ls.Add("FATAL");
                    ls.Add("NONE");
                    ls.Add("NONE");
                    ls.Add("RAFT_LO");
                    ls.Add("RESID_NO");
                    ls.Add("0");
                    ls.Add("1");
                    break;
                case StructuralLoadTaskType.NonlinearStatic:
                    ls.Add("GSRELAX");
                    ls.Add("BUCKLING_NL");
                    // Defaults:
                    ls.Add("SINGLE");
                    ls.Add("0");
                    ls.Add("BEAM_GEO_YES");
                    ls.Add("SHELL_GEO_NO");
                    ls.Add("0.1");
                    ls.Add("0.0001");
                    ls.Add("0.1");
                    ls.Add("CYCLE");
                    ls.Add("100000");
                    ls.Add("REL");
                    ls.Add("0.0010000000475");
                    ls.Add("0.0010000000475");
                    ls.Add("DISP_CTRL_YES");
                    ls.Add("0");
                    ls.Add("1");
                    ls.Add("0.01");
                    ls.Add("LOAD_CTRL_NO");
                    ls.Add("1");
                    ls.Add("");
                    ls.Add("10");
                    ls.Add("100");
                    ls.Add("RESID_NOCONV");
                    ls.Add("DAMP_VISCOUS");
                    ls.Add("0");
                    ls.Add("0");
                    ls.Add("1");
                    ls.Add("1");
                    ls.Add("1");
                    ls.Add("1");
                    ls.Add("AUTO_MASS_YES");
                    ls.Add("AUTO_DAMP_YES");
                    ls.Add("FF_SAVE_ELEM_FORCE_YES");
                    ls.Add("FF_SAVE_SPACER_FORCE_TO_ELEM");
                    ls.Add("DRCEFNSQBHU*");
                    break;
                case StructuralLoadTaskType.Modal:
                    ls.Add("GSS");
                    ls.Add("MODAL");
                    // Defaults:
                    ls.Add("1");
                    ls.Add("1");
                    ls.Add("128");
                    ls.Add("SELF");
                    ls.Add("none");
                    ls.Add("none");
                    ls.Add("DRCMEFNSQBHU*");
                    ls.Add("MIN");
                    ls.Add("AUTO");
                    ls.Add("0");
                    ls.Add("0");
                    ls.Add("0");
                    ls.Add("NONE");
                    ls.Add("FATAL");
                    ls.Add("NONE");
                    ls.Add("NONE");
                    ls.Add("RAFT_LO");
                    ls.Add("RESID_NO");
                    ls.Add("0");
                    ls.Add("1");
                    break;
                default:
                    ls.Add("GSS");
                    ls.Add("STATIC");
                    // Defaults:
                    ls.Add("1");
                    ls.Add("0");
                    ls.Add("128");
                    ls.Add("SELF");
                    ls.Add("none");
                    ls.Add("none");
                    ls.Add("DRCMEFNSQBHU*");
                    ls.Add("MIN");
                    ls.Add("AUTO");
                    ls.Add("0");
                    ls.Add("0");
                    ls.Add("0");
                    ls.Add("NONE");
                    ls.Add("FATAL");
                    ls.Add("NONE");
                    ls.Add("NONE");
                    ls.Add("RAFT_LO");
                    ls.Add("RESID_NO");
                    ls.Add("0");
                    ls.Add("1");
                    break;
            }
            GSA.RunGWACommand(string.Join("\t", ls));

            // Set ANAL
            ls.Clear();
            ls.Add("SET");
            ls.Add(keyword);
            ls.Add(index.ToString());
            ls.Add(loadTask.Name == null || loadTask.Name == "" ? " " : loadTask.Name);
            ls.Add(taskIndex.ToString());
            if (loadTask.TaskType == StructuralLoadTaskType.Modal)
                ls.Add("M1");
            else
            {
                List<string> subLs = new List<string>();
                for (int i = 0; i < loadTask.LoadCaseRefs.Count(); i++)
                {
                    int? loadCaseRef = Indexer.LookupIndex(typeof(GSALoadCase), loadTask.LoadCaseRefs[i]);

                    if (loadCaseRef.HasValue)
                    {
                        if (loadTask.LoadFactors.Count() > i)
                            subLs.Add(loadTask.LoadFactors[i].ToString() + "L" + loadCaseRef.Value.ToString());
                        else
                            subLs.Add("L" + loadCaseRef.Value.ToString());
                    }
                }
                ls.Add(string.Join(" + ", subLs));
            }
            GSA.RunGWACommand(string.Join("\t", ls));
        }
        #endregion

        #region Helper Functions
        public static StructuralLoadTaskType GetLoadTaskType(string taskRef)
        {
            string[] commands = GSA.GetGWARecords("GET,TASK.1," + taskRef);

            string[] taskPieces = commands[0].ListSplit(",");
            StructuralLoadTaskType taskType = StructuralLoadTaskType.LinearStatic;

            if (taskPieces[4] == "GSS")
            {
                if (taskPieces[5] == "STATIC")
                    taskType = StructuralLoadTaskType.LinearStatic;
                else if (taskPieces[5] == "MODAL")
                    taskType = StructuralLoadTaskType.Modal;
            }
            else if (taskPieces[4] == "GSRELAX")
            {
                if (taskPieces[5] == "BUCKLING_NL")
                    taskType = StructuralLoadTaskType.NonlinearStatic;
            }

            return taskType;
        }
        #endregion
    }
}
