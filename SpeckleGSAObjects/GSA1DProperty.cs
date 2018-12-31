using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA
{
    public class GSA1DProperty : GSAObject
    {
        public static readonly string GSAKeyword = "";
        public static readonly string Stream = "properties";
        public static readonly int ReadPriority = 1;
        public static readonly int WritePriority = 4;

        public int Material { get; set; }

        public string Type;
        public int GradeMaterial;
        public int AnalMaterial;

        public GSA1DProperty()
        {
            Material = 0;

            Type = "STEEL";
            GradeMaterial = 0;
            AnalMaterial = 0;
        }

        public override void ParseGWACommand(string command, GSAObject[] children = null)
        {
            string[] pieces = command.ListSplit(",");
            int counter = 1; // Skip identifier
            Reference = Convert.ToInt32(pieces[counter++]);
            Name = pieces[counter++].Trim(new char[] { '"' });
            Color = pieces[counter++].ParseGSAColor();
            Type = pieces[counter++];
            GradeMaterial = Convert.ToInt32(pieces[counter++]);
            AnalMaterial = Convert.ToInt32(pieces[counter++]);

            
        }

        public override string GetGWACommand(Dictionary<Type, object> dict = null)
        {
            throw new NotImplementedException();
        }

        public override List<GSAObject> GetChildren()
        {
            throw new NotImplementedException();
        }

        //public double[] ParsePropertyDesc(string desc)
        //{
        //    string[] pieces = desc.ListSplit("%");

        //    switch (pieces[0])
        //    {
        //        case "STD":
        //            switch
        //            break;
        //    }
        //}

        //public double[] ParseStandardDesc(string desc)
        //{
        //    string[] pieces = desc.ListSplit("%");

        //    double H;
        //    double W;
        //    double D;
        //    double T1;
        //    double T2;

        //    switch (pieces[1])
        //    {
        //        case "R":
        //            H = Convert.ToDouble(pieces[3]);
        //            W = Convert.ToDouble(pieces[4]);
        //            return new double[] { W/2, H/2 , 0,
        //                -W/2, H/2 , 0,
        //                -W/2, -H/2 , 0,
        //                W/2, -H/2 , 0};
        //        case "C":
        //            D = Convert.ToDouble(pieces[3]);
        //            List<double> coor = new List<double>();
        //            for (int i = 0; i < 360; i += 10)
        //            {
        //                coor.Add(D / 2 * Math.Cos(i * (Math.PI / 180)));
        //                coor.Add(D / 2 * Math.Sin(i * (Math.PI / 180)));
        //                coor.Add(0);
        //            }
        //            return coor.ToArray();
        //        case "I":
                    
        //    }
        //}
    }
}
