﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA
{
    public class GSAArea : GSAObject
    {
        // DEPRECIATED. DO NOT USE.
        public string Type { get; set; }
        public double Span { get; set; }
        public int Property { get; set; }
        public int Group { get; set; }
        public double Coefficient { get; set; }

        public GSAArea():base("AREA")
        {
            Type = "TWO_WAY";
            Span = 0;
            Property = 1;
            Group = 0;
            Coefficient = 0;
        }

        public override void ParseGWACommand(string command, GSAObject[] children = null)
        {
            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier
            Reference = Convert.ToInt32(pieces[counter++]);
            Name = pieces[counter++].Trim(new char[] { '"' });
            Color = pieces[counter++].ParseGSAColor();
            Type = pieces[counter++];
            Span = Convert.ToDouble(pieces[counter++]);
            Property = Convert.ToInt32(pieces[counter++]);
            Group = Convert.ToInt32(pieces[counter++]);
            Connectivity = pieces[counter++].ParseGSAList(gsa).ToList();
            Coefficient = Convert.ToDouble(pieces[counter++]);
        }

        public override string GetGWACommand()
        {
            List<string> ls = new List<string>();

            ls.Add("SET");
            ls.Add("AREA.2");
            ls.Add(Reference.ToString());
            ls.Add(Name);
            if (Color == null)
                ls.Add("NO_RGB");
            else
                ls.Add(((int)Color).ToString());
            ls.Add(Type);
            ls.Add(Span.ToString());
            ls.Add(Property.ToString());
            ls.Add(Group.ToString());
            string lineList = "";
            foreach (int c in Connectivity)
                lineList += c.ToString() + " ";
            ls.Add(lineList.TrimEnd());
            ls.Add(Coefficient.ToString());
            
            return string.Join(",", ls);
        }

        public override List<GSAObject> GetChildren()
        {
            List<GSAObject> children = new List<GSAObject>();
            GSALine l;

            for (int i = 0; i < Coor.Count()/3-1; i++)
            {
                l = new GSALine();
                l.Coor = Coor.Skip(i * 3).Take(6).ToList();
                children.Add(l);
            }

            l = new GSALine();
            List<double> coor = Coor.Skip(Coor.Count() - 3).Take(3).ToList();
            coor.AddRange(Coor.Take(3));
            l.Coor = coor.ToList();
            children.Add(l);

            return children;
        }
    }
}
