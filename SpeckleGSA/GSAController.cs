using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Interop.Gsa_10_0;

namespace SpeckleGSA
{
    public class GSAController
    {
        public string SenderPropertiesStreamID
        {
            get
            {
                if (senders.ContainsKey("properties"))
                    return senders["properties"].StreamID;
                else
                    return "";
            }
        }
        public string SenderNodesStreamID { get
            {
                if (senders.ContainsKey("nodes"))
                    return senders["nodes"].StreamID;
                else
                    return "";
            }
        }
        public string SenderElementsStreamID {
            get
            {
                if (senders.ContainsKey("elements"))
                    return senders["elements"].StreamID;
                else
                    return "";
            }
        }

        private StreamManager streamManager;
        private UserManager userManager;
        private Dictionary<string, SpeckleGSASender> senders;
        private Dictionary<string, SpeckleGSAReceiver> receivers;
        
        public GSAController()
        {
            userManager = null;

            senders = new Dictionary<string, SpeckleGSASender>();
            receivers = new Dictionary<string, SpeckleGSAReceiver>();
        }
        
        #region Server
        public void Login(string email, string password, string serverAddress)
        {
            var tempUserManager = new UserManager(email, password, serverAddress);
            
            if (tempUserManager.Login() == 0)
            { 
                Status.AddMessage("Successfully logged in");
                userManager = tempUserManager;
                streamManager = new StreamManager(userManager.ServerAddress, userManager.ApiToken);
            }
            else
                Status.AddError("Failed to login");
        }

        public List<Tuple<string, string>> GetStreamList()
        {
            if (userManager == null | streamManager == null)
            {
                Status.AddError("Not logged in");
                return null;
            }

            try
            {
                Status.AddMessage("Fetching stream list.");
                var response = streamManager.GetStreams().Result;
                Status.AddMessage("Finished fetching stream list.");
                return response;
            }
            catch (Exception e)
            {
                Status.AddError(e.Message);
                return null;
            }
        }

        public void CloneModelStreams()
        {
            if (userManager == null | streamManager == null)
            {
                Status.AddError("Not logged in");
                return;
            }
            
            foreach (KeyValuePair<string, SpeckleGSASender> kvp in senders)
            {
                streamManager.CloneStream(kvp.Value.StreamID).ContinueWith(res => Status.AddMessage("Cloned " + kvp.Key + " stream to ID : " + res.Result));
            }
        }

        public List<Tuple<string,string>> GetSenderStreams()
        {
            List<Tuple<string, string>> streams = new List<Tuple<string, string>>();

            foreach (KeyValuePair<string, SpeckleGSASender> kvp in senders)
                streams.Add(new Tuple<string, string>(kvp.Value.StreamName, kvp.Value.StreamID));

            return streams;
        }
        #endregion

