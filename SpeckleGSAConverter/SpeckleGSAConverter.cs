using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpeckleCore;
using System.Windows.Media.Media3D;
using System.Reflection;
using System.Collections;
using System.Text.RegularExpressions;

namespace SpeckleGSA
{
    public class ConverterHack { /*makes sure the assembly is loaded*/  public ConverterHack() { } }

    public static class SpeckleGSAConverter
    {
        #region Property Conversion
        public static object UnrollAbstractRef(object obj, string path)
        {
            string[] pieces = path.Split(new char[] { '/' });

            if (pieces.Length <= 1) // We're here!
                return obj;

            string nextPath = pieces[1];

            try
            {
                return UnrollAbstractRef((obj as Dictionary<string, object>)[nextPath], string.Join("/", pieces.Skip(1)));
            }
            catch { return null; }
        }

        public static object ToSpeckleDict(this object obj, Dictionary<int, string> traversed = null, string key = "", string path = "")
        {
            if (obj == null) { return null; }

            string nextRootPath = "";

            // TODO: GetHashCode() should not be used as unique identifier.
            // Collisions observed for larger property dictionaries.
            if (traversed == null)
            {
                traversed = new Dictionary<int, string>();
                traversed.Add(obj.GetHashCode(), "root");
                nextRootPath = "root";
            }
            else
            {
                if (traversed.ContainsKey(obj.GetHashCode()))
                {
                    //return new SpeckleAbstract()
                    //{
                    //    _type = "ref",
                    //    _ref = traversed[obj.GetHashCode()]
                    //};
                }
                else
                { 
                    traversed.Add(obj.GetHashCode(), path + "/" + key);
                }
                nextRootPath = path + "/" + key;
            }

            if (obj.GetType().IsArray)
            {
                if (((IEnumerable)obj).Cast<object>().ToList().Count() == 0) return null;
                return ((IEnumerable)obj).Cast<object>().ToList().Select((x, i) => new { x, i })
                    .ToDictionary(n => n.i.ToString(), n => n.x.ToSpeckleDict(traversed, n.i.ToString(), nextRootPath));
            }
            else if (obj.IsDictionary())
            {
                if ((obj as Dictionary<string, object>).Keys.Count() == 0) return null;
                return (obj as Dictionary<string, object>)
                    .ToDictionary(i => i.Key, i => i.Value.ToSpeckleDict(traversed, i.Key.ToString(), nextRootPath));
            }
            else if (obj.IsList())
            {
                if (((IEnumerable)obj).Cast<object>().ToList().Count() == 0) return null;
                return ((IEnumerable)obj).Cast<object>().ToList().Select((x, i) => new { x, i })
                    .ToDictionary(n => n.i.ToString(), n => n.x.ToSpeckleDict(traversed, n.i.ToString(), nextRootPath));
            }

            else if (obj is string || obj is double || obj is float || obj is int || obj is SpeckleObject)
                return obj;
            else
                return Converter.Serialise(obj);
        }

        public static Dictionary<string, object> GetSpeckleProperties(this object obj)
        {
            if (obj == null) { return null; }

            Dictionary<string, object> properties = new Dictionary<string, object>();
            properties.Add("Structural", obj.GetPropertyDict());

            return properties.ToSpeckleDict() as Dictionary<string, object>;
        }

        public static object FromSpeckleDict(this object obj, Dictionary<string, object> rootDict = null)
        {
            if (obj == null) { return null; }

            if (rootDict == null & obj.IsDictionary())
                rootDict = obj as Dictionary<string, object>;

            if (obj.IsDictionary())
            {
                if ((obj as Dictionary<string, object>).Keys.Where(k => Regex.IsMatch(k, @"[0-9]+")).ToList().Count() > 0)
                    return (obj as Dictionary<string, object>).Values.Select(v => v.FromSpeckleDict(rootDict)).ToList();
                else
                    return (obj as Dictionary<string, object>)
                        .ToDictionary(i => i.Key, i => i.Value.FromSpeckleDict(rootDict));
            }
            else if (obj is SpeckleAbstract)
                return UnrollAbstractRef(rootDict, (obj as SpeckleAbstract)._ref).FromSpeckleDict(rootDict);
            else if (obj is SpeckleObject)
                return Converter.Deserialise(obj as SpeckleObject);
            else
            {
                if (obj is string)
                    if (obj as string == "null") return null;

                return obj;
            }
        }

        public static void SetSpeckleProperties(this object obj, Dictionary<string, object> dict)
        {
            if (obj == null) return;

            if (!dict.ContainsKey("Structural")) return;

            Dictionary<string, object> properties = dict.FromSpeckleDict() as Dictionary<string, object>;

            obj.SetPropertyDict(properties["Structural"] as Dictionary<string, object>);
        }
        #endregion

        #region GSA to Speckle
        public static SpeckleBoolean ToSpeckle(this bool b)
        {
            return new SpeckleBoolean(b);
        }

