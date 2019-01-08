using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Interop.Gsa_9_0;
using System.Text.RegularExpressions;
using System.Windows.Media.Media3D;
using System.Drawing;
using SpeckleCore;
using System.Reflection;
using System.Collections;

namespace SpeckleGSA
{
    public static class HelperFunctions
    {
        public const double EPS = 1e-3;

        #region Enum
        public enum LineNumNodes
        {
            LINE = 2,
            ARC_RADIUS = 3,
            ARC_THIRD_PT = 3
        };

        public enum ElementNumNodes
        {
            BAR = 2,
            BEAM = 2,
            BEAM3 = 3,
            BRICK20 = 20,
            BRICK8 = 8,
            CABLE = 2,
            DAMPER = 2,
            GRD_DAMPER = 1,
            GRD_SPRING = 1,
            LINK = 2,
            MASS = 1,
            QUAD4 = 4,
            QUAD8 = 8,
            ROD = 2,
            SPACER = 2,
            SRING = 2,
            STRUT = 2,
            TETRA10 = 10,
            TETRA4 = 4,
            TIE = 2,
            TRI3 = 3,
            TRI6 = 6,
            WEDGE15 = 15,
            WEDGE6 = 6
        };

        #endregion

        #region Attach GSA
        public static GSAObject AttachGSA(this GSAObject obj, ComAuto gsa)
        {
            obj.gsa = gsa;
            return obj;
        }

        public static GSA0DLoad AttachGSA(this GSA0DLoad obj, ComAuto gsa)
        {
            obj.gsa = gsa;
            return obj;
        }

        public static GSA2DFaceLoad AttachGSA(this GSA2DFaceLoad obj, ComAuto gsa)
        {
            obj.gsa = gsa;
            return obj;
        }
        
        public static GSAMaterial AttachGSA(this GSAMaterial obj, ComAuto gsa)
        {
            obj.gsa = gsa;
            return obj;
        }

        public static GSA2DProperty AttachGSA(this GSA2DProperty obj, ComAuto gsa)
        {
            obj.gsa = gsa;
            return obj;
        }

        public static GSANode AttachGSA(this GSANode obj, ComAuto gsa)
        {
            obj.gsa = gsa;
            return obj;
        }

        public static GSA0DElement AttachGSA(this GSA0DElement obj, ComAuto gsa)
        {
            obj.gsa = gsa;
            return obj;
        }

        public static GSA1DElement AttachGSA(this GSA1DElement obj, ComAuto gsa)
        {
            obj.gsa = gsa;
            return obj;
        }

        public static GSA2DElement AttachGSA(this GSA2DElement obj, ComAuto gsa)
        {
            obj.gsa = gsa;
            return obj;
        }

        public static GSAMember AttachGSA(this GSAMember obj, ComAuto gsa)
        {
            obj.gsa = gsa;
            return obj;
        }
        #endregion

        #region Math
        public static double ToDegrees(this int radians)
        {
            return ((double)radians).ToDegrees();
        }

        public static double ToDegrees(this double radians)
        {
            return radians * (180 / Math.PI);
        }

        public static double ToRadians(this int degrees)
        {
            return ((double)degrees).ToRadians();
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

        public static double[] Centroid (this double[] coor)
        {
            double[] centroid = new double[3];

            int numNodes = 0;

            for (int i = 0; i < coor.Length; i+=3)
            {
                centroid[0] = coor[i];
                centroid[1] = coor[i+1];
                centroid[2] = coor[i+2];
                numNodes++;
            }

            centroid[0] /= numNodes;
            centroid[1] /= numNodes;
            centroid[2] /= numNodes;

            return centroid;
        }
        #endregion

        #region Arc Helper Methods
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

            Vector3D shiftToOrigin = Vector3D.Multiply(radius, Vector3D.Multiply(v1, originRotMat));

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


                Matrix3D reverseRotation = HelperFunctions.RotationMatrix(new Vector3D(1, 0, 0), Math.PI);

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
                (startAngle + endAngle) / 2 :
                (startAngle + endAngle) / 2 + Math.PI;

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
        #endregion

        #region Lists
        public static string[] ListSplit(this string list, string delimiter)
        {
            return Regex.Split(list, delimiter + "(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
        }

        public static int[] ParseGSAList(this string list, GsaEntity type, ComAuto gsaObj)
        {
            if (list == null) return new int[0];

            string[] pieces = list.ListSplit(" ");
            pieces = pieces.Where(s => !string.IsNullOrEmpty(s)).ToArray();

            List<int> items = new List<int>();
            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i].IsDigits())
                    items.Add(Convert.ToInt32(pieces[i]));
                else if (pieces[i].Contains('"'))
                    items.AddRange(pieces[i].ConvertNamedGSAList(type, gsaObj));
                else if (pieces[i] == "to")
                {
                    int lowerRange = Convert.ToInt32(pieces[i - 1]);
                    int upperRange = Convert.ToInt32(pieces[i + 1]);

                    for (int j = lowerRange + 1; j <= upperRange; j++)
                        items.Add(j);

                    i++;
                }
                else
                {
                    int[] entities = new int[0];
                    GsaEntity entType = type;
                    Console.WriteLine(gsaObj.EntitiesInList(pieces[i], ref entType, out entities));

                    items.AddRange(entities);
                }
            }

