using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Interop.Gsa_10_0;
using SpeckleStructuresClasses;
using System.Reflection;

namespace SpeckleGSA
{
    [GSAObject("PROP_SEC.3", "properties", true, true, new Type[] { typeof(GSAMaterial) }, new Type[] { typeof(GSAMaterial) })]
    public class GSA1DProperty : Structural1DProperty, IGSAObject
    {
        public string GWACommand { get; set; }
        public List<string> SubGWACommand { get; set; }

        #region Contructors and Converters
        public GSA1DProperty()
        {
            GWACommand = "";
            SubGWACommand = new List<string>();
        }

        public GSA1DProperty(Structural1DProperty baseClass)
        {
            GWACommand = "";
            SubGWACommand = new List<string>();
            foreach (FieldInfo f in baseClass.GetType().GetFields())
                f.SetValue(this, f.GetValue(baseClass));

            foreach (PropertyInfo p in baseClass.GetType().GetProperties())
                p.SetValue(this, p.GetValue(baseClass));
        }
        #endregion

        #region GSA Functions
        public static bool GetObjects(Dictionary<Type, List<object>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType))
                dict[MethodBase.GetCurrentMethod().DeclaringType] = new List<object>();
            
            List<object> props = new List<object>();

            string[] lines = GSA.GetGWAGetCommands("GET_ALL,PROP_SEC");
            string[] deletedLines = GSA.GetDeletedGWAGetCommands("GET_ALL,PROP_SEC");

            // Remove deleted lines
            dict[typeof(GSA1DProperty)].RemoveAll(l => deletedLines.Contains(((IGSAObject)l).GWACommand));
            foreach (KeyValuePair<Type, List<object>> kvp in dict)
                kvp.Value.RemoveAll(l => ((IGSAObject)l).SubGWACommand.Any(x => deletedLines.Contains(x)));

            // Filter only new lines
            string[] prevLines = dict[typeof(GSA1DProperty)].Select(l => ((GSA1DProperty)l).GWACommand).ToArray();
            string[] newLines = lines.Where(l => !prevLines.Contains(l)).ToArray();
            
            foreach (string p in newLines)
            {
                GSA1DProperty prop = new GSA1DProperty();
                prop.ParseGWACommand(p, dict);
                props.Add(prop);
            }
            
            dict[typeof(GSA1DProperty)].AddRange(props);

            if (props.Count() > 0 || deletedLines.Length > 0) return true;

