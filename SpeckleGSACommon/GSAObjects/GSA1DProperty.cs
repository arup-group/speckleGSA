using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Interop.Gsa_10_0;

namespace SpeckleGSA
{
    public class GSA1DProperty : GSAObject
    {
        public override string Entity { get => "1D Property"; set { } }

        public static readonly string GSAKeyword = "PROP_SEC";
        public static readonly string Stream = "properties";
        public static readonly int WritePriority = 3;

        public static readonly Type[] ReadPrerequisite = new Type[1] { typeof(GSAMaterial) };

        public int Material { get; set; }

        public GSA1DProperty()
        {
            Material = 0;

            Coor = ParseStandardDesc("STD%C%100","m").ToList();
        }

        #region GSAObject Functions
        public static void GetObjects(Dictionary<Type, object> dict)
        {
            if (!dict.ContainsKey(typeof(GSAMaterial))) return;

            List<GSAObject> materials = dict[typeof(GSAMaterial)] as List<GSAObject>;
            List<GSAObject> props = new List<GSAObject>();

            string res = (string)GSA.RunGWACommand("GET_ALL,PROP_SEC");

            if (res == "")
                return;

            string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            // Hold temporary dimension
            res = (string)GSA.RunGWACommand("GET,UNIT_DATA,LENGTH");
            string dimension = res.ListSplit(",")[2];
            dict[typeof(string)] = dimension;

            double counter = 1;
            foreach (string p in pieces)
            {
                GSAObject prop = new GSA1DProperty();
                prop.ParseGWACommand(p, dict);

                props.Add(prop);

                Status.ChangeStatus("Reading 1D properties", counter++ / pieces.Length * 100);
            }

            dict.Remove(typeof(string));
            dict[typeof(GSA1DProperty)] = props;
        }

        public static void WriteObjects(Dictionary<Type, object> dict)
        {
            if (!dict.ContainsKey(typeof(GSA1DProperty))) return;

            List<GSAObject> props = dict[typeof(GSA1DProperty)] as List<GSAObject>;

            // Hold temporary dimension
            string res = (string)GSA.RunGWACommand("GET,UNIT_DATA,LENGTH");
            string dimension = res.ListSplit(",")[2];
            dict[typeof(string)] = dimension;

            double counter = 1;
            foreach (GSAObject p in props)
            {
                GSARefCounters.RefObject(p);

                GSA.RunGWACommand(p.GetGWACommand(dict));
                Status.ChangeStatus("Writing 1D properties", counter++ / props.Count() * 100);
            }

            dict.Remove(typeof(string));
            dict.Remove(typeof(GSA1DProperty));
        }

        public override void ParseGWACommand(string command, Dictionary<Type, object> dict = null)
        {
            string[] pieces = command.ListSplit(",");
            int counter = 1; // Skip identifier
            Reference = Convert.ToInt32(pieces[counter++]);
            Name = pieces[counter++].Trim(new char[] { '"' });
            Color = pieces[counter++].ParseGSAColor();

            string materialType = pieces[counter++];
            int materialGrade = Convert.ToInt32(pieces[counter++]);

            if (dict.ContainsKey(typeof(GSAMaterial)))
            {
                List<GSAObject> materials = dict[typeof(GSAMaterial)] as List<GSAObject>;
                GSAObject matchingMaterial = materials.Cast<GSAMaterial>().Where(m => m.LocalReference == materialGrade & m.Type == materialType).FirstOrDefault();
                Material = matchingMaterial == null ? 1 : matchingMaterial.Reference;
            }
            else
                Material = 1;

            counter++; // Analysis material
            Coor = ParseDesc(pieces[counter++], dict[typeof(string)] as string).ToList();
            counter++; // Cost
        }

