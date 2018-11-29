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
        public ComAuto gsaObj;
        public List<GSANode> nodes;
        public List<GSAElement> elements;
        public List<GSALine> lines;
        public List<GSAArea> areas;
        public bool SendDesignLayer { get; set; }
        public bool SendAnalysisLayer { get; set; }

        public GSAController()
        {
            gsaObj = new ComAuto();
            gsaObj.DisplayGsaWindow(true);

            nodes = new List<GSANode>();
            elements = new List<GSAElement>();
            lines = new List<GSALine>();
            areas = new List<GSAArea>();

            SendDesignLayer = false;
            SendAnalysisLayer = false;
        }

        public void NewFile()
        {
            gsaObj.NewFile();
            gsaObj.DisplayGsaWindow(true);
        }

        public void OpenFile(string path)
        {
            gsaObj.Open(path);
            gsaObj.DisplayGsaWindow(true);
        }

        public void GetNodes()
        {
            nodes.Clear();

            string res = gsaObj.GwaCommand("GET_ALL,NODE");

            if (res == "")
                return;

            string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string p in pieces)
            {
                GSANode n = new GSANode();
                n.ParseGWACommand(p);

                nodes.Add(n);
            }
        }

        public void GetElements()
        {
            elements.Clear();

            string res = gsaObj.GwaCommand("GET_ALL,EL");

            if (res == "")
                return;

            string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string p in pieces)
            {
                GSAElement e = new GSAElement();
                e.ParseGWACommand(p);

                List<double> eNodes = new List<double>();
                foreach (int t in e.Topo)
                    eNodes.AddRange(nodes.Where(n => n.Ref == t).SelectMany(n => n.Coor));

                e.Coor = eNodes.ToArray();

                elements.Add(e);
            }
        }

        public void GetLines()
        {
            lines.Clear();

            string res = gsaObj.GwaCommand("GET_ALL,LINE");

            if (res == "")
                return;

            string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string p in pieces)
            {
                GSALine l = new GSALine();
                l.ParseGWACommand(p);

                List<double> lNodes = new List<double>();
                foreach (int t in l.Topo)
                    lNodes.AddRange(nodes.Where(n => n.Ref == t).SelectMany(n => n.Coor));

                l.Coor = lNodes.ToArray();

                lines.Add(l);
            }
        }

        public void GetAreas()
        {
            areas.Clear();

            string res = gsaObj.GwaCommand("GET_ALL,AREA");

            if (res == "")
                return;

            string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string p in pieces)
            {
                GSAArea a = new GSAArea();
                a.ParseGWACommand(p);


                List<double> aNodes = new List<double>();
                foreach (int l in a.Lines)
                    aNodes.AddRange(lines.Where(x => x.Ref == l).SelectMany(n => n.Coor.Take(3)));

                a.Coor = aNodes.ToArray();

                areas.Add(a);
            }
        }

        public List<object> ExportDesignLayerObjects()
        {
            if (!SendDesignLayer)
                return new List<object>();

            GetNodes();
            GetLines();
            GetAreas();

            List<object> BucketObjects = new List<object>();

            BucketObjects.AddRange(nodes);
            BucketObjects.AddRange(lines);
            BucketObjects.AddRange(areas);

            return BucketObjects;
        }

        public List<object> ExportAnalysisLayerObjects()
        {
            if (!SendAnalysisLayer)
                return new List<object>();

            GetNodes();
            GetElements();

            List<object> BucketObjects = new List<object>();

            BucketObjects.AddRange(nodes);
            BucketObjects.AddRange(elements);

            return BucketObjects;
        }

        public void ImportObjects(List<object> ConvertedObjects)
        {
            nodes.Clear();
            elements.Clear();
            lines.Clear();
            areas.Clear();

            GSARefCounters counter = new GSARefCounters();
            
            foreach (object obj in ConvertedObjects)
            {
                Type t = obj.GetType();
                if (t.IsArray)
                    foreach (GSAObject arrObj in (obj as Array))
                        AddGSAObj(arrObj, ref counter);
                else
                    AddGSAObj(obj as GSAObject, ref counter);
            }
        }

        public void AddGSAObj(GSAObject obj, ref GSARefCounters counter)
        {
            Type t = obj.GetType();
            PropertyInfo p = t.GetProperty("GSAEntity");
            string type = p.GetValue(obj, null).ToString();

            switch (type)
            {
                case "NODE":
                    AddNode(obj as GSANode, ref counter);
                    break;
                case "LINE":
                    AddLine(obj as GSALine, ref counter);
                    break;
                case "AREA":
                    AddArea(obj as GSAArea, ref counter);
                    break;
                default:
                    break;
            }
        }

        public int AddNode(GSANode node, ref GSARefCounters counter)
        {
            List<GSANode> matches = nodes.Where(
                n => n.Coor[0] == node.Coor[0] &
                n.Coor[1] == node.Coor[1] &
                n.Coor[2] == node.Coor[2]
                ).ToList();

            if (matches.Count == 0)
            {
                node = counter.RefNode(node);

                gsaObj.GwaCommand(node.GetGWACommand());
                nodes.Add(node);
                return node.Ref;
            }
            else
                return matches[0].Ref;
        }

        public int AddLine(GSALine line, ref GSARefCounters counter)
        {
            List<GSALine> matches = lines.Where(
                            l => (l.Coor[0] == line.Coor[0] &
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
                            l.Coor[5] == line.Coor[2])).ToList();

            if (matches.Count == 0)
            {
                line = counter.RefLine(line);

                List<GSAObject> lNodes = line.GetChildren();
                for (int i = 0; i < lNodes.Count; i++)
                    line.Topo[i] = AddNode((lNodes[i] as GSANode), ref counter);

                gsaObj.GwaCommand(line.GetGWACommand());
                lines.Add(line);
                return line.Ref;
            }
            else
                return matches[0].Ref;
        }

        public int AddArea(GSAArea area, ref GSARefCounters counter)
        {
            area = counter.RefArea(area);

            if (area.Lines[0] == 0)
            {
                List<GSAObject> aLines = area.GetChildren();
                for (int i = 0; i < aLines.Count; i++)
                    area.Lines[i] = AddLine((aLines[i] as GSALine), ref counter);
            }

            gsaObj.GwaCommand(area.GetGWACommand());
            areas.Add(area);
            return area.Ref;
        }
    }

    public class GSARefCounters
    {
        public int GetNodeRef
        { get
            {
                while (nodeRefs.Contains(nodeCounter)) nodeCounter++;
                return nodeCounter;
            }
        }
        public int GetElementRef
        {
            get
            {
                while (elementRefs.Contains(elementCounter)) elementCounter++;
                return elementCounter;
            }
        }
        public int GetLineRef
        {
            get
            {
                while (lineRefs.Contains(lineCounter)) lineCounter++;
                return lineCounter;
            }
        }
        public int GetMemberRef
        {
            get
            {
                while (memberRefs.Contains(memberCounter)) memberCounter++;
                return memberCounter;
            }
        }
        public int GetAreaRef
        {
            get
            {
                while (areaRefs.Contains(areaCounter)) areaCounter++;
                return areaCounter;
            }
        }
        public int GetRegionRef
        {
            get
            {
                while (regionRefs.Contains(regionCounter)) regionCounter++;
                return regionCounter;
            }
        }

        private int nodeCounter;
        private int elementCounter;
        private int lineCounter;
        private int memberCounter;
        private int areaCounter;
        private int regionCounter;

        private List<int> nodeRefs;
        private List<int> elementRefs;
        private List<int> lineRefs;
        private List<int> memberRefs;
        private List<int> areaRefs;
        private List<int> regionRefs;

        public GSARefCounters()
        {
            nodeCounter = 1;
            elementCounter = 1;
            lineCounter = 1;
            memberCounter = 1;
            areaCounter = 1;
            regionCounter = 1;

            nodeRefs = new List<int>();
            elementRefs = new List<int>();
            lineRefs = new List<int>();
            memberRefs = new List<int>();
            areaRefs = new List<int>();
            regionRefs = new List<int>();
        }

        public GSANode RefNode(GSANode node)
        {
            if (node.Ref == 0)
                node.Ref = GetNodeRef;
            nodeRefs.Add(node.Ref);
            return node;
        }

        public GSAElement RefElement(GSAElement element)
        {
            if (element.Ref == 0)
                element.Ref = GetElementRef;
            elementRefs.Add(element.Ref);
            return element;
        }

        public GSALine RefLine(GSALine line)
        {
            if (line.Ref == 0)
                line.Ref = GetLineRef;
            lineRefs.Add(line.Ref);
            return line;
        }

        public GSAMember RefMember(GSAMember member)
        {
            if (member.Ref == 0)
                member.Ref = GetMemberRef;
            memberRefs.Add(member.Ref);
            return member;
        }

        public GSAArea RefArea(GSAArea area)
        {
            if (area.Ref == 0)
                area.Ref = GetAreaRef;
            areaRefs.Add(area.Ref);
            return area;
        }

        public GSARegion RefRegion(GSARegion region)
        {
            if (region.Ref == 0)
                region.Ref = GetRegionRef;
            regionRefs.Add(region.Ref);
            return region;
        }
    }
}