            return false;
        }

        public static void WriteObjects(Dictionary<Type, List<StructuralObject>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType)) return;

            List<StructuralObject> props = dict[typeof(GSA1DProperty)];
            
            double counter = 1;
            foreach (StructuralObject p in props)
            {
                if (GSARefCounters.RefObject(p) != 0)
                {
                    p.Reference = 0;
                    GSARefCounters.RefObject(p);
                }

                GSA.RunGWACommand((p as GSA1DProperty).GetGWACommand(dict));
                Status.ChangeStatus("Writing 1D properties", counter++ / props.Count() * 100);
            }
        }

        public void ParseGWACommand(string command, Dictionary<Type, List<object>> dict = null)
        {
            GWACommand = command;

            string[] pieces = command.ListSplit(",");
            int counter = 1; // Skip identifier
            Reference = Convert.ToInt32(pieces[counter++]);
            Name = pieces[counter++].Trim(new char[] { '"' });
            counter++; // Color

            string materialType = pieces[counter++];
            string materialTypeEnum;
            if (materialType == "STEEL")
                materialTypeEnum = StructuralMaterialType.STEEL;
            else if (materialType == "CONCRETE")
                materialTypeEnum = StructuralMaterialType.CONCRETE;
            else
                materialTypeEnum = StructuralMaterialType.GENERIC;
            int materialGrade = Convert.ToInt32(pieces[counter++]);

            if (dict.ContainsKey(typeof(GSAMaterial)))
            {
                List<object> materials = dict[typeof(GSAMaterial)];
                GSAMaterial matchingMaterial = materials.Cast<GSAMaterial>().Where(m => m.LocalReference == materialGrade & m.Type == materialTypeEnum).FirstOrDefault();
                Material = matchingMaterial == null ? 0 : matchingMaterial.Reference;
                if (matchingMaterial != null)
                    SubGWACommand.Add(matchingMaterial.GWACommand);
            }
            else
                Material = 0;

            counter++; // Analysis material
            SetDesc(pieces[counter++]);
            counter++; // Cost
        }

        public string GetGWACommand(Dictionary<Type, List<StructuralObject>> dict = null)
        {
            List<string> ls = new List<string>();

            ls.Add("SET");
            ls.Add((string)this.GetAttribute("GSAKeyword"));
            ls.Add(Reference.ToString());
            ls.Add(Name);
            ls.Add("NO_RGB");

            if (dict.ContainsKey(typeof(GSAMaterial)))
            {
                GSAMaterial matchingMaterial = dict[typeof(GSAMaterial)].Cast<GSAMaterial>().Where(m => m.Reference == Material).FirstOrDefault();
                if (matchingMaterial != null)
                {
                    if (matchingMaterial.Type == StructuralMaterialType.STEEL)
                        ls.Add("STEEL");
                    else if (matchingMaterial.Type == StructuralMaterialType.CONCRETE)
                        ls.Add("CONCRETE");
                    else
                        ls.Add("UNDEF");
                }
                else
                    ls.Add("UNDEF");
            }
            else
                ls.Add("UNDEF");
            ls.Add(Material.ToString());
            ls.Add("0"); // Analysis material
            ls.Add(GetGeometryDesc());
            ls.Add("0"); // Cost

            return string.Join(",", ls);
        }
        #endregion

        #region Helper Functions
        public void SetDesc(string desc)
        {
            string[] pieces = desc.ListSplit("%");

            switch (pieces[0])
            {
                case "STD":
                    SetStandardDesc(desc);
                    return;
                case "GEO":
                    SetGeometryDesc(desc);
                    return;
                default:
                    SetStandardDesc("STD%C%100");
                    return;
            }
        }

        public void SetStandardDesc(string desc)
        {
            string[] pieces = desc.ListSplit("%");

            string unit = Regex.Match(pieces[1], @"(?<=()(.*)(?=))").Value;
            if (unit == "") unit = "mm";

            if (pieces[1] == "R")
            {
                // Rectangle
                double height = Convert.ToDouble(pieces[2]).ConvertUnit(unit, GSA.Units);
                double width = Convert.ToDouble(pieces[3]).ConvertUnit(unit, GSA.Units);
                Coordinates = new Coordinates(new double[] {
                    width /2, height/2 , 0,
                    -width/2, height/2 , 0,
                    -width/2, -height/2 , 0,
                    width/2, -height/2 , 0});
                Shape = Structural1DPropertyShape.RECTANGULAR;
                Hollow = false;
            }
            else if (pieces[1] == "RHS")
            {
                // Hollow Rectangle
                double height = Convert.ToDouble(pieces[2]).ConvertUnit(unit, GSA.Units);
                double width = Convert.ToDouble(pieces[3]).ConvertUnit(unit, GSA.Units);
                double t1 = Convert.ToDouble(pieces[4]).ConvertUnit(unit, GSA.Units);
                double t2 = Convert.ToDouble(pieces[5]).ConvertUnit(unit, GSA.Units);
                Coordinates = new Coordinates(new double[] {
                    width /2, height/2 , 0,
                    -width/2, height/2 , 0,
                    -width/2, -height/2 , 0,
                    width/2, -height/2 , 0});
                Shape = Structural1DPropertyShape.RECTANGULAR;
                Hollow = true;
                Thickness = (t1 + t2) / 2; // TODO: Takes average thickness
            }
            else if (pieces[1] == "C")
            {
                // Circle
                double diameter = Convert.ToDouble(pieces[2]).ConvertUnit(unit, GSA.Units);
                List<double> coor = new List<double>();
                for (int i = 0; i < 360; i += 10)
                {
                    coor.Add(diameter / 2 * Math.Cos(i.ToRadians()));
                    coor.Add(diameter / 2 * Math.Sin(i.ToRadians()));
                    coor.Add(0);
                }
                Coordinates = new Coordinates(coor.ToArray());
                Shape = Structural1DPropertyShape.CIRCULAR;
                Hollow = false;
            }
            else if (pieces[1] == "CHS")
            {
                // Hollow Circle
                double diameter = Convert.ToDouble(pieces[2]).ConvertUnit(unit, GSA.Units);
                double t = Convert.ToDouble(pieces[3]).ConvertUnit(unit, GSA.Units);
                List<double> coor = new List<double>();
                for (int i = 0; i < 360; i += 10)
                {
                    coor.Add(diameter / 2 * Math.Cos(i.ToRadians()));
                    coor.Add(diameter / 2 * Math.Sin(i.ToRadians()));
                    coor.Add(0);
                }
                Coordinates = new Coordinates(coor.ToArray());
                Shape = Structural1DPropertyShape.CIRCULAR;
                Hollow = true;
                Thickness = t;
            }
            else if (pieces[1] == "I")
            {
                // I Section
                double depth = Convert.ToDouble(pieces[2]).ConvertUnit(unit, GSA.Units);
                double width = Convert.ToDouble(pieces[3]).ConvertUnit(unit, GSA.Units);
                double webThickness = Convert.ToDouble(pieces[4]).ConvertUnit(unit, GSA.Units);
                double flangeThickness = Convert.ToDouble(pieces[5]).ConvertUnit(unit, GSA.Units);

                Coordinates = new Coordinates(new double[] {
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
                Shape = Structural1DPropertyShape.I;
                Hollow = false;
            }
            else if (pieces[1] == "T")
            {
                // T Section
                double depth = Convert.ToDouble(pieces[2]).ConvertUnit(unit, GSA.Units);
                double width = Convert.ToDouble(pieces[3]).ConvertUnit(unit, GSA.Units);
                double webThickness = Convert.ToDouble(pieces[4]).ConvertUnit(unit, GSA.Units);
                double flangeThickness = Convert.ToDouble(pieces[5]).ConvertUnit(unit, GSA.Units);

                Coordinates = new Coordinates(new double[] {
                    webThickness/2, - flangeThickness, 0,
                    width/2, - flangeThickness, 0,
                    width/2, 0, 0,
                    -width/2, 0, 0,
                    -width/2, - flangeThickness, 0,
                    -webThickness/2, - flangeThickness, 0,
                    -webThickness/2, -depth, 0,
                    webThickness/2, -depth, 0});
                Shape = Structural1DPropertyShape.T;
                Hollow = false;
            }
            else if (pieces[1] == "CH")
            {
                // Channel Section
                double depth = Convert.ToDouble(pieces[2]).ConvertUnit(unit, GSA.Units);
                double width = Convert.ToDouble(pieces[3]).ConvertUnit(unit, GSA.Units);
                double webThickness = Convert.ToDouble(pieces[4]).ConvertUnit(unit, GSA.Units);
                double flangeThickness = Convert.ToDouble(pieces[5]).ConvertUnit(unit, GSA.Units);

                Coordinates = new Coordinates(new double[] {
                    webThickness, depth/2 - flangeThickness, 0,
                    width, depth/2 - flangeThickness, 0,
                    width, depth/2, 0,
                    0, depth/2, 0,
                    0, -depth/2, 0,
                    width, -depth/2, 0,
                    width, -(depth/2 - flangeThickness), 0,
                    webThickness, -(depth/2 - flangeThickness), 0});
                Shape = Structural1DPropertyShape.GENERIC;
                Hollow = false;
            }
            else if (pieces[1] == "A")
            {
                // Angle Section
                double depth = Convert.ToDouble(pieces[2]).ConvertUnit(unit, GSA.Units);
                double width = Convert.ToDouble(pieces[3]).ConvertUnit(unit, GSA.Units);
                double webThickness = Convert.ToDouble(pieces[4]).ConvertUnit(unit, GSA.Units);
                double flangeThickness = Convert.ToDouble(pieces[5]).ConvertUnit(unit, GSA.Units);

                Coordinates = new Coordinates(new double[] {
                    0, 0, 0,
                    width, 0, 0,
                    width, flangeThickness, 0,
                    webThickness, flangeThickness, 0,
                    webThickness, depth, 0,
                    0, depth, 0});
                Shape = Structural1DPropertyShape.GENERIC;
                Hollow = false;
            }
            else if (pieces[1] == "TR")
            {
                // Taper Section
                double depth = Convert.ToDouble(pieces[2]).ConvertUnit(unit, GSA.Units);
                double topWidth = Convert.ToDouble(pieces[3]).ConvertUnit(unit, GSA.Units);
                double bottomWidth = Convert.ToDouble(pieces[4]).ConvertUnit(unit, GSA.Units);
                Coordinates = new Coordinates(new double[] {
                    topWidth /2, depth/2 , 0,
                    -topWidth/2, depth/2 , 0,
                    -bottomWidth/2, -depth/2 , 0,
                    bottomWidth/2, -depth/2 , 0});
                Shape = Structural1DPropertyShape.GENERIC;
                Hollow = false;
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
                Coordinates = new Coordinates(coor.ToArray());
                Shape = Structural1DPropertyShape.GENERIC;
                Hollow = false;
            }
            else
            {
                // TODO: IMPLEMENT ALL SECTIONS
                Status.AddError("Section " + Name + " of type " + pieces[1] + " is unsupported.");
                Coordinates = new Coordinates(new double[]
                {
                    0, 0, 0,
                    0, 0, 0,
                    0, 0, 0,
                    0, 0, 0,
                });
                Shape = Structural1DPropertyShape.GENERIC;
                Hollow = false;
            }
        }

        public void SetGeometryDesc(string desc)
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

                Coordinates = new Coordinates(coor.ToArray());
                Shape = Structural1DPropertyShape.GENERIC;
                Hollow = false;
            }
            else
            {
                // TODO: IMPLEMENT ALL SECTIONS
                Status.AddError("Section " + Name + " of type " + pieces[1] + " is unsupported.");
                Coordinates = new Coordinates(new double[]
                {
                    0, 0, 0,
                    0, 0, 0,
                    0, 0, 0,
                    0, 0, 0,
                });
                Shape = Structural1DPropertyShape.GENERIC;
                Hollow = false;
            }
        }

        public string GetGeometryDesc()
        {
            if (Coordinates.Count() == 0) return "";

            if (Shape == Structural1DPropertyShape.CIRCULAR)
            {
                if (Hollow)
                {
                    if (GSA.Units == "mm")
                        return "STD%CHS%" + (Coordinates.Values.Select(v => v.X).Max() - Coordinates.Values.Select(v => v.X).Min()).ToString() + "%" + Thickness.ToString();
                    else
                        return "STD%CHS(" + GSA.Units + ")%" + (Coordinates.Values.Select(v => v.X).Max() - Coordinates.Values.Select(v => v.X).Min()).ToString() + "%" + Thickness.ToString();
                }
                else
                {
                    if (GSA.Units == "mm")
                        return "STD%C%" + (Coordinates.Values.Select(v => v.X).Max() - Coordinates.Values.Select(v => v.X).Min()).ToString();
                    else
                        return "STD%C(" + GSA.Units + ")%" + (Coordinates.Values.Select(v => v.X).Max() - Coordinates.Values.Select(v => v.X).Min()).ToString();
                }
            }
            else if (Shape == Structural1DPropertyShape.RECTANGULAR)
            {
                if (Hollow)
                {
                    if (GSA.Units == "mm")
                        return "STD%RHS%" + (Coordinates.Values.Select(v => v.Y).Max() - Coordinates.Values.Select(v => v.Y).Min()).ToString() + "%" + (Coordinates.Values.Select(v => v.X).Max() - Coordinates.Values.Select(v => v.X).Min()).ToString() + "%" + Thickness.ToString();
                    else
                        return "STD%RHS(" + GSA.Units + ")%" + (Coordinates.Values.Select(v => v.Y).Max() - Coordinates.Values.Select(v => v.Y).Min()).ToString() + "%" + (Coordinates.Values.Select(v => v.X).Max() - Coordinates.Values.Select(v => v.X).Min()).ToString() + "%" + Thickness.ToString();
                }
                else
                {
                    if (GSA.Units == "mm")
                        return "STD%R%" + (Coordinates.Values.Select(v => v.Y).Max() - Coordinates.Values.Select(v => v.Y).Min()).ToString() + "%" + (Coordinates.Values.Select(v => v.X).Max() - Coordinates.Values.Select(v => v.X).Min()).ToString();
                    else
                        return "STD%R(" + GSA.Units + ")%" + (Coordinates.Values.Select(v => v.Y).Max() - Coordinates.Values.Select(v => v.Y).Min()).ToString() + "%" + (Coordinates.Values.Select(v => v.X).Max() - Coordinates.Values.Select(v => v.X).Min()).ToString();
                }
            }
            else if (Shape == Structural1DPropertyShape.I)
            {
                List<double> xCoor = Coordinates.Values.Select(v => v.X).Distinct().ToList();
                List<double> yCoor = Coordinates.Values.Select(v => v.Y).Distinct().ToList();

                xCoor.Sort();
                yCoor.Sort();

                if (xCoor.Count() == 4 && yCoor.Count() == 4)
                {
                    double width = xCoor.Max() - xCoor.Min();
                    double depth = yCoor.Max() - yCoor.Min();
                    double T = yCoor[3] - yCoor[2];
                    double t = xCoor[2] - xCoor[1];

                    if (GSA.Units == "mm")
                        return "STD%I%" + depth.ToString() + "%" + width.ToString() + "%" + T.ToString() + "%" + t.ToString();
                    else
                        return "STD%I(" + GSA.Units + ")%" + depth.ToString() + "%" + width.ToString() + "%" + T.ToString() + "%" + t.ToString();
                }
            }
            else if (Shape == Structural1DPropertyShape.T)
            {
                List<double> xCoor = Coordinates.Values.Select(v => v.X).Distinct().ToList();
                List<double> yCoor = Coordinates.Values.Select(v => v.Y).Distinct().ToList();

                xCoor.Sort();
                yCoor.Sort();

                if (xCoor.Count() == 4 && yCoor.Count() == 3)
                { 
                    double width = xCoor.Max() - xCoor.Min();
                    double depth = yCoor.Max() - yCoor.Min();
                    double T = yCoor[2] - yCoor[1];
                    double t = xCoor[2] - xCoor[1];

                    if (GSA.Units == "mm")
                        return "STD%T%" + depth.ToString() + "%" + width.ToString() + "%" + T.ToString() + "%" + t.ToString();
                    else
                        return "STD%T(" + GSA.Units + ")%" + depth.ToString() + "%" + width.ToString() + "%" + T.ToString() + "%" + t.ToString();
                }
            }
            else
            {
                Status.AddError("Section " + Name + " added as perimeter desc.");
            }

            if (Coordinates.Count() < 3) return "";

            List<string> ls = new List<string>();

            ls.Add("GEO");
            if (GSA.Units == "mm")
                ls.Add("P");
            else
                ls.Add("P(" + GSA.Units + ")");

            for (int i = 0; i < Coordinates.Count(); i++)
            {
                string point = i == 0 ? "M" : "L";

                point += "(" + Coordinates.Values[i].X.ToString() + "|" + Coordinates.Values[i].Y.ToString() + ")";

                ls.Add(point);
            }

            return string.Join("%", ls);
        }
        #endregion
    }
}
