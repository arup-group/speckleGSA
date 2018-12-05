using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Interop.Gsa_9_0;
using System.Text.RegularExpressions;
using System.Drawing;

namespace SpeckleGSA
{
    public abstract class GSAObject
    {
        public string GSAEntity { get; set; }
        public string Name { get; set; }

        public string[] SpeckleConnectivity { get; set; }
    
        public string SpeckleHash;
        public int Ref;
        public double[] Coor;
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
            Ref = 0;
            Name = "";
            Color = null;
            Coor = new double[1];

            SpeckleConnectivity = new string[0];

            SpeckleHash = "";

            gsa = null;
        }

        public void AttachGSA(ComAuto gsa)
        {
            this.gsa = gsa;
        }

        public abstract void ParseGWACommand(string command);

        public abstract string GetGWACommand();

        public abstract List<GSAObject> GetChildren();
    }

    public static class HelperFunctions
    {
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
    }
}
