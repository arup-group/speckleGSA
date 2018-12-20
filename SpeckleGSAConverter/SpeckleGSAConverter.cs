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


        public static SpeckleArc ArcRadiustoSpeckleArc(double[] coor, double radius, bool greaterThanHalf = false)
        {
            Point3D[] points = new Point3D[] {
                new Point3D(coor[0], coor[1], coor[2]),
                new Point3D(coor[3], coor[4], coor[5]),
                new Point3D(coor[6], coor[7], coor[8])
            };

            Vector3D v1 = Point3D.Subtract(points[1], points[0]);
            Vector3D v2 = Point3D.Subtract(points[2], points[0]);
            Vector3D v3 = Vector3D.CrossProduct(v1, v2);

            double theta = -Math.Acos(v1.Length / (2 * radius));

            v1.Normalize();
            v2.Normalize();
            v3.Normalize();

            Matrix3D originRotMat;
            if (!greaterThanHalf)
                originRotMat = HelperFunctions.RotationMatrix(v3, theta);
            else
                originRotMat = HelperFunctions.RotationMatrix(Vector3D.Multiply(-1, v3), theta);

            Vector3D shiftToOrigin = Vector3D.Multiply(radius,Vector3D.Multiply(v1, originRotMat));

            Point3D origin = Point3D.Add(points[0], shiftToOrigin);

            Vector3D startVector = new Vector3D(
                points[0].X - origin.X,
                points[0].Y - origin.Y,
                points[0].Z - origin.Z);

            Vector3D endVector = new Vector3D(
                points[1].X - origin.X,
                points[1].Y - origin.Y,
                points[1].Z - origin.Z);
            
            if (v3.Z == 1)
            {
            }
            else if (v3.Z == -1)
            {
                startVector = Vector3D.Multiply(-1, startVector);
                endVector = Vector3D.Multiply(-1, endVector);


                Matrix3D reverseRotation = HelperFunctions.RotationMatrix(new Vector3D(1,0,0), Math.PI);

                startVector = Vector3D.Multiply(startVector, reverseRotation);
                endVector = Vector3D.Multiply(endVector, reverseRotation);
            }
            else
            {
                Vector3D unitReverseRotationvector = Vector3D.CrossProduct(v3, new Vector3D(0, 0, 1));
                unitReverseRotationvector.Normalize();

                Matrix3D reverseRotation = HelperFunctions.RotationMatrix(unitReverseRotationvector, Vector3D.AngleBetween(v3, new Vector3D(0, 0, 1)).ToRadians());

                startVector = Vector3D.Multiply(startVector, reverseRotation);
                endVector = Vector3D.Multiply(endVector, reverseRotation);
            }

            double startAngle = Vector3D.AngleBetween(startVector, new Vector3D(1, 0, 0)).ToRadians();
            if (startVector.Y < 0) startAngle = 2 * Math.PI - startAngle;

            double endAngle = Vector3D.AngleBetween(endVector, new Vector3D(1, 0, 0)).ToRadians();
            if (endVector.Y < 0) endAngle = 2 * Math.PI - endAngle;

            double angle = endAngle - startAngle;
            if (angle < 0) angle = 2 * Math.PI + angle;

            if ((greaterThanHalf & angle < Math.PI) | (!greaterThanHalf & angle > Math.PI))
            {
                double temp = startAngle;
                startAngle = endAngle;
                endAngle = temp;
                angle = 2 * Math.PI - angle;
            }

            Vector3D unitX = new Vector3D(1, 0, 0);
            Vector3D unitY = new Vector3D(0, 1, 0);

            Vector3D unitRotationvector = Vector3D.CrossProduct(new Vector3D(0, 0, 1), v3);
            unitRotationvector.Normalize();
            Matrix3D rotation = HelperFunctions.RotationMatrix(unitRotationvector, Vector3D.AngleBetween(v3, new Vector3D(0, 0, 1)).ToRadians());

            unitX = Vector3D.Multiply(unitX, rotation);
            unitY = Vector3D.Multiply(unitY, rotation);

            SpecklePlane plane = new SpecklePlane(
                new SpecklePoint(origin.X, origin.Y, origin.Z),
                new SpeckleVector(v3.X, v3.Y, v3.Z),
                new SpeckleVector(unitX.X, unitX.Y, unitX.Z),
                new SpeckleVector(unitY.Y, unitY.Y, unitY.Z));

            return new SpeckleArc(
                plane,
                radius,
                startAngle,
                endAngle,
                angle);
        }

        public static SpeckleArc Arc3PointtoSpeckleArc(double[] coor)
        {
            Point3D[] points = new Point3D[] {
                new Point3D(coor[0], coor[1], coor[2]),
                new Point3D(coor[3], coor[4], coor[5]),
                new Point3D(coor[6], coor[7], coor[8])
            };

            Vector3D v1 = Point3D.Subtract(points[1], points[0]);
            Vector3D v2 = Point3D.Subtract(points[2], points[1]);
            Vector3D v3 = Point3D.Subtract(points[0], points[2]);

            double a = v1.Length;
            double b = v2.Length;
            double c = v3.Length;
            double halfPerimeter = (a + b + c) / 2;
            double triArea = Math.Sqrt(halfPerimeter * (halfPerimeter - a) * (halfPerimeter - b) * (halfPerimeter - c));
            double radius = a * b * c / (triArea * 4);

            // Check if greater than half of a circle
            Point3D midPoint = new Point3D(
               (coor[0] + coor[3]) / 2,
               (coor[1] + coor[4]) / 2,
               (coor[2] + coor[5]) / 2);
            Vector3D checkVector = Point3D.Subtract(points[2], midPoint);

            return ArcRadiustoSpeckleArc(coor, radius, checkVector.Length > radius);
        }

        public static double[] SpeckleArctoArc3Point(SpeckleArc arc)
        {
            Vector3D v3 = new Vector3D(
                arc.Plane.Normal.Value[0],
                arc.Plane.Normal.Value[1],
                arc.Plane.Normal.Value[2]);

            Vector3D origin = new Vector3D(
                arc.Plane.Origin.Value[0],
                arc.Plane.Origin.Value[1],
                arc.Plane.Origin.Value[2]);

            double radius = arc.Radius.Value;
            double startAngle = arc.StartAngle.Value;
            double endAngle = arc.EndAngle.Value;
            double midAngle = startAngle < endAngle ?
                (startAngle + endAngle)/2 :
                (startAngle + endAngle)/2 + Math.PI;

            Vector3D p1 = new Vector3D(radius * Math.Cos(startAngle), radius * Math.Sin(startAngle), 0);
            Vector3D p2 = new Vector3D(radius * Math.Cos(endAngle), radius * Math.Sin(endAngle), 0);
            Vector3D p3 = new Vector3D(radius * Math.Cos(midAngle), radius * Math.Sin(midAngle), 0);

            if (v3.Z == 1)
            {
            }
            else if (v3.Z == -1)
            {
                p1 = Vector3D.Multiply(-1, p1);
                p2 = Vector3D.Multiply(-1, p2);
                p3 = Vector3D.Multiply(-1, p3);

                Matrix3D reverseRotation = HelperFunctions.RotationMatrix(new Vector3D(1, 0, 0), Math.PI);

                p1 = Vector3D.Multiply(p1, reverseRotation);
                p2 = Vector3D.Multiply(p2, reverseRotation);
                p3 = Vector3D.Multiply(p3, reverseRotation);
            }
            else
            { 
                Vector3D unitRotationvector = Vector3D.CrossProduct(new Vector3D(0, 0, 1), v3);
                unitRotationvector.Normalize();
                Matrix3D rotation = HelperFunctions.RotationMatrix(unitRotationvector, Vector3D.AngleBetween(v3, new Vector3D(0, 0, 1)).ToRadians());

                p1 = Vector3D.Multiply(p1, rotation);
                p2 = Vector3D.Multiply(p2, rotation);
                p3 = Vector3D.Multiply(p3, rotation);
            }

            p1 = Vector3D.Add(p1, origin);
            p2 = Vector3D.Add(p2, origin);
            p3 = Vector3D.Add(p3, origin);

            return new double[]
            {
                p1.X,p1.Y,p1.Z,
                p2.X,p2.Y,p2.Z,
                p3.X,p3.Y,p3.Z,
            };
        }

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
                string key = prop.Name;
                
                if (!structuralDict.ContainsKey(key)) continue;

                object value = structuralDict[key];

                if (value == null) continue;

                if (prop.PropertyType.IsArray)
                {
                    Type type = prop.GetValue(obj).GetType().GetElementType();

                    Dictionary<string, object>.ValueCollection vals = (value as Dictionary<string, object>).Values;

                    prop.SetValue(obj, vals.Select(x => Convert.ChangeType(value.FromSpeckleDict(), type)).ToArray());
                }
                else if (prop.IsList())
                {
                    Type type = prop.GetValue(obj).GetType().GetGenericArguments()[0];
                    Dictionary<string, object>.ValueCollection vals = (value as Dictionary<string, object>).Values;

                    Type genericListType = typeof(List<>).MakeGenericType(type);
                    IList tempList = (IList)Activator.CreateInstance(genericListType);
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
                Dictionary<string, object> e = mesh.Elements[i.ToString()] as Dictionary<string, object>;
                List<int> eConnectivity = e["Connectivity"] as List<int>;

                faceConnectivity.Add(eConnectivity.Count() - 3);
                foreach (int c in eConnectivity)
                    faceConnectivity.Add(mesh.nodeMapping[c]);
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
   
        public static SpeckleObject ToSpeckle(this GSALine line)
        {
            SpeckleObject obj;
            switch(line.Type)
            {
                case "LINE":
                    obj = new SpeckleLine(
                        line.Coor,
                        line.Reference.ToString(),
                        line.GetSpeckleProperties());
                    break;
                case "ARC_RADIUS":
                    obj = ArcRadiustoSpeckleArc(line.Coor.ToArray(), line.Radius);
                    obj.ApplicationId = line.Reference.ToString();
                    obj.Properties = line.GetSpeckleProperties();
                    obj.GenerateHash();
                    break;
                case "ARC_THIRD_PT":
                    obj = Arc3PointtoSpeckleArc(line.Coor.ToArray());
                    obj.ApplicationId = line.Reference.ToString();
                    obj.Properties = line.GetSpeckleProperties();
                    obj.GenerateHash();
                    break;
                default:
                    return null;
            }
            return obj;
        }

        public static SpeckleMesh ToSpeckle(this GSAArea area)
        {
            SpeckleMesh mesh = new SpeckleMesh();
            mesh.Vertices = area.Coor.ToList();
            mesh.Faces = (new int[] { area.Coor.Count() / 3 - 3 }.Concat(
                    Enumerable.Range(0, area.Coor.Count() / 3).ToArray())).ToList();
            mesh.ApplicationId = area.Reference.ToString();
            mesh.Colors = Enumerable.Repeat(
                    area.Color.ToSpeckleColor(),
                    (int)(area.Coor.Count() / 3)).ToList();
            mesh.Properties = area.GetSpeckleProperties();
            mesh.GenerateHash();
            return mesh;
        }
        #endregion

        #region
        public static GSANode ToNative(this SpecklePoint point)
        {
            GSANode obj = new GSANode();

            if (point.Properties!=null && point.Properties.ContainsKey("Structural"))
                obj.SetSpeckleProperties(point.Properties);

            obj.Coor = point.Value;

            return obj;
        }

        public static GSAObject ToNative(this SpeckleLine line)
        {
            GSAObject obj;

            if (line.Properties == null || !line.Properties.ContainsKey("Structural"))
                obj = new GSAElement();
            else
            { 
                Dictionary<string, object> dict = line.Properties["Structural"] as Dictionary<string, object>;

                switch (dict["GSAEntity"] as string)
                {
                    case "ELEMENT":
                        obj = new GSA1DElement();
                        break;
                    case "LINE":
                        obj = new GSALine();
                        break;
                    default:
                        return null;
                }

                obj.SetSpeckleProperties(line.Properties);
            }
            obj.Coor = line.Value;
            return obj;
        }

        public static GSALine ToNative(this SpeckleArc arc)
        {
            GSALine obj;

            if (arc.Properties == null || !arc.Properties.ContainsKey("Structural"))
            {
                //TODO: Need to discretize this arc into elements!
                obj = new GSALine();
                obj.Type = "ARC_THIRD_PT";
            }
            else
            { 
                Dictionary<string, object> dict = arc.Properties["Structural"] as Dictionary<string, object>;

                switch (dict["GSAEntity"] as string)
                {
                    case "LINE":
                        obj = new GSALine();
                        break;
                    default:
                        return null;
                }

                obj.SetSpeckleProperties(arc.Properties);
            }

            obj.Coor = SpeckleArctoArc3Point(arc).ToList();
            return obj;
        }

        public static object ToNative(this SpeckleMesh mesh)
        {
            if (mesh.Properties == null || !mesh.Properties.ContainsKey("Structural"))
            {
                List<GSA2DElement> elements = new List<GSA2DElement>();

                List<double[]> coor = new List<double[]>();
                for (int i = 0; i < mesh.Vertices.Count / 3; i++)
                    coor.Add(mesh.Vertices.ToArray().Skip(i * 3).Take(3).ToArray());

                int counter = 0;
                while (counter < mesh.Faces.Count)
                {
                    int val = mesh.Faces[counter++] + 3;
                    int coorCounter = 0;

                    GSA2DElement e = new GSA2DElement();

                    if (val == 3) e.Type = "TRI3";
                    else if (val == 4) e.Type = "QUAD4";

                    e.Coor.Clear();
                    while (coorCounter < val)
                    {
                        e.Coor.AddRange(coor[mesh.Faces[counter++]]);
                        coorCounter++;
                    }

                    elements.Add(e);
                }
                return elements.ToArray();
            }


            Dictionary<string, object> dict = mesh.Properties["Structural"] as Dictionary<string, object>;

            switch (dict["GSAEntity"] as string)
            {
                case "ELEMENTMESH":
                    GSA2DElementMesh m = new GSA2DElementMesh();
                    m.SetSpeckleProperties(mesh.Properties);
                    m.Coor = mesh.Vertices;
                    m.Color = Math.Max(mesh.Colors[0], 0);

                    int elemCounter = 0;
                    for (int i = 0; i < mesh.Faces.Count(); i++)
                    {
                        i++;
                        List<int> conn = m.GetElemConnectivity(elemCounter++.ToString());

                        for(int j = 0; j < conn.Count(); j++)
                            m.nodeMapping[conn[j]] = mesh.Faces[i++];

                        i--;
                    }
                    return m;
                case "ELEMENT":
                    GSA2DElement e = new GSA2DElement();
                    e.SetSpeckleProperties(mesh.Properties);
                    e.Coor = mesh.Vertices;
                    e.Color = Math.Max(mesh.Colors[0], 0);
                    return e;
                case "AREA":
                    GSAArea a = new GSAArea();
                    a.SetSpeckleProperties(mesh.Properties);
                    a.Coor = mesh.Vertices;
                    a.Color = Math.Max(mesh.Colors[0],0);
                    return a;
                default:
                    return null;
            }
        }
        #endregion
    }
}
