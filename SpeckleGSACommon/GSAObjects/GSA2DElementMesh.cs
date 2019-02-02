using System;
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
        public static readonly int WritePriority = 0;

        public static readonly Type[] ReadPrerequisite = new Type[2] { typeof(GSA2DElement), typeof(GSA2DFaceLoad) };

        public int Property { get; set; }
        public double Offset { get; set; }
        public List<object> Elements { get; set; }

        public Dictionary<string,List<string>> Edges;
        public Dictionary<string, int> NodeMapping;
        public List<List<string>> ElementConnectivity;

        public GSA2DElementMesh()
        {
            Property = 1;
            Offset = 0;

            Elements = new List<object>();

            Edges = new Dictionary<string, List<string>>();
            NodeMapping = new Dictionary<string, int>();
            ElementConnectivity = new List<List<string>>();
        }

        #region GSAObject Functions
        public static void GetObjects(Dictionary<Type, object> dict)
        {
            if (!GSA.TargetAnalysisLayer) return;
            
            if (!dict.ContainsKey(typeof(GSA2DElement))) return;
            
            List<GSAObject> meshes = new List<GSAObject>();
            
            foreach (GSAObject e2D in dict[typeof(GSA2DElement)] as List<GSAObject>)
            {
                GSA2DElementMesh mesh = new GSA2DElementMesh();
                mesh.Property = (e2D as GSA2DElement).Property;
                mesh.Offset = (e2D as GSA2DElement).Offset;
                mesh.Color = (e2D as GSA2DElement).Color;
                mesh.AddElement(e2D as GSA2DElement);
                meshes.Add(mesh);
            }

            if (Settings.Merge2DElementsIntoMesh)
            { 
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
            }

            List<GSAObject> singleFaceMesh = meshes.Where(m => (m as GSA2DElementMesh).Elements.Count() <= 1).ToList();

            dict[typeof(GSA2DElement)] = singleFaceMesh.SelectMany(m => (m as GSA2DElementMesh).GetChildren()).ToList();
            foreach(GSAObject o in singleFaceMesh)
                meshes.Remove(o);

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

            for(int i = 0; i < ElementConnectivity.Count(); i++)
            {
                GSA2DElement elem = new GSA2DElement();

                elem.MeshReference = Reference;
                elem.Property = Property;
                elem.Offset = Offset;
                elem.Color = Color;

                elem.Coor = ElementConnectivity[i]
                    .SelectMany(c => 
                        c.ToCoor())
                        .ToList();

                switch (elem.Coor.Count() / 3)
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

                if (Elements.Count > i)
                {
                    try
                    {
                        Dictionary<string, object> elemDict = Elements[i] as Dictionary<string, object>;

                        elem.Name = (string)elemDict["Name"];
                        elem.Reference = (int)(elemDict["Reference"].ToDouble());
                        elem.Axis = (Dictionary<string, object>)elemDict["Axis"];
                    }
                    catch { }
                }

                elements.Add(elem);
            }

            return elements;
        }
        #endregion

        #region Mesh Operations
        public bool MeshMergeable(GSA2DElementMesh mesh)
        {
            if (mesh.Property != Property || mesh.Offset != Offset)
                return false;

            if ((mesh.Color == null && Color != null) || (mesh.Color != null && Color == null) ||
                (mesh.Color != null && Color != null && (int)mesh.Color != (int)Color))
                return false;

            if (EdgeinMesh(mesh.Edges)) return true;

            return false;
        }

        public void MergeMesh(GSA2DElementMesh mesh)
        {
            foreach (KeyValuePair<string, List<string>> kvp in mesh.Edges)
            {
                if (Edges.ContainsKey(kvp.Key))
                {
                    Edges[kvp.Key].AddRange(kvp.Value);
                    Edges[kvp.Key] = Edges[kvp.Key].Distinct().ToList();
                }
                else
                {
                    Edges[kvp.Key] = kvp.Value;
                }
            }

            foreach (KeyValuePair<string, int> nMap in mesh.NodeMapping)
                if (!NodeMapping.ContainsKey(nMap.Key))
                {
                    NodeMapping[nMap.Key] = Coor.Count() / 3;
                    Coor.AddRange(mesh.Coor.Skip(nMap.Value * 3).Take(3));
                }

            Elements.AddRange(mesh.Elements);
            ElementConnectivity.AddRange(mesh.ElementConnectivity);
        }
        #endregion

        #region Element Operations
        public bool EdgeinMesh(Dictionary<string, List<string>> edges)
        {
            foreach (KeyValuePair<string, List<string>> kvp in edges)
            {
                if (Edges.ContainsKey(kvp.Key))
                {
                    if (Edges[kvp.Key].Any(a => kvp.Value.Any(b => b == a)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public void AddElement(GSA2DElement element)
        {
            Dictionary<string, object> e = new Dictionary<string, object>()
            {
                { "Name", element.Name },
                { "Reference", element.Reference },
                { "Axis", element.Axis }
            };
            AddEdges(element.Coor);
            AddCoors(element.Coor);

            List<string> elementConnectivity = new List<string>();
            for (int i = 0; i < element.Coor.Count() / 3; i++)
                elementConnectivity.Add(string.Join(",",element.Coor.Skip(i * 3).Take(3).Select(x => x.ToString())));
            ElementConnectivity.Add(elementConnectivity);

            Elements.Add(e);
        }

        public void AddEdges(List<double> coordinates)
        {
            List<double> loopedCoordinates = new List<double>(coordinates);
            loopedCoordinates.AddRange(coordinates.Take(3));

            for (int i = 0; i < loopedCoordinates.Count() / 3; i++)
            {
                string key = string.Join(",",loopedCoordinates.Skip(i * 3).Take(3).Select(x => x.ToString()));
                string value = string.Join(",", loopedCoordinates.Skip((i+1) * 3).Take(3).Select(x => x.ToString()));
                if (Edges.ContainsKey(key))
                    Edges[key].Add(value);
                else
                    Edges[key] = new List<string>() { value };

                if (Edges.ContainsKey(value))
                    Edges[value].Add(key);
                else
                    Edges[value] = new List<string>() { key };
            }
        }

        public void AddCoors(List<double> coordinates)
        {
            for (int i = 0; i < coordinates.Count() / 3; i++)
            {
                string key = string.Join(",", coordinates.Skip(i * 3).Take(3).Select(x => x.ToString()));

                if (!NodeMapping.ContainsKey(key))
                {
                    NodeMapping[key] = Coor.Count() / 3;
                    Coor.AddRange(coordinates.Skip(i * 3).Take(3).ToArray());
                }
            }
        }
        #endregion
    }
}
