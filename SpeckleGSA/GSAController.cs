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
        public double ReceiveNodeTolerance { get; set; }
        public string SenderNodeStreamID { get { return senders["Nodes"].StreamID; } }
        public string SenderSectionStreamID { get { return ""; } }
        public string SenderElementStreamID { get { return senders["Elements"].StreamID; } }

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
        public List<GSANode> GetNodes()
        {
            List<GSANode> nodes = new List<GSANode>();

            string res = gsaObj.GwaCommand("GET_ALL,NODE");

            if (res == "")
                return nodes;

            string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string p in pieces)
            {
                GSANode n = new GSANode();
                n.ParseGWACommand(p);

                nodes.Add(n);
            }

            return nodes;
        }

        public List<GSAElement> GetElements(List<GSANode> nodes)
        {
            List<GSAElement> elements = new List<GSAElement>();

            string res = gsaObj.GwaCommand("GET_ALL,EL");

            if (res == "")
                return elements;

            string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string p in pieces)
            {
                GSAElement e = new GSAElement();
                e.ParseGWACommand(p);

                e.SpeckleConnectivity = nodes
                    .Where(n => e.Connectivity.Contains(n.Ref))
                    .Select(n => n.SpeckleID)
                    .ToArray();

                e.Coor = nodes
                    .Where(n => e.Connectivity.Contains(n.Ref))
                    .SelectMany(n => n.Coor)
                    .ToArray();

                elements.Add(e);
            }

            return elements;
        }

        public List<GSALine> GetLines(List<GSANode> nodes)
        {
            List<GSALine> lines = new List<GSALine>();

            string res = gsaObj.GwaCommand("GET_ALL,LINE");

            if (res == "")
                return lines;

            string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string p in pieces)
            {
                GSALine l = new GSALine();
                l.ParseGWACommand(p);

                l.SpeckleConnectivity = nodes
                    .Where(n => l.Connectivity.Contains(n.Ref))
                    .Select(n => n.SpeckleID)
                    .ToArray();

                l.Coor = nodes
                    .Where(n => l.Connectivity.Contains(n.Ref))
                    .SelectMany(n => n.Coor)
                    .ToArray();

                lines.Add(l);
            }

            return lines;
        }

        public List<GSAMember> GetMembers(List<GSANode> nodes)
        {
            // PROBLEMS WITH GWA GET_ALL COMMAND FOR MEMBERS
            // NEED WORKAROUND
            return null;
        }

        public List<GSAArea> GetAreas(List<GSALine> lines)
        {
            List<GSAArea> areas = new List<GSAArea>();

            string res = gsaObj.GwaCommand("GET_ALL,AREA");

            if (res == "")
                return areas;

            string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string p in pieces)
            {
                GSAArea a = new GSAArea();
                a.ParseGWACommand(p);

                a.SpeckleConnectivity = lines
                    .Where(l => a.Connectivity.Contains(l.Ref))
                    .Select(l => l.SpeckleID)
                    .ToArray();

                List<List<double>> lineCoor = lines
                    .Where(l => a.Connectivity.Contains(l.Ref))
                    .Select(l => l.Coor.Take(6).ToList())
                    .ToList();

                for (int i = 0; i < lineCoor.Count; i++)
                {
                    if (i == 0)
                        lineCoor[i].RemoveRange(3, 3);
                    else if (i == lineCoor.Count - 1)
                    {
                        if ((lineCoor[0][0] == lineCoor[i][0] &
                            lineCoor[0][1] == lineCoor[i][1] &
                            lineCoor[0][2] == lineCoor[i][2]) ||
                            (lineCoor[i - 1][0] == lineCoor[i][0] &
                            lineCoor[i - 1][1] == lineCoor[i][1] &
                            lineCoor[i - 1][2] == lineCoor[i][2]))
                            lineCoor[i].RemoveRange(0, 3);
                        else
                            lineCoor[i].RemoveRange(3, 3);
                    }
                    else
                    {
                        if (lineCoor[i - 1][0] == lineCoor[i][0] &
                            lineCoor[i - 1][1] == lineCoor[i][1] &
                            lineCoor[i - 1][2] == lineCoor[i][2])
                            lineCoor[i].RemoveRange(0, 3);
                        else
                            lineCoor[i].RemoveRange(3, 3);
                    }
                }

                a.Coor = lineCoor.SelectMany(d => d).ToArray();

                areas.Add(a);
            }

            return areas;
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

            // Add nodes
            Messages.AddMessage("Sending .nodes stream.");

            List<GSANode> nodes = GetNodes();
            bucketObjects["Nodes"] = new List<object>();
            foreach (GSANode n in nodes)
                bucketObjects["Nodes"].Add(n.ToSpeckle());

            if (!senders.ContainsKey("Nodes"))
            {
                Messages.AddMessage(".nodes sender not initialized. Creating new .nodes sender.");
                senders["Nodes"] = new Sender(userManager.ServerAddress, userManager.ApiToken);
                await senders["Nodes"].InitializeSender();
            }

            Messages.AddMessage(".nodes sender streamID: " + senders["Nodes"].StreamID + ".");

            Messages.AddMessage("Nodes: " + bucketObjects["Nodes"].Count() + ".");

            objectIDs = await senders["Nodes"].UpdateDataAsync(modelName + ".nodes", bucketObjects);

            Messages.AddMessage("Streamed " + objectIDs.Count() + " objects.");

            nodes = UpdateObjectIDs(objectIDs["Nodes"], nodes.ToArray()).Select(n => n as GSANode).ToList();
            bucketObjects.Remove("Nodes");

            // Add elements
            Messages.AddMessage("Sending .elements stream.");

            List<GSALine> lines = GetLines(nodes);
            bucketObjects["Lines"] = new List<object>();
            foreach (GSALine l in lines)
                bucketObjects["Lines"].Add(l.ToSpeckle());

            List<GSAArea> areas = GetAreas(lines);
            bucketObjects["Areas"] = new List<object>();
            foreach (GSAArea a in areas)
                bucketObjects["Areas"].Add(a.ToSpeckle());

            List<GSAElement> elements = GetElements(nodes);
            bucketObjects["Elements"] = new List<object>();
            foreach (GSAElement e in elements)
                bucketObjects["Elements"].Add(e.ToSpeckle());

            if (!senders.ContainsKey("Elements"))
            {
                Messages.AddMessage(".elements sender not initialized. Creating new .elements sender.");
                senders["Elements"] = new Sender(userManager.ServerAddress, userManager.ApiToken);
                await senders["Elements"].InitializeSender();
            }

            Messages.AddMessage(".elements sender streamID: " + senders["Elements"].StreamID + ".");

            Messages.AddMessage("Lines: " + bucketObjects["Lines"].Count() + ".");
            Messages.AddMessage("Areas: " + bucketObjects["Areas"].Count() + ".");
            Messages.AddMessage("Elements: " + bucketObjects["Elements"].Count() + ".");

            objectIDs = await senders["Elements"].UpdateDataAsync(modelName + ".elements", bucketObjects);

            Messages.AddMessage("Streamed " + objectIDs.Count() + " objects.");

            lines = UpdateObjectIDs(objectIDs["Lines"], lines.ToArray()).Select(n => n as GSALine).ToList();
            areas = UpdateObjectIDs(objectIDs["Areas"], areas.ToArray()).Select(n => n as GSAArea).ToList();
            elements = UpdateObjectIDs(objectIDs["Elements"], elements.ToArray()).Select(n => n as GSAElement).ToList();

            Messages.AddMessage("Sending completed!");
        }
        #endregion

        #region Import GSA

        public void AddGSAObj(GSAObject obj, ref Dictionary<string, object> dict)
        {
            Type t = obj.GetType();
            Console.WriteLine(t);
            PropertyInfo p = t.GetProperty("GSAEntity");
            string type = p.GetValue(obj, null).ToString();

            switch (type)
            {
                case "NODE":
                    (dict["Nodes"] as List<GSANode>).Add(obj as GSANode);
                    break;
                case "LINE":
                    (dict["Lines"] as List<GSALine>).Add(obj as GSALine);
                    break;
                case "AREA":
                    (dict["Areas"] as List<GSAArea>).Add(obj as GSAArea);
                    break;
                case "ELEMENT":
                    (dict["Elements"] as List<GSAElement>).Add(obj as GSAElement);
                    break;
                default:
                    break;
            }
        }

        public int AddNode(GSANode node, ref Dictionary<string, object> dict, ref GSARefCounters counter)
        {
            if (node.SpeckleID == "")
            {
                // Node generated by SpeckleGSA containing only coor
                List<GSANode> matches = (dict["Nodes"] as List<GSANode>)
                    .Where(n => (Math.Pow(n.Coor[0] - node.Coor[0], 2) +
                    Math.Pow(n.Coor[1] - node.Coor[1], 2) +
                    Math.Pow(n.Coor[2] - node.Coor[2], 2) <=
                    ReceiveNodeTolerance)
                    ).ToList();

                if (matches.Count == 0)
                {
                    node = counter.RefNode(node);

                    gsaObj.GwaCommand(node.GetGWACommand());
                    (dict["Nodes"] as List<GSANode>).Add(node);

                    Messages.AddMessage("Created new node " + node.Ref.ToString() + ".");

                    return node.Ref;
                }
                else
                    return matches[0].Ref;
            }
            else
            {
                // Node from SpeckleServer
                node = counter.RefNode(node);

                gsaObj.GwaCommand(node.GetGWACommand());
                return node.Ref;
            }
        }

        public int AddLine(GSALine line, ref Dictionary<string, object> dict, ref GSARefCounters counter)
        {
            if (line.SpeckleConnectivity.Length > 0)
            {
                // Add based on connectivity

                line = counter.RefLine(line);

                int[] connectivity = new int[line.SpeckleConnectivity.Length];

                for (int i = 0; i < line.SpeckleConnectivity.Length; i++)
                    connectivity[i] = (dict["Nodes"] as List<GSANode>)
                        .Where(n => n.SpeckleID == line.SpeckleConnectivity[i])
                        .Select(n => n.Ref).FirstOrDefault();

                line.Connectivity = connectivity;

                gsaObj.GwaCommand(line.GetGWACommand());
                return line.Ref;
            }
            else
            {
                // Line generated by SpeckleGSA containing only coor
                List<GSALine> matches = (dict["Lines"] as List<GSALine>).Where(
                            l => l.Type == line.Type &
                            ((l.Coor[0] == line.Coor[0] &
                            l.Coor[1] == line.Coor[1] &
                            l.Coor[2] == line.Coor[2] &
                            l.Coor[3] == line.Coor[3] &
                            l.Coor[4] == line.Coor[4] &
                            l.Coor[5] == line.Coor[5]) ||
                            (l.Coor[0] == line.Coor[3] &
                            l.Coor[1] == line.Coor[4] &
                            l.Coor[2] == line.Coor[5] &
                            l.Coor[3] == line.Coor[0] &
                            l.Coor[4] == line.Coor[1] &
                            l.Coor[5] == line.Coor[2]))).ToList();

                if (matches.Count == 0)
                {
                    line = counter.RefLine(line);

                    List<GSAObject> lNodes = line.GetChildren();
                    for (int i = 0; i < lNodes.Count; i++)
                        line.Connectivity[i] = AddNode((lNodes[i] as GSANode), ref dict, ref counter);

                    gsaObj.GwaCommand(line.GetGWACommand());
                    (dict["Lines"] as List<GSALine>).Add(line);

                    Messages.AddMessage("Created new line " + line.Ref.ToString() + ".");

                    return line.Ref;
                }
                else
                    return matches[0].Ref;
            }
        }

        public int AddArea(GSAArea area, ref Dictionary<string, object> dict, ref GSARefCounters counter)
        {
            if (area.SpeckleConnectivity.Length > 0)
            {
                // Add based on connectivity

                area = counter.RefArea(area);

                int[] connectivity = new int[area.SpeckleConnectivity.Length];

                for (int i = 0; i < area.SpeckleConnectivity.Length; i++)
                    connectivity[i] = (dict["Lines"] as List<GSALine>)
                        .Where(l => l.SpeckleID == area.SpeckleConnectivity[i])
                        .Select(l => l.Ref).FirstOrDefault();

                area.Connectivity = connectivity;

                gsaObj.GwaCommand(area.GetGWACommand());
                return area.Ref;
            }
            else
            {
                // Area generated by SpeckleGSA containing only coor
                // No clash or overlap check
                area = counter.RefArea(area);

                List<GSAObject> aLines = area.GetChildren();
                for (int i = 0; i < aLines.Count; i++)
                    area.Connectivity[i] = AddLine((aLines[i] as GSALine), ref dict, ref counter);

                gsaObj.GwaCommand(area.GetGWACommand());

                Messages.AddMessage("Created new area " + area.Ref.ToString() + ".");

                return area.Ref;
            }
        }

        public int AddElement(GSAElement element, ref Dictionary<string, object> dict, ref GSARefCounters counter)
        {
            element = counter.RefElement(element);

            if (element.SpeckleConnectivity.Length == 0)
            {
                // this is a bad element definition
                Messages.AddError("Bad connectivity with element " + element.SpeckleID + ".");
                return 0;
            }

            int[] connectivity = new int[element.SpeckleConnectivity.Length];

            for (int i = 0; i < element.SpeckleConnectivity.Length; i++)
                connectivity[i] = (dict["Nodes"] as List<GSANode>)
                    .Where(n => n.SpeckleID == element.SpeckleConnectivity[i])
                    .Select(n => n.Ref).FirstOrDefault();

            element.Connectivity = connectivity;

            gsaObj.GwaCommand(element.GetGWACommand());
            return element.Ref;
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
            objects["Lines"] = new List<GSALine>();
            objects["Areas"] = new List<GSAArea>();
            objects["Elements"] = new List<GSAElement>();

            Messages.AddMessage("Creating .nodes receiver.");
            receivers["Nodes"] = new Receiver(userManager.ServerAddress, userManager.ApiToken);
            await receivers["Nodes"].InitializeReceiver(streamIDs["Nodes"]);

            if (receivers["Nodes"].StreamID == null)
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

            Messages.AddMessage("Creating .elements receiver.");
            receivers["Elements"] = new Receiver(userManager.ServerAddress, userManager.ApiToken);
            await receivers["Elements"].InitializeReceiver(streamIDs["Elements"]);

            if (receivers["Elements"].StreamID == null)
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

            // Populate list
            foreach (object obj in convertedObjects)
            {
                if (obj == null) continue;

                Type t = obj.GetType();
                if (t.IsArray)
                    foreach (GSAObject arrObj in (obj as Array))
                        AddGSAObj(arrObj, ref objects);
                else
                    AddGSAObj(obj as GSAObject, ref objects);
            }

            GSARefCounters counter = new GSARefCounters();

            // Nodes
            foreach (GSANode n in objects["Nodes"] as List<GSANode>)
                AddNode(n, ref objects, ref counter);

            // Lines 
            foreach (GSALine l in objects["Lines"] as List<GSALine>)
                AddLine(l, ref objects, ref counter);

            // Areas
            foreach (GSAArea a in objects["Areas"] as List<GSAArea>)
                AddArea(a, ref objects, ref counter);

            // Elements
            foreach (GSAElement e in objects["Elements"] as List<GSAElement>)
                AddElement(e, ref objects, ref counter);

            Messages.AddMessage("Streamed " + counter.TotalObjects + " objects.");

            gsaObj.UpdateViews();

            Messages.AddMessage("Receiving completed!");
        }

        #endregion

        #region Update GSA
        public GSAObject[] UpdateObjectIDs(string[] keys, GSAObject[] objects)
        {
            if (keys.Length == 0) return objects;
            if (objects.Length == 0) return objects;

            string[] newIDs = keys.Where(k => !objects.Select(g => g.SpeckleID).ToList().Contains(k)).ToArray();

            int counter = 0;
            foreach (GSAObject o in objects)
            {
                if (o.SpeckleID == null | o.SpeckleID == "")
                {
                    o.SpeckleID = newIDs[counter++];
                    o.Name = o.SpeckleID;
                    gsaObj.GwaCommand(o.GetGWACommand());
                    Messages.AddMessage("Updated " + o.GSAEntity + " " + o.Ref + " with ID " + o.SpeckleID + ".");
                }
                if (counter >= newIDs.Length) break;
            }

            return objects;
        }
        #endregion
    }

    public class GSARefCounters
    {
        private int nodeCounter;
        private int elementCounter;
        private int lineCounter;
        private int memberCounter;
        private int areaCounter;
        private int regionCounter;

        public int TotalObjects
        {
            get
            {
                return nodeCounter + elementCounter + lineCounter + memberCounter + areaCounter + regionCounter;
            }
        }

        public GSARefCounters()
        {
            nodeCounter = 1;
            elementCounter = 1;
            lineCounter = 1;
            memberCounter = 1;
            areaCounter = 1;
            regionCounter = 1;
        }

        public GSANode RefNode(GSANode node)
        {
            node.Ref = nodeCounter++;
            return node;
        }

        public GSAElement RefElement(GSAElement element)
        {
            element.Ref = elementCounter++;
            return element;
        }

        public GSALine RefLine(GSALine line)
        {
            line.Ref = lineCounter++;
            return line;
        }

        public GSAMember RefMember(GSAMember member)
        {
            member.Ref = memberCounter++;
            return member;
        }

        public GSAArea RefArea(GSAArea area)
        {
            area.Ref = areaCounter++;
            return area;
        }

        public GSARegion RefRegion(GSARegion region)
        {
            region.Ref = regionCounter++;
            return region;
        }
    }
}
