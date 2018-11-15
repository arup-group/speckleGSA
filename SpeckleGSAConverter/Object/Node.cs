using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Interop.Gsa_9_0;

namespace SpeckleGSA
{
    public class Node
    {
        public string OBJ_TYPE { get => "NODE"; set { } }
        public int Ref { get; set; }
        public string Name { get; set; }
        public int Color { get; set; }
        public double[] Coor { get; set; }
        public int Restraint { get; set; }
        public double[] Stiffness { get; set; }

        public Node()
        {
            Ref = 0;
            Name = "";
            Color = 0;
            Coor = new double[0];
            Restraint = 0;
            Stiffness = new double[0];
        }

        public Node(GsaNode node)
        {
            Ref = node.Ref;
            Name = node.Name;
            Color = Math.Max(node.Color, 0);
            Coor = node.Coor;
            Restraint = node.Restraint;
            Stiffness = node.Stiffness;
        }

        public string GetGWACommand()
        {
            List<string> ls = new List<string>();
            StringBuilder str = new StringBuilder();

            ls.Add("SET");
            ls.Add("NODE.2");
            ls.Add(Ref.ToString());
            ls.Add(Name);
            ls.Add(Color.ToString());
            ls.Add(string.Join(",",Coor));
            ls.Add("NO_GRID");
            ls.Add("GLOBAL");
            if (Restraint == 0)
                ls.Add("NO_REST");
            else
            {
                ls.Add("REST");
                int x = Restraint;
                for (int i = 0; i < 6; i++)
                {
                    ls.Add((x % 2).ToString());
                    x = x / 2;
                }
            }
            ls.Add("STIFF");
            ls.Add(string.Join(",", Stiffness));
            ls.Add("NO_MESH");

            return string.Join(",", ls);
        }
    }
}
