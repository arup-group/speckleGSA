using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Interop.Gsa_10_0;
using SpeckleStructuresClasses;
using SpeckleCore;
using SpeckleCoreGeometryClasses;
using System.Reflection;

namespace SpeckleGSA
{
    [GSAObject("PROP_SEC.3", "properties", true, true, new Type[] { typeof(GSAMaterial) }, new Type[] { typeof(GSAMaterial) })]
    public class GSA1DProperty : Structural1DProperty, IGSAObject
    {
        public string GWACommand { get; set; } = "";
        public List<string> SubGWACommand { get; set; } = new List<string>();

        #region Sending Functions
        public static bool GetObjects(Dictionary<Type, List<IGSAObject>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType))
                dict[MethodBase.GetCurrentMethod().DeclaringType] = new List<IGSAObject>();

            List<GSA1DProperty> props = new List<GSA1DProperty>();
            List<GSAMaterial> mats = dict[typeof(GSAMaterial)].Cast<GSAMaterial>().ToList();

            string keyword = MethodBase.GetCurrentMethod().DeclaringType.GetGSAKeyword();

            string[] lines = GSA.GetGWAGetCommands("GET_ALL," + keyword);
            string[] deletedLines = GSA.GetDeletedGWAGetCommands("GET_ALL," + keyword);

            // Remove deleted lines
            dict[typeof(GSA1DProperty)].RemoveAll(l => deletedLines.Contains(l.GWACommand));
            foreach (KeyValuePair<Type, List<IGSAObject>> kvp in dict)
                kvp.Value.RemoveAll(l => l.SubGWACommand.Any(x => deletedLines.Contains(x)));

            // Filter only new lines
            string[] prevLines = dict[typeof(GSA1DProperty)].Select(l => l.GWACommand).ToArray();
            string[] newLines = lines.Where(l => !prevLines.Contains(l)).ToArray();

            foreach (string p in newLines)
            {
                GSA1DProperty prop = ParseGWACommand(p, mats);
                props.Add(prop);
            }

            dict[typeof(GSA1DProperty)].AddRange(props);

            if (props.Count() > 0 || deletedLines.Length > 0) return true;

