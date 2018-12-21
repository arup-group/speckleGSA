using System;
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
        public string SenderNodeStreamID { get
            {
                if (senders.ContainsKey("Nodes"))
                    return senders["Nodes"].StreamID;
                else
                    return "";
            }
        }
        public string SenderSectionStreamID { get { return ""; } }
        public string SenderElementStreamID {
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

            elements[0] = new List<GSAObject>();
            elements[1] = new List<GSAObject>();

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

            List<GSANode> nodes = GetNodes();
            Messages.AddMessage("Converted " + nodes.Count() + " nodes.");
            
            List<GSAObject>[] elements = GetElements(nodes);
            Messages.AddMessage("Converted " + elements[0].Count() + " 1D elements.");
            Messages.AddMessage("Converted " + elements[1].Count() + " 2D meshes.");

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
            
            bucketObjects.Remove("Nodes");

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
        #endregion

        #region Import GSA
        private void AddGSAObj(GSAObject obj, Dictionary<string, object> dict)
        {
            obj.AttachGSA(gsaObj);

            Type t = obj.GetType();

            if (t == typeof(GSANode))
                (dict["Nodes"] as List<GSANode>).Add(obj as GSANode);
                //(dict["Elements"] as List<GSAObject>).AddRange((obj as GSANode).Extract0DElement());
            else if (t == typeof(GSA1DElement))
                (dict["Elements"] as List<GSAObject>).Add(obj as GSAObject);
            else if (t == typeof(GSA2DElement))
                (dict["Elements"] as List<GSAObject>).Add(obj as GSAObject);
            else if (t == typeof(GSA2DElementMesh))
            {
                List<GSAObject> elems = obj.GetChildren();
                foreach (GSAObject e in elems)
                    (dict["Elements"] as List<GSAObject>).Add(e.AttachGSA(gsaObj));
            }
        }

        private int AddNode(GSANode node, Dictionary<string, object> dict, ref GSARefCounters counter)
        {
            int index = (dict["Nodes"] as List<GSANode>).IndexOf(node);

            if (node.Reference == 0)
            {
                List<GSANode> matches = (dict["Nodes"] as List<GSANode>)
                    .Where(n => (n.Coor[0] == node.Coor[0]) &
                    (n.Coor[1] == node.Coor[1]) &
                    (n.Coor[2] == node.Coor[2])).ToList();

                if (matches.Count > 0)
                {
                    int mergeIndex = (dict["Nodes"] as List<GSANode>).IndexOf(matches[0]);
                    (dict["Nodes"] as List<GSANode>)[mergeIndex].Merge(node);
                    return matches[0].Reference;
                }
            }
            else if ((dict["Nodes"] as List<GSANode>).Where(n => n.Reference == node.Reference).Count() > 0)
            {
                int mergeIndex = (dict["Nodes"] as List<GSANode>).IndexOf(
                    (dict["Nodes"] as List<GSANode>)
                    .Where(n => n.Reference == node.Reference).First());
                (dict["Nodes"] as List<GSANode>)[mergeIndex].Merge(node);
                return node.Reference;

            }
                    

            node = counter.RefNode(node);

            if (index != -1)
                (dict["Nodes"] as List<GSANode>)[index] = node;
            else
                (dict["Nodes"] as List<GSANode>).Add(node);

            Messages.AddMessage("Created new node " + node.Reference.ToString() + ".");

            return node.Reference;
        }
        
        private int AddElement(GSAObject element, Dictionary<string, object> dict, ref GSARefCounters counter)
        {
            int index = (dict["Elements"] as List<GSAObject>).IndexOf(element);

            List<GSAObject> eNodes = element.GetChildren();

            if (element.Connectivity.Count() == 0)
                element.Connectivity = new List<int>(new int[eNodes.Count]);

            for (int i = 0; i < eNodes.Count(); i++)
                element.Connectivity[i] = AddNode((eNodes[i] as GSANode).AttachGSA(gsaObj), dict, ref counter);
            
            element = counter.RefElement(element);

            if (index != -1)
                (dict["Elements"] as List<GSAObject>)[index] = element;
            else
                (dict["Elements"] as List<GSAObject>).Add(element);

            return element.Reference;
        }

        public async Task ImportObjects(Dictionary<string, string> streamIDs)
        {
            Dictionary<string, object> objects = new Dictionary<string, object>();
            List<object> convertedObjects = new List<object>();

            if (gsaObj == null)
            {
                Messages.AddError("GSA link not found.");
                return;
            }

            objects["Nodes"] = new List<GSANode>();
            objects["Elements"] = new List<GSAObject>();

            if (streamIDs["Nodes"] == "")
                Messages.AddMessage("No nodes stream specified.");
            else
            { 
                Messages.AddMessage("Creating .nodes receiver.");
                receivers["Nodes"] = new Receiver(userManager.ServerAddress, userManager.ApiToken);
                await receivers["Nodes"].InitializeReceiver(streamIDs["Nodes"]);

                if (receivers["Nodes"].StreamID == null || receivers["Nodes"].StreamID == "")
                {
                    Messages.AddError("Could not connect to .nodes stream.");
                }
                else
                {
                    Messages.AddMessage("Receiving nodes.");
                    try
                    {
                        await receivers["Nodes"].UpdateDataAsync().ContinueWith(res =>
                            convertedObjects.AddRange(res.Result)
                        );
                    }
                    catch (Exception e)
                    {
                        Messages.AddError(e.Message);
                    }
                }
            }

            if (streamIDs["Elements"] == "")
                Messages.AddMessage("No elements stream specified.");
            else
            {
                Messages.AddMessage("Creating .elements receiver.");
                receivers["Elements"] = new Receiver(userManager.ServerAddress, userManager.ApiToken);
                await receivers["Elements"].InitializeReceiver(streamIDs["Elements"]);

                if (receivers["Elements"].StreamID == null || receivers["Elements"].StreamID == "")
                {
                    Messages.AddError("Could not connect to .elements stream.");
                }
                else
                {
                    try
                    {
                        Messages.AddMessage("Receiving elements.");
                        await receivers["Elements"].UpdateDataAsync().ContinueWith(res =>
                            convertedObjects.AddRange(res.Result)
                        );
                    }
                    catch (Exception e)
                    {
                        Messages.AddError(e.Message);
                    }
                }
            }

            // Populate list
            foreach (object obj in convertedObjects)
            {
                if (obj == null) continue;

                Type t = obj.GetType();
                if (t.IsArray)
                    foreach (GSAObject arrObj in (obj as Array))
                        AddGSAObj(arrObj, objects);
                else
                    AddGSAObj(obj as GSAObject, objects);
            }

            // Set Up Counter
            GSARefCounters counter = new GSARefCounters();
            counter.AddNodeRefs((objects["Nodes"] as List<GSANode>).Select(n => n.Reference).ToList());
            counter.AddNodeRefs((objects["Elements"] as List<GSAObject>).SelectMany(e => e.Connectivity).ToList()); // Reserve connectivity nodes
            counter.AddElementRefs((objects["Elements"] as List<GSAObject>).Select(e => e.Reference).ToList());

            // Nodes
            for(int i = 0; i < (objects["Nodes"] as List<GSANode>).Count(); i++)
                AddNode((objects["Nodes"] as List<GSANode>)[i], objects, ref counter);

            // Elements
            for (int i = 0; i < (objects["Elements"] as List<GSAObject>).Count(); i++)
                AddElement((objects["Elements"] as List<GSAObject>)[i], objects, ref counter);

            Messages.AddMessage("Preparing to write " + counter.TotalObjects + " objects.");

            // Write objects
            foreach (GSANode n in objects["Nodes"] as List<GSANode>)
                n.WritetoGSA();

            foreach (GSAObject e in objects["Elements"] as List<GSAObject>)
                e.WritetoGSA();

            Messages.AddMessage("Preparing to derived objects.");

            // Write derived objects (e.g., section properties, 0D elements, etc.)
            foreach (GSANode n in objects["Nodes"] as List<GSANode>)
                n.WriteDerivedObjectstoGSA();

            foreach (GSAObject e in objects["Elements"] as List<GSAObject>)
                e.WriteDerivedObjectstoGSA();

            gsaObj.UpdateViews();

            Messages.AddMessage("Receiving completed!");
        }

        #endregion
    }

    public class GSARefCounters
    {
        private int nodeCounter;
        private int elementCounter;
        private int memberCounter;

        private List<int> nodeRefsUsed;
        private List<int> elementRefsUsed;
        private List<int> memberRefsUsed;

        public int TotalObjects
        {
            get
            {
                return nodeRefsUsed.Count() + elementRefsUsed.Count() + memberRefsUsed.Count();
            }
        }

        public GSARefCounters()
        {
            nodeCounter = 1;
            elementCounter = 1;
            memberCounter = 1;

            nodeRefsUsed = new List<int>();
            elementRefsUsed = new List<int>();
            memberRefsUsed = new List<int>();
        }

        public GSANode RefNode(GSANode node)
        {
            if (node.Reference > 0)
            {
                AddNodeRefs(new List<int> { node.Reference });
                return node;
            }
            while (nodeRefsUsed.Contains(nodeCounter))
                nodeCounter++;
            node.Reference = nodeCounter++;
            nodeRefsUsed.Add(node.Reference);
            return node;
        }

        public GSAObject RefElement(GSAObject element)
        {
            if (element.Reference > 0)
            {
                AddElementRefs(new List<int> { element.Reference });
                return element;
            }
            while (elementRefsUsed.Contains(elementCounter))
                elementCounter++;
            element.Reference = elementCounter++;
            elementRefsUsed.Add(element.Reference);
            return element;
        }

        public GSAMember RefMember(GSAMember member)
        {
            if (member.Reference > 0)
            {
                AddNemberRefs(new List<int> { member.Reference });
                return member;
            }
            while (memberRefsUsed.Contains(memberCounter))
                memberCounter++;
            member.Reference = memberCounter++;
            memberRefsUsed.Add(member.Reference);
            return member;
        }

        public void AddNodeRefs(List<int> refs)
        {
            nodeRefsUsed.AddRange(refs);
            nodeRefsUsed = nodeRefsUsed.Distinct().ToList();
        }

        public void AddElementRefs(List<int> refs)
        {
            elementRefsUsed.AddRange(refs);
            elementRefsUsed = elementRefsUsed.Distinct().ToList();
        }

        public void AddNemberRefs(List<int> refs)
        {
            memberRefsUsed.AddRange(refs);
            memberRefsUsed = memberRefsUsed.Distinct().ToList();
        }
    }
}
