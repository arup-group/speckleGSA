using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Interop.Gsa_10_0;
using System.Text.RegularExpressions;
using System.Windows.Media.Media3D;
using System.Drawing;
using SpeckleCore;
using System.Reflection;
using System.Collections;
using SpeckleStructures;

namespace SpeckleGSA
{
    public static class HelperFunctions
    {
        public const double EPS = 1e-16;

        #region GSA Num Nodes and Type Parsers
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

        public static bool MemberIs1D(this string type)
        {
            if (type == "1D_GENERIC" | type == "COLUMN" | type == "BEAM")
                return true;
            else
                return false;
        }

        public static bool MemberIs2D(this string type)
        {
            if (type == "2D_GENERIC" | type == "SLAB" | type == "WALL")
                return true;
            else
                return false;
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

        public static int[] ParseGSAList(this string list, GsaEntity type)
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
                    items.AddRange(pieces[i].ConvertNamedGSAList(type));
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
                    try
                    {
                        int[] itemTemp = new int[0];
                        GSA.GSAObject.EntitiesInList(pieces[i], type, out itemTemp);
                        items.AddRange(itemTemp);
                    }
                    catch
                    { }
                }
            }

            return items.ToArray();
        }

        public static int[] ConvertNamedGSAList(this string list, GsaEntity type)
        {
            list = list.Trim(new char[] { '"' });

            string res = (string)GSA.RunGWACommand("GET,LIST," + list);

            string[] pieces = res.Split(new char[] { ',' });

            return pieces[pieces.Length - 1].ParseGSAList(type);
        }

        public static int[] GetGroupsFromGSAList(this string list)
        {
            string[] pieces = list.ListSplit(" ");

            List<int> groups = new List<int>();

            foreach(string p in pieces)
                if (p.Length > 0 && p[0] == 'G')
                    groups.Add(Convert.ToInt32(p.Substring(1)));

            return groups.ToArray();
        }
        #endregion

        #region Color
        public static int? ParseGSAColor(this string str)
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
                    rgbString.Remove(0,2).PadLeft(6,'0'),
                    System.Globalization.NumberStyles.HexNumber);
                }
            }

            string colStr = str.Replace('_', ' ').ToLower();
            colStr = System.Threading.Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(colStr);
            colStr = Regex.Replace(colStr, " ", "");

            Color col = Color.FromKnownColor((KnownColor)Enum.Parse(typeof(KnownColor), colStr));
            return col.R + col.G * 256 + col.B * 256 * 256;
        }
        #endregion

        #region Axis
        public static Axis Parse0DAxis(int axis, double[] evalAtCoor = null)
        {
            Axis returnAxis = new Axis();

            Vector3D x;
            Vector3D y;
            Vector3D z;

            switch (axis)
            {
                case 0:
                    // Global
                    returnAxis.SetAxis(new double[3] { 1, 0, 0 },
                        new double[3] { 0, 1, 0 },
                        new double[3] { 0, 0, 1 });
                    return returnAxis;
                case -11:
                    // X elevation
                    returnAxis.SetAxis(new double[3] { 0, -1, 0 },
                        new double[3] { 0, 0, 1 },
                        new double[3] { -1, 0, 0 });
                    return returnAxis;
                case -12:
                    // Y elevation
                    returnAxis.SetAxis(new double[3] { 1, 0, 0 },
                        new double[3] { 0, 0, 1 },
                        new double[3] { 0, -1, 0 });
                    return returnAxis;
                case -14:
                    // Vertical
                    returnAxis.SetAxis(new double[3] { 0, 0, 1 },
                        new double[3] { 1, 0, 0 },
                        new double[3] { 0, 1, 0 });
                    return returnAxis;
                case -13:
                    // Global cylindrical
                    x = new Vector3D(evalAtCoor[0], evalAtCoor[1], 0);
                    x.Normalize();
                    z = new Vector3D(0, 0, 1);
                    y = Vector3D.CrossProduct(z, x);

                    returnAxis.SetAxis(x, y, z);
                    return returnAxis;
                default:
                    string res = (string)GSA.RunGWACommand("GET,AXIS," + axis.ToString());
                    string[] pieces = res.Split(new char[] { ',' });
                    if (pieces.Length < 13)
                    {
                        returnAxis.SetAxis(new double[3] { 1, 0, 0 },
                            new double[3] { 0, 1, 0 },
                            new double[3] { 0, 0, 1 });
                        return returnAxis;
                    }
                    Vector3D origin = new Vector3D(Convert.ToDouble(pieces[4]), Convert.ToDouble(pieces[5]), Convert.ToDouble(pieces[6]));

                    Vector3D X = new Vector3D(Convert.ToDouble(pieces[7]), Convert.ToDouble(pieces[8]), Convert.ToDouble(pieces[9]));
                    X.Normalize();


                    Vector3D Yp = new Vector3D(Convert.ToDouble(pieces[10]), Convert.ToDouble(pieces[11]), Convert.ToDouble(pieces[12]));
                    Vector3D Z = Vector3D.CrossProduct(X, Yp);
                    Z.Normalize();

                    Vector3D Y = Vector3D.CrossProduct(Z, X);

                    Vector3D pos = new Vector3D(0, 0, 0);

                    if (evalAtCoor == null)
                        pieces[3] = "CART";
                    else
                    {
                        pos = new Vector3D(evalAtCoor[0] - origin.X, evalAtCoor[1] - origin.Y, evalAtCoor[2] - origin.Z);
                        if (pos.Length == 0)
                            pieces[3] = "CART";
                    }

                    switch (pieces[3])
                    {
                        case "CART":
                            returnAxis.SetAxis(X, Y, Z);
                            return returnAxis;
                        case "CYL":
                            x = new Vector3D(pos.X, pos.Y, 0);
                            x.Normalize();
                            z = Z;
                            y = Vector3D.CrossProduct(Z, x);
                            y.Normalize();

                            returnAxis.SetAxis(x, y, z);
                            return returnAxis;
                        case "SPH":
                            x = pos;
                            x.Normalize();
                            z = Vector3D.CrossProduct(Z, x);
                            z.Normalize();
                            y = Vector3D.CrossProduct(z, x);
                            z.Normalize();

                            returnAxis.SetAxis(x, y, z);
                            return returnAxis;
                        default:
                            returnAxis.SetAxis(new double[3] { 1, 0, 0 },
                                new double[3] { 0, 1, 0 },
                                new double[3] { 0, 0, 1 });
                            return returnAxis;
                    }
            }
        }

        public static Axis Parse1DAxis(double[] coor, double rotationAngle = 0, double[] orientationNode = null)
        {
            Axis returnAxis = new Axis();

            Dictionary<string, object> axisVectors = new Dictionary<string, object>();

            Vector3D x;
            Vector3D y;
            Vector3D z;

            x = new Vector3D(coor[3] - coor[0], coor[4] - coor[1], coor[5] - coor[2]);
            x.Normalize();

            if (orientationNode == null)
            {
                if (x.X == 0 && x.Y == 0)
                {
                    //Column
                    y = new Vector3D(0, 1, 0);
                    z = Vector3D.CrossProduct(x, y);
                }
                else
                {
                    //Non-Vertical
                    Vector3D Z = new Vector3D(0, 0, 1);
                    y = Vector3D.CrossProduct(Z, x);
                    y.Normalize();
                    z = Vector3D.CrossProduct(x, y);
                    z.Normalize();
                }
            }
            else
            {
                Vector3D Yp = new Vector3D(orientationNode[0], orientationNode[1], orientationNode[2]);
                z = Vector3D.CrossProduct(x, Yp);
                z.Normalize();
                y = Vector3D.CrossProduct(z, x);
                y.Normalize();
            }

            //Rotation
            Matrix3D rotMat = HelperFunctions.RotationMatrix(x, rotationAngle.ToRadians());
            y = Vector3D.Multiply(y, rotMat);
            z = Vector3D.Multiply(z, rotMat);

            returnAxis.SetAxis(x, y, z);

            return returnAxis;
        }

        public static Axis Parse2DAxis(double[] coor, double rotationAngle = 0, bool isLocalAxis = false)
        {
            Axis returnAxis = new Axis();
            
            Vector3D x;
            Vector3D y;
            Vector3D z;

            List<Vector3D> nodes = new List<Vector3D>();

            for (int i = 0; i < coor.Length; i += 3)
                nodes.Add(new Vector3D(coor[i], coor[i + 1], coor[i + 2]));

            if (isLocalAxis)
            {
                if (nodes.Count == 3)
                {
                    x = Vector3D.Subtract(nodes[1], nodes[0]);
                    x.Normalize();
                    z = Vector3D.CrossProduct(x, Vector3D.Subtract(nodes[2], nodes[0]));
                    z.Normalize();
                    y = Vector3D.CrossProduct(z, x);
                    y.Normalize();
                }
                else if (nodes.Count == 4)
                {
                    x = Vector3D.Subtract(nodes[2], nodes[0]);
                    x.Normalize();
                    z = Vector3D.CrossProduct(x, Vector3D.Subtract(nodes[3], nodes[1]));
                    z.Normalize();
                    y = Vector3D.CrossProduct(z, x);
                    y.Normalize();
                }
                else
                {
                    // Default to QUAD method
                    x = Vector3D.Subtract(nodes[2], nodes[0]);
                    x.Normalize();
                    z = Vector3D.CrossProduct(x, Vector3D.Subtract(nodes[3], nodes[1]));
                    z.Normalize();
                    y = Vector3D.CrossProduct(z, x);
                    y.Normalize();
                }
            }
            else
            {
                x = Vector3D.Subtract(nodes[1], nodes[0]);
                x.Normalize();
                z = Vector3D.CrossProduct(x, Vector3D.Subtract(nodes[2], nodes[0]));
                z.Normalize();

                x = new Vector3D(1, 0, 0);
                x = Vector3D.Subtract(x, Vector3D.Multiply(Vector3D.DotProduct(x, z), z));

                if (x.Length == 0)
                    x = new Vector3D(0, z.X > 0 ? -1 : 1, 0);

                y = Vector3D.CrossProduct(z, x);

                x.Normalize();
                y.Normalize();
            }

            //Rotation
            Matrix3D rotMat = HelperFunctions.RotationMatrix(z, rotationAngle * (Math.PI / 180));
            x = Vector3D.Multiply(x, rotMat);
            y = Vector3D.Multiply(y, rotMat);

            returnAxis.SetAxis(x, y, z);

            return returnAxis;
        }

        public static double Get1DAngle(Axis axis)
        {
            if (axis.X.X == 0 & axis.X.Y == 0)
            {
                // Column
                Vector3D Yglobal = new Vector3D(0, 1, 0);

                double angle = Math.Acos(Vector3D.DotProduct(Yglobal, axis.Y) / (Yglobal.Length * axis.Y.Length)).ToDegrees();
                if (double.IsNaN(angle)) return 0;

                Vector3D signVector = Vector3D.CrossProduct(Yglobal, axis.Y);
                double sign = Vector3D.DotProduct(signVector, axis.X);

                return sign >= 0 ? angle : -angle;
            }
            else
            {
                Vector3D Zglobal = new Vector3D(0, 0, 1);
                Vector3D Y0 = Vector3D.CrossProduct(Zglobal, axis.X);
                double angle = Math.Acos(Vector3D.DotProduct(Y0, axis.Y) / (Y0.Length * axis.Y.Length)).ToDegrees();
                if (double.IsNaN(angle)) angle = 0;

                Vector3D signVector = Vector3D.CrossProduct(Y0, axis.Y);
                double sign = Vector3D.DotProduct(signVector, axis.X);

                return sign >= 0 ? angle : 360 - angle;
            }
        }

        public static double Get2DAngle(double[] coor, Axis axis)
        {
            Vector3D x0;
            Vector3D z0;

            List<Vector3D> nodes = new List<Vector3D>();

            for (int i = 0; i < coor.Length; i += 3)
                nodes.Add(new Vector3D(coor[i], coor[i + 1], coor[i + 2]));

            // Get 0 angle axis in GLOBAL coordinates
            x0 = Vector3D.Subtract(nodes[1], nodes[0]);
            x0.Normalize();
            z0 = Vector3D.CrossProduct(x0, Vector3D.Subtract(nodes[2], nodes[0]));
            z0.Normalize();

            x0 = new Vector3D(1, 0, 0);
            x0 = Vector3D.Subtract(x0, Vector3D.Multiply(Vector3D.DotProduct(x0, z0), z0));

            if (x0.Length == 0)
                x0 = new Vector3D(0, z0.X > 0 ? -1 : 1, 0);

            x0.Normalize();

            // Find angle
            double angle = Math.Acos(Vector3D.DotProduct(x0, axis.X) / (x0.Length * axis.X.Length)).ToDegrees();
            if (double.IsNaN(angle)) return 0;

            Vector3D signVector = Vector3D.CrossProduct(x0, axis.X);
            double sign = Vector3D.DotProduct(signVector, axis.Z);

            return sign >= 0 ? angle : -angle;
        }
        #endregion

        #region Unit Conversion
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

        public static string LongUnitName(this string unit)
        {
            switch (unit)
            {
                case "m":
                    return "Meters";
                case "mm":
                    return "Millimeters";
                case "cm":
                    return "Centimeters";
                case "ft":
                    return "Feet";
                case "in":
                    return "Inches";
                default:
                    return unit;
            }
        }

        public static string ShortUnitName(this string unit)
        {
            switch (unit)
            {
                case "Meters":
                    return "m";
                case "Millimeters":
                    return "mm";
                case "Centimeters":
                    return "cm";
                case "Feet":
                    return "ft";
                case "Inches":
                    return "in";
                default:
                    return unit;
            }
        }
        #endregion

        #region Comparison
        public static bool IsDigits(this string str)
        {
            foreach (char c in str)
                if (c < '0' || c > '9')
                    return false;

            return true;
        }
        #endregion

        #region Miscellanious
        public static List<GSANode> CollapseNodes(List<GSANode> mainList, List<GSANode> addList)
        {
            if (addList == null || addList.Count() == 0) return mainList;

            if (mainList == null || mainList.Count() == 0)
            {
                if (addList != null)
                    foreach (GSANode n in addList)
                        GSARefCounters.RefObject(n);
                mainList = new List<GSANode>(addList);
                return mainList;
            }

            foreach (GSANode newNode in addList)
            {
                GSANode match = mainList.Where(n => n.Coordinates.Equals(newNode.Coordinates)).FirstOrDefault();

                if (match != null)
                {
                    GSARefCounters.RefObject(match);
                    newNode.Reference = match.Reference;
                    match.Merge(newNode);
                }
                else
                {
                    GSARefCounters.RefObject(newNode);
                    mainList.Add(newNode);
                }
            }

            return mainList;
        }

        public static StructuralObject GetBase(this object obj)
        {
            StructuralObject baseClass = (StructuralObject)Activator.CreateInstance(obj.GetType().BaseType);

            foreach (FieldInfo f in baseClass.GetType().GetFields())
                f.SetValue(baseClass, f.GetValue(obj));

            foreach (PropertyInfo p in baseClass.GetType().GetProperties())
                p.SetValue(baseClass, p.GetValue(obj));

            return baseClass;
        }

        public static object GetAttribute(this object t, string attribute)
        {
            try
            { 
                if (t is Type)
                { 
                    GSAObject attObj = (GSAObject)Attribute.GetCustomAttribute((Type)t, typeof(GSAObject));
                    return typeof(GSAObject).GetProperty(attribute).GetValue(attObj);
                }
                else
                {
                    GSAObject attObj = (GSAObject)Attribute.GetCustomAttribute(t.GetType(), typeof(GSAObject));
                    return typeof(GSAObject).GetProperty(attribute).GetValue(attObj);
                }
            }
            catch { return null; }
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

        public static int RefObject(string key)
        {
            if (!counter.ContainsKey(key))
                counter[key] = 1;

            if (refsUsed.ContainsKey(key))
                while (refsUsed[key].Contains(counter[key]))
                    counter[key]++;

            return counter[key]++;
        }

        public static int RefObject(StructuralObject obj)
        {
            // Returns 0 if success else 1
            try
            {
                string key = (string)obj.GetAttribute("GSAKeyword");

                if (obj.Reference == 0)
                    obj.Reference = RefObject(key);
                else
                {
                    if (refsUsed[key].Contains(obj.Reference))
                        return 1;
                }

                AddObjRefs(key, new List<int>() { obj.Reference });
                return 0;
            }
            catch { throw new Exception("Unable to reference object type " + obj.GetType().Name); }
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
