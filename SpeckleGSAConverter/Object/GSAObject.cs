using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Interop.Gsa_9_0;
using System.Text.RegularExpressions;
using System.Drawing;
using System.Windows.Media.Media3D;

namespace SpeckleGSA
{
    public abstract class GSAObject
    {
        public string GSAEntity { get; set; }

        public int Reference { get; set; }
        public string Name { get; set; }
        public List<int> Connectivity { get; set; }

        public List<double> Coor;
        public object Color;

        public ComAuto gsa;

        public enum LineNumNodes
        {
            LINE=2,
            ARC_RADIUS=3 ,
            ARC_THIRD_PT=3
        };
        
        public enum ElementNumNodes
        {
            BAR=2 ,
            BEAM=2 ,
            BEAM3=3 ,
            BRICK20=20 ,
            BRICK8=8 ,
            CABLE=2 ,
            DAMPER=2 ,
            GRD_DAMPER=1 ,
            GRD_SPRING=1 ,
            LINK=2 ,
            MASS=1 ,
            QUAD4=4 ,
            QUAD8=8 ,
            ROD=2 ,
            SPACER=2 ,
            SRING=2 ,
            STRUT=2 ,
            TETRA10=10 ,
            TETRA4=4 ,
            TIE=2 ,
            TRI3=3 ,
            TRI6=6 ,
            WEDGE15=15 ,
            WEDGE6=6 
        };

        public GSAObject(string entity)
        {
            GSAEntity = entity;
            Reference = 0;
            Name = "";
            Color = null;
            Coor = new List<double>();
            Connectivity = new List<int>();

            gsa = null;
        }

        public abstract void ParseGWACommand(string command, GSAObject[] children = null);

        public abstract string GetGWACommand();

        public abstract List<GSAObject> GetChildren();
    }

    public static class HelperFunctions
    {
        #region Attach GSA
        public static GSAObject AttachGSA(this GSAObject obj, ComAuto gsa)
        {
            obj.gsa = gsa;
            return obj;
        }

        public static GSANode AttachGSA(this GSANode obj, ComAuto gsa)
        {
            obj.gsa = gsa;
            return obj;
        }

        public static GSALine AttachGSA(this GSALine obj, ComAuto gsa)
        {
            obj.gsa = gsa;
            return obj;
        }

        public static GSAArea AttachGSA(this GSAArea obj, ComAuto gsa)
        {
            obj.gsa = gsa;
            return obj;
        }

        public static GSARegion AttachGSA(this GSARegion obj, ComAuto gsa)
        {
            obj.gsa = gsa;
            return obj;
        }

        public static GSAElement AttachGSA(this GSAElement obj, ComAuto gsa)
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

        #region Axis
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

