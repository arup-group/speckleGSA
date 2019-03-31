using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpeckleStructuresClasses;
using SpeckleCore;

namespace SpeckleGSA
{
    public class Receiver
    {
        public Dictionary<string, SpeckleGSAReceiver> Receivers;
        public Dictionary<Type, List<object>> ReceiverObjectCache;
        public List<string> ReceiverIDCache;

        public Dictionary<Type, List<Type>> TypePrerequisites;
        public List<KeyValuePair<Type, List<Type>>> TypeCastPriority;

        public bool IsInit;
        public bool IsBusy;

        public Receiver()
        {
            Receivers = new Dictionary<string, SpeckleGSAReceiver>();
            ReceiverObjectCache = new Dictionary<Type, List<object>>();
            ReceiverIDCache = new List<string>();
            TypePrerequisites = new Dictionary<Type, List<Type>>();
            TypeCastPriority = new List<KeyValuePair<Type, List<Type>>>();
            IsInit = false;
        }

        public async Task Initialize(string restApi, string apiToken)
        {
            if (IsInit) return;

            if (!GSA.IsInit)
            {
                Status.AddError("GSA link not found.");
                return;
            }

            GSA.FullClearCache();

            // Initialize object write priority list
            IEnumerable<Type> objTypes = typeof(GSA)
                .Assembly.GetTypes()
                .Where(t => t.IsSubclassOf(typeof(SpeckleObject)) && !t.IsAbstract);

            foreach (Type t in objTypes)
            {
                if (t.GetMethod("SetObjects",
                    new Type[] { typeof(Dictionary<Type, List<IStructural>>) }) == null)
                    continue;

                if (t.GetAttribute("AnalysisLayer") != null)
                    if (GSA.TargetAnalysisLayer && !(bool)t.GetAttribute("AnalysisLayer")) continue;

                if (t.GetAttribute("DesignLayer") != null)
                    if (GSA.TargetDesignLayer && !(bool)t.GetAttribute("DesignLayer")) continue;

                List<Type> prereq = new List<Type>();
                if (t.GetAttribute("WritePrerequisite") != null)
                    prereq = ((Type[])t.GetAttribute("WritePrerequisite")).ToList();

                TypePrerequisites[t] = prereq;
            }

            // Remove wrong layer objects from prerequisites
            foreach (Type t in objTypes)
            {
                if (t.GetAttribute("AnalysisLayer") != null)
                    if (GSA.TargetAnalysisLayer && !(bool)t.GetAttribute("AnalysisLayer"))
                        foreach (KeyValuePair<Type, List<Type>> kvp in TypePrerequisites)
                            kvp.Value.Remove(t);

                if (t.GetAttribute("DesignLayer") != null)
                    if (GSA.TargetDesignLayer && !(bool)t.GetAttribute("DesignLayer"))
                        foreach (KeyValuePair<Type, List<Type>> kvp in TypePrerequisites)
                            kvp.Value.Remove(t);
            }

            // Generate which GSA object to cast for each type
            TypeCastPriority = TypePrerequisites.ToList();
            TypeCastPriority.Sort((x, y) => x.Value.Count().CompareTo(y.Value.Count()));

            // Add existing GSA file objects to counters
            Indexer.Clear();
            foreach (KeyValuePair<Type, List<Type>> kvp in TypePrerequisites)
            {
                Status.ChangeStatus("Clearing " + kvp.Key.Name);

                try
                {
                    string keyword = (string)kvp.Key.GetAttribute("GSAKeyword");

                    int highestRecord = (int)GSA.RunGWACommand("HIGHEST," + keyword);

                    if (highestRecord > 0)
                    {
                        Indexer.ReserveIndices(keyword, Enumerable.Range(1, highestRecord).ToList());
                        //GSA.RunGWACommand("DELETE," + kvp.Key.GetAttribute("GSAKeyword") + ",1," + highestRecord.ToString());
                    }
                    else
                    {
                        // TODO: Causes GSA to crash sometimes
                        //GSA.RunGWACommand("DELETE," + kvp.Key.GetAttribute("GSAKeyword"));
                    }
                }
                catch { }
            }
            Indexer.SetBaseline();

            // Create receivers
            Status.ChangeStatus("Accessing stream");

            foreach (string streamID in GSA.Receivers)
            {
                if (streamID == "")
                    Status.AddMessage("No " + streamID + " stream specified.");
                else
                {
                    Status.AddMessage("Creating receiver " + streamID);
                    Receivers[streamID] = new SpeckleGSAReceiver(restApi, apiToken);
                    await Receivers[streamID].InitializeReceiver(streamID);
                    Receivers[streamID].UpdateGlobalTrigger += Trigger;
                }
            }

            Status.ChangeStatus("Ready to receive");
            IsInit = true;
        }

