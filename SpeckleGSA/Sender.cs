using SpeckleStructures;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA
{
    public class Sender
    {
        public Dictionary<string, SpeckleGSASender> Senders;
        public Dictionary<Type, List<object>> SenderObjectCache;

        public Dictionary<Type, List<Type>> TypePrerequisites;

        public bool IsInit;

        public Sender()
        {
            Senders = new Dictionary<string, SpeckleGSASender>();
            SenderObjectCache = new Dictionary<Type, List<object>>();
            TypePrerequisites = new Dictionary<Type, List<Type>>();
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

            // Initialize object read priority list
            IEnumerable<Type> objTypes = typeof(GSA)
                .Assembly.GetTypes()
                .Where(t => t.IsSubclassOf(typeof(StructuralObject)) && !t.IsAbstract);

            Status.ChangeStatus("Preparing to read GSA Objects");

            foreach (Type t in objTypes)
            {
                if (t.GetMethod("GetObjects",
                    new Type[] { typeof(Dictionary<Type, List<object>>) }) == null)
                    continue;

                if (t.GetAttribute("Stream") == null) continue;

                if (t.GetAttribute("AnalysisLayer") != null)
                    if (GSA.TargetAnalysisLayer && !(bool)t.GetAttribute("AnalysisLayer")) continue;

                if (t.GetAttribute("DesignLayer") != null)
                    if (GSA.TargetDesignLayer && !(bool)t.GetAttribute("DesignLayer")) continue;

                List<Type> prereq = new List<Type>();
                if (t.GetAttribute("ReadPrerequisite") != null)
                    prereq = ((Type[])t.GetAttribute("ReadPrerequisite")).ToList();

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

            // Create the streams
            Status.ChangeStatus("Creating streams");

            List<string> streamNames = new List<string>();

            if (Settings.SeperateStreams)
            {
                foreach (Type t in objTypes)
                    streamNames.Add((string)t.GetAttribute("Stream"));
                streamNames = streamNames.Distinct().ToList();
            }
            else
                streamNames.Add("Full Model");

            foreach (string streamName in streamNames)
            {
                Senders[streamName] = new SpeckleGSASender(restApi, apiToken);

                if (!GSA.Senders.ContainsKey(streamName))
                {
                    Status.AddMessage(streamName + " sender not initialized. Creating new " + streamName + " sender.");
                    await Senders[streamName].InitializeSender(null, streamName);
                    GSA.Senders[streamName] = Senders[streamName].StreamID;
                }
                else
                    await Senders[streamName].InitializeSender(GSA.Senders[streamName], streamName);
            }

            Status.ChangeStatus("Ready to stream");
            IsInit = true;
        }

        public void Trigger()
        {
            if (!IsInit) return;

            GSA.ClearCache();
            GSA.UpdateUnits();

            // Read objects
            List<Type> currentBatch = new List<Type>();
            List<Type> traversedTypes = new List<Type>();

            bool changeDetected = false;
            do
            {
                currentBatch = TypePrerequisites.Where(i => i.Value.Count(x => !traversedTypes.Contains(x)) == 0).Select(i => i.Key).ToList();
                currentBatch.RemoveAll(i => traversedTypes.Contains(i));

                foreach (Type t in currentBatch)
                {
                    //Status.ChangeStatus("Reading " + t.Name);

                    bool result = (bool)t.GetMethod("GetObjects",
                        new Type[] { typeof(Dictionary<Type, List<object>>) })
                        .Invoke(null, new object[] { SenderObjectCache });

                    if (result)
                        changeDetected = true;

                    traversedTypes.Add(t);
                }
            } while (currentBatch.Count > 0);

            if (!changeDetected)
            {
                Status.ChangeStatus("Finished sending", 100);
                return;
            }

            // Convert objects to base class
            Dictionary<Type, List<StructuralObject>> convertedBucket = new Dictionary<Type, List<StructuralObject>>();
            foreach (KeyValuePair<Type, List<object>> kvp in SenderObjectCache)
            {
                if ((kvp.Key == typeof(GSANode)) && Settings.SendOnlyMeaningfulNodes && SenderObjectCache.ContainsKey(typeof(GSANode)))
                {
                    // Remove unimportant nodes
                    convertedBucket[kvp.Key] = kvp.Value
                        .Where(n => (n as GSANode).ForceSend ||
                        !(n as GSANode).Restraint.Equals(new SixVectorBool()) ||
                        !(n as GSANode).Stiffness.Equals(new SixVectorDouble()) ||
                        (n as GSANode).Mass > 0)
                        .Select(x => x.GetBase()).Cast<StructuralObject>().ToList();
                }
                else
                {
                    convertedBucket[kvp.Key] = kvp.Value.Select(
                        x => x.GetBase()).Cast<StructuralObject>().ToList();
                }
            }

            // Seperate objects into streams
            Dictionary<string, Dictionary<string, List<object>>> streamBuckets = new Dictionary<string, Dictionary<string, List<object>>>();

            Status.ChangeStatus("Preparing stream buckets");

            foreach (KeyValuePair<Type, List<StructuralObject>> kvp in convertedBucket)
            {
                string stream;

                if (Settings.SeperateStreams)
                    stream = (string)kvp.Key.GetAttribute("Stream");
                else
                    stream = "Full Model";

                if (!streamBuckets.ContainsKey(stream))
                    streamBuckets[stream] = new Dictionary<string, List<object>>() { { kvp.Key.BaseType.Name.ToString(), (kvp.Value as IList).Cast<object>().ToList() } };
                else
                    streamBuckets[stream][kvp.Key.BaseType.Name.ToString()] = (kvp.Value as IList).Cast<object>().ToList();
            }

            // Send package
            Status.ChangeStatus("Sending to Server");

            foreach (KeyValuePair<string, Dictionary<string, List<object>>> kvp in streamBuckets)
            {
                string streamName = "";

                if (Settings.SeperateStreams)
                    streamName = GSA.Title() + "." + kvp.Key;
                else
                    streamName = GSA.Title();

                Senders[kvp.Key].UpdateName(streamName);
                Senders[kvp.Key].SendGSAObjects(kvp.Value);
            }

            Status.ChangeStatus("Finished sending", 100);
        }
    }
}
