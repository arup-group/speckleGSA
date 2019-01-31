using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Interop.Gsa_10_0;

namespace SpeckleGSA
{
    public class GSA0DElement : GSAObject
    {
        public override string Entity { get => "0D Element"; set { } }

        public static readonly string GSAKeyword = "EL";
        public static readonly string Stream = "elements";
        public static readonly int ReadPriority = 1;
        public static readonly int WritePriority = 9999;
        
        public string Type { get; set; }
        public int Property { get; set; }
        public int Group { get; set; }

        public double Mass;
        public List<int> Connectivity;

        public GSA0DElement()
        {
            Type = "MASS";
            Property = 1;
            Group = 0;

            Mass = 0;
        }

        #region GSAObject Functions
        public static void GetObjects(Dictionary<Type, object> dict)
        {
            List<GSAObject> e0Ds = new List<GSAObject>();

            string res = (string)GSA.RunGWACommand("GET_ALL,EL");

            if (res == "")
                return;

            string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            double counter = 1;
            foreach (string p in pieces)
            {
                string[] pPieces = p.ListSplit(",");
                if (pPieces[4].ParseElementNumNodes() == 1)
                {
                    GSA0DElement e0D = new GSA0DElement();
                    e0D.ParseGWACommand(p);

                    e0Ds.Add(e0D);
                }

                Status.ChangeStatus("Reading 0D elements", counter++ / pieces.Length * 100);
            }
            
            dict[typeof(GSA0DElement)] = e0Ds;
        }

        public static void WriteObjects(Dictionary<Type, object> dict)
        {
            if (!dict.ContainsKey(typeof(GSA0DElement))) return;

            List<GSAObject> e0Ds = dict[typeof(GSA0DElement)] as List<GSAObject>;

            double counter = 1;
            foreach (GSAObject e in e0Ds)
            {
                GSARefCounters.RefObject(e);
                
                if ((e as GSA0DElement).Type == "MASS")
                    (e as GSA0DElement).Property = (e as GSA0DElement).WriteMassProptoGSA((e as GSA0DElement).Mass);

                GSA.RunGWACommand(e.GetGWACommand());
                
                Status.ChangeStatus("Writing 0D elements", counter++/e0Ds.Count() * 100);
            }

            dict.Remove(typeof(GSA0DElement));
        }

        public override void ParseGWACommand(string command, Dictionary<Type, object> dict = null)
        {
            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier
            counter++; // Reference
            Name = pieces[counter++].Trim(new char[] { '"' });
            Color = pieces[counter++].ParseGSAColor();
            Type = pieces[counter++];
            Property = Convert.ToInt32(pieces[counter++]);
            Group = Convert.ToInt32(pieces[counter++]);
            
            // Rest is unimportant for 0D element
        }

        public override string GetGWACommand(Dictionary<Type, object> dict = null)
        {
            List<string> ls = new List<string>();

            ls.Add("SET");
            ls.Add(GSAKeyword);
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
            ls.Add("0"); // Orient Node
            ls.Add("0"); // Beta
            ls.Add("NO_RLS"); // Release

            ls.Add("0"); // Offset x-start
            ls.Add("0"); // Offset y-start
            ls.Add("0"); // Offset y
            ls.Add("0"); // Offset z

            //ls.Add("NORMAL"); // Action // TODO: EL.4 SUPPORT
            ls.Add(""); //Dummy

            return string.Join(",", ls);
        }

        public override List<GSAObject> GetChildren()
        {
            throw new NotImplementedException();
        }
        #endregion

        private int WriteMassProptoGSA(double mass)
        {
            List<string> ls = new List<string>();

            int res = (int)GSA.RunGWACommand("HIGHEST,PROP_MASS");

            ls.Add("SET");
            ls.Add("PROP_MASS.2");
            ls.Add((res + 1).ToString());
            ls.Add("");
            ls.Add("NO_RGB");
            ls.Add("GLOBAL");
            ls.Add(mass.ToString());
            ls.Add("0");
            ls.Add("0");
            ls.Add("0");
            ls.Add("0");
            ls.Add("0");
            ls.Add("0");

            ls.Add("MOD");
            ls.Add("100%");
            ls.Add("100%");
            ls.Add("100%");

            GSA.RunGWACommand(string.Join(",", ls));

            return res + 1;
        }
    }
}
