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
        public static readonly bool AnalysisLayer = false;
        public static readonly bool DesignLayer = true;

        public List<int> Connectivity;
        public int Group;
        public int PolylineReference;

        #region Contructors and Converters
        public GSA1DMember()
        {
            Connectivity = new List<int>();
            Group = 0;
            PolylineReference = 0;
        }

        public GSA1DMember(Structural1DElement baseClass)
        {
            Connectivity = new List<int>();
            Group = 0;
            PolylineReference = 0;

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
        #endregion

        #region GSA Functions
        public static void GetObjects(Dictionary<Type, List<StructuralObject>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType))
                dict[MethodBase.GetCurrentMethod().DeclaringType] = new List<StructuralObject>();

            if (!GSA.TargetDesignLayer) return;

            List<StructuralObject> m1Ds = new List<StructuralObject>();

            // Workaround for GET_ALL,MEMB GWA bug
            int[] memberRefs = new int[0];
            GSA.GSAObject.EntitiesInList("all", GsaEntity.MEMBER, out memberRefs);

            if (memberRefs == null || memberRefs.Length == 0)
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

            dict[typeof(GSA1DMember)].AddRange(m1Ds);
        }

        public static void WriteObjects(Dictionary<Type, List<StructuralObject>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType)) return;

            List<StructuralObject> m1Ds = dict[typeof(GSA1DMember)];

            double counter = 1;
            foreach (StructuralObject m in m1Ds)
            {
                int currRef = m.Reference;
                if (GSARefCounters.RefObject(m) != 0)
                {
                    m.Reference = 0;
                    GSARefCounters.RefObject(m);
                    if (dict.ContainsKey(typeof(GSA1DLoad)))
                    {
                        foreach (StructuralObject o in dict[typeof(GSA1DLoad)])
                        {
                            if ((o as GSA1DLoad).Elements.Contains(currRef))
                            {
                                (o as GSA1DLoad).Elements.Remove(currRef);
                                (o as GSA1DLoad).Elements.Add(m.Reference);
                            }
                        }
                    }
                }

                List<StructuralObject> eNodes = (m as GSA1DMember).GetChildren();

                // Ensure no coincident nodes
                if (!dict.ContainsKey(typeof(GSANode)))
                    dict[typeof(GSANode)] = new List<StructuralObject>();

                dict[typeof(GSANode)] = HelperFunctions.CollapseNodes(dict[typeof(GSANode)].Cast<GSANode>().ToList(), eNodes.Cast<GSANode>().ToList()).Cast<StructuralObject>().ToList();

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
            Group = Convert.ToInt32(pieces[counter++]); // Keep group for load targetting

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

            counter += 9; //Skip to end conditions

            EndCondition1 = ParseEndCondition(Convert.ToInt32(pieces[counter++]));
            EndCondition2 = ParseEndCondition(Convert.ToInt32(pieces[counter++]));

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
            if (PolylineReference == 0)
                ls.Add(Reference.ToString());
            else
                ls.Add(PolylineReference.ToString());  // TODO: This allows for targeting of elements from members group
            string topo = "";
            foreach (int c in Connectivity)
                topo += c.ToString() + " ";
            ls.Add(topo);
            ls.Add("0"); // Orientation node
            ls.Add(HelperFunctions.Get1DAngle(Axis).ToString());
            ls.Add("1"); // Target mesh size
            ls.Add("MESH"); // TODO: What is this?
            ls.Add("BEAM"); // Element type
            ls.Add("0"); // Fire
            ls.Add("0"); // Time 1
            ls.Add("0"); // Time 2
            ls.Add("0"); // Time 3
            ls.Add("0"); // TODO: What is this?
            ls.Add("ACTIVE"); // Dummy

            if (EndCondition1.Equals(ParseEndCondition(1)))
                ls.Add("1");
            else if (EndCondition1.Equals(ParseEndCondition(2)))
                ls.Add("2");
            else if (EndCondition1.Equals(ParseEndCondition(3)))
                ls.Add("3");
            else
                ls.Add("2");

            if (EndCondition2.Equals(ParseEndCondition(1)))
                ls.Add("1");
            else if (EndCondition2.Equals(ParseEndCondition(2)))
                ls.Add("2");
            else if (EndCondition2.Equals(ParseEndCondition(3)))
                ls.Add("3");
            else
                ls.Add("2");

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
        #endregion

        #region Helper Functions
        public List<StructuralObject> GetChildren()
        {
            List<StructuralObject> children = new List<StructuralObject>();

            for (int i = 0; i < Coordinates.Count(); i++)
            {
                GSANode n = new GSANode();
                n.Coordinates = new Coordinates(Coordinates.Values[i].ToArray());
                children.Add(n);
            }

            return children;
        }

        public SixVectorBool ParseEndCondition(int option)
        {
            switch(option)
            {
                case 1:
                    // Pinned
                    return new SixVectorBool(true, true, true, true, false, false);
                case 2:
                    // Fixed
                    return new SixVectorBool(true, true, true, true, true, true);
                case 3:
                    // Free
                    return new SixVectorBool(false, false, false, false, false, false);
                case 4:
                    // Full rotational
                    return new SixVectorBool(true, true, true, true, true, true);
                case 5:
                    // Partial rotational
                    return new SixVectorBool(true, true, true, true, false, false);
                case 6:
                    // Top flange lateral
                    return new SixVectorBool(true, true, true, true, true, true);
                default:
                    // Pinned
                    return new SixVectorBool(true, true, true, true, false, false);
            }
        }
        #endregion
    }
}
