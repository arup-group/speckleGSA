using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Interop.Gsa_10_0;
using SpeckleStructures;
using System.Reflection;

namespace SpeckleGSA
{
    public class GSA1DProperty : Structural1DProperty
    {
        public static readonly string GSAKeyword = "PROP_SEC";
        public static readonly string Stream = "properties";

        public static readonly Type[] ReadPrerequisite = new Type[1] { typeof(GSAMaterial) };
        public static readonly Type[] WritePrerequisite = new Type[1] { typeof(GSAMaterial) };
        public static readonly bool AnalysisLayer = true;
        public static readonly bool DesignLayer = true;

        #region Contructors and Converters
        public GSA1DProperty()
        {

        }

        public GSA1DProperty(Structural1DProperty baseClass)
        {
            foreach (FieldInfo f in baseClass.GetType().GetFields())
                f.SetValue(this, f.GetValue(baseClass));

            foreach (PropertyInfo p in baseClass.GetType().GetProperties())
                p.SetValue(this, p.GetValue(baseClass));
        }

        public StructuralObject GetBase()
        {
            StructuralObject baseClass = (StructuralObject)Activator.CreateInstance(this.GetType().BaseType);

            foreach (FieldInfo f in baseClass.GetType().GetFields())
                f.SetValue(baseClass, f.GetValue(this));

            foreach (PropertyInfo p in baseClass.GetType().GetProperties())
                p.SetValue(baseClass, p.GetValue(this));

            return baseClass;
        }
        #endregion

        #region GSA Functions
        public static void GetObjects(Dictionary<Type, List<StructuralObject>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType))
                dict[MethodBase.GetCurrentMethod().DeclaringType] = new List<StructuralObject>();
            
            List<StructuralObject> props = new List<StructuralObject>();

            string res = (string)GSA.RunGWACommand("GET_ALL,PROP_SEC");

            if (res == "")
                return;

            string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            double counter = 1;
            foreach (string p in pieces)
            {
                GSA1DProperty prop = new GSA1DProperty();
                prop.ParseGWACommand(p, dict);
                props.Add(prop);

                Status.ChangeStatus("Reading 1D properties", counter++ / pieces.Length * 100);
            }
            
            dict[typeof(GSA1DProperty)].AddRange(props);
        }

        public static void WriteObjects(Dictionary<Type, List<StructuralObject>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType)) return;

            List<StructuralObject> props = dict[typeof(GSA1DProperty)];
            
            double counter = 1;
            foreach (StructuralObject p in props)
            {
                GSARefCounters.RefObject(p);

                GSA.RunGWACommand((p as GSA1DProperty).GetGWACommand(dict));
                Status.ChangeStatus("Writing 1D properties", counter++ / props.Count() * 100);
            }
        }

        public void ParseGWACommand(string command, Dictionary<Type, List<StructuralObject>> dict = null)
        {
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
                List<StructuralObject> materials = dict[typeof(GSAMaterial)];
                GSAMaterial matchingMaterial = materials.Cast<GSAMaterial>().Where(m => m.LocalReference == materialGrade & m.Type == materialTypeEnum).FirstOrDefault();
                Material = matchingMaterial == null ? 0 : matchingMaterial.Reference;
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
            ls.Add(GSAKeyword);
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
                        ls.Add("GENERAL");
                }
                else
                    ls.Add("GENERAL");
            }
            else
                ls.Add("GENERAL");
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

            if (pieces[1][0] == 'R')
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
            else if (pieces[1][0] == 'C')
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
            else if (pieces[1][0] == 'I')
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
            else if (pieces[1][0] == 'T')
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
            else if (pieces[1].Substring(0,2) == "CH")
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
            else if (pieces[1][0] == 'A')
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
            else if (pieces[1].Substring(0, 2) == "TR")
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
            else if (pieces[1][0] == 'E')
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
                SetStandardDesc("STD%C%100");

            // TODO: IMPLEMENT ALL SECTIONS
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
                SetStandardDesc("STD%C%100");

            // TODO: IMPLEMENT ALL SECTIONS
        }

        public string GetGeometryDesc()
        {
            if (Shape == Structural1DPropertyShape.CIRCULAR)
            {
                // TODO
            }
            else if (Shape == Structural1DPropertyShape.RECTANGULAR)
            {
                // TODO
            }
            else if (Shape == Structural1DPropertyShape.I)
            {
                // TODO
            }
            else if (Shape == Structural1DPropertyShape.T)
            {
                // TODO
            }
            else
            {
                // TODO
            }

            if (Coordinates.Count() < 3) return "STD%C%100";

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