        public static Dictionary<string,object> EvaluateGSANodeAxis(this int axis, ComAuto gsaObj, double[] evalAtCoor = null)
        {
            // Returns unit vector of each X, Y, Z axis
            Dictionary<string, object> axisVectors = new Dictionary<string, object>();

            Vector3D x;
            Vector3D y;
            Vector3D z;

            switch (axis)
            {
                case 0:
                    // Global
                    axisVectors["X"] = new Dictionary<string, object> { { "x", 1 }, { "y", 0 }, { "z", 0 } };
                    axisVectors["Y"] = new Dictionary<string, object> { { "x", 0 }, { "y", 1 }, { "z", 0 } };
                    axisVectors["Z"] = new Dictionary<string, object> { { "x", 0 }, { "y", 0 }, { "z", 1 } };
                    return axisVectors;
                case -11:
                    // X elevation
                    axisVectors["X"] = new Dictionary<string, object> { { "x", 0 }, { "y", -1 }, { "z", 0 } };
                    axisVectors["Y"] = new Dictionary<string, object> { { "x", 0 }, { "y", 0 }, { "z", 1 } };
                    axisVectors["Z"] = new Dictionary<string, object> { { "x", -1 }, { "y", 0 }, { "z", 0 } };
                    return axisVectors;
                case -12:
                    // Y elevation
                    axisVectors["X"] = new Dictionary<string, object> { { "x", 1 }, { "y", 0 }, { "z", 0 } };
                    axisVectors["Y"] = new Dictionary<string, object> { { "x", 0 }, { "y", 0 }, { "z", 1 } };
                    axisVectors["Z"] = new Dictionary<string, object> { { "x", 0 }, { "y", -1 }, { "z", 0 } };
                    return axisVectors;
                case -14:
                    // Vertical
                    axisVectors["X"] = new Dictionary<string, object> { { "x", 0 }, { "y", 0 }, { "z", 1 } };
                    axisVectors["Y"] = new Dictionary<string, object> { { "x", 1 }, { "y", 0 }, { "z", 0 } };
                    axisVectors["Z"] = new Dictionary<string, object> { { "x", 0 }, { "y", 1 }, { "z", 0 } };
                    return axisVectors;
                case -13:
                    // Global cylindrical
                    x = new Vector3D(evalAtCoor[0], evalAtCoor[1], 0);
                    x.Normalize();
                    z = new Vector3D(0, 0, 1);
                    y = Vector3D.CrossProduct(z, x);

                    axisVectors["X"] = new Dictionary<string, object> { { "x", x.X }, { "y", x.Y }, { "z", x.Z } };
                    axisVectors["Y"] = new Dictionary<string, object> { { "x", y.X }, { "y", y.Y }, { "z", y.Z } };
                    axisVectors["Z"] = new Dictionary<string, object> { { "x", z.X }, { "y", z.Y }, { "z", z.Z } };
                    return axisVectors;
                default:
                    string res = gsaObj.GwaCommand("GET,AXIS," + axis.ToString());
                    string[] pieces = res.Split(new char[] { ',' });
                    if (pieces.Length < 13)
                    {
                        axisVectors["X"] = new Dictionary<string, object> { { "x", 1 }, { "y", 0 }, { "z", 0 } };
                        axisVectors["Y"] = new Dictionary<string, object> { { "x", 0 }, { "y", 1 }, { "z", 0 } };
                        axisVectors["Z"] = new Dictionary<string, object> { { "x", 0 }, { "y", 0 }, { "z", 1 } };
                        return axisVectors;
                    }
                    Vector3D origin = new Vector3D(Convert.ToDouble(pieces[4]), Convert.ToDouble(pieces[5]), Convert.ToDouble(pieces[6]));

                    Vector3D X = new Vector3D(Convert.ToDouble(pieces[7]), Convert.ToDouble(pieces[8]), Convert.ToDouble(pieces[9]));
                    X.Normalize();


                    Vector3D Yp = new Vector3D(Convert.ToDouble(pieces[10]), Convert.ToDouble(pieces[11]), Convert.ToDouble(pieces[12]));
                    Vector3D Z = Vector3D.CrossProduct(X, Yp);
                    Z.Normalize();

                    Vector3D Y = Vector3D.CrossProduct(Z, X);

                    Vector3D pos = new Vector3D(0,0,0);

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
                            axisVectors["X"] = new Dictionary<string, object> { { "x", X.X }, { "y", X.Y }, { "z", X.Z } };
                            axisVectors["Y"] = new Dictionary<string, object> { { "x", Y.X }, { "y", Y.Y }, { "z", Y.Z } };
                            axisVectors["Z"] = new Dictionary<string, object> { { "x", Z.X }, { "y", Z.Y }, { "z", Z.Z } };
                            return axisVectors;
                        case "CYL":
                            x = new Vector3D(pos.X, pos.Y, 0);
                            x.Normalize();
                            z = Z;
                            y = Vector3D.CrossProduct(Z, x);
                            y.Normalize();

                            axisVectors["X"] = new Dictionary<string, object> { { "x", x.X }, { "y", x.Y }, { "z", x.Z } };
                            axisVectors["Y"] = new Dictionary<string, object> { { "x", y.X }, { "y", y.Y }, { "z", y.Z } };
                            axisVectors["Z"] = new Dictionary<string, object> { { "x", z.X }, { "y", z.Y }, { "z", z.Z } };
                            return axisVectors;
                        case "SPH":
                            x = pos;
                            x.Normalize();
                            z = Vector3D.CrossProduct(Z, x);
                            z.Normalize();
                            y = Vector3D.CrossProduct(z, x);
                            z.Normalize();

                            axisVectors["X"] = new Dictionary<string, object> { { "x", x.X }, { "y", x.Y }, { "z", x.Z } };
                            axisVectors["Y"] = new Dictionary<string, object> { { "x", y.X }, { "y", y.Y }, { "z", y.Z } };
                            axisVectors["Z"] = new Dictionary<string, object> { { "x", z.X }, { "y", z.Y }, { "z", z.Z } };
                            return axisVectors;
                        default:
                            axisVectors["X"] = new Dictionary<string, object> { { "x", 1 }, { "y", 0 }, { "z", 0 } };
                            axisVectors["Y"] = new Dictionary<string, object> { { "x", 0 }, { "y", 1 }, { "z", 0 } };
                            axisVectors["Z"] = new Dictionary<string, object> { { "x", 0 }, { "y", 0 }, { "z", 1 } };
                            return axisVectors;
                    }
            }
        }

