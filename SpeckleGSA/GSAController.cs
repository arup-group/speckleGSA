using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Interop.Gsa_9_0;

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

        public event EventHandler<StatusEventArgs> StatusChanged;

        private StreamManager streamManager;
        private UserManager userManager;
        private Dictionary<string, SpeckleGSASender> senders;
        private Dictionary<string, SpeckleGSAReceiver> receivers;
        private ComAuto gsaObj;
        
        public GSAController()
        {
            userManager = null;

            senders = new Dictionary<string, SpeckleGSASender>();
            receivers = new Dictionary<string, SpeckleGSAReceiver>();
        }
        
        public void AttachStatusHandler(EventHandler<StatusEventArgs> statusHandler)
        {
            StatusChanged = statusHandler;
        }

        public void ChangeStatus(string eventName, double percent = -1)
        {
            if (StatusChanged != null)
            {
                StatusChanged(null, new StatusEventArgs(eventName, percent));
            }
        }

        #region Server
        public async Task Login(string email, string password, string serverAddress)
        {
            await Task.Run(delegate
            {
                userManager = new UserManager(email, password, serverAddress);
            }).ContinueWith(delegate
            {
                if (userManager.Login() == 0)
                    MessageLog.AddMessage("Successfully logged in");
                else
                    MessageLog.AddError("Failed to login");

                streamManager = new StreamManager(userManager.ServerAddress, userManager.ApiToken);
            });
        }

        public async Task<List<Tuple<string, string>>> GetStreamList()
        {
            if (userManager == null | streamManager == null)
            {
                MessageLog.AddError("Not logged in");
                return null;
            }

            try
            {
                MessageLog.AddMessage("Fetching stream list.");
                var response = await streamManager.GetStreams();
                MessageLog.AddMessage("Finished fetching stream list.");
                return response;
            }
            catch (Exception e)
            {
                MessageLog.AddError(e.Message);
                return null;
            }
        }

        public async Task CloneModelStreams()
        {
            if (userManager == null | streamManager == null)
            {
                MessageLog.AddError("Not logged in");
                return;
            }
            
            foreach (KeyValuePair<string, SpeckleGSASender> kvp in senders)
            {
                streamManager.CloneStream(kvp.Value.StreamID).ContinueWith(res => MessageLog.AddMessage("Cloned " + kvp.Key + " stream to ID : " + res.Result));
            }
        }
        #endregion

        #region GSA
        public void Link()
        {
            if (userManager == null)
            {
                MessageLog.AddError("Not logged in");
                return;
            }

            gsaObj = new ComAuto();
            MessageLog.AddMessage("GSA link established");
        }

        public void NewFile()
        {
            if (gsaObj == null)
            {
                MessageLog.AddError("No GSA link found");
                return;
            }

            gsaObj.NewFile();
            gsaObj.DisplayGsaWindow(true);
            MessageLog.AddMessage("New file created");
        }

        public void OpenFile(string path)
        {
            if (gsaObj == null)
            {
                MessageLog.AddError("No GSA link found");
                return;
            }

            gsaObj.Open(path);
            gsaObj.DisplayGsaWindow(true);
            MessageLog.AddMessage("Opened " + path);
        }
        #endregion

        #region Extract GSA
        public async Task ExportObjects(string modelName)
        {
            List<Task> taskList = new List<Task>();

            if (gsaObj == null)
            {
                MessageLog.AddError("GSA link not found.");
                return;
            }

            // Initialize object read priority list
            SortedDictionary<int, List<Type>> typePriority = new SortedDictionary<int, List<Type>>();

            IEnumerable<Type> objTypes = typeof(GSAObject)
                .Assembly.GetTypes()
                .Where(t => t.IsSubclassOf(typeof(GSAObject)) && !t.IsAbstract);

            ChangeStatus("PREPARING TO READ GSA OBJECTS");

            foreach (Type t in objTypes)
            {
                if (t.GetMethod("GetObjects",
                    new Type[] { typeof(ComAuto), typeof(Dictionary<Type, object>) }) == null)
                    continue;

                if (t.GetField("Stream") == null) continue;

                int priority = 0;
                if (t.GetField("ReadPriority") != null)
                    priority = (int)t.GetField("ReadPriority").GetValue(null);

                if (!typePriority.ContainsKey(priority))
                    typePriority[priority] = new List<Type>();

                typePriority[priority].Add(t);
            }

            // Read objects
            Dictionary<Type, object> bucketObjects = new Dictionary<Type, object>();

            foreach (KeyValuePair<int, List<Type>> kvp in typePriority)
            {
                foreach (Type t in kvp.Value)
                {
                    ChangeStatus("READING " + t.Name);

                    t.GetMethod("GetObjects",
                        new Type[] { typeof(ComAuto), typeof(Dictionary<Type, object>)})
                        .Invoke(null, new object[] { gsaObj, bucketObjects });
                }
            }

            // Seperate objects into streams
            Dictionary<string, List<object>> streamBuckets = new Dictionary<string, List<object>>();

            ChangeStatus("PREPARING STREAM BUCKETS");

            foreach (KeyValuePair<Type, object> kvp in bucketObjects)
            {
                string stream = (string)kvp.Key.GetField("Stream").GetValue(null);
                if (!streamBuckets.ContainsKey(stream))
                    streamBuckets[stream] = (kvp.Value as IList).Cast<object>().ToList();
                else
                    streamBuckets[stream].AddRange((kvp.Value as IList).Cast<object>().ToList());
            }

            // Send package
            ChangeStatus("SENDING TO SERVER");

            foreach (KeyValuePair<string, List<object>> kvp in streamBuckets)
            {
                // Create sender if not initialized
                if (!senders.ContainsKey(kvp.Key))
                {
                    MessageLog.AddMessage(kvp.Key + " sender not initialized. Creating new " + kvp.Key + " sender.");
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
                        });
                    }
                    catch (Exception ex)
                    {
                        MessageLog.AddError(ex.Message);
                    }
                });
                task.Start();
                taskList.Add(task);
            }
            
            await Task.WhenAll(taskList);

            // Complete
            ChangeStatus("SENDING COMPLETE", 0);

            MessageLog.AddMessage("Sending complete!");
        }        
        #endregion

        #region Import GSA
        public async Task ImportObjects(Dictionary<string, string> streamIDs)
        {
            List<Task> taskList = new List<Task>();

            Dictionary<Type, object> objects = new Dictionary<Type, object>();

            if (gsaObj == null)
            {
                MessageLog.AddError("GSA link not found.");
                return;
            }

            // Pull objects from server asynchronously
            List<object> convertedObjects = new List<object>();

            ChangeStatus("RECEIVING FROM SERVER");
            foreach (KeyValuePair<string, string> kvp in streamIDs)
            {
                if (kvp.Value == "")
                    MessageLog.AddMessage("No " + kvp.Key + " stream specified.");
                else
                {
                    MessageLog.AddMessage("Creating " + kvp.Key + " receiver.");
                    receivers[kvp.Key] = new SpeckleGSAReceiver(userManager.ServerAddress, userManager.ApiToken);
                    await receivers[kvp.Key].InitializeReceiver(kvp.Value);

                    if (receivers[kvp.Key].StreamID == null || receivers[kvp.Key].StreamID == "")
                        MessageLog.AddError("Could not connect to " + kvp.Key + " stream.");
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
                                MessageLog.AddError(ex.Message);
                            }
                        });
                        task.Start();
                        taskList.Add(task);
                    }
                }
            }

            await Task.WhenAll(taskList);

            // Populate dictionary
            ChangeStatus("BUCKETING OBJECTS");
            foreach (object obj in convertedObjects)
            {
                if (!objects.ContainsKey(obj.GetType()))
                    objects[obj.GetType()] = new List<GSAObject>() { obj as GSAObject };
                else
                    (objects[obj.GetType()] as List<GSAObject>).Add(obj as GSAObject);
            }

            // Set up counter
            GSARefCounters.Clear();

            foreach (KeyValuePair<Type, object> kvp in objects)
            {
                // Reserve reference
                GSARefCounters.AddObjRefs((string)kvp.Key.GetField("GSAKeyword").GetValue(null),
                    (kvp.Value as IList).Cast<GSAObject>().Select(o => o.Reference).ToList());

                // Reserve connectivities
                GSARefCounters.AddObjRefs("NODE",
                    (kvp.Value as IList).Cast<GSAObject>().SelectMany(e => e.Connectivity).ToList());
            }

            // Initialize object write priority list
            SortedDictionary<int, List<Type>> typePriority = new SortedDictionary<int, List<Type>>();

            IEnumerable<Type> objTypes = typeof(GSAObject)
                .Assembly.GetTypes()
                .Where(t => t.IsSubclassOf(typeof(GSAObject)) && !t.IsAbstract);

            ChangeStatus("PREPARING TO WRITE GSA OBJECTS");

            foreach (Type t in objTypes)
            {
                if (t.GetMethod("WriteObjects",
                    new Type[] { typeof(ComAuto), typeof(Dictionary<Type, object>) }) == null)
                    continue;

                int priority = 0;
                if (t.GetField("WritePriority") != null)
                    priority = (int)t.GetField("WritePriority").GetValue(null);

                if (!typePriority.ContainsKey(priority))
                    typePriority[priority] = new List<Type>();

                typePriority[priority].Add(t);
            }

            // Write objects
            foreach (KeyValuePair<int, List<Type>> kvp in typePriority)
            {
                foreach (Type t in kvp.Value)
                {
                    ChangeStatus("WRITING " + t.Name);

                    t.GetMethod("WriteObjects",
                        new Type[] { typeof(ComAuto), typeof(Dictionary<Type, object>) })
                        .Invoke(null, new object[] { gsaObj, objects });
                }
            }
            
            gsaObj.UpdateViews();

            ChangeStatus("RECEIVING COMPLETE", 0);
            MessageLog.AddMessage("Receiving completed!");
        }
        #endregion
    }

    public class StatusEventArgs : EventArgs
    {
        private readonly double percent;
        private readonly string name;

        public StatusEventArgs(string name, double percent)
        {
            this.name = name;
            this.percent = percent;
        }

        public double Percent
        {
            get { return percent; }
        }

        public string Name
        {
            get { return name; }
        }
    }

}
