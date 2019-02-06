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
    public class GSA2DMember : Structural2DElementMesh
    {
        public static readonly string GSAKeyword = "MEMB";
        public static readonly string Stream = "elements";

        public static readonly Type[] ReadPrerequisite = new Type[3] { typeof(GSANode), typeof(GSA2DProperty), typeof(GSA2DElementMesh) };
        public static readonly Type[] WritePrerequisite = new Type[1] { typeof(GSA2DElementMesh) };
        
        public List<int> Connectivity;

        public GSA2DMember()
        {
            Connectivity = new List<int>();
        }
        
        public GSA2DMember(Structural2DElementMesh baseClass)
        {
            Connectivity = new List<int>();

            foreach (FieldInfo f in baseClass.GetType().GetFields())
                f.SetValue(this, f.GetValue(baseClass));

            foreach (PropertyInfo p in baseClass.GetType().GetProperties())
                p.SetValue(this, p.GetValue(baseClass));
        }

        #region GSAObject Functions
        public static void GetObjects(Dictionary<Type, List<StructuralObject>> dict)
        {
            if (!GSA.TargetDesignLayer) return;

            if (!dict.ContainsKey(typeof(GSANode))) return;

            List<StructuralObject> nodes = dict[typeof(GSANode)];
            List<StructuralObject> m2Ds = new List<StructuralObject>();

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
                if (pPieces[4].MemberIs2D())
                {
                    GSA2DMember m2D = new GSA2DMember();
                    m2D.ParseGWACommand(p, dict);
                    m2Ds.Add(m2D);
                }

                Status.ChangeStatus("Reading 2D members", counter++ / pieces.Length * 100);
            }

            dict[typeof(GSA2DMember)] = m2Ds;
        }

        public static void WriteObjects(Dictionary<Type, List<StructuralObject>> dict)
        {
            if (!dict.ContainsKey(typeof(GSA2DMember))) return;

            List<StructuralObject> m2Ds = dict[typeof(GSA2DMember)];

            double counter = 1;
            foreach (StructuralObject m in m2Ds)
            {
                GSARefCounters.RefObject(m);

                List<StructuralObject> eNodes = (m as GSA2DMember).GetChildren();

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

                (m as GSA2DMember).Connectivity = eNodes.Select(n => n.Reference).ToList();

                GSA.RunGWACommand((m as GSA2DMember).GetGWACommand());
                Status.ChangeStatus("Writing 2D members", counter++ / m2Ds.Count() * 100);
            }
        }

        public void ParseGWACommand(string command, Dictionary<Type, List<StructuralObject>> dict = null)
        {
            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier
            Reference = Convert.ToInt32(pieces[counter++]);
            Name = pieces[counter++].Trim(new char[] { '"' });
            Color = pieces[counter++].ParseGSAColor();
            
            string type = pieces[counter++];
            if (type == "SLAB")
                Type = Structural2DElementType.SLAB;
            else if (type == "WALL")
                Type = Structural2DElementType.WALL;
            else
                Type = Structural2DElementType.GENERIC;
            
            Property = Convert.ToInt32(pieces[counter++]);
            counter++; // Group

            List<double> coordinates = new List<double>();
            string[] nodeRefs = pieces[counter++].ListSplit(" ");

            for (int i = 0; i < nodeRefs.Length; i++)
                coordinates.AddRange(dict[typeof(GSANode)].Cast<GSANode>().Where(n => n.Reference == Convert.ToInt32(nodeRefs[i])).FirstOrDefault().Coordinates.ToArray());

            SetFromEdge(new Coordinates(coordinates.ToArray()));

            counter++; // Orientation node

            if (dict.ContainsKey(typeof(GSA2DProperty)))
            {
                List<StructuralObject> props = dict[typeof(GSA2DProperty)];
                GSA2DProperty prop = props.Cast<GSA2DProperty>().Where(p => p.Reference == Property).FirstOrDefault();
                Axis = HelperFunctions.Parse2DAxis(coordinates.ToArray(),
                    Convert.ToDouble(pieces[counter++]),
                    prop == null ? false : (prop as GSA2DProperty).IsAxisLocal);
            }
            else
            {
                Axis = HelperFunctions.Parse2DAxis(coordinates.ToArray(),
                    Convert.ToDouble(pieces[counter++]),
                    false);
            }

            // Skip to offsets at second to last
            counter = pieces.Length - 2;
            Offset = Convert.ToDouble(pieces[counter++]);
        }

        public string GetGWACommand(Dictionary<Type, List<StructuralObject>> dict = null)
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
            if (Type == Structural2DElementType.SLAB)
                ls.Add("SLAB");
            else if (Type == Structural2DElementType.WALL)
                ls.Add("WALL");
            else
                ls.Add("2D_GENERIC");
            ls.Add(Property.ToString());
            ls.Add(Reference.ToString()); // TODO: This allows for targeting of elements from members group
            string topo = "";
            foreach (int c in Connectivity)
                topo += c.ToString() + " ";
            ls.Add(topo);
            ls.Add("0"); // Orientation node
            ls.Add(HelperFunctions.Get2DAngle(Coordinates().ToArray(), Axis).ToString());
            ls.Add("0"); // Target mesh size
            ls.Add("0"); // Fire
            ls.Add("0"); // Time 1
            ls.Add("0"); // Time 2
            ls.Add("0"); // Time 3
            ls.Add("0"); // TODO: What is this?
            ls.Add("ACTIVE"); // Dummy
            ls.Add("0"); // End 1 condition
            ls.Add("0"); // End 2 condition
            ls.Add("AUTOMATIC"); // Effective length option
            ls.Add("0"); // Pool
            ls.Add("0"); // Height
            ls.Add("MAN"); // Auto offset 1
            ls.Add("MAN"); // Auto offset 2
            ls.Add("0"); // Offset x 1
            ls.Add("0"); // Offset x 2
            ls.Add("0"); // Offset y
            ls.Add(Offset.ToNumString()); // Offset z
            ls.Add("ALL"); // Exposure

            return string.Join(",", ls);
        }

        public List<StructuralObject> GetChildren()
        {
            List<StructuralObject> children = new List<StructuralObject>();

            List<int[]> connectivities = EdgeConnectivities();
            Coordinates coordinates = Coordinates();

            foreach(int[] conn in connectivities)
            { 
                foreach(int c in conn)
                { 
                    GSANode n = new GSANode();
                    n.Coordinates = new Coordinates(coordinates.Values[c].ToArray());
                    children.Add(n);
                }
            }
            return children;
        }
        #endregion
    }
}
