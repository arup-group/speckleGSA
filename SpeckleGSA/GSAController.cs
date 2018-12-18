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

            foreach (string p in pieces)
            {
                string[] pPieces = p.ListSplit(",");
                int numConnectivity = pPieces[4].ParseElementNumNodes();
                switch(numConnectivity)
                {
                    case 1:
                        GSAElement e0D = new GSAElement().AttachGSA(gsaObj);
                        e0D.ParseGWACommand(p);
                        nodes.Where(n => e0D.Connectivity.Contains(n.Reference)).First().Merge0DElement(e0D);
                        break;
                    case 2:
                        GSA1DElement e1D = new GSA1DElement().AttachGSA(gsaObj);
                        e1D.ParseGWACommand(p);
                        e1D.Coor = nodes
                            .Where(n => e1D.Connectivity.Contains(n.Reference))
                            .SelectMany(n => n.Coor)
                            .ToArray();
                        elements[0].Add(e1D);
                        break;
                    default:
                        GSAElement e2D = new GSAElement().AttachGSA(gsaObj);
                        e2D.ParseGWACommand(p);
                        e2D.Coor = nodes
                            .Where(n => e2D.Connectivity.Contains(n.Reference))
                            .SelectMany(n => n.Coor)
                            .ToArray();
                        elements[1].Add(e2D);
                        break;
                }
            }

            return elements;
        }

        private List<GSALine> GetLines(List<GSANode> nodes)
        {
            List<GSALine> lines = new List<GSALine>();

            string res = gsaObj.GwaCommand("GET_ALL,LINE");

            if (res == "")
                return lines;

            string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string p in pieces)
            {
                GSALine l = new GSALine().AttachGSA(gsaObj);
                l.ParseGWACommand(p);

                l.Coor = nodes
                    .Where(n => l.Connectivity.Contains(n.Reference))
                    .SelectMany(n => n.Coor)
                    .ToArray();

                lines.Add(l);
            }

            return lines;
        }

        private List<GSAMember> GetMembers(List<GSANode> nodes)
        {
            // PROBLEMS WITH GWA GET_ALL COMMAND FOR MEMBERS
            // NEED WORKAROUND
            return null;
        }

        private List<GSAArea> GetAreas(List<GSALine> lines)
        {
            List<GSAArea> areas = new List<GSAArea>();

            string res = gsaObj.GwaCommand("GET_ALL,AREA");

            if (res == "")
                return areas;

            string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string p in pieces)
            {
                GSAArea a = new GSAArea().AttachGSA(gsaObj);
                a.ParseGWACommand(p);

                List<List<double>> lineCoor = lines
                    .Where(l => a.Connectivity.Contains(l.Reference))
                    .Select(l => l.Coor.Take(6).ToList())
                    .ToList();

                // Culls unique list of boundary points for areas
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

            Messages.AddMessage("Converting objects.");

            List<GSANode> nodes = GetNodes();
            List<GSALine> lines = GetLines(nodes);
            List<GSAArea> areas = GetAreas(lines);
            List<GSAObject>[] elements = GetElements(nodes);
            
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

            bucketObjects["Lines"] = new List<object>();
            foreach (GSALine l in lines)
                bucketObjects["Lines"].Add(l.ToSpeckle());

            bucketObjects["Areas"] = new List<object>();
            foreach (GSAArea a in areas)
                bucketObjects["Areas"].Add(a.ToSpeckle());

            bucketObjects["Elements - 1D"] = new List<object>();
            foreach (GSAObject e1D in elements[0])
                bucketObjects["Elements - 1D"].Add((e1D as GSA1DElement).ToSpeckle());

            bucketObjects["Elements - 2D"] = new List<object>();
            foreach (GSAObject e2D in elements[1])
                bucketObjects["Elements - 2D"].Add((e2D as GSAElement).ToSpeckle());

            Messages.AddMessage("Lines: " + bucketObjects["Lines"].Count() + ".");
            Messages.AddMessage("Areas: " + bucketObjects["Areas"].Count() + ".");
            Messages.AddMessage("Elements - 1D: " + bucketObjects["Elements - 1D"].Count() + ".");
            Messages.AddMessage("Elements - 2D: " + bucketObjects["Elements - 2D"].Count() + ".");

            objectIDs = await senders["Elements"].UpdateDataAsync(modelName + ".elements", bucketObjects);

            Messages.AddMessage("Streamed " + objectIDs.Count() + " objects.");

            Messages.AddMessage("Sending completed!");
        }
        #endregion

        #region Import GSA

        private void AddGSAObj(GSAObject obj, ref Dictionary<string, object> dict)
        {
            obj.AttachGSA(gsaObj);

            Type t = obj.GetType();
            PropertyInfo p = t.GetProperty("GSAEntity");
            string type = p.GetValue(obj, null).ToString();

            switch (type)
            {
                case "NODE":
                    (dict["Nodes"] as List<GSANode>).Add(obj as GSANode);
                    (dict["Elements"] as List<GSAObject>).AddRange((obj as GSANode).Extract0DElement());
                    break;
                case "LINE":
                    (dict["Lines"] as List<GSALine>).Add(obj as GSALine);
                    break;
                case "AREA":
                    (dict["Areas"] as List<GSAArea>).Add(obj as GSAArea);
                    break;
                case "ELEMENT":
                    (dict["Elements"] as List<GSAObject>).Add(obj as GSAObject);
                    break;
                default:
                    break;
            }
        }

        private int AddNode(GSANode node, ref Dictionary<string, object> dict, ref GSARefCounters counter, bool addToDict)
        {
            if (node.Reference == 0)
            {
                List<GSANode> matches = (dict["Nodes"] as List<GSANode>)
                    .Where(n => (Math.Pow(n.Coor[0] - node.Coor[0], 2) +
                    Math.Pow(n.Coor[1] - node.Coor[1], 2) +
                    Math.Pow(n.Coor[2] - node.Coor[2], 2) <=
                    0)
                    ).ToList();

                if (matches.Count == 0)
                {
                    node = counter.RefNode(node);
                    if (addToDict)
                        (dict["Nodes"] as List<GSANode>).Add(node);

                    gsaObj.GwaCommand(node.GetGWACommand());

                    Messages.AddMessage("Created new node " + node.Reference.ToString() + ".");

                    return node.Reference;
                }
                else
                {
                    //(dict["Nodes"] as List<GSANode>).Where(n => n.Reference == matches[0].Reference).First().Merge(node);
                    return matches[0].Reference;
                }
            }

            else
            {
                node = counter.RefNode(node);

                gsaObj.GwaCommand(node.GetGWACommand());

                return node.Reference;
            }
        }

        private int AddLine(GSALine line, ref Dictionary<string, object> dict, ref GSARefCounters counter, bool addToDict)
        {
            if (line.Reference == 0)
            {
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
                    List<GSAObject> lNodes = line.GetChildren();
                    line.Connectivity = new int[lNodes.Count];
                    for (int i = 0; i < lNodes.Count; i++)
                        line.Connectivity[i] = AddNode((lNodes[i] as GSANode).AttachGSA(gsaObj), ref dict, ref counter, true);

                    line = counter.RefLine(line);
                    if (addToDict)
                        (dict["Lines"] as List<GSALine>).Add(line);

                    gsaObj.GwaCommand(line.GetGWACommand());

                    Messages.AddMessage("Created new line " + line.Reference.ToString() + ".");

                    return line.Reference;
                }
                else
                    return matches[0].Reference;
            }
            else
            {
                for (int i = 0; i < line.Connectivity.Length; i++)
                    line.Connectivity[i] = (dict["Nodes"] as List<GSANode>)
                        .Where(n => n.Reference == line.Connectivity[i])
                        .Select(n => n.Reference).FirstOrDefault();

                if (line.Connectivity.Contains(0))
                {
                    List<GSAObject> lNodes = line.GetChildren();
                    for (int i = 0; i < line.Connectivity.Length; i++)
                        if (line.Connectivity[i] == 0)
                            line.Connectivity[i] = AddNode((lNodes[i] as GSANode).AttachGSA(gsaObj), ref dict, ref counter, true);
                }

                line = counter.RefLine(line);

                gsaObj.GwaCommand(line.GetGWACommand());

                return line.Reference;
            }
        }

        private int AddArea(GSAArea area, ref Dictionary<string, object> dict, ref GSARefCounters counter, bool addToDict)
        {
            if (area.Reference == 0)
            {
                // No clash or overlap check
                area = counter.RefArea(area);

                List<GSAObject> aLines = area.GetChildren();
                area.Connectivity = new int[aLines.Count];
                for (int i = 0; i < aLines.Count; i++)
                    area.Connectivity[i] = AddLine((aLines[i] as GSALine).AttachGSA(gsaObj), ref dict, ref counter, true);

                gsaObj.GwaCommand(area.GetGWACommand());
                if (addToDict)
                    (dict["Areas"] as List<GSAArea>).Add(area);

                Messages.AddMessage("Created new area " + area.Reference.ToString() + ".");

                return area.Reference;
            }

            else
            {
                for (int i = 0; i < area.Connectivity.Length; i++)
                    area.Connectivity[i] = (dict["Lines"] as List<GSALine>)
                        .Where(l => l.Reference == area.Connectivity[i])
                        .Select(l => l.Reference).FirstOrDefault();

                if (area.Connectivity.Contains(0))
                {
                    List<GSAObject> aLines = area.GetChildren();
                    for (int i = 0; i < area.Connectivity.Length; i++)
                        if (area.Connectivity[i] == 0)
                            area.Connectivity[i] = AddLine((aLines[i] as GSALine).AttachGSA(gsaObj), ref dict, ref counter, true);
                }

                area = counter.RefArea(area);

                gsaObj.GwaCommand(area.GetGWACommand());
                return area.Reference;
            }
        }

        private int AddElement(GSAObject element, ref Dictionary<string, object> dict, ref GSARefCounters counter, bool addToDict)
        {
            for (int i = 0; i < element.Connectivity.Length; i++)
                element.Connectivity[i] = (dict["Nodes"] as List<GSANode>)
                    .Where(n => n.Reference == element.Connectivity[i])
                    .Select(n => n.Reference).FirstOrDefault();
            
            if (element.Connectivity.Length==0 | element.Connectivity.Contains(0))
            {
                List<GSAObject> eNodes = element.GetChildren();

                if (element.Connectivity.Length==0)
                    element.Connectivity = new int[eNodes.Count];

                for (int i = 0; i < element.Connectivity.Length; i++)
                    if (element.Connectivity[i] == 0)
                        element.Connectivity[i] = AddNode((eNodes[i] as GSANode).AttachGSA(gsaObj), ref dict, ref counter, true);
            }

            if (element.Reference == 0 & addToDict)
            {
                element = counter.RefElement(element);
                (dict["Element"] as List<GSAObject>).Add(element);
            }
            else
                element = counter.RefElement(element);
            
            gsaObj.GwaCommand(element.GetGWACommand());

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
            objects["Lines"] = new List<GSALine>();
            objects["Areas"] = new List<GSAArea>();
            objects["Elements"] = new List<GSAObject>();

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
            if (objects.ContainsKey("Nodes"))
                foreach (GSANode n in objects["Nodes"] as List<GSANode>)
                    AddNode(n, ref objects, ref counter, false);

            // Lines 
            if (objects.ContainsKey("Lines"))
                foreach (GSALine l in objects["Lines"] as List<GSALine>)
                    AddLine(l, ref objects, ref counter, false);

            // Areas
            if (objects.ContainsKey("Areas"))
                foreach (GSAArea a in objects["Areas"] as List<GSAArea>)
                    AddArea(a, ref objects, ref counter, false);

            // Elements
            if (objects.ContainsKey("Elements"))
                foreach (GSAObject e in objects["Elements"] as List<GSAObject>)
                    AddElement(e, ref objects, ref counter, false);

            Messages.AddMessage("Streamed " + counter.TotalObjects + " objects.");

            gsaObj.UpdateViews();

            Messages.AddMessage("Receiving completed!");
        }

        #endregion

        #region Update GSA
        //public GSAObject[] UpdateObjectIDs(string[] keys, GSAObject[] objects)
        //{
        //    if (keys.Length == 0) return objects;
        //    if (objects.Length == 0) return objects;

        //    string[] newIDs = keys.Where(k => !objects.Select(g => g.Reference).ToList().Contains(k)).ToArray();

        //    int counter = 0;
        //    foreach (GSAObject o in objects)
        //    {
        //        if (o.SpeckleID == null | o.SpeckleID == "")
        //        {
        //            o.SpeckleID = newIDs[counter++];
        //            gsaObj.GwaCommand(o.GetGWACommand());
        //            Messages.AddMessage("Updated " + o.GSAEntity + " " + o.Ref + " with ID " + o.SpeckleID + ".");
        //        }
        //        if (counter >= newIDs.Length) break;
        //    }

        //    return objects;
        //}
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

        private List<int> nodeRefsUsed;
        private List<int> elementRefsUsed;
        private List<int> lineRefsUsed;
        private List<int> memberRefsUsed;
        private List<int> areaRefsUsed;
        private List<int> regionRefsUsed;

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

            nodeRefsUsed = new List<int>();
            elementRefsUsed = new List<int>();
            lineRefsUsed = new List<int>();
            memberRefsUsed = new List<int>();
            areaRefsUsed = new List<int>();
            regionRefsUsed = new List<int>();
        }

        public GSANode RefNode(GSANode node)
        {
            if (node.Reference > 0) return node;
            while (nodeRefsUsed.Contains(nodeCounter))
                nodeCounter++;
            node.Reference = nodeCounter++;
            nodeRefsUsed.Add(node.Reference);
            return node;
        }

        public GSAObject RefElement(GSAObject element)
        {
            if (element.Reference > 0) return element;
            while (elementRefsUsed.Contains(elementCounter))
                elementCounter++;
            element.Reference = elementCounter++;
            elementRefsUsed.Add(element.Reference);
            return element;
        }

        public GSA1DElement Ref1DElement(GSA1DElement element)
        {
            if (element.Reference > 0) return element;
            while (elementRefsUsed.Contains(elementCounter))
                elementCounter++;
            element.Reference = elementCounter++;
            elementRefsUsed.Add(element.Reference);
            return element;
        }

        public GSALine RefLine(GSALine line)
        {
            if (line.Reference > 0) return line;
            while (lineRefsUsed.Contains(lineCounter))
                lineCounter++;
            line.Reference = lineCounter++;
            lineRefsUsed.Add(line.Reference);
            return line;
        }

        public GSAMember RefMember(GSAMember member)
        {
            if (member.Reference > 0) return member;
            while (memberRefsUsed.Contains(memberCounter))
                memberCounter++;
            member.Reference = memberCounter++;
            memberRefsUsed.Add(member.Reference);
            return member;
        }

        public GSAArea RefArea(GSAArea area)
        {
            if (area.Reference > 0) return area;
            while (areaRefsUsed.Contains(areaCounter))
                areaCounter++;
            area.Reference = areaCounter++;
            areaRefsUsed.Add(area.Reference);
            return area;
        }

        public GSARegion RefRegion(GSARegion region)
        {
            if (region.Reference > 0) return region;
            while (regionRefsUsed.Contains(regionCounter))
                regionCounter++;
            region.Reference = regionCounter++;
            regionRefsUsed.Add(region.Reference);
            return region;
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

        public void AddLineRefs(List<int> refs)
        {
            lineRefsUsed.AddRange(refs);
            lineRefsUsed = lineRefsUsed.Distinct().ToList();
        }

        public void AddNemberRefs(List<int> refs)
        {
            memberRefsUsed.AddRange(refs);
            memberRefsUsed = memberRefsUsed.Distinct().ToList();
        }

        public void AddAreaRefs(List<int> refs)
        {
            areaRefsUsed.AddRange(refs);
            areaRefsUsed = areaRefsUsed.Distinct().ToList();
        }

        public void AddRegionRefs(List<int> refs)
        {
            regionRefsUsed.AddRange(refs);
            regionRefsUsed = regionRefsUsed.Distinct().ToList();
        }
    }
}
