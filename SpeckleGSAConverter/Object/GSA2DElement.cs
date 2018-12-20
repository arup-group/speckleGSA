using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA
{
    public class GSA2DElement : GSAObject
    {
        public string Type { get; set; }
        public int Property { get; set; }
        public Dictionary<string, object> Axis { get; set; }
        public double InsertionPoint { get; set; }

        public int Group;
        public string Action;
        public bool Dummy;
        public double rotationAngle;

        public GSA2DElement() : base("ELEMENT")
        {
            Type = "QUAD4";
            Property = 1;
            Axis = new Dictionary<string, object>()
            {
                { "X", new Dictionary<string, object> { { "x", 1 }, { "y", 0 },{ "z", 0 }  } },
                { "Y", new Dictionary<string, object> { { "x", 0 }, { "y", 1 },{ "z", 0 }  } },
                { "Z", new Dictionary<string, object> { { "x", 0 }, { "y", 0 },{ "z", 1 }  } },
            };

            InsertionPoint = 0;

            Group = 0;
            Action = "NORMAL"; // Artifact of 1D element
            Dummy = false;
        }

        public override void ParseGWACommand(string command, GSAObject[] children = null)
        {
            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier
            Reference = Convert.ToInt32(pieces[counter++]);
            Name = pieces[counter++].Trim(new char[] { '"' });
            Color = pieces[counter++].ParseGSAColor();
            Type = pieces[counter++];
            Property = Convert.ToInt32(pieces[counter++]);
            Group = Convert.ToInt32(pieces[counter++]);

            Connectivity.Clear();
            Coor.Clear();
            for (int i = 0; i < Type.ParseElementNumNodes(); i++)
            { 
                Connectivity.Add(Convert.ToInt32(pieces[counter++]));
                Coor.AddRange(children.Where(n => n.Reference == Connectivity[i]).FirstOrDefault().Coor);
            }

            counter++; // Orientation node

            rotationAngle = Convert.ToDouble(pieces[counter++]);

            Axis = Coor.ToArray().EvaluateGSA2DElementAxis(gsa, rotationAngle, Property);

            if (pieces[counter++] != "NO_RLS")
            {
                string start = pieces[counter++];
                string end = pieces[counter++];

                counter += start.Split('K').Length - 1 + end.Split('K').Length - 1;
            }
            
            counter++;
            counter++;
            counter++;

            InsertionPoint = Convert.ToDouble(pieces[counter++]);

            counter++;
            Dummy = pieces[counter++] == "DUMMY";
        }

        public override string GetGWACommand()
        {
            List<string> ls = new List<string>();

            ls.Add("SET");
            ls.Add("EL.4");
            ls.Add(Reference.ToString());
            ls.Add(Name);
            if (Color == null)
                ls.Add("NO_RGB");
            else
                ls.Add(Color.ToNumString());
            ls.Add(Type);
            ls.Add(Property.ToString());
            ls.Add(Group.ToString());
            foreach (int c in Connectivity)
                ls.Add(c.ToString());
            ls.Add("0"); //Orientation node
            ls.Add(Axis.GetGSA2DElementAngle(gsa).ToNumString());
            ls.Add("NO_RLS");

            ls.Add("0");
            ls.Add("0");
            ls.Add("0");
            ls.Add(InsertionPoint.ToNumString());

            ls.Add("NORMAL");
            ls.Add(Dummy ? "DUMMY" : "");

            return string.Join(",", ls);
        }

        public override List<GSAObject> GetChildren()
        {
            List<GSAObject> children = new List<GSAObject>();

            for (int i = 0; i < Coor.Count() / 3; i++)
            {
                GSANode n = new GSANode();
                n.Coor = Coor.Skip(i * 3).Take(3).ToList();
                children.Add(n);
            }

            return children;
        }
    }
}