            return items.ToArray();
        }

        public static int[] ConvertNamedGSAList(this string list, GsaEntity type, ComAuto gsaObj)
        {
            list = list.Trim(new char[] { '"' });

            string res = gsaObj.GwaCommand("GET,LIST," + list);

            string[] pieces = res.Split(new char[] { ',' });

            return pieces[pieces.Length - 1].ParseGSAList(type, gsaObj);
        }
        #endregion

        #region Color
        public static object ParseGSAColor(this string str)
        {
            if (str.Contains("NO_RGB"))
                return null;

            if (str.Contains("RGB"))
            {
                string rgbString = str.Split(new char[] { '(', ')' })[1];
                if (rgbString.Contains(","))
                {
                    string[] rgbValues = rgbString.Split(',');
                    int hexVal = Convert.ToInt32(rgbValues[0])
                        + Convert.ToInt32(rgbValues[1]) * 256
                        + Convert.ToInt32(rgbValues[2]) * 256 * 256;
                    return hexVal;
                }
                else
                {
                    return Int32.Parse(
                    rgbString.Substring(2, 6),
                    System.Globalization.NumberStyles.HexNumber);
                }
            }

            string colStr = str.Replace('_', ' ').ToLower();
            colStr = System.Threading.Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(colStr);
            colStr = Regex.Replace(colStr, " ", "");

            Color col = Color.FromKnownColor((KnownColor)Enum.Parse(typeof(KnownColor), colStr));
            return col.R + col.G * 256 + col.B * 256 * 256;
        }

        public static int ToSpeckleColor(this object color)
        {
            if (color == null)
                return Color.FromArgb(255, 100, 100, 100).ToArgb();

            return Color.FromArgb(255,
                           (int)color % 256,
                           ((int)color / 256) % 256,
                           ((int)color / 256 / 256) % 256).ToArgb();
        }
        #endregion

        #region Conversion
        public static double ToDouble(this object obj)
        {
            if (obj.GetType() == typeof(int))
                return ((int)obj);
            else if (obj.GetType() == typeof(double))
                return ((double)obj);
            else
                return 0;
        }

        public static string ToNumString(this object obj)
        {
            if (obj.GetType() == typeof(int))
                return ((int)obj).ToString();
            else if (obj.GetType() == typeof(double))
                return ((double)obj).ToString();
            else
                return "0";
        }

        public static int ParseElementType(this string type)
        {
            return (int)((ElementType)Enum.Parse(typeof(ElementType), type));
        }

        public static int ParseLineNumNodes(this string type)
        {
            return (int)((LineNumNodes)Enum.Parse(typeof(LineNumNodes), type));
        }

        public static int ParseElementNumNodes(this string type)
        {
            return (int)((ElementNumNodes)Enum.Parse(typeof(ElementNumNodes), type));
        }

        public static double ConvertUnit(this double value, string originalDimension, string targetDimension)
        {
            if (originalDimension == targetDimension)
                return value;

            if (targetDimension == "m")
            {
                switch (originalDimension)
                {
                    case "mm":
                        return value / 1000;
                    case "cm":
                        return value / 100;
                    case "ft":
                        return value / 3.281;
                    case "in":
                        return value / 39.37;
                    default:
                        return value;
                }
            }
            else if (originalDimension == "m")
            {
                switch (targetDimension)
                {
                    case "mm":
                        return value * 1000;
                    case "cm":
                        return value * 100;
                    case "ft":
                        return value * 3.281;
                    case "in":
                        return value * 39.37;
                    default:
                        return value;
                }
            }
            else
                return value.ConvertUnit(originalDimension, "m").ConvertUnit("m", targetDimension);

        }

        public static bool IsList(this PropertyInfo prop)
        {
            if (prop == null) return false;
            return prop.PropertyType.IsGenericType &&
                   prop.PropertyType.GetGenericTypeDefinition().IsAssignableFrom(typeof(List<>));
        }