        public static SpeckleNumber ToSpeckle(this float num)
        {
            return new SpeckleNumber(num);
        }

        public static SpeckleNumber ToSpeckle(this long num)
        {
            return new SpeckleNumber(num);
        }

        public static SpeckleNumber ToSpeckle(this int num)
        {
            return new SpeckleNumber(num);
        }

        public static SpeckleNumber ToSpeckle(this double num)
        {
            return new SpeckleNumber(num);
        }

        public static SpeckleString ToSpeckle(this GSAMaterial material)
        {
            SpeckleString s = new SpeckleString("MATERIAL", material.GetSpeckleProperties());

            return s;
        }

        public static SpecklePolyline ToSpeckle(this GSA1DProperty prop)
        {
            SpecklePolyline p = new SpecklePolyline(
                prop.Coor,
                prop.Reference.ToString(),
                prop.GetSpeckleProperties());

            p.Closed = true;
            p.GenerateHash();

            return p;
        }

        public static SpeckleString ToSpeckle(this GSA2DProperty prop)
        {
            SpeckleString s = new SpeckleString("2D PROPERTY", prop.GetSpeckleProperties());

            return s;
        }

        public static SpecklePoint ToSpeckle(this GSANode node)
        {
            SpecklePoint p = new SpecklePoint(node.Coor[0], node.Coor[1], node.Coor[2], null, node.GetSpeckleProperties());
            return p;
        }

        public static SpeckleString ToSpeckle(this GSA0DLoad load)
        {
            SpeckleString s = new SpeckleString("0D LOAD", load.GetSpeckleProperties());
            return s;
        }

        public static SpeckleString ToSpeckle(this GSA2DFaceLoad load)
        {
            SpeckleString s = new SpeckleString("2D FACELOAD", load.GetSpeckleProperties());
            return s;
        }

        public static SpeckleLine ToSpeckle(this GSA1DElement element)
        {
            SpeckleLine l = new SpeckleLine(
                element.Coor,
                element.Reference.ToString(),
                element.GetSpeckleProperties());

            return l;
        }

        public static SpeckleMesh ToSpeckle(this GSA2DElement element)
        {
            SpeckleMesh m = new SpeckleMesh(
                        element.Coor.ToArray(),
                        new int[] { element.Coor.Count() / 3 - 3 }.Concat(
                            Enumerable.Range(0, element.Coor.Count() / 3).ToArray())
                            .ToArray(),
                        Enumerable.Repeat(
                            element.Color.ToSpeckleColor(),
                            element.Coor.Count() / 3).ToArray(),
                        null,
                        element.Reference.ToString(),
                        element.GetSpeckleProperties());
            
            return m;
        }

        public static SpeckleMesh ToSpeckle(this GSA2DElementMesh mesh)
        {
            List<int> faceConnectivity = new List<int>();

            foreach(List<string> coordinates in mesh.ElementConnectivity)
            {
                faceConnectivity.Add(coordinates.Count() - 3);
                foreach(string coor in coordinates)
                    faceConnectivity.Add(mesh.NodeMapping[coor]);
            }

            SpeckleMesh m = new SpeckleMesh(
                        mesh.Coor.ToArray(),
                        faceConnectivity.ToArray(),
                        Enumerable.Repeat(
                            mesh.Color.ToSpeckleColor(),
                            mesh.Coor.Count() / 3).ToArray(),
                        null,
                        "",
                        mesh.GetSpeckleProperties());

            return m;
        }

        public static SpeckleLine ToSpeckle(this GSA1DMember member)
        {
            SpeckleLine l = new SpeckleLine(
                member.Coor,
                member.Reference.ToString(),
                member.GetSpeckleProperties());

            return l;
        }

        public static SpeckleMesh ToSpeckle(this GSA2DMember member)
        {
            // Perform mesh making
            List<List<int>> faces = (Enumerable.Range(0, member.Coor.Count() / 3).ToList()).SplitMesh(member.Coor);
            
            List<int> faceMap = new List<int>();
            foreach (List<int> f in faces)
            {
                if (f.Count() < 3) continue;
                faceMap.Add(f.Count() - 3);
                faceMap.AddRange(f);
            }

            SpeckleMesh m = new SpeckleMesh(
                member.Coor.ToArray(),
                faceMap.ToArray(),
                Enumerable.Repeat(
                    member.Color.ToSpeckleColor(),
                    member.Coor.Count() / 3).ToArray(),
                null,
                "",
                member.GetSpeckleProperties());

            return m;
        }
        #endregion

        #region Speckle to GSA
        public static bool? ToNative(this SpeckleBoolean b)
        {
            return b.Value;
        }

        public static double? ToNative(this SpeckleNumber num)
        {
            return num.Value;
        }

        public static object ToNative(this SpeckleString str)
        {
            GSAObject obj;

