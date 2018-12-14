﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA
{
    public class GSANode : GSAObject
    {
        public Dictionary<string, object> Axis { get; set; }
        public Dictionary<string, object> Restraint { get; set; }
        public Dictionary<string, object> Stiffness { get; set; }
        public double Mass { get; set; }

        public Dictionary<string, object> GridData;// { get; set; }
        public Dictionary<string, object> MeshData;// { get; set; }

        public GSANode() : base("NODE")
        {
            Axis = new Dictionary<string, object>()
            {
                { "X", new Dictionary<string, object> { { "x", 1 }, { "y", 0 },{ "z", 0 }  } },
                { "Y", new Dictionary<string, object> { { "x", 0 }, { "y", 1 },{ "z", 0 }  } },
                { "Z", new Dictionary<string, object> { { "x", 0 }, { "y", 0 },{ "z", 1 }  } },
            };
            Restraint = new Dictionary<string, object>()
            {
                { "x", false },
                { "y", false },
                { "z", false },
                { "xx", false },
                { "yy", false },
                { "zz", false },
            };
            Stiffness = new Dictionary<string, object>()
            {
                { "x", 0.0 },
                { "y", 0.0 },
                { "z", 0.0 },
                { "xx", 0.0 },
                { "yy", 0.0 },
                { "zz", 0.0 },
            };
            Mass = 0;

            GridData = new Dictionary<string, object>();
            MeshData = new Dictionary<string, object>();
        }

        public override void ParseGWACommand(string command)
        {
            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier
            Reference = Convert.ToInt32(pieces[counter++]);
            Name = pieces[counter++].Trim(new char[] { '"' });
            Color = pieces[counter++].ParseGSAColor();
            Coor = new double[3];
            Coor[0] = Convert.ToDouble(pieces[counter++]);
            Coor[1] = Convert.ToDouble(pieces[counter++]);
            Coor[2] = Convert.ToDouble(pieces[counter++]);

            while (counter < pieces.Length)
            {
                string s = pieces[counter++];
                if (s == "GRID")
                {
                    GridData["GridPlane"] = pieces[counter++];
                    GridData["Datum"] = pieces[counter++];
                    GridData["GridLineA"] = pieces[counter++];
                    GridData["GridLineB"] = pieces[counter++];
                }
                else if (s == "REST")
                {
                    Restraint["x"] = pieces[counter++] == "0" ? false : true;
                    Restraint["y"] = pieces[counter++] == "0" ? false : true;
                    Restraint["z"] = pieces[counter++] == "0" ? false : true;
                    Restraint["xx"] = pieces[counter++] == "0" ? false : true;
                    Restraint["yy"] = pieces[counter++] == "0" ? false : true;
                    Restraint["zz"] = pieces[counter++] == "0" ? false : true;
                }
                else if (s == "STIFF")
                {
                    Stiffness["x"] = Convert.ToDouble(pieces[counter++]);
                    Stiffness["y"] = Convert.ToDouble(pieces[counter++]);
                    Stiffness["z"] = Convert.ToDouble(pieces[counter++]);
                    Stiffness["xx"] = Convert.ToDouble(pieces[counter++]);
                    Stiffness["yy"] = Convert.ToDouble(pieces[counter++]);
                    Stiffness["zz"] = Convert.ToDouble(pieces[counter++]);
                }
                else if (s == "MESH")
                {
                    MeshData["EdgeLength"] = pieces[counter++];
                    MeshData["Radius"] = pieces[counter++];
                    MeshData["TietoMesh"] = pieces[counter++];
                    MeshData["ColumnRigidity"] = pieces[counter++];
                    MeshData["ColumnProp"] = pieces[counter++];
                    MeshData["ColumnNode"] = pieces[counter++];
                    MeshData["ColumnAngle"] = pieces[counter++];
                    MeshData["ColumnFactor"] = pieces[counter++];
                    MeshData["ColumnSlabFactor"] = pieces[counter++];
                }
                else
                    Axis = Convert.ToInt32(pieces[counter++]).EvaluateGSAAxis(gsa, Coor);
            }
            return;
        }

        public override string GetGWACommand()
        {
            List<string> ls = new List<string>();

            ls.Add("SET");
            ls.Add("NODE.2");
            ls.Add(Reference.ToString());
            ls.Add(Name);
            if (Color == null)
                ls.Add("NO_RGB");
            else
                ls.Add(((int)Color).ToString());
            ls.Add(string.Join(",", Coor));

            if (GridData.Count == 0)
                ls.Add("NO_GRID");
            else
            {
                ls.Add("GRID");
                ls.Add((string)GridData["GridPlane"]);
                ls.Add((string)GridData["Datum"]);
                ls.Add((string)GridData["GridLineA"]);
                ls.Add((string)GridData["GridLineB"]);
            }

            ls.Add(Axis.AddAxistoGSA(gsa).ToString());

            if (Restraint.Count == 0)
                ls.Add("NO_REST");
            else
            {
                ls.Add("REST");
                ls.Add(((bool)Restraint["x"]) ? "1" : "0");
                ls.Add(((bool)Restraint["y"]) ? "1" : "0");
                ls.Add(((bool)Restraint["z"]) ? "1" : "0");
                ls.Add(((bool)Restraint["xx"]) ? "1" : "0");
                ls.Add(((bool)Restraint["yy"]) ? "1" : "0");
                ls.Add(((bool)Restraint["zz"]) ? "1" : "0");
            }

            if (Stiffness.Count == 0)
                ls.Add("NO_STIFF");
            else
            {
                ls.Add("STIFF");
                ls.Add(Stiffness["x"].ToNumString());
                ls.Add(Stiffness["y"].ToNumString());
                ls.Add(Stiffness["z"].ToNumString());
                ls.Add(Stiffness["xx"].ToNumString());
                ls.Add(Stiffness["yy"].ToNumString());
                ls.Add(Stiffness["zz"].ToNumString());
            }

            if (MeshData.Count == 0)
                ls.Add("NO_MESH");
            else
            {
                ls.Add("MESH");
                ls.Add((string)MeshData["EdgeLength"]);
                ls.Add((string)MeshData["Radius"]);
                ls.Add((string)MeshData["TietoMesh"]);
                ls.Add((string)MeshData["ColumnRigidity"]);
                ls.Add((string)MeshData["ColumnProp"]);
                ls.Add((string)MeshData["ColumnNode"]);
                ls.Add((string)MeshData["ColumnAngle"]);
                ls.Add((string)MeshData["ColumnFactor"]);
                ls.Add((string)MeshData["ColumnSlabFactor"]);

            }

            return string.Join(",", ls);
        }

        public override List<GSAObject> GetChildren()
        {
            return null;
        }

        public void Merge0DElement(GSAElement elem)
        {
            if (elem.Type == "MASS")
            {
                Mass = elem.GetGSAMass(gsa);
            }
        }

        public List<GSAElement> Extract0DElement()
        {
            List<GSAElement> elemList = new List<GSAElement>();

            if (Mass > 0)
            {
                GSAElement massElem = new GSAElement().AttachGSA(gsa);
                massElem.Type = "MASS";
                massElem.Connectivity = new int[] { Reference };
                massElem.Property = Mass.AddMasstoGSA(gsa);
                elemList.Add(massElem);
            }
            return elemList;
        }

        public void Merge(GSANode mergeNode)
        {
            Dictionary<string, object> temp = new Dictionary<string, object>();
            foreach (string key in Restraint.Keys)
                temp[key] = (bool)Restraint[key] | (bool)mergeNode.Restraint[key];
            Restraint = temp;

            temp = new Dictionary<string, object>();
            foreach (string key in Stiffness.Keys)
                temp[key] = Math.Max((double)Stiffness[key], (double)mergeNode.Stiffness[key]);
            Stiffness = temp;

            Mass += mergeNode.Mass;

            gsa.GwaCommand(GetGWACommand());
        }
    }
}
