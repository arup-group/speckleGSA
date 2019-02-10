using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Interop.Gsa_10_0;
using SpeckleStructures;

namespace SpeckleGSA
{
    public class Controller
    {
        public Dictionary<string, SpeckleGSASender> senders;
        public Dictionary<string, SpeckleGSAReceiver> receivers;
        
        public Controller()
        {
            senders = new Dictionary<string, SpeckleGSASender>();
            receivers = new Dictionary<string, SpeckleGSAReceiver>();
        }
        
        public async Task ExportObjects(string restApi, string apiToken)
        {
            List<Task> taskList = new List<Task>();

            if (!GSA.IsInit)
            {
                Status.AddError("GSA link not found.");
                return;
            }

            GSA.ClearCache();
            GSA.UpdateUnits();

            // Initialize object read priority list
            Dictionary<Type, List<Type>> typePrerequisites = new Dictionary<Type, List<Type>>();
            
            IEnumerable<Type> objTypes = typeof(GSA)
                .Assembly.GetTypes()
                .Where(t => t.IsSubclassOf(typeof(StructuralObject)) && !t.IsAbstract);

            Status.ChangeStatus("Preparing to read GSA Objects");

            foreach (Type t in objTypes)
            {
                if (t.GetMethod("GetObjects",
                    new Type[] { typeof(Dictionary<Type, List<StructuralObject>>) }) == null)
                    continue;

                if (t.GetField("Stream") == null) continue;

                if (t.GetField("AnalysisLayer") != null)
                    if (GSA.TargetAnalysisLayer && !(bool)t.GetField("AnalysisLayer").GetValue(null)) continue;

                if (t.GetField("DesignLayer") != null)
                    if (GSA.TargetDesignLayer && !(bool)t.GetField("DesignLayer").GetValue(null)) continue;

                List<Type> prereq = new List<Type>();
                if (t.GetField("ReadPrerequisite") != null)
                    prereq = ((Type[])t.GetField("ReadPrerequisite").GetValue(null)).ToList();
                        
                typePrerequisites[t] = prereq;
            }

            // Remove wrong layer objects from prerequisites
            foreach (Type t in objTypes)
            {
                if (t.GetField("AnalysisLayer") != null)
                    if (GSA.TargetAnalysisLayer && !(bool)t.GetField("AnalysisLayer").GetValue(null))
                        foreach (KeyValuePair<Type, List<Type>> kvp in typePrerequisites)
                            kvp.Value.Remove(t);

                if (t.GetField("DesignLayer") != null)
                    if (GSA.TargetDesignLayer && !(bool)t.GetField("DesignLayer").GetValue(null))
                        foreach (KeyValuePair<Type, List<Type>> kvp in typePrerequisites)
                            kvp.Value.Remove(t);
            }

            // Read objects
            Dictionary<Type, List<StructuralObject>> bucketObjects = new Dictionary<Type, List<StructuralObject>>();

            List<Type> currentBatch = new List<Type>();
            do
            {
                currentBatch = typePrerequisites.Where(i => i.Value.Count == 0).Select(i => i.Key).ToList();
                
                foreach (Type t in currentBatch)
                {
                    Status.ChangeStatus("Reading " + t.Name);

                    t.GetMethod("GetObjects",
                        new Type[] { typeof(Dictionary<Type, List<StructuralObject>>) })
                        .Invoke(null, new object[] { bucketObjects });
                    
                    typePrerequisites.Remove(t);

                    foreach (KeyValuePair<Type,List<Type>> kvp in typePrerequisites)
                        if (kvp.Value.Contains(t))
                            kvp.Value.Remove(t);
                }
            } while (currentBatch.Count > 0);
            
            // Remove unimportant nodes
            if (Settings.SendOnlyMeaningfulNodes && bucketObjects.ContainsKey(typeof(GSANode)))
                bucketObjects[typeof(GSANode)] = bucketObjects[typeof(GSANode)]
                    .Where(n => (n as GSANode).ForceSend ||
                    !(n as GSANode).Restraint.Equals(new SixVectorBool()) ||
                    !(n as GSANode).Stiffness.Equals(new SixVectorDouble()) ||
                    (n as GSANode).Mass > 0).ToList();

            // Convert objects to base class
            Dictionary<Type, List<StructuralObject>> tempBucket = new Dictionary<Type, List<StructuralObject>>();
            foreach (KeyValuePair<Type, List<StructuralObject>> kvp in bucketObjects)
            {
                tempBucket[kvp.Key] = kvp.Value.Select(
                    x => x.GetType().GetMethod("GetBase").Invoke(x, new object[] { })).Cast<StructuralObject>().ToList();
            }
            bucketObjects = tempBucket;

            // Seperate objects into streams
            Dictionary<string, List<object>> streamBuckets = new Dictionary<string, List<object>>();

            Status.ChangeStatus("Preparing stream buckets");

            foreach (KeyValuePair<Type, List<StructuralObject>> kvp in bucketObjects)
            {
                string stream = (string)kvp.Key.GetField("Stream").GetValue(null);

                if (!streamBuckets.ContainsKey(stream))
                    streamBuckets[stream] = (kvp.Value as IList).Cast<object>().ToList();
                else
                    streamBuckets[stream].AddRange((kvp.Value as IList).Cast<object>().ToList());
            }

            // Send package
            Status.ChangeStatus("Sending to Server");

            foreach (KeyValuePair<string, List<object>> kvp in streamBuckets)
            {
                // Create sender
                senders[kvp.Key] = new SpeckleGSASender(restApi, apiToken);

                if (!GSA.Senders.ContainsKey(kvp.Key))
                {
                    Status.AddMessage(kvp.Key + " sender not initialized. Creating new " + kvp.Key + " sender.");
                    await senders[kvp.Key].InitializeSender(null, GSA.Title() + "." + kvp.Key);
                    GSA.Senders[kvp.Key] = senders[kvp.Key].StreamID;
                }
                else
                    await senders[kvp.Key].InitializeSender(GSA.Senders[kvp.Key], GSA.Title() + "." + kvp.Key);

                // Send package asynchronously
                Task task = new Task(() =>
                {
                    try
                    { 
                        senders[kvp.Key].SendGSAObjects(
                            new Dictionary<string, List<object>>() {
                                { "All", kvp.Value }
                            });
                    }
                    catch (Exception ex)
                    {
                        Status.AddError(ex.Message);
                    }
                });
                task.Start();
                taskList.Add(task);
            }
            
            await Task.WhenAll(taskList);

            // Complete
            Status.ChangeStatus("Sending complete", 0);

            Status.AddMessage("Sending complete!");
        }        

