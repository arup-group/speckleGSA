using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Interop.Gsa_9_0;

namespace SpeckleGSA
{
    public class GSAController
    {
        public ComAuto gsaObj;
        public GsaNode[] nodes;
        public GsaElement[] elements;

        public int Test { get => elements[0].Color; }

        public GSAController()
        {
            gsaObj = new ComAuto();
            gsaObj.NewFile();;
            gsaObj.DisplayGsaWindow(true);
        }

        public void GetNodes()
        {
            int[] nodeRefs;
            gsaObj.EntitiesInList("all", GsaEntity.NODE, out nodeRefs);

            if (nodeRefs != null)
                gsaObj.Nodes(nodeRefs, out nodes);

            foreach (int n in nodeRefs)
            {
                string res = gsaObj.GwaCommand("GET,NODE," + n.ToString());
                Console.WriteLine(res);
            }
        }

        public void SetNodes(Node[] nArr)
        {
            foreach (Node n in nArr)
                gsaObj.GwaCommand(n.GetGWACommand());
        }

        public void GetElements()
        {
            int[] elementRefs;
            gsaObj.EntitiesInList("all", GsaEntity.ELEMENT, out elementRefs);

            if (elementRefs != null)
                gsaObj.Elements(elementRefs, out elements);
        }

        public void SetElements(Element[] eArr)
        {
            foreach (Element e in eArr)
                gsaObj.GwaCommand(e.GetGWACommand());
        }

        public List<object> ExportObjects()
        {
            List<object> BucketObjects = new List<object>();

            if (nodes != null)
                foreach (GsaNode n in nodes)
                    BucketObjects.Add(new Node(n));

            if (elements != null)
                foreach (GsaElement e in elements)
                {
                    GsaNode[] elemNodes;
                    gsaObj.Nodes(e.Topo, out elemNodes);

                    Element el = new Element();
                    el.SetCoorArr(elemNodes.SelectMany(n => n.Coor).ToArray());
                    el.ParseGWACommand(gsaObj.GwaCommand("GET,EL," + e.Ref.ToString()));
                    BucketObjects.Add(el);
                }

            return BucketObjects;
        }
    }
}