        public static Dictionary<string, object> EvaluateGSA1DElementAxis(this double[] coor, ComAuto gsaObj, double rotationAngle = 0, double[] orientationNode = null)
        {
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
            Matrix3D rotMat = RotationMatrix(x, rotationAngle*(Math.PI/180));
            y = Vector3D.Multiply(y, rotMat);
            z = Vector3D.Multiply(z, rotMat);

            axisVectors["X"] = new Dictionary<string, object> { { "x", x.X }, { "y", x.Y }, { "z", x.Z } };
            axisVectors["Y"] = new Dictionary<string, object> { { "x", y.X }, { "y", y.Y }, { "z", y.Z } };
            axisVectors["Z"] = new Dictionary<string, object> { { "x", z.X }, { "y", z.Y }, { "z", z.Z } };

            return axisVectors;
        }

        public static Dictionary<string, object> EvaluateGSA2DElementAxis(this double[] coor, ComAuto gsaObj, double rotationAngle = 0, int property = 0)
        {
            Dictionary<string, object> axisVectors = new Dictionary<string, object>();
            
            Vector3D x;
            Vector3D y;
            Vector3D z;

            List<Vector3D> nodes = new List<Vector3D>();

            for (int i = 0; i < coor.Length; i += 3)
                nodes.Add(new Vector3D(coor[i], coor[i + 1], coor[i + 2]));

            if (property.Is2DElementLocalAxis(gsaObj))
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
                y = Vector3D.CrossProduct(z, x);
                y.Normalize();

                x = new Vector3D(1, 0, 0);
                y = new Vector3D(0, 1, 0);
                x = Vector3D.Subtract(x, Vector3D.Multiply(Vector3D.DotProduct(x, z), z));
                
                y = Vector3D.Subtract(y, Vector3D.Multiply(Vector3D.DotProduct(y, z), z));
                
                if (x.Length == 0)
                {
                    x = new Vector3D(0, z.X > 0 ? -1 : 1, 0);
                    y = Vector3D.CrossProduct(z, x);
                }

                x.Normalize();
                y.Normalize();
            }

            //Rotation
            Matrix3D rotMat = RotationMatrix(z, rotationAngle * (Math.PI / 180));
            x = Vector3D.Multiply(x, rotMat);
            y = Vector3D.Multiply(y, rotMat);

            axisVectors["X"] = new Dictionary<string, object> { { "x", x.X }, { "y", x.Y }, { "z", x.Z } };
            axisVectors["Y"] = new Dictionary<string, object> { { "x", y.X }, { "y", y.Y }, { "z", y.Z } };
            axisVectors["Z"] = new Dictionary<string, object> { { "x", z.X }, { "y", z.Y }, { "z", z.Z } };
            
