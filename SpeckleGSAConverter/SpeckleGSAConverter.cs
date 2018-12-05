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

                Matrix3D reverseRotation = RotationMatrix(new Vector3D(1, 0, 0), Math.PI);

                p1 = Vector3D.Multiply(p1, reverseRotation);
                p2 = Vector3D.Multiply(p2, reverseRotation);
                p3 = Vector3D.Multiply(p3, reverseRotation);
            }
            else
            { 
                Vector3D unitRotationvector = Vector3D.CrossProduct(new Vector3D(0, 0, 1), v3);
                unitRotationvector.Normalize();
                Matrix3D rotation = RotationMatrix(unitRotationvector, Vector3D.AngleBetween(v3, new Vector3D(0, 0, 1)).ToRadians());

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

            var structuralDict = new Dictionary<string, object>();
            foreach(var prop in obj.GetType().GetProperties())
            {
                string key = prop.Name;
                
                object value = prop.GetValue(obj, null);
                Type t = value.GetType();

                if (t.IsArray)
                    value = ((Array)value).ToSpeckle();
                else if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                    value = (value as Dictionary<string, object>).ToSpeckle();
                structuralDict.Add(key, value);
            }

            var speckleDict = new Dictionary<string, object>()
            {
                { "Structural", structuralDict }
            };
            
            return speckleDict;
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
            SpecklePoint p = new SpecklePoint(node.Coor[0], node.Coor[1], node.Coor[2], "", node.GetSpeckleProperties());
            p._id = node.SpeckleID == ""? p._id: node.SpeckleID;
            node.SpeckleID = p._id;
            return p;
        }
        
        public static SpeckleObject ToSpeckle(this GSAElement element)
        {
            switch (element.Coor.Length/3)
            {
                case 1:
                    SpecklePoint p = new SpecklePoint(
                        element.Coor[0],
                        element.Coor[1],
                        element.Coor[2],
                        element.Ref.ToString(),
                        element.GetSpeckleProperties());
                    p._id = element.SpeckleID == "" ? p._id : element.SpeckleID;
                    element.SpeckleID = p._id;
                    return p;
                case 2:
                    SpeckleLine l = new SpeckleLine(
                        element.Coor,
                        element.Ref.ToString(),
                        element.GetSpeckleProperties());
                    l._id = element.SpeckleID == "" ? l._id : element.SpeckleID;
                    element.SpeckleID = l._id;
                    return l;
                default:
                    SpeckleMesh m = new SpeckleMesh(
                        element.Coor,
                        new int[] { element.Coor.Length / 3 - 3 }.Concat(
                            Enumerable.Range(0, element.Coor.Length / 3).ToArray())
                            .ToArray(),
                        Enumerable.Repeat(
                            element.Color.ToSpeckleColor(),
                            element.Coor.Length / 3).ToArray(),
                        null,
                        element.Ref.ToString(),
                        element.GetSpeckleProperties());
                    m._id = element.SpeckleID == "" ? m._id : element.SpeckleID;
                    element.SpeckleID = m._id;
                    return m;
            }
        }

        public static SpeckleObject ToSpeckle(this GSALine line)
        {
            switch(line.Type)
            {
                case "LINE":
                    SpeckleLine l = new SpeckleLine(
                        line.Coor,
                        line.Ref.ToString(),
                        line.GetSpeckleProperties());
                    l._id = line.SpeckleID == "" ? l._id : line.SpeckleID;
                    line.SpeckleID = l._id;
                    return l;
                case "ARC_RADIUS":
                    SpeckleArc arcR = ArcRadiustoSpeckleArc(line.Coor, line.Radius);
                    arcR.ApplicationId = line.Ref.ToString();
                    arcR.Properties = line.GetSpeckleProperties();
                    arcR.GenerateHash();
                    arcR._id = line.SpeckleID == "" ? arcR._id : line.SpeckleID;
                    line.SpeckleID = arcR._id;
                    return arcR;
                case "ARC_THIRD_PT":
                    SpeckleArc arc3 = Arc3PointtoSpeckleArc(line.Coor);
                    arc3.ApplicationId = line.Ref.ToString();
                    arc3.Properties = line.GetSpeckleProperties();
                    arc3.GenerateHash();
                    arc3._id = line.SpeckleID == "" ? arc3._id : line.SpeckleID;
                    line.SpeckleID = arc3._id;
                    return arc3;
                default:
                    return null;
            }
        }

        public static SpeckleMesh ToSpeckle(this GSAArea area)
        {
            SpeckleMesh mesh = new SpeckleMesh();
            mesh.Vertices = area.Coor.ToList();
            mesh.Faces = (new int[] { area.Coor.Length / 3 - 3 }.Concat(
                    Enumerable.Range(0, area.Coor.Length / 3).ToArray())).ToList();
            mesh.ApplicationId = area.Ref.ToString();
            mesh.Colors = Enumerable.Repeat(
                    area.Color.ToSpeckleColor(),
                    (int)(area.Coor.Length / 3)).ToList();
            mesh.Properties = area.GetSpeckleProperties();
            mesh.GenerateHash();
            mesh._id = area.SpeckleID == "" ? mesh._id : area.SpeckleID;
            area.SpeckleID = mesh._id;
            return mesh;
        }
        #endregion

        #region
        public static GSAObject ToNative(this SpecklePoint point)
        {
            if (point.Properties==null || !point.Properties.ContainsKey("Structural"))
            {
                GSANode n = new GSANode();
                n.Coor = point.Value.ToArray();
                n.SpeckleID = point._id;
                n.Name = point._id;
                return n;
            }

            Dictionary<string, object> dict = point.Properties["Structural"] as Dictionary<string, object>;

            switch(dict["GSAEntity"] as string)
            {
                case "NODE":
                    GSANode n = new GSANode();
                    n.SetSpeckleProperties(point.Properties);
                    n.Coor = point.Value.ToArray();
                    n.SpeckleID = point._id;
                    n.Name = point._id;
                    return n;
                case "ELEMENT":
                    GSAElement e = new GSAElement();
                    e.SetSpeckleProperties(point.Properties);
                    e.Coor = point.Value.ToArray();
                    e.SpeckleID = point._id;
                    e.Name = point._id;
                    return e;
                default:
                    return null;
            }
        }

        public static GSAObject ToNative(this SpeckleLine line)
        {
            if (line.Properties == null || !line.Properties.ContainsKey("Structural"))
            {
                GSALine l = new GSALine();
                l.Coor = line.Value.ToArray();
                l.SpeckleID = line._id;
                l.Name = line._id;
                return l;
            }

            Dictionary<string, object> dict = line.Properties["Structural"] as Dictionary<string, object>;

            switch (dict["GSAEntity"] as string)
            {
                case "ELEMENT":
                    GSAElement e = new GSAElement();
                    e.SetSpeckleProperties(line.Properties);
                    e.Coor = line.Value.ToArray();
                    e.SpeckleID = line._id;
                    e.Name = line._id;
                    return e;
                case "LINE":
                    GSALine l = new GSALine();
                    l.SetSpeckleProperties(line.Properties);
                    l.Coor = line.Value.ToArray();
                    l.SpeckleID = line._id;
                    l.Name = line._id;
                    return l;
                default:
                    return null;
            }
        }

        public static GSALine ToNative(this SpeckleArc arc)
        {
            if (arc.Properties == null || !arc.Properties.ContainsKey("Structural"))
            {
                GSALine l = new GSALine();
                l.Coor = SpeckleArctoArc3Point(arc);
                l.Type = "ARC_THIRD_PT";
                l.SpeckleID = arc._id;
                l.Name = arc._id;
                return l;
            }

            Dictionary<string, object> dict = arc.Properties["Structural"] as Dictionary<string, object>;

            switch (dict["GSAEntity"] as string)
            {
                case "LINE":
                    GSALine l = new GSALine();
                    l.SetSpeckleProperties(arc.Properties);
                    l.Coor = SpeckleArctoArc3Point(arc);
                    l.SpeckleID = arc._id;
                    l.Name = arc._id;
                    return l;
                default:
                    return null;
            }
        }

        public static object ToNative(this SpeckleMesh mesh)
        {
            if (mesh.Properties == null || !mesh.Properties.ContainsKey("Structural"))
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
                    a.Connectivity = new int[aCoor.Count / 3];
                    areas.Add(a);
                }
                return areas.ToArray();
            }


            Dictionary<string, object> dict = mesh.Properties["Structural"] as Dictionary<string, object>;

            switch (dict["GSAEntity"] as string)
            {
                case "ELEMENT":
                    GSAElement e = new GSAElement();
                    e.SetSpeckleProperties(mesh.Properties);
                    e.Coor = mesh.Vertices.ToArray();
                    e.Color = Math.Max(mesh.Colors[0], 0);
                    e.Name = mesh._id;
                    return e;
                case "AREA":
                    GSAArea a = new GSAArea();
                    a.SetSpeckleProperties(mesh.Properties);
                    a.Coor = mesh.Vertices.ToArray();
                    a.Color = Math.Max(mesh.Colors[0],0);
                    a.Name = mesh._id;
                    return a;
                default:
                    return null;
            }
        }
        #endregion
    }
}
