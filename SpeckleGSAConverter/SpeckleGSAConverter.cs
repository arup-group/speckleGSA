using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpeckleCore;

namespace SpeckleGSA
{
    public class ConverterHack { /*makes sure the assembly is loaded*/  public ConverterHack() { } }

    public static class SpeckleGSAConverter
    {
        const double EPS = 1e-6;
        const string appId = null;

        #region Helper Methods
        public static double ToDegrees(this double radians)
        {
            return radians * (180 / Math.PI);
        }

        public static double ToRadians(this double degrees)
        {
            return degrees * (Math.PI / 180);
        }

        public static bool Threshold(double value1, double value2, double error = EPS)
        {
            return Math.Abs(value1 - value2) <= error;
        }

        public static double Median(double min, double max)
        {
            return ((max - min) * 0.5) + min;
        }

        public static Dictionary<string,object> GetSpeckleProperties(this Array arr)
        {
            if (arr == null) { return null; }

            int count = 0;
            var speckleDict = new Dictionary<string, object>();
            foreach(var value in arr)
            {
                string key = count.ToString();
                speckleDict.Add(key, value);
                count++;
            }
            return speckleDict;
        }

        public static Dictionary<string,object> GetSpeckleProperties(this object obj)
        {
            if (obj == null) { return null; }

            var speckleDict = new Dictionary<string, object>();
            foreach(var prop in obj.GetType().GetProperties())
            {
                string key = prop.Name;
                object value = prop.GetValue(obj, null);
                if(prop.PropertyType.IsArray)
                {
                    value = ((Array)value).GetSpeckleProperties();
                }
                speckleDict.Add(key, value);
            }
            return speckleDict;
        }
        
        public static void SetSpeckleProperties(this object obj, Dictionary<string, object> dict)
        {
            if (obj == null) { return; }
            foreach (var prop in obj.GetType().GetProperties())
            {
                Console.WriteLine(prop.Name);
                if (!dict.ContainsKey(prop.Name)) continue;

                object value = dict[prop.Name];

                if (prop.PropertyType.IsArray)
                {
                    Type type = prop.GetValue(obj).GetType().GetElementType();
                    var arr = Array.CreateInstance(type, (value as Dictionary<string, object>).Count);
                    
                    foreach (KeyValuePair<string, object> kp in (value as Dictionary<string, object>))
                        arr.SetValue(Convert.ChangeType(kp.Value, type), Convert.ToInt32(kp.Key));
                    
                    prop.SetValue(obj, arr);
                }
                else
                {
                    prop.SetValue(obj, Convert.ChangeType(value, prop.PropertyType));
                }
            }
        }
        #endregion

        #region Numbers
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

        public static double? ToNative(this SpeckleNumber num)
        {
            return num.Value;
        }
        #endregion

        #region Booleans
        public static SpeckleBoolean ToSpeckle(this bool b)
        {
            return new SpeckleBoolean(b);
        }

        public static bool? ToNative(this SpeckleBoolean b)
        {
            return b.Value;
        }
        #endregion

        #region Strings
        public static SpeckleString ToSpeckle(this string b)
        {
            return new SpeckleString(b);
        }

        public static string ToNative(this SpeckleString b)
        {
            return b.Value;
        }
        #endregion

        #region
        public static SpecklePoint ToSpeckle(this Node node)
        {
            return new SpecklePoint(node.Coor[0], node.Coor[1], node.Coor[2], node.Ref.ToString(), node.GetSpeckleProperties());
        }

        public static double[] ToFlatArray(this IEnumerable<Node> node)
        {
            return node.SelectMany(n => n.Coor).ToArray();
        }

        public static object ToSpeckle(this Element element)
        {
            var colour = System.Drawing.Color.FromArgb(255,
                element.Color % 256,
                (element.Color / 256) % 256,
                (element.Color / 256 / 256) % 256);

            switch (element.NumTopo)
            {
                case 1:
                    return new SpecklePoint(
                        element.GetCoorArr()[0],
                        element.GetCoorArr()[1],
                        element.GetCoorArr()[2],
                        element.Ref.ToString(),
                        element.GetSpeckleProperties());
                case 2:
                    return new SpeckleLine(
                        element.GetCoorArr(),
                        element.Ref.ToString(),
                        element.GetSpeckleProperties());
                default:
                    return new SpeckleMesh(
                        element.GetCoorArr(),
                        new int[] {element.NumTopo - 3}.Concat(
                            Enumerable.Range(0, element.NumTopo).ToArray())
                            .ToArray(),
                        Enumerable.Repeat(
                            colour.ToArgb(),
                            element.NumTopo).ToArray(),
                        null,
                        element.Ref.ToString(),
                        element.GetSpeckleProperties());
            }
        }
        #endregion

        #region
        public static object ToNative(this SpecklePoint point)
        {
            switch(point.Properties["OBJ_TYPE"])
            {
                case "NODE":
                    Node n = new Node();
                    n.SetSpeckleProperties(point.Properties);
                    return n;
                case "ELEMENT":
                    Element e = new Element();
                    e.SetSpeckleProperties(point.Properties);
                    return e;
                default:
                    return null;
            }
        }

        public static Element ToNative(this SpeckleLine line)
        {
            switch (line.Properties["OBJ_TYPE"])
            {
                case "ELEMENT":
                    Element e = new Element();
                    e.SetSpeckleProperties(line.Properties);
                    return e;
                default:
                    return null;
            }
        }

        public static Element ToNative(this SpeckleMesh mesh)
        {
            switch (mesh.Properties["OBJ_TYPE"])
            {
                case "ELEMENT":
                    Element e = new Element();
                    e.SetSpeckleProperties(mesh.Properties);
                    return e;
                default:
                    return null;
            }
        }
        #endregion
    }
}