        #region GSA
        public async Task ExportObjects(string modelName)
        {
            List<Task> taskList = new List<Task>();

            if (!GSA.IsInit)
            {
                Status.AddError("GSA link not found.");
                return;
            }

            // Initialize object read priority list
            Dictionary<Type, List<Type>> typePrerequisites = new Dictionary<Type, List<Type>>();
            
            IEnumerable<Type> objTypes = typeof(GSAObject)
                .Assembly.GetTypes()
                .Where(t => t.IsSubclassOf(typeof(GSAObject)) && !t.IsAbstract);

            Status.ChangeStatus("Preparing to read GSA Objects");

            foreach (Type t in objTypes)
            {
                if (t.GetMethod("GetObjects",
                    new Type[] { typeof(Dictionary<Type, object>) }) == null)
                    continue;

                if (t.GetField("Stream") == null) continue;

                List<Type> prereq = new List<Type>();
                if (t.GetField("ReadPrerequisite") != null)
                    prereq = ((Type[])t.GetField("ReadPrerequisite").GetValue(null)).ToList();
                        
                typePrerequisites[t] = prereq;
            }

            // Getting document settings
            Dictionary<string, object> baseProps = new Dictionary<string, object>();
            baseProps["units"] = "Millimeters";// gsaObj.GwaCommand("GET,UNIT_DATA,LENGTH");
            baseProps["tolerance"] = HelperFunctions.EPS;
            baseProps["angleTolerance"] = HelperFunctions.EPS;

            // Read objects
            Dictionary<Type, object> bucketObjects = new Dictionary<Type, object>();

            List<Type> currentBatch = new List<Type>();
            do
            {
                currentBatch = typePrerequisites.Where(i => i.Value.Count == 0).Select(i => i.Key).ToList();
                
                foreach (Type t in currentBatch)
                {
                    Status.ChangeStatus("Reading " + t.Name);

                    t.GetMethod("GetObjects",
                        new Type[] { typeof(Dictionary<Type, object>) })
                        .Invoke(null, new object[] { bucketObjects });
                    
                    typePrerequisites.Remove(t);

                    foreach (KeyValuePair<Type,List<Type>> kvp in typePrerequisites)
                        if (kvp.Value.Contains(t))
                            kvp.Value.Remove(t);
                }
            } while (currentBatch.Count > 0);
            
            // Seperate objects into streams
            Dictionary<string, List<object>> streamBuckets = new Dictionary<string, List<object>>();

            Status.ChangeStatus("Preparing stream buckets");

            foreach (KeyValuePair<Type, object> kvp in bucketObjects)
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
                // Create sender if not initialized
                if (!senders.ContainsKey(kvp.Key))
                {
                    Status.AddMessage(kvp.Key + " sender not initialized. Creating new " + kvp.Key + " sender.");
                    senders[kvp.Key] = new SpeckleGSASender(userManager.ServerAddress, userManager.ApiToken);
                    await senders[kvp.Key].InitializeSender();
                }

                senders[kvp.Key].UpdateName(modelName + "." + kvp.Key);
                
                // Send package asynchronously
                Task task = new Task(() =>
                {
                    try
                    { 
                        senders[kvp.Key].SendGSAObjects(
                            new Dictionary<string, List<object>>() {
                                { "All", kvp.Value }
                            }, baseProps);
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

        public async Task ImportObjects(Dictionary<string, string> streamIDs)
        {
            List<Task> taskList = new List<Task>();

            Dictionary<Type, object> objects = new Dictionary<Type, object>();

            if (!GSA.IsInit)
            {
                Status.AddError("GSA link not found.");
                return;
            }

            // Pull objects from server asynchronously
            List<object> convertedObjects = new List<object>();

            Status.ChangeStatus("Receiving from server");
            foreach (KeyValuePair<string, string> kvp in streamIDs)
            {
                if (kvp.Value == "")
                    Status.AddMessage("No " + kvp.Key + " stream specified.");
                else
                {
                    Status.AddMessage("Creating " + kvp.Key + " receiver.");
                    receivers[kvp.Key] = new SpeckleGSAReceiver(userManager.ServerAddress, userManager.ApiToken);
                    await receivers[kvp.Key].InitializeReceiver(kvp.Value);

                    if (receivers[kvp.Key].StreamID == null || receivers[kvp.Key].StreamID == "")
                        Status.AddError("Could not connect to " + kvp.Key + " stream.");
                    else
                    {
                        Task task = new Task(() =>
                        {
                            try
                            {
                                convertedObjects.AddRange(receivers[kvp.Key].GetGSAObjects());
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

            // Populate dictionary
            Status.ChangeStatus("Bucketing objects");
            foreach (object obj in convertedObjects)
            {
                if (obj == null) continue;

                try
                { 
                    if (!objects.ContainsKey(obj.GetType()))
                    { 
                        if (obj.IsList())
                            objects[obj.GetType()] = (obj as IList).Cast<GSAObject>().ToList();
                        else if (obj is GSAObject)
                                objects[obj.GetType()] = new List<GSAObject>() { obj as GSAObject };
                    }
                    else
                    { 
                        if (obj.IsList())
                            (objects[obj.GetType()] as List<GSAObject>)
                                .AddRange((obj as IList).Cast<GSAObject>().ToList());
                        else if (obj is GSAObject)
                                (objects[obj.GetType()] as List<GSAObject>).Add(obj as GSAObject);
                    }
                }
                catch (Exception ex)
                {
                    Status.AddError(ex.Message);
                }
            }

            // Set up counter
            GSARefCounters.Clear();

            foreach (KeyValuePair<Type, object> kvp in objects)
            {
                // Reserve reference
                GSARefCounters.AddObjRefs((string)kvp.Key.GetField("GSAKeyword").GetValue(null),
                    (kvp.Value as IList).Cast<GSAObject>().Select(o => o.Reference).ToList());
            }

            // Initialize object write priority list
            SortedDictionary<int, List<Type>> typePriority = new SortedDictionary<int, List<Type>>();

            IEnumerable<Type> objTypes = typeof(GSAObject)
                .Assembly.GetTypes()
                .Where(t => t.IsSubclassOf(typeof(GSAObject)) && !t.IsAbstract);

            Status.ChangeStatus("Preparing to write GSA Object");

            foreach (Type t in objTypes)
            {
                if (t.GetMethod("WriteObjects",
                    new Type[] { typeof(Dictionary<Type, object>) }) == null)
                    continue;

                int priority = 0;
                if (t.GetField("WritePriority") != null)
                    priority = (int)t.GetField("WritePriority").GetValue(null);

                if (!typePriority.ContainsKey(priority))
                    typePriority[priority] = new List<Type>();

                typePriority[priority].Add(t);
            }

            // Clear GSA file
            foreach (KeyValuePair<int, List<Type>> kvp in typePriority)
            {
                foreach (Type t in kvp.Value)
                {
                    Status.ChangeStatus("Clearing " + t.Name);

                    try
                    {
                        string keyword = (string)t.GetField("GSAKeyword").GetValue(null);
                        int highestRecord = (int)GSA.RunGWACommand("HIGHEST," + keyword);

                        GSA.RunGWACommand("BLANK," + t.GetField("GSAKeyword").GetValue(null) + ",1," + highestRecord.ToString());
                    }
                    catch { }
                }
            }

            // Write objects
            foreach (KeyValuePair<int, List<Type>> kvp in typePriority)
            {
                foreach (Type t in kvp.Value)
                {
                    Status.ChangeStatus("Writing " + t.Name);
                    
                    t.GetMethod("WriteObjects",
                        new Type[] { typeof(Dictionary<Type, object>) })
                        .Invoke(null, new object[] { objects });
                }
            }
            
            GSA.UpdateViews();

            Status.ChangeStatus("Receiving complete", 0);
            Status.AddMessage("Receiving completed!");
        }
        #endregion
    }

}
