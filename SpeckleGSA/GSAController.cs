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
        public string SenderNodesStreamID { get
            {
                if (senders.ContainsKey("Nodes"))
                    return senders["Nodes"].StreamID;
                else
                    return "";
            }
        }
        public string SenderPropertiesStreamID {
            get
            {
                if (senders.ContainsKey("Properties"))
                    return senders["Properties"].StreamID;
                else
                    return "";
            }
        }
        public string SenderElementsStreamID {
            get
            {
                if (senders.ContainsKey("Elements"))
                    return senders["Elements"].StreamID;
                else
                    return "";
            }
        }

        public MessageLog Messages;

        private StreamManager streamManager;
        private UserManager userManager;
        private Dictionary<string, Sender> senders;
        private Dictionary<string, Receiver> receivers;
        private ComAuto gsaObj;

        public GSAController()
        {
            userManager = null;

            senders = new Dictionary<string, Sender>();
            receivers = new Dictionary<string, Receiver>();

            Messages = new MessageLog();
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
                    Messages.AddMessage("Successfully logged in");
                else
                    Messages.AddError("Failed to login");

                streamManager = new StreamManager(userManager.ServerAddress, userManager.ApiToken);
            });
        }

        public async Task<List<Tuple<string, string>>> GetStreamList()
        {
            if (userManager == null | streamManager == null)
            {
                Messages.AddError("Not logged in");
                return null;
            }

            try
            {
                Messages.AddMessage("Fetching stream list.");
                var response = await streamManager.GetStreams();
                Messages.AddMessage("Finished fetching stream list.");
                return response;
            }
            catch (Exception e)
            {
                Messages.AddError(e.Message);
                return null;
            }
        }

        public async Task CloneModelStreams()
        {
            if (userManager == null | streamManager == null)
            {
                Messages.AddError("Not logged in");
                return;
            }
            
            foreach (KeyValuePair<string, Sender> kvp in senders)
            {
                streamManager.CloneStream(kvp.Value.StreamID).ContinueWith(res => Messages.AddMessage("Cloned " + kvp.Key + " stream to ID : " + res.Result));
            }
        }
        #endregion

        #region GSA
        public async Task Link()
        {
            if (userManager == null)
            {
                Messages.AddError("Not logged in");
                return;
            }

            await Task.Run(delegate { gsaObj = new ComAuto(); }).ContinueWith(
                delegate
                {
                    gsaObj.DisplayGsaWindow(true);
                    Messages.AddMessage("GSA link established");
                }
            );
        }

        public async Task NewFile()
        {
            if (gsaObj == null)
            {
                Messages.AddError("No GSA link found");
                return;
            }

            await Task.Run(delegate { gsaObj.NewFile(); }).ContinueWith(
                delegate
                {
                    gsaObj.DisplayGsaWindow(true);
                    Messages.AddMessage("New file created");
                }
            );
        }

        public async Task OpenFile(string path)
        {
            if (gsaObj == null)
            {
                Messages.AddError("No GSA link found");
                return;
            }

            await Task.Run(delegate { gsaObj.Open(path); }).ContinueWith(
                delegate
                {
                    gsaObj.DisplayGsaWindow(true);
                    Messages.AddMessage("Opened " + path);
                }
            );
        }
        #endregion

        #region Extract GSA
        public async Task ExportObjects(string modelName)
        {
            Dictionary<string, List<object>> bucketObjects = new Dictionary<string, List<object>>();
            Dictionary<string, string[]> objectIDs;

            if (gsaObj == null)
            {
                Messages.AddError("GSA link not found.");
                return;
            }

            Messages.AddMessage("Converting objects.");

            List<GSAMaterial> materials = GetMaterials();
            Messages.AddMessage("Converted " + materials.Count() + " materials");

            List<GSA2DProperty> props2D = Get2DProperties(materials);
            Messages.AddMessage("Converted " + props2D.Count() + " 2D properties");

            List<GSANode> nodes = GetNodes();
            Messages.AddMessage("Converted " + nodes.Count() + " nodes.");

            List<GSAObject>[] elements = GetElements(nodes);
            Messages.AddMessage("Converted " + elements[0].Count() + " 1D elements.");
            Messages.AddMessage("Converted " + elements[1].Count() + " 2D meshes.");

            // Send properties
            Messages.AddMessage("Sending .properties stream.");

            if (!senders.ContainsKey("Properties"))
            {
                Messages.AddMessage(".properties sender not initialized. Creating new .properties sender.");
                senders["Properties"] = new Sender(userManager.ServerAddress, userManager.ApiToken);
                await senders["Properties"].InitializeSender();
            }

            Messages.AddMessage(".properties sender streamID: " + senders["Properties"].StreamID + ".");

            bucketObjects["Materials"] = new List<object>();
            foreach (GSAMaterial m in materials)
                bucketObjects["Materials"].Add(m.ToSpeckle());

            bucketObjects["2D Properties"] = new List<object>();
            foreach (GSA2DProperty p in props2D)
                bucketObjects["2D Properties"].Add(p.ToSpeckle());

            Messages.AddMessage("Materials: " + bucketObjects["Materials"].Count() + ".");
            Messages.AddMessage("2D Properties: " + bucketObjects["2D Properties"].Count() + ".");

            objectIDs = await senders["Properties"].UpdateDataAsync(modelName + ".properties", bucketObjects);

            Messages.AddMessage("Streamed " + objectIDs.Count() + " objects.");

            bucketObjects.Clear();

            // Send nodes
            Messages.AddMessage("Sending .nodes stream.");

            if (!senders.ContainsKey("Nodes"))
            {
                Messages.AddMessage(".nodes sender not initialized. Creating new .nodes sender.");
                senders["Nodes"] = new Sender(userManager.ServerAddress, userManager.ApiToken);
                await senders["Nodes"].InitializeSender();
            }

            Messages.AddMessage(".nodes sender streamID: " + senders["Nodes"].StreamID + ".");

            bucketObjects["Nodes"] = new List<object>();
            foreach (GSANode n in nodes)
                bucketObjects["Nodes"].Add(n.ToSpeckle());

            Messages.AddMessage("Nodes: " + bucketObjects["Nodes"].Count() + ".");

            objectIDs = await senders["Nodes"].UpdateDataAsync(modelName + ".nodes", bucketObjects);

            Messages.AddMessage("Streamed " + objectIDs.Count() + " objects.");

            bucketObjects.Clear();

            // Send .elements
            Messages.AddMessage("Sending .elements stream.");

            if (!senders.ContainsKey("Elements"))
            {
                Messages.AddMessage(".elements sender not initialized. Creating new .elements sender.");
                senders["Elements"] = new Sender(userManager.ServerAddress, userManager.ApiToken);
                await senders["Elements"].InitializeSender();
            }

            Messages.AddMessage(".elements sender streamID: " + senders["Elements"].StreamID + ".");

            bucketObjects["Elements - 1D"] = new List<object>();
            foreach (GSAObject e1D in elements[0])
                bucketObjects["Elements - 1D"].Add((e1D as GSA1DElement).ToSpeckle());

            bucketObjects["Elements - 2D Mesh"] = new List<object>();
            foreach (GSAObject eMesh in elements[1])
                bucketObjects["Elements - 2D Mesh"].Add((eMesh as GSA2DElementMesh).ToSpeckle());

            Messages.AddMessage("Elements - 1D: " + bucketObjects["Elements - 1D"].Count() + ".");
            Messages.AddMessage("Elements - 2D Mesh: " + bucketObjects["Elements - 2D Mesh"].Count() + ".");

            objectIDs = await senders["Elements"].UpdateDataAsync(modelName + ".elements", bucketObjects);

            Messages.AddMessage("Streamed " + objectIDs.Count() + " objects.");

            Messages.AddMessage("Sending completed!");
        }

        private List<GSAMaterial> GetMaterials()
        {
            string[] materialIdentifier = new string[]
                { "MAT_STEEL", "MAT_CONCRETE" };

            List<GSAMaterial> materials = new List<GSAMaterial>();

            List<string> pieces = new List<string>();
            foreach (string id in materialIdentifier)
            {
                string res = gsaObj.GwaCommand("GET_ALL," + id);

                if (res == "")
                    continue;

                pieces.AddRange(res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries));
            }
            pieces = pieces.Distinct().ToList();

            for (int i = 0; i < pieces.Count(); i++)
            {
                GSAMaterial mat = new GSAMaterial().AttachGSA(gsaObj);
                mat.ParseGWACommand(pieces[i]);
                mat.Reference = i+1; // Offset references
                materials.Add(mat);
            }

            return materials;
        }

        private List<GSA2DProperty> Get2DProperties(List<GSAMaterial> materials)
        {
            List<GSA2DProperty> props = new List<GSA2DProperty>();

            string res = gsaObj.GwaCommand("GET_ALL,PROP_2D");

            if (res == "")
                return props;

            string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string p in pieces)
            {
                GSA2DProperty prop = new GSA2DProperty().AttachGSA(gsaObj);
                prop.ParseGWACommand(p, materials.ToArray());

                props.Add(prop);
            }

            return props;
        }

        private List<GSANode> GetNodes()
        {
            List<GSANode> nodes = new List<GSANode>();

            string res = gsaObj.GwaCommand("GET_ALL,NODE");

            if (res == "")
                return nodes;

            string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string p in pieces)
            {
                GSANode n = new GSANode().AttachGSA(gsaObj);
                n.ParseGWACommand(p);

                nodes.Add(n);
            }

            return nodes;
        }

        private List<GSAObject>[] GetElements(List<GSANode> nodes)
        {
            List<GSAObject>[] elements = new List<GSAObject>[2];

            elements[0] = new List<GSAObject>(); // 1D elements
            elements[1] = new List<GSAObject>(); // 2D elements

            string res = gsaObj.GwaCommand("GET_ALL,EL");

            if (res == "")
                return elements;

            string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            int counter = 0;
            foreach (string p in pieces)
            {
                string[] pPieces = p.ListSplit(",");
                int numConnectivity = pPieces[4].ParseElementNumNodes();
                switch(numConnectivity)
                {
                    case 1:
                        GSA0DElement e0D = new GSA0DElement().AttachGSA(gsaObj);
                        e0D.ParseGWACommand(p);
                        nodes.Where(n => e0D.Connectivity.Contains(n.Reference)).First().Merge0DElement(e0D);
                        break;
                    case 2:
                        GSA1DElement e1D = new GSA1DElement().AttachGSA(gsaObj);
                        e1D.ParseGWACommand(p, nodes.ToArray());
                        elements[0].Add(e1D);
                        break;
                    default:
                        GSA2DElement e2D = new GSA2DElement().AttachGSA(gsaObj);
                        e2D.ParseGWACommand(p, nodes.ToArray());

                        GSA2DElementMesh mesh = new GSA2DElementMesh();
                        mesh.Property = (e2D as GSA2DElement).Property;
                        mesh.InsertionPoint = (e2D as GSA2DElement).InsertionPoint;
                        mesh.AddElement(e2D as GSA2DElement);
                        elements[1].Add(mesh);
                        break;
                }
                Console.WriteLine("E:" + counter++.ToString() + "/" + pieces.Count());
            }
            
            for (int i = 0; i < elements[1].Count(); i++)
            {
                List<GSAObject> matches = elements[1].Where((m,j) => (elements[1][i] as GSA2DElementMesh).MeshMergeable(m as GSA2DElementMesh) & j != i).ToList();

                foreach(GSAObject m in matches)
                    (elements[1][i] as GSA2DElementMesh).MergeMesh(m as GSA2DElementMesh);

                foreach (GSAObject m in matches)
                    elements[1].Remove(m);
                
                Console.WriteLine("MESH:" + (i+1).ToString() + "/" + elements[1].Count());

                if (matches.Count() > 0) i--;
            }
            
            return elements;
        }
        
        private List<GSAMember> GetMembers(List<GSANode> nodes)
        {
            // PROBLEMS WITH GWA GET_ALL COMMAND FOR MEMBERS
            // NEED WORKAROUND
            return null;
        }
        
        #endregion

        #region Import GSA
        public async Task ImportObjects(Dictionary<string, string> streamIDs)
        {
            Dictionary<Type, object> objects = new Dictionary<Type, object>();
            List<object> convertedObjects = new List<object>();

            if (gsaObj == null)
            {
                Messages.AddError("GSA link not found.");
                return;
            }

            // TODO: AUTOMATE THIS INSTEAD OF HARDCODE
            IEnumerable<Type> objTypes = typeof(GSAObject)
                .Assembly.GetTypes()
                .Where(t => t.IsSubclassOf(typeof(GSAObject)) && !t.IsAbstract);

            foreach (Type t in objTypes)
                objects[t] = new List<GSAObject>();

            // Pull objects from server
            foreach (KeyValuePair<string, string> kvp in streamIDs)
            {
                if (kvp.Value == "")
                    Messages.AddMessage("No " + kvp.Key + " stream specified.");
                else
                {
                    Messages.AddMessage("Creating " + kvp.Key + " receiver.");
                    receivers[kvp.Key] = new Receiver(userManager.ServerAddress, userManager.ApiToken);
                    await receivers[kvp.Key].InitializeReceiver(kvp.Value);

                    if (receivers[kvp.Key].StreamID == null || receivers[kvp.Key].StreamID == "")
                        Messages.AddError("Could not connect to " + kvp.Key + " stream.");
                    else
                    {
                        Messages.AddMessage("Receiving " + kvp.Key + ".");
                        try
                        {
                            convertedObjects.AddRange(receivers[kvp.Key].GetGSAObjects());
                        }
                        catch (Exception e)
                        {
                            Messages.AddError(e.Message);
                        }
                    }
                }
            }

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
            for (int i = 0; i < objects.Keys.Count(); i++)
            {
                Type key = objects.Keys.ElementAt(i);
                List<GSAObject> value = (objects.Values.ElementAt(i) as IList).Cast<GSAObject>().ToList();

                for (int j = 0; j < value.Count(); j++)
                    PrepareGSAObj(value[j], objects, ref counter);
            }

            // Write objects
            foreach (KeyValuePair<Type, object> kvp in objects)
                foreach (GSAObject obj in (kvp.Value as IList).Cast<GSAObject>())
                    obj.WritetoGSA(objects);

            Messages.AddMessage("Preparing to write derived objects.");

            // Write derived objects (e.g., 0D elements from nodes)
            foreach (KeyValuePair<Type, object> kvp in objects)
                foreach (GSAObject obj in (kvp.Value as IList).Cast<GSAObject>())
                    obj.WriteDerivedObjectstoGSA(objects);

            gsaObj.UpdateViews();

            Messages.AddMessage("Receiving completed!");
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
}
