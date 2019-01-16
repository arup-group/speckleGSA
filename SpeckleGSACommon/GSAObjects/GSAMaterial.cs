﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Interop.Gsa_10_0;

namespace SpeckleGSA
{
    public class GSAMaterial : GSAObject
    {
        public static readonly string GSAKeyword  = "MAT";
        public static readonly string Stream = "properties";
        public static readonly int ReadPriority = 0;
        public static readonly int WritePriority = 4;

        public string Type { get; set; }
        public string Grade { get; set; }

        public int LocalReference;

        public GSAMaterial()
        {
            Type = "CONCRETE";
            Grade = "35MPa";

            LocalReference = 0;
        }

        #region GSAObject Functions
        public static void GetObjects(Dictionary<Type, object> dict)
        {
            string[] materialIdentifier = new string[]
                { "MAT_STEEL", "MAT_CONCRETE" };

            List<GSAObject> materials = new List<GSAObject>();

            List<string> pieces = new List<string>();
            foreach (string id in materialIdentifier)
            {
                string res = (string)GSA.RunGWACommand("GET_ALL," + id);

                if (res == "")
                    continue;

                pieces.AddRange(res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries));
            }
            pieces = pieces.Distinct().ToList();
            
            for (int i = 0; i < pieces.Count(); i++)
            {
                GSAMaterial mat = new GSAMaterial();
                mat.ParseGWACommand(pieces[i]);
                mat.Reference = i + 1; // Offset references
                materials.Add(mat);

                Status.ChangeStatus("Reading materials", (double)(i+1) / pieces.Count() * 100);
            }

            dict[typeof(GSAMaterial)] = materials;
        }

        public static void WriteObjects(Dictionary<Type, object> dict)
        {
            if (!dict.ContainsKey(typeof(GSAMaterial))) return;

            List<GSAObject> materials = dict[typeof(GSAMaterial)] as List<GSAObject>;

            double counter = 1;
            foreach (GSAObject m in materials)
            {
                GSARefCounters.RefObject(m);

                GSA.RunGWACommand(m.GetGWACommand());
                Status.ChangeStatus("Writing materials", counter++ / materials.Count() * 100);
            }

            dict.Remove(typeof(GSAMaterial));
        }

        public override void ParseGWACommand(string command, Dictionary<Type, object> dict = null)
        {
            string[] pieces = command.ListSplit(",");

            int counter = 0;
            string identifier = pieces[counter++];
            LocalReference = Convert.ToInt32(pieces[counter++]);

            if (identifier.Contains("STEEL"))
            {
                Type = "STEEL";
                counter++; // Move to name field of basic MAT definition
            }
            else if (identifier.Contains("CONCRETE"))
            {
                Type = "CONCRETE";
                counter++; // Move to name field of basic MAT definition
            }
            else
                Type = "GENERAL";

            Grade = pieces[counter++].Trim(new char[] { '"' }); // Use name as grade

            // TODO: Skip all other properties for now
        }

        public override string GetGWACommand(Dictionary<Type, object> dict = null)
        {
            List<string> ls = new List<string>();

            ls.Add("SET");
            switch (Type)
            {
                case "STEEL":
                    ls.Add("MAT_STEEL.3");
                    ls.Add(Reference.ToString());
                    ls.Add("MAT.6");
                    ls.Add(Grade);
                    break;
                case "CONCRETE":
                    ls.Add("MAT_CONCRETE.16");
                    ls.Add(Reference.ToString());
                    ls.Add("MAT.6");
                    ls.Add(Grade);
                    break;
                default:
                    ls.Add("MAT.6");
                    ls.Add(Reference.ToString());
                    ls.Add(Grade);
                    break;
            }

            ls.Add("YES");
            ls.Add("0"); // E
            ls.Add("0"); // nu
            ls.Add("0"); // rho
            ls.Add("0"); // alpha
            ls.Add("0"); // num ULS C curve
            ls.Add("0"); // num SLS C curve
            ls.Add("0"); // num ULS T curve
            ls.Add("0"); // num SLS T curve
            ls.Add("0"); // limit strain
            ls.Add("MAT_CURVE_PARAM.2");
            ls.Add("");
            ls.Add("UNDEF");
            ls.Add("0");
            ls.Add("0");
            ls.Add("MAT_CURVE_PARAM.2");
            ls.Add("");
            ls.Add("UNDEF");
            ls.Add("0");
            ls.Add("0");
            ls.Add("0"); // cost
            
            return string.Join(",", ls);
        }

        public override List<GSAObject> GetChildren()
        {
            throw new NotImplementedException();
        }
        #endregion

    }
}
