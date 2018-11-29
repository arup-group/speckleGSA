using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpeckleCore;
using System.Windows.Media.Media3D;
using System.Reflection;

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

        public static Matrix3D RotationMatrix(Vector3D zUnitVector, double angle)
        {
            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);
            
            //return new Matrix3D(
            //    cos + Math.Pow(zUnitVector.X, 2) * (1 - cos),
            //    zUnitVector.X * zUnitVector.Y * (1 - cos) - zUnitVector.Z * sin,
            //    zUnitVector.X * zUnitVector.Z * (1 - cos) + zUnitVector.Y * sin,
            //    0,
            //    zUnitVector.Y * zUnitVector.X * (1 - cos) + zUnitVector.Z * sin,
            //    cos + Math.Pow(zUnitVector.Y, 2) * (1 - cos),
            //    zUnitVector.Y * zUnitVector.Z * (1 - cos) - zUnitVector.X * sin,
            //    0,
            //    zUnitVector.Z * zUnitVector.X * (1 - cos) - zUnitVector.Y * sin,
            //    zUnitVector.Z * zUnitVector.Y * (1 - cos) + zUnitVector.X * sin,
            //    cos + Math.Pow(zUnitVector.Z, 2) * (1 - cos),
            //    0,
            //    0, 0, 0, 1
            //);

            // TRANSPOSED MATRIX TO ACCOMODATE MULTIPLY FUNCTION
            return new Matrix3D(
                cos + Math.Pow(zUnitVector.X, 2) * (1 - cos),
                zUnitVector.Y * zUnitVector.X * (1 - cos) + zUnitVector.Z * sin,
                zUnitVector.Z * zUnitVector.X * (1 - cos) - zUnitVector.Y * sin,
                0,

                zUnitVector.X * zUnitVector.Y * (1 - cos) - zUnitVector.Z * sin,
                cos + Math.Pow(zUnitVector.Y, 2) * (1 - cos),
                zUnitVector.Z * zUnitVector.Y * (1 - cos) + zUnitVector.X * sin,
                0,

                zUnitVector.X * zUnitVector.Z * (1 - cos) + zUnitVector.Y * sin,
                zUnitVector.Y * zUnitVector.Z * (1 - cos) - zUnitVector.X * sin,
                cos + Math.Pow(zUnitVector.Z, 2) * (1 - cos),
                0,
                
                0, 0, 0, 1
            );
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
                originRotMat = RotationMatrix(v3, theta);
            else
                originRotMat = RotationMatrix(Vector3D.Multiply(-1, v3), theta);

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


                Matrix3D reverseRotation = RotationMatrix(new Vector3D(1,0,0), Math.PI);

                startVector = Vector3D.Multiply(startVector, reverseRotation);
                endVector = Vector3D.Multiply(endVector, reverseRotation);
            }
            else
            {
                Vector3D unitReverseRotationvector = Vector3D.CrossProduct(v3, new Vector3D(0, 0, 1));
                unitReverseRotationvector.Normalize();

                Matrix3D reverseRotation = RotationMatrix(unitReverseRotationvector, Vector3D.AngleBetween(v3, new Vector3D(0, 0, 1)).ToRadians());

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
            Matrix3D rotation = RotationMatrix(unitRotationvector, Vector3D.AngleBetween(v3, new Vector3D(0, 0, 1)).ToRadians());

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

        public static Dictionary<string,object> ToSpeckle(this Array arr)
        {
            if (arr == null) return null;

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

        public static Dictionary<string,object> ToSpeckle(this Dictionary<string, object> dict)
        {
            if (dict == null) return null;

            var speckleDict = new Dictionary<string, object>();
            foreach (string key in dict.Keys)
            {
                object value = dict[key];
                Type t = value.GetType();

                if (t.IsArray)
                    value = ((Array)value).ToSpeckle();
                else if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                    value = (value as Dictionary<string, object>).ToSpeckle();
                speckleDict.Add(key, value);
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

                if (key == "Color") continue;
                if (key == "Coor") continue;

                object value = prop.GetValue(obj, null);
                Type t = value.GetType();

                if (t.IsArray)
                    value = ((Array)value).ToSpeckle();
                else if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                    value = (value as Dictionary<string, object>).ToSpeckle();
                speckleDict.Add(key, value);
            }
            return speckleDict;
        }
        
        public static void SetSpeckleProperties(this object obj, Dictionary<string, object> dict)
        {
            if (obj == null) { return; }
            foreach (var prop in obj.GetType().GetProperties())
            {
                string key = prop.Name;

                if (key == "Color") continue;
                if (key == "Coor") continue;

                if (!dict.ContainsKey(key)) continue;

                object value = dict[key];

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
        public static SpecklePoint ToSpeckle(this GSANode node)
        {
            return new SpecklePoint(node.Coor[0], node.Coor[1], node.Coor[2], node.Ref.ToString(), node.GetSpeckleProperties());
        }
        
        public static SpeckleObject ToSpeckle(this GSAElement element)
        {
            switch (element.NumTopo)
            {
                case 1:
                    return new SpecklePoint(
                        element.Coor[0],
                        element.Coor[1],
                        element.Coor[2],
                        element.Ref.ToString(),
                        element.GetSpeckleProperties());
                case 2:
                    return new SpeckleLine(
                        element.Coor,
                        element.Ref.ToString(),
                        element.GetSpeckleProperties());
                default:
                    if (element.Color == null)
                    {
                        var color = System.Drawing.Color.FromArgb(255,
                            100, 100, 100);
                        return new SpeckleMesh(
                        element.Coor,
                        new int[] { element.NumTopo - 3 }.Concat(
                            Enumerable.Range(0, element.NumTopo).ToArray())
                            .ToArray(),
                        Enumerable.Repeat(
                            color.ToArgb(),
                            element.NumTopo).ToArray(),
                        null,
                        element.Ref.ToString(),
                        element.GetSpeckleProperties());
                    }
                    else
                    {
                        var color = System.Drawing.Color.FromArgb(255,
                           (int)element.Color % 256,
                           ((int)element.Color / 256) % 256,
                           ((int)element.Color / 256 / 256) % 256);
                        return new SpeckleMesh(
                            element.Coor,
                            new int[] { element.NumTopo - 3 }.Concat(
                                Enumerable.Range(0, element.NumTopo).ToArray())
                                .ToArray(),
                            Enumerable.Repeat(
                                color.ToArgb(),
                                element.NumTopo).ToArray(),
                            null,
                            element.Ref.ToString(),
                            element.GetSpeckleProperties());
                    }
            }
        }

        public static SpeckleObject ToSpeckle(this GSALine line)
        {
            switch(line.Type)
            {
                case "LINE":
                    return new SpeckleLine(
                        line.Coor,
                        line.Ref.ToString(),
                        line.GetSpeckleProperties());
                case "ARC_RADIUS":
                    SpeckleArc arcR = ArcRadiustoSpeckleArc(line.Coor, line.Radius);
                    arcR.ApplicationId = line.Ref.ToString();
                    arcR.Properties = line.GetSpeckleProperties();
                    return arcR;
                case "ARC_THIRD_PT":
                    SpeckleArc arc3 = Arc3PointtoSpeckleArc(line.Coor);
                    arc3.ApplicationId = line.Ref.ToString();
                    arc3.Properties = line.GetSpeckleProperties();
                    return arc3;
                default:
                    return null;
            }
        }

        public static SpeckleMesh ToSpeckle(this GSAArea area)
        {
            //SpecklePolycurve curve = new SpecklePolycurve();
            //curve.ApplicationId = area.Ref.ToString();
            //curve.Segments = area.GetCurves().Select(c => c.ToSpeckle()).ToList();
            //curve.Properties = area.GetSpeckleProperties();
            //curve.GenerateHash();

            //return curve;

            SpeckleMesh mesh = new SpeckleMesh();
            mesh.Vertices = area.Coor.ToList();
            mesh.Faces = (new int[] { (int)(area.Coor.Length / 3) - 3 }.Concat(
                    Enumerable.Range(0, (int)(area.Coor.Length / 3)).ToArray())).ToList();
            mesh.ApplicationId = area.Ref.ToString();
            if (area.Color == null)
            {
                var color = System.Drawing.Color.FromArgb(255,
                    100, 100, 100);
                mesh.Colors = Enumerable.Repeat(
                    color.ToArgb(),
                    (int)(area.Coor.Length / 3)).ToList();
            }
            else
            {
                var color = System.Drawing.Color.FromArgb(255,
                   (int)area.Color % 256,
                   ((int)area.Color / 256) % 256,
                   ((int)area.Color / 256 / 256) % 256);
                mesh.Colors = Enumerable.Repeat(
                    color.ToArgb(),
                    (int)(area.Coor.Length / 3)).ToList();
            }
            mesh.Properties = area.GetSpeckleProperties();
            return mesh;

            //if (area.Color == null)
            //{
            //    var color = System.Drawing.Color.FromArgb(255,
            //        100, 100, 100);
            //    return new SpeckleMesh(
            //    area.Coor,
            //    new int[] { (int)(area.Coor.Length / 3) - 3 }.Concat(
            //        Enumerable.Range(0, (int)(area.Coor.Length / 3)).ToArray())
            //        .ToArray(),
            //    Enumerable.Repeat(
            //        color.ToArgb(),
            //        (int)(area.Coor.Length / 3)).ToArray(),
            //    null,
            //    area.Ref.ToString(),
            //    area.GetSpeckleProperties());
            //}
            //else
            //{
            //    var color = System.Drawing.Color.FromArgb(255,
            //       (int)area.Color % 256,
            //       ((int)area.Color / 256) % 256,
            //       ((int)area.Color / 256 / 256) % 256);
            //    return new SpeckleMesh(
            //        area.Coor,
            //        new int[] { (int)(area.Coor.Length / 3) - 3 }.Concat(
            //            Enumerable.Range(0, (int)(area.Coor.Length / 3)).ToArray())
            //            .ToArray(),
            //        Enumerable.Repeat(
            //            color.ToArgb(),
            //            (int)(area.Coor.Length / 3)).ToArray(),
            //        null,
            //        area.Ref.ToString(),
            //        area.GetSpeckleProperties());
            //}
        }
        #endregion

        #region
        public static GSAObject ToNative(this SpecklePoint point)
        {
            if (point.Properties==null)
            {
                GSANode n = new GSANode();
                n.Coor = point.Value.ToArray();
                return n;
            }

            if(!point.Properties.ContainsKey("GSAEntity"))
            {
                GSANode n = new GSANode();
                n.SetSpeckleProperties(point.Properties);
                n.Coor = point.Value.ToArray();
                return n;
            }

            switch(point.Properties["GSAEntity"])
            {
                case "NODE":
                    GSANode n = new GSANode();
                    n.SetSpeckleProperties(point.Properties);
                    n.Coor = point.Value.ToArray();
                    return n;
                case "ELEMENT":
                    GSAElement e = new GSAElement();
                    e.SetSpeckleProperties(point.Properties);
                    e.Coor = point.Value.ToArray();
                    return e;
                default:
                    return null;
            }
        }

        public static GSAObject ToNative(this SpeckleLine line)
        {
            if (line.Properties == null)
            {
                GSALine l = new GSALine();
                l.Coor = line.Value.ToArray();
                return l;
            }

            if (!line.Properties.ContainsKey("GSAEntity"))
            {
                GSALine l = new GSALine();
                l.Coor = line.Value.ToArray();
                return l;
            }

            switch (line.Properties["GSAEntity"])
            {
                case "ELEMENT":
                    GSAElement e = new GSAElement();
                    e.SetSpeckleProperties(line.Properties);
                    e.Coor = line.Value.ToArray();
                    return e;
                default:
                    return null;
            }
        }

        public static object ToNative(this SpeckleMesh mesh)
        {
            if (mesh.Properties == null || !mesh.Properties.ContainsKey("GSAEntity"))
            {
                List<GSAArea> areas = new List<GSAArea>();

                List<double[]> coor = new List<double[]>();
                for (int i = 0; i < mesh.Vertices.Count / 3; i++)
                    coor.Add(mesh.Vertices.ToArray().Skip(i * 3).Take(3).ToArray());

                int counter = 0;
                while (counter < mesh.Faces.Count)
                {
                    int val = mesh.Faces[counter++] + 3;
                    int coorCounter = 0;

                    GSAArea a = new GSAArea();
                    List<double> aCoor = new List<double>();
                    while (coorCounter < val)
                    {
                        aCoor.AddRange(coor[mesh.Faces[counter++]]);
                        coorCounter++;
                    }
                    a.Coor = aCoor.ToArray();
                    a.Lines = new int[aCoor.Count / 3];
                    areas.Add(a);
                }
                return areas.ToArray();
            }


            switch (mesh.Properties["GSAEntity"])
            {
                case "ELEMENT":
                    GSAElement e = new GSAElement();
                    e.SetSpeckleProperties(mesh.Properties);
                    e.Coor = mesh.Vertices.ToArray();
                    e.Color = mesh.Colors[0];
                    return e;
                default:
                    return null;
            }
        }
        #endregion
    }
}