            return false;
        }

        public static GSA1DProperty ParseGWACommand(string command, List<GSAMaterial> materials)
        {
            GSA1DProperty ret = new GSA1DProperty();

            ret.GWACommand = command;

            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier
            ret.StructuralId = pieces[counter++];
            ret.Name = pieces[counter++].Trim(new char[] { '"' });
            counter++; // Color
            string materialType = pieces[counter++];
            StructuralMaterialType materialTypeEnum = StructuralMaterialType.Generic;
            if (materialType == "STEEL")
                materialTypeEnum = StructuralMaterialType.Steel;
            else if (materialType == "CONCRETE")
                materialTypeEnum = StructuralMaterialType.Concrete;
            int materialGrade = Convert.ToInt32(pieces[counter++]);

            if (materials != null)
            {
                GSAMaterial matchingMaterial = materials.Where(m => m.LocalReference == materialGrade & m.MaterialType == materialTypeEnum).FirstOrDefault();
                ret.MaterialRef = matchingMaterial == null ? null : matchingMaterial.StructuralId;
                if (matchingMaterial != null)
                    ret.SubGWACommand.Add(matchingMaterial.GWACommand);
            }
            else
                ret.MaterialRef = null;

            counter++; // Analysis material
            ret = SetDesc(ret, pieces[counter++]);
            counter++; // Cost
            
            return ret;
        }
        #endregion

        #region Receiving Functions
        public static void SetObjects(Dictionary<Type, List<IStructural>> dict)
        {
            if (!dict.ContainsKey(typeof(Structural1DProperty))) return;

            foreach (IStructural obj in dict[typeof(Structural1DProperty)])
            {
                Set(obj as Structural1DProperty);
            }
        }

        public static void Set(Structural1DProperty prop)
        {
            if (prop == null)
                return;

            if (prop.Profile == null)
                return;

            string keyword = MethodBase.GetCurrentMethod().DeclaringType.GetGSAKeyword();

            int index = Indexer.ResolveIndex(MethodBase.GetCurrentMethod().DeclaringType, prop);
            int materialRef = 0;
            try
            {
                materialRef = Indexer.LookupIndex(typeof(GSAMaterial), prop.MaterialRef).Value;
            }
            catch { }

            List<string> ls = new List<string>();

            ls.Add("SET");
            ls.Add(keyword);
            ls.Add(index.ToString());
            ls.Add(prop.Name == null || prop.Name == "" ? " " : prop.Name);
            ls.Add("NO_RGB");
            ls.Add(GetMaterialType(materialRef));
            ls.Add(materialRef.ToString());
            ls.Add("0"); // Analysis material
            ls.Add(GetGSADesc(prop));
            ls.Add("0"); // Cost

            GSA.RunGWACommand(string.Join(",", ls));
        }

        public static string GetMaterialType(int materialRef)
        {
            // Steel
            if ((string)GSA.RunGWACommand("GET,MAT_STEEL.3," + materialRef.ToString()) != string.Empty) return "STEEL";

            // Concrete
            if ((string)GSA.RunGWACommand("GET,MAT_CONCRETE.16," + materialRef.ToString()) != string.Empty) return "CONCRETE";

            // Default
            return "UNDEF";
        }
        #endregion

        #region Helper Functions
        public static GSA1DProperty SetDesc(GSA1DProperty prop, string desc)
        {
            string[] pieces = desc.ListSplit("%");

            switch (pieces[0])
            {
                case "STD":
                    return SetStandardDesc(prop, desc);
                case "GEO":
                    return SetStandardDesc(prop, desc);
                default:
                    Status.AddError("Unsupported profile type " + pieces[0] + ".");
                    return prop;
            }
        }

        public static GSA1DProperty SetStandardDesc(GSA1DProperty prop, string desc)
        {
            string[] pieces = desc.ListSplit("%");

            string unit = Regex.Match(pieces[1], @"(?<=()(.*)(?=))").Value;
            if (unit == "") unit = "mm";

            if (pieces[1] == "R")
            {
                // Rectangle
                double height = Convert.ToDouble(pieces[2]).ConvertUnit(unit, GSA.Units);
                double width = Convert.ToDouble(pieces[3]).ConvertUnit(unit, GSA.Units);
                prop.Profile = new SpecklePolyline(new double[] {
                    width /2, height/2 , 0,
                    -width/2, height/2 , 0,
                    -width/2, -height/2 , 0,
                    width/2, -height/2 , 0});
                prop.Shape = Structural1DPropertyShape.Rectangular;
                prop.Hollow = false;
            }
            else if (pieces[1] == "RHS")
            {
                // Hollow Rectangle
                double height = Convert.ToDouble(pieces[2]).ConvertUnit(unit, GSA.Units);
                double width = Convert.ToDouble(pieces[3]).ConvertUnit(unit, GSA.Units);
                double t1 = Convert.ToDouble(pieces[4]).ConvertUnit(unit, GSA.Units);
                double t2 = Convert.ToDouble(pieces[5]).ConvertUnit(unit, GSA.Units);
                prop.Profile = new SpecklePolyline(new double[] {
                    width /2, height/2 , 0,
                    -width/2, height/2 , 0,
                    -width/2, -height/2 , 0,
                    width/2, -height/2 , 0});
                prop.Shape = Structural1DPropertyShape.Rectangular;
                prop.Hollow = true;
                prop.Thickness = (t1 + t2) / 2; // TODO: Takes average thickness
            }
            else if (pieces[1] == "C")
            {
                // Circle
                double diameter = Convert.ToDouble(pieces[2]).ConvertUnit(unit, GSA.Units);
                prop.Profile = new SpeckleCircle(
                    new SpecklePlane(new SpecklePoint(0, 0, 0),
                        new SpeckleVector(0, 0, 1),
                        new SpeckleVector(1, 0, 0),
                        new SpeckleVector(0, 1, 0)),
                    diameter / 2);
                prop.Shape = Structural1DPropertyShape.Circular;
                prop.Hollow = false;
            }
            else if (pieces[1] == "CHS")
            {
                // Hollow Circle
                double diameter = Convert.ToDouble(pieces[2]).ConvertUnit(unit, GSA.Units);
                double t = Convert.ToDouble(pieces[3]).ConvertUnit(unit, GSA.Units);
                prop.Profile = new SpeckleCircle(
                    new SpecklePlane(new SpecklePoint(0, 0, 0),
                        new SpeckleVector(0, 0, 1),
                        new SpeckleVector(1, 0, 0),
                        new SpeckleVector(0, 1, 0)),
                    diameter / 2);
                prop.Shape = Structural1DPropertyShape.Circular;
                prop.Hollow = true;
                prop.Thickness = t;
            }
            else if (pieces[1] == "I")
            {
                // I Section
                double depth = Convert.ToDouble(pieces[2]).ConvertUnit(unit, GSA.Units);
                double width = Convert.ToDouble(pieces[3]).ConvertUnit(unit, GSA.Units);
                double webThickness = Convert.ToDouble(pieces[4]).ConvertUnit(unit, GSA.Units);
                double flangeThickness = Convert.ToDouble(pieces[5]).ConvertUnit(unit, GSA.Units);

                prop.Profile = new SpecklePolyline(new double[] {
                    webThickness/2, depth/2 - flangeThickness, 0,
                    width/2, depth/2 - flangeThickness, 0,
                    width/2, depth/2, 0,
                    -width/2, depth/2, 0,
                    -width/2, depth/2 - flangeThickness, 0,
                    -webThickness/2, depth/2 - flangeThickness, 0,
                    -webThickness/2, -(depth/2 - flangeThickness), 0,
                    -width/2, -(depth/2 - flangeThickness), 0,
                    -width/2, -depth/2, 0,
                    width/2, -depth/2, 0,
                    width/2, -(depth/2 - flangeThickness), 0,
                    webThickness/2, -(depth/2 - flangeThickness), 0});
                prop.Shape = Structural1DPropertyShape.I;
                prop.Hollow = false;
            }
            else if (pieces[1] == "T")
            {
                // T Section
                double depth = Convert.ToDouble(pieces[2]).ConvertUnit(unit, GSA.Units);
                double width = Convert.ToDouble(pieces[3]).ConvertUnit(unit, GSA.Units);
                double webThickness = Convert.ToDouble(pieces[4]).ConvertUnit(unit, GSA.Units);
                double flangeThickness = Convert.ToDouble(pieces[5]).ConvertUnit(unit, GSA.Units);

                prop.Profile = new SpecklePolyline(new double[] {
                    webThickness/2, - flangeThickness, 0,
                    width/2, - flangeThickness, 0,
                    width/2, 0, 0,
                    -width/2, 0, 0,
                    -width/2, - flangeThickness, 0,
                    -webThickness/2, - flangeThickness, 0,
                    -webThickness/2, -depth, 0,
                    webThickness/2, -depth, 0});
                prop.Shape = Structural1DPropertyShape.T;
                prop.Hollow = false;
            }
            else if (pieces[1] == "CH")
            {
                // Channel Section
                double depth = Convert.ToDouble(pieces[2]).ConvertUnit(unit, GSA.Units);
                double width = Convert.ToDouble(pieces[3]).ConvertUnit(unit, GSA.Units);
                double webThickness = Convert.ToDouble(pieces[4]).ConvertUnit(unit, GSA.Units);
                double flangeThickness = Convert.ToDouble(pieces[5]).ConvertUnit(unit, GSA.Units);

                prop.Profile = new SpecklePolyline(new double[] {
                    webThickness, depth/2 - flangeThickness, 0,
                    width, depth/2 - flangeThickness, 0,
                    width, depth/2, 0,
                    0, depth/2, 0,
                    0, -depth/2, 0,
                    width, -depth/2, 0,
                    width, -(depth/2 - flangeThickness), 0,
                    webThickness, -(depth/2 - flangeThickness), 0});
                prop.Shape = Structural1DPropertyShape.Generic;
                prop.Hollow = false;
            }
            else if (pieces[1] == "A")
            {
                // Angle Section
                double depth = Convert.ToDouble(pieces[2]).ConvertUnit(unit, GSA.Units);
                double width = Convert.ToDouble(pieces[3]).ConvertUnit(unit, GSA.Units);
                double webThickness = Convert.ToDouble(pieces[4]).ConvertUnit(unit, GSA.Units);
                double flangeThickness = Convert.ToDouble(pieces[5]).ConvertUnit(unit, GSA.Units);

                prop.Profile = new SpecklePolyline(new double[] {
                    0, 0, 0,
                    width, 0, 0,
                    width, flangeThickness, 0,
                    webThickness, flangeThickness, 0,
                    webThickness, depth, 0,
                    0, depth, 0});
                prop.Shape = Structural1DPropertyShape.Generic;
                prop.Hollow = false;
            }
            else if (pieces[1] == "TR")
            {
                // Taper Section
                double depth = Convert.ToDouble(pieces[2]).ConvertUnit(unit, GSA.Units);
                double topWidth = Convert.ToDouble(pieces[3]).ConvertUnit(unit, GSA.Units);
                double bottomWidth = Convert.ToDouble(pieces[4]).ConvertUnit(unit, GSA.Units);
                prop.Profile = new SpecklePolyline(new double[] {
                    topWidth /2, depth/2 , 0,
                    -topWidth/2, depth/2 , 0,
                    -bottomWidth/2, -depth/2 , 0,
                    bottomWidth/2, -depth/2 , 0});
                prop.Shape = Structural1DPropertyShape.Generic;
                prop.Hollow = false;
            }
            else if (pieces[1] == "E")
            {
                // Ellipse Section
                double depth = Convert.ToDouble(pieces[2]).ConvertUnit(unit, GSA.Units);
                double width = Convert.ToDouble(pieces[3]).ConvertUnit(unit, GSA.Units);
                int index = Convert.ToInt32(pieces[4]);

                List<double> coor = new List<double>();
                for (int i = 0; i < 360; i += 10)
                {
                    double radius =
                        depth * width / Math.Pow(
                            Math.Pow(depth * Math.Cos(i.ToRadians()), index)
                            + Math.Pow(width * Math.Sin(i.ToRadians()), index),
                            1 / index);

                    coor.Add(radius * Math.Cos(i.ToRadians()));
                    coor.Add(radius * Math.Sin(i.ToRadians()));
                    coor.Add(0);
                }
                prop.Profile = new SpecklePolyline(coor.ToArray());
                prop.Shape = Structural1DPropertyShape.Generic;
                prop.Hollow = false;
            }
            else
            {
                // TODO: IMPLEMENT ALL SECTIONS
                Status.AddError("Section " + prop.Name + " of type " + pieces[1] + " is unsupported.");
            }

            return prop;
        }

        public static GSA1DProperty SetGeometryDesc(GSA1DProperty prop, string desc)
        {
            string[] pieces = desc.ListSplit("%");

            string unit = Regex.Match(pieces[1], @"(?<=()(.*?)(?=))").Value;
            if (unit == "") unit = "mm";

            if (pieces[1] == "P")
            {
                // Perimeter Section
                List<double> coor = new List<double>();

                MatchCollection points = Regex.Matches(desc, @"(?<=\()(.*?)(?=\))");
                foreach (Match point in points)
                {
                    string[] n = point.Value.Split(new char[] { '|' });

                    coor.Add(Convert.ToDouble(n[0]).ConvertUnit(unit, GSA.Units));
                    coor.Add(Convert.ToDouble(n[1]).ConvertUnit(unit, GSA.Units));
                    coor.Add(0);
                }

                prop.Profile = new SpecklePolyline(coor.ToArray());
                prop.Shape = Structural1DPropertyShape.Generic;
                prop.Hollow = false;
                return prop;
            }
            else
            {
                // TODO: IMPLEMENT ALL SECTIONS
                Status.AddError("Section " + prop.Name + " of type " + pieces[1] + " is unsupported.");
                return prop;
            }
        }

        public static string GetGSADesc(Structural1DProperty prop)
        {
            if (prop.Profile == null)
            {
                Status.AddError("Invalid profile for " + prop.Name);
                return "";
            }

            if (prop.Profile is SpeckleCircle)
            {
                if (prop.Shape != Structural1DPropertyShape.Circular)
                    Status.AddError("Inconsistent profile and shape for " + prop.Name + ". Profile used.");

                SpeckleCircle profile = prop.Profile as SpeckleCircle;

                if (prop.Hollow)
                    return "STD%CHS(" + GSA.Units + ")%" + (profile.Radius * 2).ToString() + "%" + prop.Thickness.ToString();
                else
                    return "STD%C(" + GSA.Units + ")%" + (profile.Radius * 2).ToString();
            }

            if (prop.Profile is SpecklePolyline)
            {
                List<double> X = (prop.Profile as SpecklePolyline).Value.Where((x, i) => i % 3 == 0).ToList();
                List<double> Y = (prop.Profile as SpecklePolyline).Value.Where((x, i) => i % 3 == 1).ToList();
                if (prop.Shape == Structural1DPropertyShape.Circular)
                {
                    if (prop.Hollow)
                        return "STD%CHS(" + GSA.Units + ")%" + (X.Max() - X.Min()).ToString() + "%" + prop.Thickness.ToString();
                    else
                        return "STD%C(" + GSA.Units + ")%" + (X.Max() - X.Min()).ToString();
                }
                else if (prop.Shape == Structural1DPropertyShape.Rectangular)
                {
                    if (prop.Hollow)
                        return "STD%RHS(" + GSA.Units + ")%" + (Y.Max() - Y.Min()).ToString() + "%" + (X.Max() - X.Min()).ToString() + "%" + prop.Thickness.ToString();
                    else
                        return "STD%R(" + GSA.Units + ")%" + (Y.Max() - Y.Min()).ToString() + "%" + (X.Max() - X.Min()).ToString();
                }
                else if (prop.Shape == Structural1DPropertyShape.I)
                {
                    List<double> xDist = X.Distinct().ToList();
                    List<double> yDist = Y.Distinct().ToList();

                    xDist.Sort();
                    yDist.Sort();

                    if (xDist.Count() == 4 && yDist.Count() == 4)
                    {
                        double width = xDist.Max() - xDist.Min();
                        double depth = yDist.Max() - yDist.Min();
                        double T = yDist[3] - yDist[2];
                        double t = xDist[2] - xDist[1];
                        
                        return "STD%I(" + GSA.Units + ")%" + depth.ToString() + "%" + width.ToString() + "%" + T.ToString() + "%" + t.ToString();
                    }
                }
                else if (prop.Shape == Structural1DPropertyShape.T)
                {
                    List<double> xDist = X.Distinct().ToList();
                    List<double> yDist = Y.Distinct().ToList();

                    xDist.Sort();
                    yDist.Sort();

                    if (xDist.Count() == 4 && yDist.Count() == 3)
                    {
                        double width = xDist.Max() - xDist.Min();
                        double depth = yDist.Max() - yDist.Min();
                        double T = yDist[2] - yDist[1];
                        double t = xDist[2] - xDist[1];
                        
                        return "STD%T(" + GSA.Units + ")%" + depth.ToString() + "%" + width.ToString() + "%" + T.ToString() + "%" + t.ToString();
                    }
                }
                else if(prop.Shape == Structural1DPropertyShape.Generic)
                {
                }
                else
                {
                    Status.AddError("Unrecognized section " + prop.Name + " added as perimeter desc.");
                }

                if (X.Count() < 3 || Y.Count() < 3) return "";

                List<string> ls = new List<string>();

                ls.Add("GEO");
                if (GSA.Units == "mm")
                    ls.Add("P");
                else
                    ls.Add("P(" + GSA.Units + ")");

                for (int i = 0; i < X.Count(); i++)
                {
                    string point = i == 0 ? "M" : "L";

                    point += "(" + X[i].ToString() + "|" + Y[i].ToString() + ")";

                    ls.Add(point);
                }

                return string.Join("%", ls);
            }

            Status.AddError("Invalid profile for " + prop.Name);
            return "";
        }
        #endregion
    }
}
