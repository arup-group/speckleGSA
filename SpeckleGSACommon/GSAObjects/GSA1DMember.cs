using Interop.Gsa_10_0;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using SpeckleStructures;
using System.Reflection;

namespace SpeckleGSA
{
    public class GSA1DMember : Structural1DElement
    {
        public static readonly string GSAKeyword = "MEMB";
        public static readonly string Stream = "elements";

        public static readonly Type[] ReadPrerequisite = new Type[1] { typeof(GSANode) };
        public static readonly Type[] WritePrerequisite = new Type[1] { typeof(GSA1DProperty) };

        public List<int> Connectivity;

        public GSA1DMember()
        {
            Connectivity = new List<int>();
        }

        public GSA1DMember(Structural1DElement baseClass)
        {
            Connectivity = new List<int>();

            foreach (FieldInfo f in baseClass.GetType().GetFields())
                f.SetValue(this, f.GetValue(baseClass));

            foreach (PropertyInfo p in baseClass.GetType().GetProperties())
                p.SetValue(this, p.GetValue(baseClass));
        }

        public StructuralObject GetBase()
        {
            StructuralObject baseClass = (StructuralObject)Activator.CreateInstance(this.GetType().BaseType);

            foreach (FieldInfo f in baseClass.GetType().GetFields())
                f.SetValue(baseClass, f.GetValue(this));

            foreach (PropertyInfo p in baseClass.GetType().GetProperties())
                p.SetValue(baseClass, p.GetValue(this));

            return baseClass;
        }

        public static void GetObjects(Dictionary<Type, List<StructuralObject>> dict)
        {
            if (!GSA.TargetDesignLayer) return;

            if (!dict.ContainsKey(typeof(GSANode))) return;

            List<StructuralObject> nodes = dict[typeof(GSANode)];
            List<StructuralObject> m1Ds = new List<StructuralObject>();

            // TODO: Workaround for GET_ALL,MEMB bug
            int[] memberRefs = new int[0];
            GSA.GSAObject.EntitiesInList("all", GsaEntity.MEMBER, out memberRefs);

            if (memberRefs.Length == 0)
                return;

            List<string> tempPieces = new List<string>();

            foreach (int r in memberRefs)
                tempPieces.Add((string)GSA.RunGWACommand("GET,MEMB," + r.ToString()));

            string[] pieces = tempPieces.ToArray();
            
            double counter = 1;
            foreach (string p in pieces)
            {
                string[] pPieces = p.ListSplit(",");
                if (pPieces[4].MemberIs1D())
                {
                    GSA1DMember m1D = new GSA1DMember();
                    m1D.ParseGWACommand(p, dict);
                    m1Ds.Add(m1D);
                }

                Status.ChangeStatus("Reading 1D members", counter++ / pieces.Length * 100);
            }

            dict[typeof(GSA1DMember)] = m1Ds;
        }

