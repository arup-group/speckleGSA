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

        private Dictionary<string, List<Type>> objectsInStream = new Dictionary<string, List<Type>>()
        {
            { "properties", new List<Type>(){
                typeof(GSAMaterial),
                typeof(GSA1DProperty),
                typeof(GSA2DProperty),
            } },
            { "nodes", new List<Type>(){
                typeof(GSANode)
            } },
            { "elements", new List<Type>(){
                typeof(GSA1DElement),
                typeof(GSA2DElement),
            } },
        };

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

        public void ChangeStatus(string eventName, double percent)
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
            
            Dictionary<Type, object> bucketObjects = new Dictionary<Type, object>();

            foreach (KeyValuePair<string, List<Type>> kvp in objectsInStream)
            { 
                // Get objects belonging to that stream
                foreach(Type t in kvp.Value)
                    bucketObjects[t] = GetObjects(t, bucketObjects);

                // Create sender if not initialized
                if (!senders.ContainsKey(kvp.Key))
                {
                    MessageLog.AddMessage(kvp.Key + " sender not initialized. Creating new " + kvp.Key + " sender.");
                    senders[kvp.Key] = new SpeckleGSASender(userManager.ServerAddress, userManager.ApiToken);
                    await senders[kvp.Key].InitializeSender();
                }

                senders[kvp.Key].UpdateName(modelName + "." + kvp.Key);

                // Package objects for sending
                List<object> streamObjects = new List<object>();

                foreach (Type t in kvp.Value)
                    streamObjects.AddRange((bucketObjects[t] as IList).Cast<object>().ToList());

                // Send package asynchronously
                Task task = new Task(() =>
                {
                    try
                    { 
                        senders[kvp.Key].SendGSAObjects(
                        new Dictionary<string, List<object>>() {
                            { "All", streamObjects}
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

            ChangeStatus("SENDING TO SERVER", -1);

            await Task.WhenAll(taskList);

            ChangeStatus("SENDING COMPLETE", 0);

            MessageLog.AddMessage("Sending complete!");
        }

        private List<GSAObject> GetObjects(Type type, Dictionary<Type, object> dict)
        {
            List<GSAObject> res = new List<GSAObject>();

            if (type == typeof(GSAMaterial))
                res = GetMaterials();
            else if (type == typeof(GSA2DProperty))
                res = Get2DProperties((dict[typeof(GSAMaterial)] as IList).Cast<GSAMaterial>().ToList());
            else if (type == typeof(GSANode))
                res = GetNodes();
            else if (type == typeof(GSA1DElement))
                res = Get1DElements((dict[typeof(GSANode)] as IList).Cast<GSANode>().ToList());
            else if (type == typeof(GSA2DElement))
                res = Get2DElements((dict[typeof(GSANode)] as IList).Cast<GSANode>().ToList());

            return res;
        }

        private List<GSAObject> GetMaterials()
        {
            int counter;

            string[] materialIdentifier = new string[]
                { "MAT_STEEL", "MAT_CONCRETE" };

            List<GSAObject> materials = new List<GSAObject>();

            List<string> pieces = new List<string>();
            foreach (string id in materialIdentifier)
            {
                string res = gsaObj.GwaCommand("GET_ALL," + id);

                if (res == "")
                    continue;

                pieces.AddRange(res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries));
            }
            pieces = pieces.Distinct().ToList();

            counter = 1;
            for (int i = 0; i < pieces.Count(); i++)
            {
                GSAMaterial mat = new GSAMaterial().AttachGSA(gsaObj);
                mat.ParseGWACommand(pieces[i]);
                mat.Reference = i+1; // Offset references
                materials.Add(mat);

                ChangeStatus("GETTING MATERIALS: " + counter.ToString() + "/" + pieces.Count().ToString(), counter * 100 / pieces.Count());
                counter++;
            }

            return materials;
        }

        private List<GSAObject> Get2DProperties(List<GSAMaterial> materials)
        {
            int counter;

            List<GSAObject> props = new List<GSAObject>();

            string res = gsaObj.GwaCommand("GET_ALL,PROP_2D");

            if (res == "")
                return props;

            string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            counter = 1;
            foreach (string p in pieces)
            {
                GSA2DProperty prop = new GSA2DProperty().AttachGSA(gsaObj);
                prop.ParseGWACommand(p, materials.ToArray());

                props.Add(prop);

                ChangeStatus("GETTING 2D PROPERTIES: " + counter.ToString() + "/" + pieces.Length.ToString(), counter * 100 / pieces.Length);
                counter++;
            }

            return props;
        }

        private List<GSAObject> GetNodes()
        {
            int counter;

            List<GSAObject> nodes = new List<GSAObject>();

            string res = gsaObj.GwaCommand("GET_ALL,NODE");

            if (res == "")
                return nodes;

            string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            counter = 1;
            foreach (string p in pieces)
            {
                GSANode n = new GSANode().AttachGSA(gsaObj);
                n.ParseGWACommand(p);

                nodes.Add(n);

                ChangeStatus("GETTING NODES: " + counter.ToString() + "/" + pieces.Length.ToString(), counter * 100 / pieces.Length );
                counter++;
            }

            res = gsaObj.GwaCommand("GET_ALL,EL");

            if (res == "")
                return nodes;

            pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            counter = 1;
            foreach (string p in pieces)
            {
                string[] pPieces = p.ListSplit(",");
                if (pPieces[4].ParseElementNumNodes() == 1)
                {
                    GSA0DElement e0D = new GSA0DElement().AttachGSA(gsaObj);
                    e0D.ParseGWACommand(p);
                    (nodes.Where(n => e0D.Connectivity.Contains(n.Reference)).First() as GSANode).Merge0DElement(e0D);
                    ChangeStatus("GETTING 0D ELEMENTS: " + counter++.ToString(), -1);
                }
            }

            return nodes;
        }
        
        private List<GSAObject> Get1DElements(List<GSANode> nodes)
        {
            int counter;

            List<GSAObject> elements = new List<GSAObject>();

            string res = gsaObj.GwaCommand("GET_ALL,EL");

            if (res == "")
                return elements;

            string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            counter = 1;
            foreach (string p in pieces)
            {
                string[] pPieces = p.ListSplit(",");
                if (pPieces[4].ParseElementNumNodes() == 2)
                {
                    GSA1DElement e1D = new GSA1DElement().AttachGSA(gsaObj);
                    e1D.ParseGWACommand(p, nodes.ToArray());
                    elements.Add(e1D);
                    ChangeStatus("GETTING 1D ELEMENTS: " + counter++.ToString(), -1);
                }
            }
            
            return elements;
        }

        private List<GSAObject> Get2DElements(List<GSANode> nodes)
        {
            int counter;

            List<GSAObject> elements = new List<GSAObject>();

            string res = gsaObj.GwaCommand("GET_ALL,EL");

            if (res == "")
                return elements;

            string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            counter = 1;
            foreach (string p in pieces)
            {
                string[] pPieces = p.ListSplit(",");
                int numConnectivity = pPieces[4].ParseElementNumNodes();
                if (pPieces[4].ParseElementNumNodes() >= 3 )
                {
                    GSA2DElement e2D = new GSA2DElement().AttachGSA(gsaObj);
                    e2D.ParseGWACommand(p, nodes.ToArray());

                    GSA2DElementMesh mesh = new GSA2DElementMesh();
                    mesh.Property = (e2D as GSA2DElement).Property;
                    mesh.InsertionPoint = (e2D as GSA2DElement).InsertionPoint;
                    mesh.AddElement(e2D as GSA2DElement);
                    elements.Add(mesh);

                    ChangeStatus("GETTING 2D ELEMENTS: " + counter++.ToString(), -1);
                }
            }
            
            for (int i = 0; i < elements.Count(); i++)
            {
                List<GSAObject> matches = elements.Where((m,j) => (elements[i] as GSA2DElementMesh).MeshMergeable(m as GSA2DElementMesh) & j != i).ToList();

                foreach(GSAObject m in matches)
                    (elements[i] as GSA2DElementMesh).MergeMesh(m as GSA2DElementMesh);

                foreach (GSAObject m in matches)
                    elements.Remove(m);

                ChangeStatus("MERGING 2D ELEMENTS: " + (i + 1).ToString() + "/" + elements.Count(), (i+1) * 100 / elements.Count());

                if (matches.Count() > 0) i--;
            }
            
            return elements;
        }
        
        private List<GSAObject> GetMembers(List<GSANode> nodes)
        {
            // PROBLEMS WITH GWA GET_ALL COMMAND FOR MEMBERS
            // NEED WORKAROUND
            return null;
        }
        
        #endregion

        #region Import GSA
        public async Task ImportObjects(Dictionary<string, string> streamIDs)
        {
            int progCounter;

            List<Task> taskList = new List<Task>();

            Dictionary<Type, object> objects = new Dictionary<Type, object>();
            List<object> convertedObjects = new List<object>();

            if (gsaObj == null)
            {
                MessageLog.AddError("GSA link not found.");
                return;
            }

            // Initialize object dictionary
            IEnumerable<Type> objTypes = typeof(GSAObject)
                .Assembly.GetTypes()
                .Where(t => t.IsSubclassOf(typeof(GSAObject)) && !t.IsAbstract);

            foreach (Type t in objTypes)
                objects[t] = new List<GSAObject>();

            // Pull objects from server asynchronously
            ChangeStatus("RECEIVING FROM SERVER", -1);

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
            foreach (object obj in convertedObjects)
            {
                if (obj == null) continue;

                UnpackGSAObj(obj as GSAObject, objects);
            }

            // Set Up Counter
            GSARefCounters counter = new GSARefCounters();
            foreach (KeyValuePair<Type, object> kvp in objects)
            {
                Type subType = kvp.Value.GetType().GetGenericArguments()[0];
                
                // Reserve reference
                counter.AddObjRefs(subType.ToString(),
                    (kvp.Value as IList).Cast<GSAObject>().Select(o => o.Reference).ToList());

                // Reserve connectivities
                counter.AddObjRefs(subType.ToString(),
                    (kvp.Value as IList).Cast<GSAObject>().SelectMany(e => e.Connectivity).ToList());
            }

            // Prepare objects (fix connectivity, add children, etc.)
            progCounter = 0;

            for (int i = 0; i < objects.Keys.Count(); i++)
            {
                Type key = objects.Keys.ElementAt(i);
                List<GSAObject> value = (objects.Values.ElementAt(i) as IList).Cast<GSAObject>().ToList();

                for (int j = 0; j < value.Count(); j++)
                { 
                    PrepareGSAObj(value[j], objects, ref counter);
                    ChangeStatus("PREPARNG OBJECTS: " + (j + progCounter).ToString() + "/" + counter.TotalObjects.ToString(), (j + progCounter) * 100 / counter.TotalObjects);
                }

                progCounter += value.Count;
            }

            // Write objects
            MessageLog.AddMessage("Writing objects.");

            progCounter = 1;
            foreach (KeyValuePair<Type, object> kvp in objects)
                foreach (GSAObject obj in (kvp.Value as IList).Cast<GSAObject>())
                { 
                    obj.WritetoGSA(objects);
                    ChangeStatus("WRITING OBJECTS: " + progCounter.ToString() + "/" + counter.TotalObjects.ToString(), progCounter * 100 / counter.TotalObjects);
                    progCounter++;
                }


            // Write derived objects (e.g., 0D elements from nodes)
            MessageLog.AddMessage("Writing derived objects.");

            progCounter = 1;
            foreach (KeyValuePair<Type, object> kvp in objects)
                foreach (GSAObject obj in (kvp.Value as IList).Cast<GSAObject>())
                { 
                    obj.WriteDerivedObjectstoGSA(objects);
                    ChangeStatus("WRITING DERIVED OBJECTS: " + progCounter.ToString() + "/" + counter.TotalObjects.ToString(), progCounter * 100 / counter.TotalObjects);
                    progCounter++;
                }

            gsaObj.UpdateViews();

            ChangeStatus("RECEIVING COMPLETE", 0);
            MessageLog.AddMessage("Receiving completed!");
        }

        private void UnpackGSAObj(GSAObject obj, Dictionary<Type, object> dict)
        {
            obj.AttachGSA(gsaObj);

            Type t = obj.GetType();

            if (t== typeof(GSA2DElementMesh))
            {
                List<GSAObject> elems = obj.GetChildren();
                foreach (GSAObject e in elems)
                    (dict[typeof(GSA2DElement)] as IList).Add(e.AttachGSA(gsaObj));
            }
            else
                (dict[t] as IList).Add(obj);
        }

        private int PrepareGSAObj(GSAObject obj, Dictionary<Type, object> dict, ref GSARefCounters counter)
        {
            obj.AttachGSA(gsaObj);

            if (obj.GetType() == typeof(GSANode))
                return PrepareNode(obj as GSANode, dict, ref counter);

            else if (obj.GetType() == typeof(GSA1DElement) | obj.GetType() == typeof(GSA2DElement))
                return PrepareElement(obj, dict, ref counter);

            else
                return PrepareGeneric(obj, dict, ref counter);
        }

        private int PrepareGeneric(GSAObject obj, Dictionary<Type, object> dict, ref GSARefCounters counter)
        {
            int index = (dict[obj.GetType()] as IList).IndexOf(obj);

            obj = counter.RefObject(obj);

            if (index != -1)
                (dict[obj.GetType()] as IList)[index] = obj;
            else
                (dict[obj.GetType()] as IList).Add(obj);

            return obj.Reference;
        }
        
        private int PrepareNode(GSANode node, Dictionary<Type, object> dict, ref GSARefCounters counter)
        {
            int index = (dict[typeof(GSANode)] as IList).IndexOf(node);

            if (node.Reference == 0)
            {
                List<GSANode> matches = (dict[typeof(GSANode)] as IList)
                    .Cast<GSANode>()
                    .Where(n => (n.Coor[0] == node.Coor[0]) &
                    (n.Coor[1] == node.Coor[1]) &
                    (n.Coor[2] == node.Coor[2])).ToList();

                if (matches.Count > 0)
                {
                    int mergeIndex = (dict[typeof(GSANode)] as IList).IndexOf(matches[0]);
                    ((dict[typeof(GSANode)] as IList)[mergeIndex] as GSANode).Merge(node);
                    return matches[0].Reference;
                }
            }
            else if ((dict[typeof(GSANode)] as IList).Cast<GSANode>().Where(n => n.Reference == node.Reference).Count() > 0)
            {
                int mergeIndex = (dict[typeof(GSANode)] as IList).IndexOf(
                    (dict[typeof(GSANode)] as IList).Cast<GSANode>()
                    .Where(n => n.Reference == node.Reference).First());
                ((dict[typeof(GSANode)] as IList)[mergeIndex] as GSANode).Merge(node);
                return node.Reference;

            }

            node = counter.RefObject(node) as GSANode;

            if (index != -1)
                (dict[typeof(GSANode)] as IList)[index] = node;
            else
                (dict[typeof(GSANode)] as IList).Add(node);

            return node.Reference;
        }
        
        private int PrepareElement(GSAObject element, Dictionary<Type, object> dict, ref GSARefCounters counter)
        {
            int index = (dict[element.GetType()] as IList).IndexOf(element);

            List<GSAObject> eNodes = element.GetChildren();

            if (element.Connectivity.Count() == 0)
                element.Connectivity = new List<int>(new int[eNodes.Count]);

            for (int i = 0; i < eNodes.Count(); i++)
                element.Connectivity[i] = PrepareGSAObj(eNodes[i], dict, ref counter);
            
            element = counter.RefObject(element);

            if (index != -1)
                (dict[element.GetType()] as IList)[index] = element;
            else
                (dict[element.GetType()] as IList).Add(element);

            return element.Reference;
        }
        #endregion
    }

    public class GSARefCounters
    {
        private Dictionary<string, int> counter;
        private Dictionary<string, List<int>> refsUsed;

        public int TotalObjects
        {
            get
            {
                int total = 0;

                foreach (KeyValuePair<string, List<int>> kvp in refsUsed)
                    total += kvp.Value.Count();

                return total;
            }
        }

        public GSARefCounters()
        {
            counter = new Dictionary<string, int>();
            refsUsed = new Dictionary<string, List<int>>();
        }

        public GSAObject RefObject(GSAObject obj)
        {
            string key = obj.GetType().ToString();

            if (obj.Reference == 0)
            {
                if (!counter.ContainsKey(key))
                    counter[key] = 1;

                while (refsUsed[key].Contains(counter[key]))
                    counter[key]++;

                obj.Reference = counter[key]++;
            }

            AddObjRefs(key, new List<int>() { obj.Reference });
            return obj;
        }
        
        public void AddObjRefs(string key, List<int> refs)
        {
            if (!refsUsed.ContainsKey(key))
                refsUsed[key] = refs;
            else
                refsUsed[key].AddRange(refs);

            refsUsed[key] = refsUsed[key].Distinct().ToList();
        }
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
