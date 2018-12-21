using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA
{
    public class GSA2DElementMesh : GSAObject
    {
        public string Type { get; set; }
        public int Property { get; set; }
        public double InsertionPoint { get; set; }
        public List<object> Elements { get; set; }

        public List<int[]> Edges;
        public Dictionary<int, int> NodeMapping;

        public GSA2DElementMesh()
        {
            Type = "MESH";
            Property = 1;
            InsertionPoint = 0;
            Elements = new List<object>();

            Edges = new List<int[]>();
            NodeMapping = new Dictionary<int, int>();
        }

        #region GSAObject Functions
        public override void ParseGWACommand(string command, GSAObject[] children = null)
        {
            throw new NotImplementedException();
        }

        public override string GetGWACommand()
        {
            throw new NotImplementedException();
        }

        public override List<GSAObject> GetChildren()
        {
            List<GSAObject> elements = new List<GSAObject>();

            foreach (object e in Elements)
            {
                Dictionary<string, object> elemDict = e as Dictionary<string, object>;
                GSA2DElement elem = new GSA2DElement();

                if (elemDict.ContainsKey("Connectivity"))
                    elem.Connectivity = GetElemConnectivity(elemDict);
                else if (elemDict.ContainsKey("Coor"))
                    elem.Coor = GetElemCoor(elemDict);
                else
                    continue;

                elem.Property = Property;
                elem.InsertionPoint = InsertionPoint;
                elem.Name = (string)elemDict["Name"];
                elem.Reference = (int)(elemDict["Reference"].ToDouble());
                elem.Axis = (Dictionary<string, object>)elemDict["Axis"];

                switch (elem.Connectivity.Count() + elem.Coor.Count() / 3)
                {
                    case 3:
                        elem.Type = "TRI3";
                        break;
                    case 4:
                        elem.Type = "QUAD4";
                        break;
                    default:
                        continue;
                }

                if (elem.Coor.Count == 0)
                    foreach (int c in elem.Connectivity)
                        elem.Coor.AddRange(Coor.Skip(NodeMapping[c] * 3).Take(3));

                elements.Add(elem);
            }


            return elements;
        }
        #endregion

        #region Mesh Operations
        public bool MeshMergeable(GSA2DElementMesh mesh)
        {
            if (mesh.Property != Property | mesh.InsertionPoint != InsertionPoint)
                return false;

            foreach (int[] edge in mesh.Edges)
                if (EdgeinMesh(edge)) return true;

            return false;
        }

        public void MergeMesh(GSA2DElementMesh mesh)
        {
            Edges.AddRange(mesh.Edges);

            foreach(KeyValuePair<int, int> nMap in mesh.NodeMapping)
                if (!NodeMapping.ContainsKey(nMap.Key))
                {
                    NodeMapping[nMap.Key] = Coor.Count() / 3;
                    Coor.AddRange(mesh.Coor.Skip(nMap.Value * 3).Take(3));
                }

            Elements.AddRange(mesh.Elements);
        }
        #endregion

        #region Element Operations
        public bool ElementAddable(GSA2DElement element)
        {
            if (element.Property != Property | element.InsertionPoint != InsertionPoint)
                return false;

            List<int> connectivity = element.Connectivity;
            connectivity.Add(element.Connectivity[0]);

            for (int i = 0; i < connectivity.Count(); i ++)
                if (EdgeinMesh(new int[] { connectivity[i], connectivity[i + 1] }))
                    return true;

            return false;
        }

        public bool EdgeinMesh(int[] edge)
        {
            foreach (int[] e in Edges)
                if ((e[0] == edge[0] && e[1] == edge[1]) || (e[0] == edge[1] && e[1] == edge[0])) return true;
            
            return false;
        }

        public void AddElement(GSA2DElement element)
        {
            Dictionary<string, object> e = new Dictionary<string, object>()
            {
                { "Name", element.Name },
                { "Reference", element.Reference },
                { "Connectivity", element.Connectivity },
                { "Axis", element.Axis }
            };
            AddEdges(element.Connectivity);
            AddCoors(element.Coor, element.Connectivity);
            Elements.Add(e);
        }

        public void AddEdges(List<int> connectivity)
        {
            for (int i = 0; i < connectivity.Count() - 1; i++)
                Edges.Add(connectivity.Skip(i).Take(2).ToArray());

            Edges.Add(new int[] {
                    connectivity[connectivity.Count() - 1],
                    connectivity[0]});
        }

        public void AddCoors(List<double> coor, List<int> connectivity)
        {
            for (int i = 0; i < connectivity.Count(); i++)
                if (!NodeMapping.ContainsKey(connectivity[i]))
                {
                    NodeMapping[connectivity[i]] = Coor.Count() / 3;
                    Coor.AddRange(coor.Skip(i * 3).Take(3));
                }
        }
        #endregion

        #region Helper Functions
        public List<int> GetElemConnectivity (Dictionary<string,object> elem)
        {
            return ((IEnumerable)elem["Connectivity"]).Cast<object>()
                .Select(e => (int)e.ToDouble()).ToList();
        }

        public List<double> GetElemCoor(Dictionary<string, object> elem)
        {
            return ((IEnumerable)elem["Coor"]).Cast<object>()
                .Select(e => e.ToDouble()).ToList();
        }
        #endregion
    }
}