        public static void WriteObjects(Dictionary<Type, List<StructuralObject>> dict)
        {
            if (!dict.ContainsKey(typeof(GSA1DMember))) return;

            List<StructuralObject> m1Ds = dict[typeof(GSA1DMember)];

            double counter = 1;
            foreach (StructuralObject m in m1Ds)
            {
                GSARefCounters.RefObject(m);

                List<StructuralObject> eNodes = (m as GSA1DMember).GetChildren();

                if (dict.ContainsKey(typeof(GSANode)))
                {
                    List<StructuralObject> nodes = dict[typeof(GSANode)];

                    for (int i = 0; i < eNodes.Count(); i++)
                    {
                        List<StructuralObject> matches = nodes
                            .Where(n => (n as GSANode).Coordinates.Equals((eNodes[i] as GSANode).Coordinates)).ToList();

                        if (matches.Count() > 0)
                        {
                            if (matches[0].Reference == 0)
                                matches[0] = GSARefCounters.RefObject(matches[0]);

                            eNodes[i].Reference = matches[0].Reference;
                            (matches[0] as GSANode).Merge(eNodes[i] as GSANode);
                        }
                        else
                        {
                            GSARefCounters.RefObject(eNodes[i]);
                            dict[typeof(GSANode)].Add(eNodes[i]);
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < eNodes.Count(); i++)
                        GSARefCounters.RefObject(eNodes[i]);

                    dict[typeof(GSANode)] = eNodes;
                }

                (m as GSA1DMember).Connectivity = eNodes.Select(n => n.Reference).ToList();

                GSA.RunGWACommand((m as GSA1DMember).GetGWACommand());
                Status.ChangeStatus("Writing 1D members", counter++ / m1Ds.Count() * 100);
            }
        }

        public void ParseGWACommand(string command, Dictionary<Type, List<StructuralObject>> dict = null)
        {
            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier
            Reference = Convert.ToInt32(pieces[counter++]);
            Name = pieces[counter++].Trim(new char[] { '"' });
            counter++; // Color

            string type = pieces[counter++];
            if (type == "BEAM")
                Type = Structural1DElementType.BEAM;
            else if (type == "COLUMN")
                Type = Structural1DElementType.COLUMN;
            else if (type == "CANTILEVER")
                Type = Structural1DElementType.CANTILEVER;
            else
                Type = Structural1DElementType.GENERIC;
            
            Property = Convert.ToInt32(pieces[counter++]);
            counter++; // Group

            List<double> coordinates = new List<double>();
            string[] nodeRefs = pieces[counter++].ListSplit(" ");
            for (int i = 0; i < nodeRefs.Length; i++)
                coordinates.AddRange(dict[typeof(GSANode)].Cast<GSANode>().Where(n => n.Reference == Convert.ToInt32(nodeRefs[i])).FirstOrDefault().Coordinates.ToArray());

            Coordinates = new Coordinates(coordinates.ToArray());

            int orientationNodeRef = Convert.ToInt32(pieces[counter++]);
            double rotationAngle = Convert.ToDouble(pieces[counter++]);

            if (orientationNodeRef != 0)
                Axis = HelperFunctions.Parse1DAxis(Coordinates.ToArray(),
                    rotationAngle,
                    dict[typeof(GSANode)].Cast<GSANode>().Where(n => n.Reference == orientationNodeRef).FirstOrDefault().Coordinates.ToArray());
            else
                Axis = HelperFunctions.Parse1DAxis(Coordinates.ToArray(), rotationAngle);

            // Skip to offsets at fifth to last
            counter = pieces.Length - 5;
            Offset1.X = Convert.ToDouble(pieces[counter++]);
            Offset2.X = Convert.ToDouble(pieces[counter++]);

            Offset1.Y = Convert.ToDouble(pieces[counter++]);
            Offset2.Y = Offset1.Y;

            Offset1.Z = Convert.ToDouble(pieces[counter++]);
            Offset2.Z = Offset1.Z;
        }

        public string GetGWACommand(Dictionary<Type, List<StructuralObject>> dict = null)
        {
            List<string> ls = new List<string>();

            ls.Add("SET");
            ls.Add(GSAKeyword);
            ls.Add(Reference.ToString());
            ls.Add(Name);
            ls.Add("NO_RGB");
            if (Type == Structural1DElementType.BEAM)
                ls.Add("BEAM");
            else if (Type == Structural1DElementType.COLUMN)
                ls.Add("COLUMN");
            else if (Type == Structural1DElementType.CANTILEVER)
                ls.Add("CANTILEVER");
            else
                ls.Add("1D_GENERIC");
            ls.Add(Property.ToString());
            ls.Add("1"); // Group
            string topo = "";
            foreach (int c in Connectivity)
                topo += c.ToString() + " ";
            ls.Add(topo);
            ls.Add("0"); // Orientation node
            ls.Add(HelperFunctions.Get1DAngle(Axis).ToNumString());
            ls.Add("0"); // Target mesh size
            ls.Add("0"); // Fire
            ls.Add("0"); // Time 1
            ls.Add("0"); // Time 2
            ls.Add("0"); // Time 3
            ls.Add("0"); // TODO: What is this?
            ls.Add("ACTIVE"); // Dummy
            ls.Add("1"); // End 1 condition
            ls.Add("1"); // End 2 condition
            ls.Add("AUTOMATIC"); // Effective length option
            ls.Add("0"); // Pool
            ls.Add("0"); // Height
            ls.Add("MAN"); // Auto offset 1
            ls.Add("MAN"); // Auto offset 2
            ls.Add(Offset1.X.ToString()); // Offset x 1
            ls.Add(Offset2.X.ToString()); // Offset x 2
            ls.Add(Offset1.Y.ToString()); // Offset y
            ls.Add(Offset1.Z.ToString()); // Offset z
            ls.Add("ALL"); // Exposure

            return string.Join(",", ls);
        }

        public List<StructuralObject> GetChildren()
        {
            List<StructuralObject> children = new List<StructuralObject>();

            for (int i = 0; i < Coordinates.Count(); i++)
            {
                GSANode n = new GSANode();
                n.Coordinates = new Coordinates(Coordinates[i]);
                children.Add(n);
            }

            return children;
        }
    }
}
