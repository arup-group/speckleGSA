﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Interop.Gsa_10_0;

namespace SpeckleGSA
{
    public class GSA2DElementMesh : GSAObject
    {
        public override string Entity { get => "2D Element"; set { } }

        public static readonly string GSAKeyword = "";
        public static readonly string Stream = "elements";
        public static readonly int ReadPriority = 5;
        public static readonly int WritePriority = 0;
        
        public int Property { get; set; }
        public double Offset { get; set; }
        public List<object> Elements { get; set; }

        public List<int[]> Edges;
        public Dictionary<int, int> NodeMapping;

        public GSA2DElementMesh()
        {
            Property = 1;
            Offset = 0;
            Elements = new List<object>();

            Edges = new List<int[]>();
            NodeMapping = new Dictionary<int, int>();
        }

        #region GSAObject Functions
        public static void GetObjects(Dictionary<Type, object> dict)
        {
            if (!dict.ContainsKey(typeof(GSA2DElement))) return;
            
            List<GSAObject> meshes = new List<GSAObject>();
            
            foreach (GSAObject e2D in dict[typeof(GSA2DElement)] as List<GSAObject>)
            {
                GSA2DElementMesh mesh = new GSA2DElementMesh();
                mesh.Property = (e2D as GSA2DElement).Property;
                mesh.Offset = (e2D as GSA2DElement).Offset;
                mesh.AddElement(e2D as GSA2DElement);
                meshes.Add(mesh);
            }

            dict.Remove(typeof(GSA2DElement));

            for (int i = 0; i < meshes.Count(); i++)
            {
                List<GSAObject> matches = meshes.Where((m, j) => (meshes[i] as GSA2DElementMesh).MeshMergeable(m as GSA2DElementMesh) & j != i).ToList();

                foreach (GSAObject m in matches)
                    (meshes[i] as GSA2DElementMesh).MergeMesh(m as GSA2DElementMesh);

                foreach (GSAObject m in matches)
                    meshes.Remove(m);
                
                if (matches.Count() > 0) i--;

                Status.ChangeStatus("Merging 2D elements", (double)(i+1) / meshes.Count() * 100);
            }

            dict[typeof(GSA2DElementMesh)] = meshes;
        }

        public static void WriteObjects(Dictionary<Type, object> dict)
        {
            if (!dict.ContainsKey(typeof(GSA2DElementMesh))) return;

            List<GSAObject> meshes = dict[typeof(GSA2DElementMesh)] as List<GSAObject>;

            double counter = 1;
            foreach (GSAObject m in meshes)
            {
                List<GSAObject> e2Ds = m.GetChildren();

                for (int i = 0; i < e2Ds.Count(); i++)
                    GSARefCounters.RefObject(e2Ds[i]);

                if (!dict.ContainsKey(typeof(GSA2DElement)))
                    dict[typeof(GSA2DElement)] = e2Ds;
                else
                    (dict[typeof(GSA2DElement)] as List<GSAObject>).AddRange(e2Ds);

                Status.ChangeStatus("Unrolling 2D meshes", counter++ / meshes.Count() * 100);
            }

            dict.Remove(typeof(GSA2DElementMesh));
        }

        public override void ParseGWACommand(string command, Dictionary<Type, object> dict = null)
        {
            throw new NotImplementedException();
        }

        public override string GetGWACommand(Dictionary<Type, object> dict = null)
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

                elem.MeshReference = Reference;
                elem.Property = Property;
                elem.Offset = Offset;
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
            if (mesh.Property != Property | mesh.Offset != Offset)
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
            if (element.Property != Property | element.Offset != Offset)
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
        public List<int> GetNodeReferences()
        {
            List<int> nodeRefs = new List<int>();

            foreach (object e in Elements)
                nodeRefs.AddRange(GetElemConnectivity(e as Dictionary<string, object>));

            return nodeRefs;
        }

        public List<int> GetElemConnectivity (Dictionary<string,object> elem)
        {
            try { 
            return ((IEnumerable)elem["Connectivity"]).Cast<object>()
                .Select(e => Convert.ToInt32(e)).ToList();
            }
            catch { return new List<int>(); }
        }

        public List<double> GetElemCoor(Dictionary<string, object> elem)
        {
            try
            {
                return ((IEnumerable)elem["Coor"]).Cast<object>()
                .Select(e => e.ToDouble()).ToList();
            }
            catch { return new List<double>(); }
        }
    #endregion
}
}