        public static bool IsDictionary(this PropertyInfo prop)
        {
            if (prop == null) return false;
            return prop.PropertyType.IsGenericType &&
                   prop.PropertyType.GetGenericTypeDefinition().IsAssignableFrom(typeof(Dictionary<,>));
        }

        public static Dictionary<string,object> GetPropertyDict(this object obj)
        {
            Dictionary<string, object> properties = new Dictionary<string, object>();

            foreach(var prop in obj.GetType().GetProperties())
            {
                if (!prop.CanWrite)
                    continue;

                string key = prop.Name;
                object value = prop.GetValue(obj, null);
                properties.Add(key, value);
            }

            return properties;
        }

        public static void SetPropertyDict(this object obj, Dictionary<string, object> properties)
        {
            foreach (var prop in obj.GetType().GetProperties())
            {
                if (!prop.CanWrite)
                    continue;

                if (!properties.ContainsKey(prop.Name)) continue;

                if (properties[prop.Name] == null) continue;
                
                try
                { 
                    if (prop.PropertyType.IsArray) 
                    {
                        Type subType = prop.PropertyType.GetElementType();

                        object value = (properties[prop.Name] as IEnumerable).Cast<object>()
                            .Select(o => Convert.ChangeType(o, subType)).ToArray();

                        if ((value as Array).Length > 0)
                            prop.SetValue(obj, value);
                    }
                    else if (prop.IsList())
                    {
                        Type subType = prop.PropertyType.GetGenericArguments()[0];

                        Type genericListType = typeof(List<>).MakeGenericType(subType);
                        IList value = (IList)Activator.CreateInstance(genericListType);

                        if (subType != typeof(object))
                            foreach (object o in (properties[prop.Name] as IList))
                                value.Add(Convert.ChangeType(o, subType));
                        else
                            foreach (object o in (properties[prop.Name] as IList))
                                value.Add(o);

                        if ((value as IList).Count > 0)
                            prop.SetValue(obj, value);
                    }
                    else if (prop.IsDictionary())
                    {
                        prop.SetValue(obj, properties[prop.Name]);
                    }
                    else
                    { 
                        object value = Convert.ChangeType(properties[prop.Name], prop.PropertyType);

                        if (value != null)
                            prop.SetValue(obj, value);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }
        #endregion

        #region Comparison

        public static bool IsDigits(this string str)
        {
            foreach (char c in str)
            {
                if (c < '0' || c > '9')
                    return false;
            }

            return true;
        }


        public static bool Equal(this object obj, double val)
        {
            if (obj.GetType() == typeof(int))
                return (int)obj == Math.Round(val);
            else if (obj.GetType() == typeof(double))
                return (double)obj == val;
            else
                return false;
        }

        public static bool IsCoincident(this GSANode n1, GSANode n2)
        {
            if (Math.Pow(n1.Coor[0] - n2.Coor[0],2) +
                Math.Pow(n1.Coor[1] - n2.Coor[1], 2) +
                Math.Pow(n1.Coor[2] - n2.Coor[2], 2) < Math.Pow(EPS,2))
                return true;
            else
                return false;
        }

        public static bool IsAxisEqual(this Dictionary<string, object> axis1, Dictionary<string, object> axis2)
        {
            //TODO: NEED TO IMPLEMENT EPS
            if (axis1.GetHashCode() == axis2.GetHashCode()) return true;
            return false;
        }
        #endregion
    }

    public static class GSARefCounters
    {
        private static Dictionary<string, int> counter = new Dictionary<string, int>();
        private static Dictionary<string, List<int>> refsUsed = new Dictionary<string, List<int>>();

        public static int TotalObjects
        {
            get
            {
                int total = 0;

                foreach (KeyValuePair<string, List<int>> kvp in refsUsed)
                    total += kvp.Value.Count();

                return total;
            }
        }

        public static void Clear()
        {
            counter.Clear();
            refsUsed.Clear();
        }

        public static GSAObject RefObject(GSAObject obj)
        {
            string key = (string)obj.GetType().GetField("GSAKeyword").GetValue(null);

            if (obj.Reference == 0)
            {
                if (!counter.ContainsKey(key))
                    counter[key] = 1;

                if (refsUsed.ContainsKey(key))
                    while (refsUsed[key].Contains(counter[key]))
                        counter[key]++;

                obj.Reference = counter[key]++;
            }

            AddObjRefs(key, new List<int>() { obj.Reference });
            return obj;
        }

        public static void AddObjRefs(string key, List<int> refs)
        {
            if (!refsUsed.ContainsKey(key))
                refsUsed[key] = refs;
            else
                refsUsed[key].AddRange(refs);

            refsUsed[key] = refsUsed[key].Distinct().ToList();
        }
    }
}
