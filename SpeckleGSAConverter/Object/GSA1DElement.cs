using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace SpeckleGSA
{
    public class GSA1DElement : GSAObject
    {
        public string Type { get; set; }
        public int Property { get; set; }
        public Dictionary<string, object> Axis { get; set; }
        public Dictionary<string, object> Stiffness { get; set; }
        public Dictionary<string, object> InsertionPoint { get; set; }

        public GSA1DElement()
        {
            Type = "BEAM";
            Property = 1;
            Axis = new Dictionary<string, object>()
            {
                { "X", new Dictionary<string, object> { { "x", 1 }, { "y", 0 },{ "z", 0 }  } },
                { "Y", new Dictionary<string, object> { { "x", 0 }, { "y", 1 },{ "z", 0 }  } },
                { "Z", new Dictionary<string, object> { { "x", 0 }, { "y", 0 },{ "z", 1 }  } },
            };
            Stiffness = new Dictionary<string, object>()
            {
                { "start", new Dictionary<string,object>()
                {
                    { "x", "FIXED" },
                    { "y", "FIXED" },
                    { "z", "FIXED" },
                    { "xx", "FIXED" },
                    { "yy", "FIXED" },
                    { "zz", "FIXED" },
                }
                },
                { "end", new Dictionary<string,object>()
                {
                    { "x", "FIXED" },
                    { "y", "FIXED" },
                    { "z", "FIXED" },
                    { "xx", "FIXED" },
                    { "yy", "FIXED" },
                    { "zz", "FIXED" },
                }
                },
            };
            InsertionPoint = new Dictionary<string, object>()
            {
                { "Vertical", "MID" },
                { "Horizontal", "MID" },
            };
        }

        #region GSAObject Functions
        public override void ParseGWACommand(string command, GSAObject[] children = null)
        {
            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier
            Reference = Convert.ToInt32(pieces[counter++]);
            Name = pieces[counter++].Trim(new char[] { '"' });
            Color = pieces[counter++].ParseGSAColor();
            Type = pieces[counter++];
            Property = Convert.ToInt32(pieces[counter++]);
            counter++; // Group

            Connectivity.Clear();
            Coor.Clear();
            for (int i = 0; i < 2; i++)
            {
                Connectivity.Add(Convert.ToInt32(pieces[counter++]));
                Coor.AddRange(children.Where(n => n.Reference == Connectivity[i]).FirstOrDefault().Coor);
            }

            int orientationNodeRef = Convert.ToInt32(pieces[counter++]);
            double rotationAngle = Convert.ToDouble(pieces[counter++]);

            if (orientationNodeRef != 0)
                Axis = Parse1DElementAxis(Coor.ToArray(),
                    rotationAngle,
                    children.Where(n => n.Reference == orientationNodeRef).FirstOrDefault().Coor.ToArray());
            else
                Axis = Parse1DElementAxis(Coor.ToArray(), rotationAngle);


            if (pieces[counter++] != "NO_RLS")
            {
                string start = pieces[counter++];
                string end = pieces[counter++];
                (Stiffness["start"] as Dictionary<string, object>)["x"] = ParseEndStiffness(start[0], pieces, ref counter);
                (Stiffness["start"] as Dictionary<string, object>)["y"] = ParseEndStiffness(start[1], pieces, ref counter);
                (Stiffness["start"] as Dictionary<string, object>)["z"] = ParseEndStiffness(start[2], pieces, ref counter);
                (Stiffness["start"] as Dictionary<string, object>)["xx"] = ParseEndStiffness(start[3], pieces, ref counter);
                (Stiffness["start"] as Dictionary<string, object>)["yy"] = ParseEndStiffness(start[4], pieces, ref counter);
                (Stiffness["start"] as Dictionary<string, object>)["zz"] = ParseEndStiffness(start[5], pieces, ref counter);
                (Stiffness["end"] as Dictionary<string, object>)["x"] = ParseEndStiffness(end[0], pieces, ref counter);
                (Stiffness["end"] as Dictionary<string, object>)["y"] = ParseEndStiffness(end[1], pieces, ref counter);
                (Stiffness["end"] as Dictionary<string, object>)["z"] = ParseEndStiffness(end[2], pieces, ref counter);
                (Stiffness["end"] as Dictionary<string, object>)["xx"] = ParseEndStiffness(end[3], pieces, ref counter);
                (Stiffness["end"] as Dictionary<string, object>)["yy"] = ParseEndStiffness(end[4], pieces, ref counter);
                (Stiffness["end"] as Dictionary<string, object>)["zz"] = ParseEndStiffness(end[5], pieces, ref counter);
            }

            counter++; // offset x-start
            counter++; // offset x-end

            InsertionPoint["Horizontal"] = Convert.ToDouble(pieces[counter++]);
            InsertionPoint["Vertical"] = Convert.ToDouble(pieces[counter++]);

            //counter++; // Action // TODO: EL.4 SUPPORT
            counter++; // Dummy
        }

        public override string GetGWACommand(GSAObject[] children = null)
        {
            List<string> ls = new List<string>();

            ls.Add("SET");
            ls.Add("EL.3");
            ls.Add(Reference.ToString());
            ls.Add(Name);
            if (Color == null)
                ls.Add("NO_RGB");
            else
                ls.Add(Color.ToNumString());
            ls.Add(Type);
            ls.Add(Property.ToString());
            ls.Add("0"); // Group
            foreach (int c in Connectivity)
                ls.Add(c.ToString());

            ls.Add("0"); // Orientation Node
            ls.Add(GetGSA1DElementAngle(Axis).ToString());

            if (Coor.Count() / 3 == 2)
            {
                ls.Add("RLS");

                string start = "";
                string end = "";
                List<double> stiffness = new List<double>();

                start += GetEndStiffness((Stiffness["start"] as Dictionary<string, object>)["x"], ref stiffness);
                start += GetEndStiffness((Stiffness["start"] as Dictionary<string, object>)["y"], ref stiffness);
                start += GetEndStiffness((Stiffness["start"] as Dictionary<string, object>)["z"], ref stiffness);
                start += GetEndStiffness((Stiffness["start"] as Dictionary<string, object>)["xx"], ref stiffness);
                start += GetEndStiffness((Stiffness["start"] as Dictionary<string, object>)["yy"], ref stiffness);
                start += GetEndStiffness((Stiffness["start"] as Dictionary<string, object>)["zz"], ref stiffness);

                end += GetEndStiffness((Stiffness["end"] as Dictionary<string, object>)["x"], ref stiffness);
                end += GetEndStiffness((Stiffness["end"] as Dictionary<string, object>)["y"], ref stiffness);
                end += GetEndStiffness((Stiffness["end"] as Dictionary<string, object>)["z"], ref stiffness);
                end += GetEndStiffness((Stiffness["end"] as Dictionary<string, object>)["xx"], ref stiffness);
                end += GetEndStiffness((Stiffness["end"] as Dictionary<string, object>)["yy"], ref stiffness);
                end += GetEndStiffness((Stiffness["end"] as Dictionary<string, object>)["zz"], ref stiffness);

                ls.Add(start);
                ls.Add(end);

                foreach (double d in stiffness)
                    ls.Add(d.ToString());
            }
            else
                ls.Add("NO_RLS");

            ls.Add("0"); // Offset x-start
            ls.Add("0"); // Offset x-end

            ls.Add(InsertionPoint["Horizontal"].ToNumString());
            ls.Add(InsertionPoint["Vertical"].ToNumString());

            //ls.Add("NORMAL"); // Action // TODO: EL.4 SUPPORT
            ls.Add(""); // Dummy

            return string.Join(",", ls);
        }

        public override List<GSAObject> GetChildren()
        {
            List<GSAObject> children = new List<GSAObject>();

            for (int i = 0; i < Coor.Count() / 3; i++)
            {
                GSANode n = new GSANode();
                n.Coor = Coor.Skip(i * 3).Take(3).ToList();
                n.Reference = Connectivity[i];
                children.Add(n);
            }

            return children;
        }
        #endregion

        #region Axis
        private Dictionary<string, object> Parse1DElementAxis(double[] coor, double rotationAngle = 0, double[] orientationNode = null)
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
            Matrix3D rotMat = HelperFunctions.RotationMatrix(x, rotationAngle * (Math.PI / 180));
            y = Vector3D.Multiply(y, rotMat);
            z = Vector3D.Multiply(z, rotMat);

            axisVectors["X"] = new Dictionary<string, object> { { "x", x.X }, { "y", x.Y }, { "z", x.Z } };
            axisVectors["Y"] = new Dictionary<string, object> { { "x", y.X }, { "y", y.Y }, { "z", y.Z } };
            axisVectors["Z"] = new Dictionary<string, object> { { "x", z.X }, { "y", z.Y }, { "z", z.Z } };

            return axisVectors;
        }

        private double GetGSA1DElementAngle(Dictionary<string, object> axis)
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

                double angle = Math.Acos(Vector3D.DotProduct(Yglobal, y) / (Yglobal.Length * y.Length)) * (180 / Math.PI);
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

                return sign >= 0 ? angle : 360 - angle;
            }
        }
        #endregion

        #region Helper Functions
        private object ParseEndStiffness(char code, string[] pieces, ref int counter)
        {
            switch (code)
            {
                case 'F':
                    return "FIXED";
                case 'R':
                    return 0;
                default:
                    return Convert.ToDouble(pieces[counter++]);
            }
        }

        private string GetEndStiffness(object code, ref List<double> stiffness)
        {
            if (code.GetType() == typeof(string)) return "F";
            else if ((double)code == 0) return "R";
            else
            {
                stiffness.Add((double)code);
                return "K";
            }
        }
        #endregion

    }
}