        public void Trigger(object sender, EventArgs e)
        {
            if (IsBusy) return;
            if (!IsInit) return;

            IsBusy = true;
            GSA.ClearCache();
            GSA.UpdateUnits();

            Dictionary<string, List<SpeckleObject>> convertedObjects = new Dictionary<string, List<SpeckleObject>>();

            // Read objects
            Status.ChangeStatus("Receiving stream");
            foreach (KeyValuePair<string, SpeckleGSAReceiver> kvp in Receivers)
                convertedObjects[kvp.Key] = Receivers[kvp.Key].GetStructuralObjects();
            
            // Populate new item dictionary
            Status.ChangeStatus("Bucketing objects");

            Dictionary<Type, List<IStructural>> newObjects = new Dictionary<Type, List<IStructural>>();

            foreach (KeyValuePair<string, List<SpeckleObject>> kvp in convertedObjects)
            {
                double scaleFactor = (1.0).ConvertUnit(Receivers[kvp.Key].Units.ShortUnitName(), GSA.Units);

                foreach (object obj in kvp.Value)
                {
                    if (obj == null) continue;
                    if (obj is SpeckleCore.SpecklePlaceholder) continue;

                    try
                    {
                        if (obj is IList)
                        {
                            foreach (SpeckleObject o in obj as IList)
                            {
                                SpeckleObject copy = o.CreateSpeckleCopy();
                                
                                if (!newObjects.ContainsKey(copy.GetType()))
                                    newObjects[copy.GetType()] = new List<IStructural>() { (IStructural)copy };
                                else
                                    (newObjects[copy.GetType()] as List<IStructural>).Add((IStructural)copy);
                            }
                        }
                        else
                        {
                            SpeckleObject copy = (obj as SpeckleObject).CreateSpeckleCopy();
                            copy.Scale(scaleFactor);

                            if (!newObjects.ContainsKey(copy.GetType()))
                                newObjects[copy.GetType()] = new List<IStructural>() { (IStructural)copy };
                            else
                                (newObjects[copy.GetType()] as List<IStructural>).Add((IStructural)copy);
                        }
                    }
                    catch (Exception ex)
                    {
                        Status.AddError(ex.Message);
                    }
                }
            }

            // Set up counter
            Indexer.ResetToBaseline();

            // Write objects
            Status.ChangeStatus("Writing objects");
            List<Type> currentBatch = new List<Type>();
            List<Type> traversedTypes = new List<Type>();

            do
            {
                currentBatch = TypePrerequisites.Where(i => i.Value.Count(x => !traversedTypes.Contains(x)) == 0).Select(i => i.Key).ToList();
                currentBatch.RemoveAll(i => traversedTypes.Contains(i));

                foreach (Type t in currentBatch)
                {
                    //Status.ChangeStatus("Writing " + t.Name);

                    t.GetMethod("SetObjects",
                        new Type[] { typeof(Dictionary<Type, List<IStructural>>) })
                        .Invoke(null, new object[] { newObjects });

                    traversedTypes.Add(t);
                }
            } while (currentBatch.Count > 0);

            GSA.BlankDepreciatedGWASetCommands();
            GSA.UpdateViews();

            IsBusy = false;
            Status.ChangeStatus("Finished receiving", 100);
        }
        
        public void Dispose()
        {
            foreach (string streamID in GSA.Receivers)
                Receivers[streamID].UpdateGlobalTrigger -= Trigger;
        }
    }
}
