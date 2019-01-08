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
        public static bool IsList(this object o)
        {
            if (o == null) return false;
            return o.GetType().IsGenericType &&
                   o.GetType().GetGenericTypeDefinition().IsAssignableFrom(typeof(List<>));
        }

        public static bool IsList(this PropertyInfo prop)
        {
            if (prop == null) return false;
            return prop.PropertyType.IsGenericType &&
                   prop.PropertyType.GetGenericTypeDefinition().IsAssignableFrom(typeof(List<>));
        }

        public static bool IsDictionary(this object o)
        {
            if (o == null) return false;
            return o.GetType().IsGenericType &&
                   o.GetType().GetGenericTypeDefinition().IsAssignableFrom(typeof(Dictionary<,>));
        }

        public static bool IsDictionary(this PropertyInfo prop)
        {
            if (prop == null) return false;
            return prop.PropertyType.IsGenericType &&
                   prop.PropertyType.GetGenericTypeDefinition().IsAssignableFrom(typeof(Dictionary<,>));
        }

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
                    return new SpeckleAbstract()
                    {
                        _type = "ref",
                        _ref = traversed[obj.GetHashCode()]
                    };
                }
                traversed.Add(obj.GetHashCode(), path + "/" + key);
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
                prop.Coor.Concat(prop.Coor.Take(3)),
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

            for (int i = 0; i < mesh.Elements.Count; i++)
            {
                Dictionary<string, object> e = mesh.Elements[i] as Dictionary<string, object>;
                List<int> eConnectivity = e["Connectivity"] as List<int>;

                faceConnectivity.Add(eConnectivity.Count() - 3);
                foreach (int c in eConnectivity)
                    faceConnectivity.Add(mesh.NodeMapping[c]);
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

            if (point.Properties!=null && point.Properties.ContainsKey("Structural"))
                obj.SetSpeckleProperties(point.Properties);

            obj.Coor = point.Value;

            return obj;
        }

        public static GSA1DElement ToNative(this SpeckleLine line)
        {
            GSA1DElement e = new GSA1DElement();

            if (line.Properties != null && line.Properties.ContainsKey("Structural"))
                e.SetSpeckleProperties(line.Properties);

            e.Coor = line.Value;

            return e;
        }

        public static object ToNative(this SpecklePolyline poly)
        {
            if (poly.Properties != null && poly.Properties.ContainsKey("Structural"))
            {
                GSA1DProperty prop = new GSA1DProperty();

                prop.SetSpeckleProperties(poly.Properties);

                if (poly.Closed)
                    prop.Coor = poly.Value.Take(poly.Value.Count() - 3).ToList();
                else
                    prop.Coor = poly.Value.Take(poly.Value.Count()).ToList();
                
                return prop;
            }
            else
            {
                List<GSAObject> e1Ds = new List<GSAObject>();

                for(int i = 0; i < poly.Value.Count(); i+=3)
                {
                    GSA1DElement e = new GSA1DElement();
                    e.Coor = poly.Value.Skip(i).Take(3).ToList();
                    e1Ds.Add(e);
                }

                return e1Ds;
            }

        }
        
        public static GSA2DElementMesh ToNative(this SpeckleMesh mesh)
        {
            GSA2DElementMesh m = new GSA2DElementMesh();

            if (mesh.Properties == null || !mesh.Properties.ContainsKey("Structural"))
            {
                m.Coor = mesh.Vertices;
                m.Color = mesh.Colors.Count > 0 ? Math.Max(mesh.Colors[0], 0) : 0;

                for (int i = 0; i < mesh.Faces.Count(); i++)
                {
                    int numNodes = mesh.Faces[i++] + 3;

                    List<double> coor = new List<double>();
                    for (int j = 0; j < numNodes; j++)
                        coor.AddRange(mesh.Vertices.Skip(mesh.Faces[i++] * 3).Take(3));

                    m.Elements.Add(new Dictionary<string, object>()
                    {
                        { "Name", "" },
                        { "Reference", 0 },
                        { "Axis", new Dictionary<string, object>()
                            {
                                { "X", new Dictionary<string, object> { { "x", 1 }, { "y", 0 },{ "z", 0 }  } },
                                { "Y", new Dictionary<string, object> { { "x", 0 }, { "y", 1 },{ "z", 0 }  } },
                                { "Z", new Dictionary<string, object> { { "x", 0 }, { "y", 0 },{ "z", 1 }  } },
                            }
                        },
                        { "Coor", coor.ToArray() }
                    });

                    i--;
                }
            }
            else
            {
                m.SetSpeckleProperties(mesh.Properties);
                m.Coor = mesh.Vertices;
                m.Color = mesh.Colors.Count > 0 ? Math.Max(mesh.Colors[0], 0) : 0;

                int elemCounter = 0;
                for (int i = 0; i < mesh.Faces.Count(); i++)
                {
                    try
                    {
                        int innerCounter = i;
                        innerCounter++;

                        List<int> conn = m.GetElemConnectivity(m.Elements[elemCounter] as Dictionary<string, object>);

                        for (int j = 0; j < conn.Count(); j++)
                            m.NodeMapping[conn[j]] = mesh.Faces[innerCounter++];

                        elemCounter++;
                        innerCounter--;
                        i = innerCounter;
                    }
                    catch
                    {
                        int innerCounter = i;
                        int numNodes = mesh.Faces[innerCounter++] + 3;

                        List<double> coor = new List<double>();
                        for (int j = 0; j < numNodes; j++)
                            coor.AddRange(mesh.Vertices.Skip(mesh.Faces[innerCounter++] * 3).Take(3));

                        m.Elements.Add(new Dictionary<string, object>()
                        {
                            { "Name", "" },
                            { "Reference", 0 },
                            { "Axis", new Dictionary<string, object>()
                                {
                                    { "X", new Dictionary<string, object> { { "x", 1 }, { "y", 0 },{ "z", 0 }  } },
                                    { "Y", new Dictionary<string, object> { { "x", 0 }, { "y", 1 },{ "z", 0 }  } },
                                    { "Z", new Dictionary<string, object> { { "x", 0 }, { "y", 0 },{ "z", 1 }  } },
                                }
                            },
                            { "Coor", coor.ToArray() }
                        });

                        elemCounter++;
                        innerCounter--;
                        i = innerCounter;
                    }
                }
                m.Connectivity = m.GetNodeReferences();
            }

            return m;
        }
        #endregion
    }
}