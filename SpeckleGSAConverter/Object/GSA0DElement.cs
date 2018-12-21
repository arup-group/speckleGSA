using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA
{
    public class GSA0DElement : GSAObject
    {
        public string Type { get; set; }
        public int Property { get; set; }
        public int Group { get; set; }

        public GSA0DElement() : base("ELEMENT")
        {
            Type = "MASS";
            Property = 1;
            Group = 0;
        }

        #region GSAObject Functions
        public override void ParseGWACommand(string command, GSAObject[] children = null)
        {
            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier
            counter++; // Reference
            Name = pieces[counter++].Trim(new char[] { '"' });
            Color = pieces[counter++].ParseGSAColor();
            Type = pieces[counter++];
            Property = Convert.ToInt32(pieces[counter++]);
            Group = Convert.ToInt32(pieces[counter++]);
            Connectivity.Clear();
            for (int i = 0; i < Type.ParseElementNumNodes(); i++)
                Connectivity.Add(Convert.ToInt32(pieces[counter++]));
            
            // Rest is unimportant for 0D element
        }

        public override string GetGWACommand()
        {
            List<string> ls = new List<string>();

            ls.Add("SET");
            ls.Add("EL.4");
            ls.Add(((int)RunGWACommand("HIGHEST,EL")+1).ToNumString());
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
            ls.Add("0"); // Orient Node
            ls.Add("0"); // Beta
            ls.Add("NO_RLS"); // Release

            ls.Add("0"); // Offset x-start
            ls.Add("0"); // Offset y-start
            ls.Add("0"); // Offset y
            ls.Add("0"); // Offset z

            ls.Add("NORMAL"); // Action
            ls.Add(""); //Dummy

            return string.Join(",", ls);
        }

        public override List<GSAObject> GetChildren()
        {
            GSANode n = new GSANode();
            n.Reference = n.Connectivity[0];
            n.Coor = Coor;

            return new List<GSAObject>() { n };
        }
        #endregion
    }
}