        public async Task ImportObjects(string restApi, string apiToken)
        {
            List<Task> taskList = new List<Task>();

            Dictionary<Type, List<StructuralObject>> objects = new Dictionary<Type, List<StructuralObject>>();

            if (!GSA.IsInit)
            {
                Status.AddError("GSA link not found.");
                return;
            }

            GSA.ClearCache();
            GSA.UpdateUnits();

            // Pull objects from server asynchronously
            Dictionary<string,List<object>> convertedObjects = new Dictionary<string, List<object>>();

            Status.ChangeStatus("Receiving from server");
            foreach (string streamID in GSA.Receivers)
            {
                if (streamID == "")
                    Status.AddMessage("No " + streamID + " stream specified.");
                else
                {
                    Status.AddMessage("Creating receiver " + streamID);
                    receivers[streamID] = new SpeckleGSAReceiver(restApi, apiToken);
                    await receivers[streamID].InitializeReceiver(streamID);

                    if (receivers[streamID].StreamID == null || receivers[streamID].StreamID == "")
                        Status.AddError("Could not connect to " + streamID + " stream.");
                    else
                    {
                        
                        Task task = new Task(() =>
                        {
                            try
                            {
                                convertedObjects[streamID] = receivers[streamID].GetGSAObjects();
                            }
                            catch (Exception ex)
                            {
                                Status.AddError(ex.Message);
                            }
                        });
                        task.Start();
                        taskList.Add(task);
                    }
                }
            }

            await Task.WhenAll(taskList);

            // Initialize object write priority list
            Dictionary<Type, List<Type>> typePrerequisites = new Dictionary<Type, List<Type>>();

            IEnumerable<Type> objTypes = typeof(GSA)
                .Assembly.GetTypes()
                .Where(t => t.IsSubclassOf(typeof(StructuralObject)) && !t.IsAbstract);

            Status.ChangeStatus("Preparing to write GSA Objects");

            foreach (Type t in objTypes)
            {
                if (t.GetMethod("WriteObjects",
                    new Type[] { typeof(Dictionary<Type, List<StructuralObject>>) }) == null)
                    continue;

                if (t.GetField("AnalysisLayer") != null)
                    if (GSA.TargetAnalysisLayer && !(bool)t.GetField("AnalysisLayer").GetValue(null)) continue;

                if (t.GetField("DesignLayer") != null)
                    if (GSA.TargetDesignLayer && !(bool)t.GetField("DesignLayer").GetValue(null)) continue;

                List<Type> prereq = new List<Type>();
                if (t.GetField("WritePrerequisite") != null)
                    prereq = ((Type[])t.GetField("WritePrerequisite").GetValue(null)).ToList();

                typePrerequisites[t] = prereq;
            }

            // Remove wrong layer objects from prerequisites
            foreach (Type t in objTypes)
            {
                if (t.GetField("AnalysisLayer") != null)
                    if (GSA.TargetAnalysisLayer && !(bool)t.GetField("AnalysisLayer").GetValue(null))
                        foreach(KeyValuePair<Type,List<Type>> kvp in typePrerequisites)
                            kvp.Value.Remove(t);

                if (t.GetField("DesignLayer") != null)
                    if (GSA.TargetDesignLayer && !(bool)t.GetField("DesignLayer").GetValue(null))
                        foreach (KeyValuePair<Type, List<Type>> kvp in typePrerequisites)
                            kvp.Value.Remove(t);
            }

            List<KeyValuePair<Type,List<Type>>> typeCastPriorty = typePrerequisites.ToList();

            typeCastPriorty.Sort((x, y) => x.Value.Count().CompareTo(y.Value.Count()));

            // Populate dictionary
            Status.ChangeStatus("Bucketing objects");
            foreach (KeyValuePair<string,List<object>> kvp in convertedObjects)
            {
                double scaleFactor = (1.0).ConvertUnit(receivers[kvp.Key].Units.ShortUnitName(), GSA.Units);

                foreach (object obj in kvp.Value)
                { 
                    if (obj == null) continue;
                    
                    try
                    {
                        if (obj is IEnumerable)
                        {
                            foreach(StructuralObject o in obj as IList)
                            {
                                o.Scale(scaleFactor);

                                Type castType = typeCastPriorty.Where(t => t.Key.IsSubclassOf(o.GetType())).First().Key;

                                if (castType == null) continue;

                                if (!objects.ContainsKey(castType))
                                    objects[castType] = new List<StructuralObject>() { (StructuralObject)Activator.CreateInstance(castType, o)};
                                else
                                    (objects[castType] as List<StructuralObject>).Add( (StructuralObject)Activator.CreateInstance(castType, o) );
                            }
                        }
                        else
                        {
                            (obj as StructuralObject).Scale(scaleFactor);

                            Type castType = typeCastPriorty.Where(t => t.Key.IsSubclassOf(obj.GetType())).First().Key;

                            if (castType == null) continue;

                            if (!objects.ContainsKey(castType))
                                objects[castType] = new List<StructuralObject>() { (StructuralObject)Activator.CreateInstance(castType, obj) };
                            else
                                (objects[castType] as List<StructuralObject>).Add( (StructuralObject)Activator.CreateInstance(castType, obj) );
                        }
                    }
                    catch (Exception ex)
                    {
                        Status.AddError(ex.Message);
                    }
                }
            }
            
            // Set up counter
            GSARefCounters.Clear();

            foreach (KeyValuePair<Type, List<StructuralObject>> kvp in objects)
            {
                // Reserve reference
                GSARefCounters.AddObjRefs((string)kvp.Key.GetField("GSAKeyword").GetValue(null),
                    (kvp.Value as IList).Cast<StructuralObject>().Select(o => o.Reference).ToList());
            }

            // Initialize object write priority list
            Status.ChangeStatus("Preparing to write GSA Object");

            // Clear GSA file
            foreach (KeyValuePair<Type, List<Type>> kvp in typePrerequisites)
            {
                Status.ChangeStatus("Clearing " + kvp.Key.Name);

                try
                {
                    string keyword = (string)kvp.Key.GetField("GSAKeyword").GetValue(null);
                    int highestRecord = (int)GSA.RunGWACommand("HIGHEST," + keyword);

                    GSA.RunGWACommand("BLANK," + kvp.Key.GetField("GSAKeyword").GetValue(null) + ",1," + highestRecord.ToString());
                }
                catch { }
            }

            // Write objects
            List<Type> currentBatch = new List<Type>();
            do
            {
                currentBatch = typePrerequisites.Where(i => i.Value.Count == 0).Select(i => i.Key).ToList();

                foreach (Type t in currentBatch)
                {
                    Status.ChangeStatus("Writing " + t.Name);

                    t.GetMethod("WriteObjects",
                        new Type[] { typeof(Dictionary<Type, List<StructuralObject>>) })
                        .Invoke(null, new object[] { objects });

                    typePrerequisites.Remove(t);

                    foreach (KeyValuePair<Type, List<Type>> kvp in typePrerequisites)
                        if (kvp.Value.Contains(t))
                            kvp.Value.Remove(t);
                }
            } while (currentBatch.Count > 0);

            GSA.UpdateViews();

            Status.ChangeStatus("Receiving complete", 0);
            Status.AddMessage("Receiving completed!");
        }
    }

}