        public override string GetGWACommand(Dictionary<Type, object> dict = null)
        {
            List<string> ls = new List<string>();

            ls.Add("SET");
            ls.Add(GSAKeyword);
            ls.Add(Reference.ToNumString());
            ls.Add(Name);
            if (Color == null)
                ls.Add("NO_RGB");
            else
                ls.Add(Color.ToNumString());

            if (dict.ContainsKey(typeof(GSAMaterial)))
            {
                GSAMaterial matchingMaterial = (dict[typeof(GSAMaterial)] as List<GSAObject>).Cast<GSAMaterial>().Where(m => m.Reference == Material).FirstOrDefault();
                ls.Add(matchingMaterial == null ? "" : matchingMaterial.Type);
            }
            else
                ls.Add("");

            ls.Add(Material.ToNumString());
            ls.Add("0"); // Analysis material
            ls.Add(GetGeometryDesc(Coor.ToArray(), dict[typeof(string)] as string));
            ls.Add("0"); // Cost

            return string.Join(",", ls);
        }

        public override List<GSAObject> GetChildren()
        {
            throw new NotImplementedException();
        }
        #endregion

        #region Property Parser
        public double[] ParseDesc(string desc, string globalDimension)
        {
            string[] pieces = desc.ListSplit("%");

            switch (pieces[0])
            {
                case "STD":
                    return ParseStandardDesc(desc, globalDimension);
                case "GEO":
                    return ParseGeometryDesc(desc, globalDimension);
                default:
                    return ParseStandardDesc("STD%C%100", globalDimension);
            }
        }