            return axisVectors;
        }

        public static bool Is2DElementLocalAxis(this int prop, ComAuto gsaObj)
        {
            string res = gsaObj.GwaCommand("GET,PROP_2D," + prop);

            if (res == null || res == "")
                return false;

            string[] pieces = res.ListSplit(",");

            return pieces[5] == "LOCAL";
        }

        public static double GetGSA1DElementAngle(this Dictionary<string,object> axis, ComAuto gsaObj)
        {
            Dictionary<string, object> X = axis["X"] as Dictionary<string, object>;
            Dictionary<string, object> Y = axis["Y"] as Dictionary<string, object>;
            Dictionary<string, object> Z = axis["Z"] as Dictionary<string, object>;

            Vector3D x = new Vector3D(X["x"].ToDouble(), X["y"].ToDouble(), X["z"].ToDouble());
            Vector3D y = new Vector3D(Y["x"].ToDouble(), Y["y"].ToDouble(), Y["z"].ToDouble());
            Vector3D z = new Vector3D(Z["x"].ToDouble(), Z["y"].ToDouble(), Z["z"].ToDouble());

            if (x.X == 0 & x.Y == 0)
            {
                // Column
                Vector3D Yglobal = new Vector3D(0, 1, 0);

                double angle = Math.Acos(Vector3D.DotProduct(Yglobal, y) / (Yglobal.Length * y.Length)) * (180/Math.PI);
                if (double.IsNaN(angle)) angle = 0;

                Vector3D signVector = Vector3D.CrossProduct(Yglobal, y);
                double sign = Vector3D.DotProduct(signVector, x);

                return angle * sign / Math.Abs(sign);
            }
            else
            {
                Vector3D Zglobal = new Vector3D(0, 0, 1);
                Vector3D Y0 = Vector3D.CrossProduct(Zglobal, x);
                double angle = Math.Acos(Vector3D.DotProduct(Y0, y) / (Y0.Length * y.Length)) * (180 / Math.PI);
                if (double.IsNaN(angle)) angle = 0;

                Vector3D signVector = Vector3D.CrossProduct(Y0, y);
                double sign = Vector3D.DotProduct(signVector, x);

                return sign >= 0 ? angle : 360-angle;
            }
        }

        public static double GetGSA2DElementAngle(this Dictionary<string, object> axis, ComAuto gsaObj)
        {
            return 0;
        }

        public static int AddAxistoGSA(this Dictionary<string, object> axis, ComAuto gsaObj)
        {
            Dictionary<string, object> X = axis["X"] as Dictionary<string, object>;
            Dictionary<string, object> Y = axis["Y"] as Dictionary<string, object>;
            Dictionary<string, object> Z = axis["Z"] as Dictionary<string, object>;

            if (X["x"].Equal(1) & X["y"].Equal(0) & X["z"].Equal(0) &
                Y["x"].Equal(0) & Y["y"].Equal(1) & Y["z"].Equal(0) &
                Z["x"].Equal(0) & Z["y"].Equal(0) & Z["z"].Equal(1))
            {
                return 0;
            }

            List<string> ls = new List<string>();

            int res = gsaObj.GwaCommand("HIGHEST,AXIS");

            ls.Add("AXIS");
            ls.Add((res + 1).ToString());
            ls.Add("");
            ls.Add("CART");

            ls.Add("0");
            ls.Add("0");
            ls.Add("0");
            
            ls.Add(X["x"].ToNumString());
            ls.Add(X["y"].ToNumString());
            ls.Add(X["z"].ToNumString());
            
            ls.Add(Y["x"].ToNumString());
            ls.Add(Y["y"].ToNumString());
            ls.Add(Y["z"].ToNumString());

            gsaObj.GwaCommand(string.Join(",", ls));

            return res + 1;
        }
        #endregion

        #region Mass
        public static double GetGSAMass(this GSAElement elem, ComAuto gsaObj)
        {
            string res = gsaObj.GwaCommand("GET,PROP_MASS," + elem.Property.ToString());
            string[] pieces = res.ListSplit(",");

            return Convert.ToDouble(pieces[5]);
        }

        public static int AddMasstoGSA(this double mass, ComAuto gsaObj)
        {
            List<string> ls = new List<string>();

            int res = gsaObj.GwaCommand("HIGHEST,PROP_MASS");

            ls.Add("SET");
            ls.Add("PROP_MASS.2");
            ls.Add((res + 1).ToString());
            ls.Add("");
            ls.Add("NO_RGB");
            ls.Add("GLOBAL");
            ls.Add(mass.ToString());
            ls.Add("0");
            ls.Add("0");
            ls.Add("0");
            ls.Add("0");
            ls.Add("0");
            ls.Add("0");

            ls.Add("MOD");
            ls.Add("100%");
            ls.Add("100%");
            ls.Add("100%");

            gsaObj.GwaCommand(string.Join(",", ls));

            return res + 1;
        }

        #endregion

        #region Lists
        public static string[] ListSplit(this string list, string delimiter)
        {
            return Regex.Split(list, delimiter + "(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
        }

        public static int[] ConvertGSAList(this string list, ComAuto gsaObj)
        {
            list = list.Trim(new char[] { '"' });

            string res = gsaObj.GwaCommand("GET,LIST," + list);

            string[] pieces = res.Split(new char[] { ',' });

            return pieces[pieces.Length - 1].ParseGSAList(gsaObj);
        }

        public static int[] ParseGSAList(this string list, ComAuto gsaObj)
        {
            if (list == null) return new int[0];

            string[] pieces = list.ListSplit(" ");
            pieces = pieces.Where(s => !string.IsNullOrEmpty(s)).ToArray();

            List<int> items = new List<int>();
            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i].Contains('"'))
                    items.AddRange(pieces[i].ConvertGSAList(gsaObj));
                if (pieces[i] == "to")
                {
                    int lowerRange = Convert.ToInt32(pieces[i - 1]);
                    int upperRange = Convert.ToInt32(pieces[i + 1]);

                    for (int j = lowerRange + 1; j <= upperRange; j++)
                        items.Add(j);

                    i++;
                }
                else
                    items.Add(Convert.ToInt32(pieces[i]));
            }

            return items.ToArray();
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
                    rgbString.Substring(2,6),
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

        #region Element Type
        public static int ParseElementType(this string type)
        {
            return (int)((ElementType)Enum.Parse(typeof(ElementType), type));
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

        public static bool Equal(this object obj, double val)
        {
            if (obj.GetType() == typeof(int))
                return (int)obj == Math.Round(val);
            else if (obj.GetType() == typeof(double))
                return (double)obj == val;
            else
                return false;
        }

        public static int ParseLineNumNodes(this string type)
        {
            return (int)((GSAObject.LineNumNodes)Enum.Parse(typeof(GSAObject.LineNumNodes), type));
        }

        public static int ParseElementNumNodes(this string type)
        {
            return (int)((GSAObject.ElementNumNodes)Enum.Parse(typeof(GSAObject.ElementNumNodes), type));
        }
        #endregion
    }
}