            switch (str.Value)
            {
                case "MATERIAL":
                    obj = new GSAMaterial();
                    break;
                case "2D PROPERTY":
                    obj = new GSA2DProperty();
                    break;
                case "0D LOAD":
                    obj = new GSA0DLoad();
                    break;
                case "2D FACELOAD":
                    obj = new GSA2DFaceLoad();
                    break;
                default:
                    return str.Value;
            }

            obj.SetSpeckleProperties(str.Properties);
            return obj;
        }
        
        public static GSANode ToNative(this SpecklePoint point)
        {
            GSANode obj = new GSANode();

            obj.SetSpeckleProperties(point.Properties);

            obj.Coor = point.Value;

            return obj;
        }

        public static GSAObject ToNative(this SpeckleLine line)
        {
            GSAObject obj;

            if (GSA.TargetDesignLayer)
                obj = new GSA1DMember();
            else
                obj = new GSA1DElement();
            
            obj.SetSpeckleProperties(line.Properties);
            obj.Coor = line.Value;

            return obj;
        }

        public static GSAObject ToNative(this SpecklePolyline poly)
        {
            GSAObject obj;
            
            string type = poly.GetGSAObjectEntityType();
            
            if (type == "1D Property")
                // Assumes that 1D properties will automatically have its entity type set
                obj = new GSA1DProperty();
            else
            { 
                if (GSA.TargetDesignLayer)
                    obj = new GSA1DMember();
                else
                    obj = new GSA1DElement();
            }

            obj.Coor = poly.Value.Take(poly.Value.Count()).ToList();

            return obj;
        }
        
        public static GSAObject ToNative(this SpeckleMesh mesh)
        {
            GSAObject obj;
            
            if (GSA.TargetDesignLayer)
            { 
                obj = new GSA2DMember();

                obj.SetSpeckleProperties(mesh.Properties);

                // Need to collapse mesh
                List<Tuple<int,int>> edges = new List<Tuple<int,int>>();

                for (int i = 0; i < mesh.Faces.Count();)
                {
                    int numNodes = mesh.Faces[i++] + 3;

                    List<int> vertices = mesh.Faces.Skip(i).Take(numNodes).ToList();

                    i += numNodes;

                    // Add edges
                    vertices.Add(vertices[0]);
                    for (int j = 0; j < vertices.Count() - 1; j++)
                    {
                        if (edges.Where(e => (e.Item1 == vertices[j] & e.Item2 == vertices[j + 1]) |
                            (e.Item1 == vertices[j + 1] & e.Item2 == vertices[j])).Count() == 0)
                        {
                            if (vertices[j] < vertices[j + 1])
                                edges.Add(new Tuple<int, int>(vertices[j], vertices[j + 1]));
                            else
                                edges.Add(new Tuple<int, int>(vertices[j + 1], vertices[j]));
                        }
                        else
                        { 
                            edges.Remove(new Tuple<int, int>(vertices[j], vertices[j + 1]));
                            edges.Remove(new Tuple<int, int>(vertices[j+1], vertices[j]));
                        }
                    }
                }

                // Reorder the edges
                List<int> reorderedEdges = new List<int>();
                reorderedEdges.Add(edges[0].Item1);
                reorderedEdges.Add(edges[0].Item2);
                edges.RemoveAt(0);

                while(edges.Count > 0)
                {
                    int commonVertex = reorderedEdges[reorderedEdges.Count() - 1];

                    List<Tuple<int, int>> nextEdge = edges.Where(e => e.Item1 == commonVertex | e.Item2 == commonVertex).ToList();

                    if (nextEdge.Count > 0)
                    {
                        reorderedEdges.Add(nextEdge[0].Item1 == commonVertex ? nextEdge[0].Item2 : nextEdge[0].Item1);
                        edges.Remove(nextEdge[0]);
                    }
                    else
                        // Next edge not found
                        return null;
                }
                reorderedEdges.RemoveAt(0);

                // Get coordinates
                List<double> coordinates = new List<double>();
                foreach(int e in reorderedEdges)
                    coordinates.AddRange(mesh.Vertices.Skip(e * 3).Take(3));

                obj.Coor = coordinates;
            }
            else
            { 
                obj = new GSA2DElementMesh();

                obj.SetSpeckleProperties(mesh.Properties);

                obj.Coor = mesh.Vertices;
                obj.Color = mesh.Colors.Count > 0 ? Math.Max(mesh.Colors[0], 0) : 0;

                int elemCounter = 0;
                for (int i = 0; i < mesh.Faces.Count();)
                {
                    int numNodes = mesh.Faces[i++] + 3;

                    List<int> vertices = mesh.Faces.Skip(i).Take(numNodes).ToList();
                    
                    List<string> coordinates = new List<string>();
                    foreach (int e in vertices)
                        coordinates.Add(string.Join(",",mesh.Vertices.Skip(e * 3).Take(3)));

                    (obj as GSA2DElementMesh).ElementConnectivity.Add(coordinates);
                    
                    elemCounter++;
                    i += numNodes;
                }
            }

            return obj;
        }
        #endregion
    }
}