        public double[] ParseStandardDesc(string desc, string globalDimension)
        {
            string[] pieces = desc.ListSplit("%");

            string unit = Regex.Match(pieces[1], @"(?<=()(.*)(?=))").Value;
            if (unit == "") unit = "mm";

            if (pieces[1][0] == 'R')
            {
                // Rectangle
                double height = Convert.ToDouble(pieces[2]).ConvertUnit(unit, globalDimension);
                double width = Convert.ToDouble(pieces[3]).ConvertUnit(unit, globalDimension);
                return new double[] {
                    width /2, height/2 , 0,
                    -width/2, height/2 , 0,
                    -width/2, -height/2 , 0,
                    width/2, -height/2 , 0};
            }
            else if (pieces[1][0] == 'C')
            {
                // Circle
                double diameter = Convert.ToDouble(pieces[2]).ConvertUnit(unit, globalDimension);
                List<double> coor = new List<double>();
                for (int i = 0; i < 360; i += 10)
                {
                    coor.Add(diameter / 2 * Math.Cos(i.ToRadians()));
                    coor.Add(diameter / 2 * Math.Sin(i.ToRadians()));
                    coor.Add(0);
                }
                return coor.ToArray();
            }
            else if (pieces[1][0] == 'I')
            {
                // I Section
                double depth = Convert.ToDouble(pieces[2]).ConvertUnit(unit, globalDimension);
                double width = Convert.ToDouble(pieces[3]).ConvertUnit(unit, globalDimension);
                double webThickness = Convert.ToDouble(pieces[4]).ConvertUnit(unit, globalDimension);
                double flangeThickness = Convert.ToDouble(pieces[5]).ConvertUnit(unit, globalDimension);

                return new double[] {
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
                    webThickness/2, -(depth/2 - flangeThickness), 0};
            }
            else if (pieces[1][0] == 'T')
            {
                // T Section
                double depth = Convert.ToDouble(pieces[2]).ConvertUnit(unit, globalDimension);
                double width = Convert.ToDouble(pieces[3]).ConvertUnit(unit, globalDimension);
                double webThickness = Convert.ToDouble(pieces[4]).ConvertUnit(unit, globalDimension);
                double flangeThickness = Convert.ToDouble(pieces[5]).ConvertUnit(unit, globalDimension);

                return new double[] {
                    webThickness/2, - flangeThickness, 0,
                    width/2, - flangeThickness, 0,
                    width/2, 0, 0,
                    -width/2, 0, 0,
                    -width/2, - flangeThickness, 0,
                    -webThickness/2, - flangeThickness, 0,
                    -webThickness/2, -depth, 0,
                    webThickness/2, -depth, 0};
            }
            else if (pieces[1].Substring(0,2) == "CH")
            {
                // Channel Section
                double depth = Convert.ToDouble(pieces[2]).ConvertUnit(unit, globalDimension);
                double width = Convert.ToDouble(pieces[3]).ConvertUnit(unit, globalDimension);
                double webThickness = Convert.ToDouble(pieces[4]).ConvertUnit(unit, globalDimension);
                double flangeThickness = Convert.ToDouble(pieces[5]).ConvertUnit(unit, globalDimension);

                return new double[] {
                    webThickness, depth/2 - flangeThickness, 0,
                    width, depth/2 - flangeThickness, 0,
                    width, depth/2, 0,
                    0, depth/2, 0,
                    0, -depth/2, 0,
                    width, -depth/2, 0,
                    width, -(depth/2 - flangeThickness), 0,
                    webThickness, -(depth/2 - flangeThickness), 0};
            }
            else if (pieces[1][0] == 'A')
            {
                // Angle Section
                double depth = Convert.ToDouble(pieces[2]).ConvertUnit(unit, globalDimension);
                double width = Convert.ToDouble(pieces[3]).ConvertUnit(unit, globalDimension);
                double webThickness = Convert.ToDouble(pieces[4]).ConvertUnit(unit, globalDimension);
                double flangeThickness = Convert.ToDouble(pieces[5]).ConvertUnit(unit, globalDimension);

                return new double[] {
                    0, 0, 0,
                    width, 0, 0,
                    width, flangeThickness, 0,
                    webThickness, flangeThickness, 0,
                    webThickness, depth, 0,
                    0, depth, 0};
            }
            else if (pieces[1].Substring(0, 2) == "TR")
            {
                // Taper Section
                double depth = Convert.ToDouble(pieces[2]).ConvertUnit(unit, globalDimension);
                double topWidth = Convert.ToDouble(pieces[3]).ConvertUnit(unit, globalDimension);
                double bottomWidth = Convert.ToDouble(pieces[4]).ConvertUnit(unit, globalDimension);
                return new double[] {
                    topWidth /2, depth/2 , 0,
                    -topWidth/2, depth/2 , 0,
                    -bottomWidth/2, -depth/2 , 0,
                    bottomWidth/2, -depth/2 , 0};
            }
            else if (pieces[1][0] == 'E')
            {
                // Ellipse Section
                double depth = Convert.ToDouble(pieces[2]).ConvertUnit(unit, globalDimension);
                double width = Convert.ToDouble(pieces[3]).ConvertUnit(unit, globalDimension);
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
                return coor.ToArray();
            }
            else
                return ParseStandardDesc("STD%C%100", globalDimension);

            // TODO: IMPLEMENT ALL SECTIONS
        }

        public double[] ParseGeometryDesc(string desc, string globalDimension)
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

                    coor.Add(Convert.ToDouble(n[0]).ConvertUnit(unit, globalDimension));
                    coor.Add(Convert.ToDouble(n[1]).ConvertUnit(unit, globalDimension));
                    coor.Add(0);
                }

                return coor.ToArray();
            }
            else
                return ParseStandardDesc("STD%C%100", globalDimension);

            // TODO: IMPLEMENT ALL SECTIONS
        }

        public string GetGeometryDesc(double[] coor, string globalDimension)
        {
            if (coor.Count() < 9) return "STD%D%100";

            List<string> ls = new List<string>();

            ls.Add("GEO");
            if (globalDimension == "mm")
                ls.Add("P");
            else
                ls.Add("P(" + globalDimension + ")");

            for(int i = 0; i < coor.Count(); i += 3)
            {
                string point = i == 0? "M" : "L";

                point += "(" + coor[i].ToNumString() + "|" + coor[i+1].ToNumString() + ")";

                ls.Add(point);
            }

            return string.Join("%", ls);
        }

        #endregion
    }
}
