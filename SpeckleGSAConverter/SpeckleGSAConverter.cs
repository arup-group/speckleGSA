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

        public static object ToSpeckleDict(this object obj)
        {
            if (obj == null) { return null; }
            
            if (obj.GetType().IsArray)
            {
                if (((IEnumerable)obj).Cast<object>().ToList().Count() == 0) return null;
                return ((IEnumerable)obj).Cast<object>().ToList().Select((x, i) => new { x, i })
                    .ToDictionary(n => n.i.ToString(), n => n.x.ToSpeckleDict());
            }
            else if (obj.IsDictionary())
            {
                if ((obj as Dictionary<string, object>).Keys.Count() == 0) return null;
                return (obj as Dictionary<string, object>)
                    .ToDictionary(i => i.Key, i => i.Value.ToSpeckleDict());
            }
            else if (obj.IsList())
            {
                if (((IEnumerable)obj).Cast<object>().ToList().Count() == 0) return null;
                return ((IEnumerable)obj).Cast<object>().ToList().Select((x, i) => new { x, i })
                    .ToDictionary(n => n.i.ToString(), n => n.x.ToSpeckleDict());
            }

            else
                return obj;
        }

        public static Dictionary<string,object> GetSpeckleProperties(this object obj)
        {
            if (obj == null) { return null; }

            var structuralDict = new Dictionary<string, object>();
            foreach(var prop in obj.GetType().GetProperties())
            {
                if (!prop.CanWrite)
                    continue;

                string key = prop.Name;
                
                object value = prop.GetValue(obj, null).ToSpeckleDict();
                if (value != null)
                    structuralDict.Add(key, value);
            }

            var speckleDict = new Dictionary<string, object>()
            {
                { "Structural", structuralDict }
            };
            
            return speckleDict;
        }

        public static object FromSpeckleDict(this object obj)
        {
            if (obj == null) { return null; }

            if (obj.IsDictionary())
            {
                if ((obj as Dictionary<string, object>).Keys.Where(k => Regex.IsMatch(k, @"[0-9]+")).ToList().Count() > 0)
                    return (obj as Dictionary<string, object>).Values.Select(v => v.FromSpeckleDict()).ToArray();
                else
                    return (obj as Dictionary<string, object>)
                        .ToDictionary(i => i.Key, i => i.Value.FromSpeckleDict());
            }
            else
                return obj;
        }

        public static void SetSpeckleProperties(this object obj, Dictionary<string, object> dict)
        {
            if (obj == null) return;
        
            if (!dict.ContainsKey("Structural")) return;

            Dictionary<string, object> structuralDict = (dict["Structural"] as Dictionary<string, object>);

            foreach (var prop in obj.GetType().GetProperties())
            {
                if (!prop.CanWrite)
                    continue;

                string key = prop.Name;
                
                if (!structuralDict.ContainsKey(key)) continue;

                object value = structuralDict[key];

                if (value == null) continue;

                if (prop.PropertyType.IsArray)
                {
                    Type type = prop.GetValue(obj).GetType().GetElementType();

                    Dictionary<string, object>.ValueCollection vals = (value as Dictionary<string, object>).Values;

                    prop.SetValue(obj, vals.Select(x => Convert.ChangeType(x.FromSpeckleDict(), type)).ToArray());
                }
                else if (prop.IsList())
                {
                    Type type = prop.GetValue(obj).GetType().GetGenericArguments()[0];
                    Dictionary<string, object>.ValueCollection vals = (value as Dictionary<string, object>).Values;

                    Type genericListType = typeof(List<>).MakeGenericType(type);
                    IList tempList = (IList)Activator.CreateInstance(genericListType);
                    if (type == typeof(object))
                        foreach (object x in vals)
                            tempList.Add(x.FromSpeckleDict());
                    else
                        foreach (object x in vals)
                            tempList.Add(Convert.ChangeType(x.FromSpeckleDict(), type));


                    prop.SetValue(obj, tempList);
                }
                else if (prop.IsDictionary())
                {
                    prop.SetValue(obj,
                        Convert.ChangeType((value as Dictionary<string, object>)
                        .ToDictionary(i => i.Key, i => i.Value.FromSpeckleDict()),
                        prop.PropertyType));
                }
                else
                    prop.SetValue(obj, Convert.ChangeType(value, prop.PropertyType));
            }
        }
        #endregion

        #region GSA to Speckle
        public static SpeckleString ToSpeckle(this GSAMaterial material)
        {
            SpeckleString s = new SpeckleString("MATERIAL", material.GetSpeckleProperties());

            return s;
        }

        public static SpeckleString ToSpeckle(this GSA2DProperty prop)
        {
            SpeckleString s = new SpeckleString("2D PROPERTY", prop.GetSpeckleProperties());

            return s;
        }

        public static SpecklePoint ToSpeckle(this GSANode node)
        {
            SpecklePoint p = new SpecklePoint(node.Coor[0], node.Coor[1], node.Coor[2], "", node.GetSpeckleProperties());
            return p;
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
                Dictionary<string, object> dict = mesh.Properties["Structural"] as Dictionary<string, object>;
            
                m.SetSpeckleProperties(mesh.Properties);
                m.Coor = mesh.Vertices;
                m.Color = Math.Max(mesh.Colors[0], 0);

                int elemCounter = 0;
                for (int i = 0; i < mesh.Faces.Count(); i++)
                {
                    i++;
                    List<int> conn = m.GetElemConnectivity(m.Elements[elemCounter++] as Dictionary<string,object>);

                    for(int j = 0; j < conn.Count(); j++)
                        m.NodeMapping[conn[j]] = mesh.Faces[i++];

                    i--;
                }
            }
            return m;
        }
        #endregion
    }
}