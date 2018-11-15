using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Interop.Gsa_9_0;
using System.Text.RegularExpressions;

namespace SpeckleGSA
{
    public class Element
    {
        public string OBJ_TYPE { get => "ELEMENT"; set { } }
        public int Ref { get; set; }
        public string Name { get; set; }
        public int Color { get; set; }
        public int eType { get; set; }
        public int Property { get; set; }
        public int Group { get; set; }
        public int NumTopo { get; set; }
        public int[] Topo { get; set; }
        public double Beta { get; set; }
        public int OrientNode { get; set; }
        public string Releases { get; set; }
        public double[] Stiffness { get; set; }
        public double[] EndOffset { get; set; }
        public double[] Offset { get; set; }
        public string Dummy { get; set; }

        public Dictionary<string, int> NumNodes = new Dictionary<string, int>()
        {
            {"BAR", 2 },
            {"BEAM", 2 },
            {"QUAD4", 4 }
        };

        private double[] _coor;
        private int _color;

        public Element()
        {
            Ref = 0;
            Name = "";
            Color = 0;
            eType = 0;
            Property = 0;
            Group = 0;
            NumTopo = 0;
            Topo = new int[0];
            Beta = 0;
            OrientNode = 0;
            Releases = "FFFFFFFFFFFF";
            Stiffness = new double[0];
            EndOffset = new double[2];
            Offset = new double[3];
            Dummy = "";
        }

        public double[] GetCoorArr()
        {
            return _coor;
        }
        
        public void SetCoorArr(double[] coor)
        {
            _coor = coor;
        }

        public int GetColor()
        {
            return _color;
        }

        public void SetColor(int color)
        {
            _color = color;
        }

        public void ParseGWACommand(string command)
        {
            Console.WriteLine(command);
            int temp;
            string[] pieces = Regex.Split(command, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
            
            int counter = 1; // Skip identifier
            Ref = Convert.ToInt32(pieces[counter++]);
            Name = pieces[counter++].Replace("\"", "");
            if (pieces[counter] == "NO_RGB")
            {
                Color = 0;
                counter++;
            }
            else
                Color = Convert.ToInt32(pieces[counter++]);

            string type = pieces[counter++];
            eType = (int)((ElementType) Enum.Parse(typeof(ElementType), type));

            Property = Convert.ToInt32(pieces[counter++]);
            Group = Convert.ToInt32(pieces[counter++]);

            NumTopo = NumNodes[type];
            Topo = new int[NumTopo];
            for (int i = 0; i < Topo.Length; i++)
            {
                Topo[i] = Convert.ToInt32(pieces[counter++]);
            }
            OrientNode = Convert.ToInt32(pieces[counter++]);
            Beta = Convert.ToDouble(pieces[counter++]);
            if (pieces[counter++] != "NO_RLS")
            {
                Releases = pieces[counter++];
                Releases += pieces[counter++];
            }
            temp = Releases.Count(k => k == 'K');
            Stiffness = new double[temp];
            for (int i = 0; i < Stiffness.Length; i++)
                Stiffness[i] = Convert.ToDouble(pieces[counter++]);
            EndOffset[0] = Convert.ToDouble(pieces[counter++]);
            EndOffset[1] = Convert.ToDouble(pieces[counter++]);
            temp = pieces.Length - counter - 1;
            Offset = new double[temp];
            for (int i = 0; i < Offset.Length; i++)
                Offset[i] = Convert.ToDouble(pieces[counter++]);
            Dummy = pieces[counter++];
        }

        public string GetGWACommand()
        {
            List<string> ls = new List<string>();
            StringBuilder str = new StringBuilder();

            ls.Add("SET");
            ls.Add("EL.3");
            ls.Add(Ref.ToString());
            ls.Add(Name);
            if (Color == 0)
                ls.Add("NO_RGB");
            else
                ls.Add(Color.ToString());
            ls.Add(((ElementType)eType).ToString());
            ls.Add(Property.ToString());
            ls.Add(Group.ToString());
            foreach (int t in Topo)
                ls.Add(t.ToString());
            ls.Add(OrientNode.ToString());
            ls.Add(Beta.ToString());
            ls.Add("NO_RLS");
            ls.Add(0.ToString());
            ls.Add(0.ToString());
            ls.Add(0.ToString());
            ls.Add(Dummy.ToString());

            return string.Join(",", ls);
        }
    }
}
