using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA
{
    public class GSAElement : GSAObject
    {
        public string eType { get; set; }
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

        public GSAElement() : base("ELEMENT")
        {
            eType = "BEAM";
            Property = 1;
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

        public override void ParseGWACommand(string command)
        {
            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier
            Ref = Convert.ToInt32(pieces[counter++]);
            Name = pieces[counter++].Trim(new char[] { '"' });
            Color = pieces[counter++].ParseGSAColor();
            eType = pieces[counter++];
            Property = Convert.ToInt32(pieces[counter++]);
            Group = Convert.ToInt32(pieces[counter++]);
            NumTopo = ElementNumNodes[eType];
            Topo = new int[NumTopo];
            for (int i = 0; i < Topo.Length; i++)
                Topo[i] = Convert.ToInt32(pieces[counter++]);
            OrientNode = Convert.ToInt32(pieces[counter++]);
            Beta = Convert.ToDouble(pieces[counter++]);
            if (pieces[counter++] != "NO_RLS")
            {
                Releases = pieces[counter++];
                Releases += pieces[counter++];
            }
            Stiffness = new double[Releases.Count(k => k == 'K')];
            for (int i = 0; i < Stiffness.Length; i++)
                Stiffness[i] = Convert.ToDouble(pieces[counter++]);
            EndOffset[0] = Convert.ToDouble(pieces[counter++]);
            EndOffset[1] = Convert.ToDouble(pieces[counter++]);
            Offset = new double[pieces.Length - counter - 1];
            for (int i = 0; i < Offset.Length; i++)
                Offset[i] = Convert.ToDouble(pieces[counter++]);
            Dummy = pieces[counter++];
        }

        public override string GetGWACommand()
        {
            List<string> ls = new List<string>();

            ls.Add("SET");
            ls.Add("EL.3");
            ls.Add(Ref.ToString());
            ls.Add(Name);
            if (Color == null)
                ls.Add("NO_RGB");
            else
                ls.Add(((int)Color).ToString());
            ls.Add(eType);
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

        public override List<GSAObject> GetChildren()
        {
            List<GSAObject> children = new List<GSAObject>();

            for (int i = 0; i < NumTopo; i++)
            {
                GSANode n = new GSANode();
                n.Coor = Coor.Skip(i * 3).Take(3).ToArray();
                children.Add(n);
            }

            return children;
        }
    }
}
