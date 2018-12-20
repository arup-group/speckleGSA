using System;
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
        public Dictionary<string, object> Elements { get; set; }

        public List<int[]> edges;
        public Dictionary<int, int> nodeMapping;

        public GSA2DElementMesh() : base("ELEMENTMESH")
        {
            Type = "MESH";
            Property = 1;
            InsertionPoint = 0;
            Elements = new Dictionary<string, object>();

            edges = new List<int[]>();
            nodeMapping = new Dictionary<int, int>();
        }

        public bool MeshMergeable(GSA2DElementMesh mesh)
        {
            if (mesh.Property != Property | mesh.InsertionPoint != InsertionPoint)
                return false;

            foreach (int[] edge in mesh.edges)
                if (EdgeinMesh(edge)) return true;

            return false;
        }

        public void MergeMesh(GSA2DElementMesh mesh)
        {
            edges.AddRange(mesh.edges);

            List<double> tempCoor = Coor.ToList();
            
            foreach(KeyValuePair<int, int> nMap in mesh.nodeMapping)
            {
                if (!nodeMapping.ContainsKey(nMap.Key))
                {
                    nodeMapping[nMap.Key] = tempCoor.Count() / 3;
                    tempCoor.AddRange(mesh.Coor.Skip(nMap.Value * 3).Take(3));
                }
            }

            foreach(KeyValuePair<string, object> elem in mesh.Elements)
                Elements[Elements.Keys.Count().ToString()] = elem.Value;

            Coor = tempCoor.ToArray();
        }

        public bool ElementAddable(GSA2DElement element)
        {
            if (element.Property != Property | element.InsertionPoint != InsertionPoint)
                return false;

            List<int> connectivity = element.Connectivity.ToList();
            connectivity.Add(element.Connectivity[0]);

            for (int i = 0; i < connectivity.Count(); i ++)
                if (EdgeinMesh(new int[] { connectivity[i], connectivity[i + 1] }))
                    return true;

            return false;
        }

        public bool EdgeinMesh(int[] edge)
        {
            int matches = edges.Where(e =>
                (e[0] == edge[0] && e[1] == edge[1]) ||
                (e[0] == edge[1] && e[1] == edge[0])).Count();

            return matches > 0;
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
            Elements[Elements.Keys.Count().ToString()] = e;
        }

        public void AddEdges(int[] connectivity)
        {
            for (int i = 0; i < connectivity.Length - 1; i++)
                edges.Add(connectivity.Skip(i).Take(2).ToArray());
            edges.Add(new int[] {
                    connectivity[connectivity.Length - 1],
                    connectivity[0]});
        }

        public void AddCoors(double[] coor, int[] connectivity)
        {
            List<double> tempCoor = Coor.ToList();

            for (int i = 0; i < connectivity.Length; i++)
            {
                if (!nodeMapping.ContainsKey(connectivity[i]))
                {
                    nodeMapping[connectivity[i]] = tempCoor.Count() / 3;
                    tempCoor.AddRange(coor.Skip(i * 3).Take(3));
                }
            }

            Coor = tempCoor.ToArray();
        }

        public override string GetGWACommand()
        {
            throw new NotImplementedException();
        }

        public override void ParseGWACommand(string command)
        {
            throw new NotImplementedException();
        }

        public override List<GSAObject> GetChildren()
        {
            List<GSAObject> elements = new List<GSAObject>();

            foreach (KeyValuePair<string,object> e in Elements)
            {
                GSA2DElement elem = new GSA2DElement();
                elem.Property = Property;
                elem.InsertionPoint = InsertionPoint;
                elem.Name = (string)((e.Value as Dictionary<string,object>)["Name"]);
                elem.Reference = (int)(((e.Value as Dictionary<string, object>)["Reference"]).ToDouble());
                Dictionary<string, object> conn = (e.Value as Dictionary<string, object>)["Connectivity"] as Dictionary<string, object>;
                List<int> tempConn = new List<int>();
                for (int i = 0; i < conn.Keys.Count(); i++)
                    tempConn.Add((int)(conn[i.ToString()].ToDouble()));
                elem.Connectivity = tempConn.ToArray();
                elem.Axis = (Dictionary<string, object>)((e.Value as Dictionary<string, object>)["Axis"]);

                switch(elem.Connectivity.Count())
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

                List<double> tempCoor = new List<double>();
                foreach (int c in elem.Connectivity)
                    tempCoor.AddRange(Coor.Skip(nodeMapping[c] * 3).Take(3));
                elem.Coor = tempCoor.ToArray();

                elements.Add(elem);
            }


            return elements;
        }

        public int[] GetElemConnectivity (string key)
        {
            Dictionary<string, object> elem = Elements[key] as Dictionary<string,object>;
            Dictionary<string, object> conn = elem["Connectivity"] as Dictionary<string, object>;

            List<int> tempConn = new List<int>();
            for (int i = 0; i < conn.Keys.Count(); i++)
                tempConn.Add((int)(conn[i.ToString()].ToDouble()));
            return tempConn.ToArray();
        }
    }
}
