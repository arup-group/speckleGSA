using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using SpeckleStructures;

namespace SpeckleGSA
{
    public class GSA1DElement : Structural1DElement
    {
        public static readonly string GSAKeyword = "EL";
        public static readonly string Stream = "elements";

        public static readonly Type[] ReadPrerequisite = new Type[1] { typeof(GSANode) };
        public static readonly Type[] WritePrerequisite = new Type[1] { typeof(GSA1DProperty) };

        private List<int> Connectivity;
        public int PolylineReference;

        #region Contructors and Converters
        public GSA1DElement()
        {
            Connectivity = new List<int>();
            PolylineReference = 0;
        }
        
        public GSA1DElement(Structural1DElement baseClass)
        {
            Connectivity = new List<int>();
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
            dict[MethodBase.GetCurrentMethod().DeclaringType] = new List<StructuralObject>();

            foreach (Type t in ReadPrerequisite)
                if (!dict.ContainsKey(t)) return;

            if (!GSA.TargetAnalysisLayer) return;

            List<StructuralObject> e1Ds = new List<StructuralObject>();
            
            // Grab all elements and check if they are 1D elements
            string res = (string)GSA.RunGWACommand("GET_ALL,EL");

            if (res == "")
                return;

            string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            List<StructuralObject> nodes = dict[typeof(GSANode)];

            double counter = 1;
            foreach (string p in pieces)
            {
                string[] pPieces = p.ListSplit(",");
                if (pPieces[4].ParseElementNumNodes() == 2)
                {
                    GSA1DElement e1D = new GSA1DElement();
                    e1D.ParseGWACommand(p, dict);
                    e1Ds.Add(e1D);
                }
                Status.ChangeStatus("Reading 1D elements", counter++ / pieces.Length * 100);
            }

            dict[typeof(GSA1DElement)].AddRange(e1Ds);
        }

        public static void WriteObjects(Dictionary<Type, List<StructuralObject>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType))
                dict[MethodBase.GetCurrentMethod().DeclaringType] = new List<StructuralObject>();

            List<StructuralObject> e1Ds = dict[typeof(GSA1DElement)];

            double counter = 1;
            foreach (StructuralObject e in e1Ds)
            {
                GSARefCounters.RefObject(e);

                List<StructuralObject> eNodes = (e as GSA1DElement).GetChildren();

                // Ensure no coincident nodes
                if (!dict.ContainsKey(typeof(GSANode)))
                    dict[typeof(GSANode)] = new List<StructuralObject>();
                
                dict[typeof(GSANode)] = HelperFunctions.CollapseNodes(dict[typeof(GSANode)].Cast<GSANode>().ToList(), eNodes.Cast<GSANode>().ToList()).Cast<StructuralObject>().ToList();

                (e as GSA1DElement).Connectivity = eNodes.Select(n => n.Reference).ToList();

                GSA.RunGWACommand((e as GSA1DElement).GetGWACommand());

                Status.ChangeStatus("Writing 1D elements", counter++ / e1Ds.Count() * 100);
            }
        }

        public void ParseGWACommand(string command, Dictionary<Type, List<StructuralObject>> dict = null)
        {
            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier
            Reference = Convert.ToInt32(pieces[counter++]);
            Name = pieces[counter++].Trim(new char[] { '"' });
            counter++; // Colour
            counter++; // Type
            Property = Convert.ToInt32(pieces[counter++]);
            counter++; // Group

            Coordinates = new Coordinates();
            for (int i = 0; i < 2; i++)
            {
                int key = Convert.ToInt32(pieces[counter++]);
                Coordinates.Add(dict[typeof(GSANode)].Cast<GSANode>().Where(n => n.Reference == key).FirstOrDefault().Coordinates);
            }

            int orientationNodeRef = Convert.ToInt32(pieces[counter++]);
            double rotationAngle = Convert.ToDouble(pieces[counter++]);

            if (orientationNodeRef != 0)
                Axis = HelperFunctions.Parse1DAxis(Coordinates.ToArray(),
                    rotationAngle,
                    dict[typeof(GSANode)].Cast<GSANode>().Where(n => n.Reference == orientationNodeRef).FirstOrDefault().Coordinates.ToArray());
            else
                Axis = HelperFunctions.Parse1DAxis(Coordinates.ToArray(), rotationAngle);


            if (pieces[counter++] != "NO_RLS")
            {
                string start = pieces[counter++];
                string end = pieces[counter++];

                EndCondition1.X = ParseEndRelease(start[0], pieces, ref counter);
                EndCondition1.Y = ParseEndRelease(start[1], pieces, ref counter);
                EndCondition1.Z = ParseEndRelease(start[2], pieces, ref counter);
                EndCondition1.XX = ParseEndRelease(start[3], pieces, ref counter);
                EndCondition1.YY = ParseEndRelease(start[4], pieces, ref counter);
                EndCondition1.ZZ = ParseEndRelease(start[5], pieces, ref counter);
                EndCondition2.X = ParseEndRelease(end[0], pieces, ref counter);
                EndCondition2.Y = ParseEndRelease(end[1], pieces, ref counter);
                EndCondition2.Z = ParseEndRelease(end[2], pieces, ref counter);
                EndCondition2.XX = ParseEndRelease(end[3], pieces, ref counter);
                EndCondition2.YY = ParseEndRelease(end[4], pieces, ref counter);
                EndCondition2.ZZ = ParseEndRelease(end[5], pieces, ref counter);
            }

            Offset1.X = Convert.ToDouble(pieces[counter++]);
            Offset2.X = Convert.ToDouble(pieces[counter++]);

            Offset1.Y = Convert.ToDouble(pieces[counter++]);
            Offset2.Y = Offset1.Y;

            Offset1.Z = Convert.ToDouble(pieces[counter++]);
            Offset2.Z = Offset1.Z;

            //counter++; // Action // TODO: EL.4 SUPPORT
            counter++; // Dummy
        }

        public string GetGWACommand(Dictionary<Type, List<StructuralObject>> dict = null)
        {
            List<string> ls = new List<string>();

            ls.Add("SET");
            ls.Add(GSAKeyword);
            ls.Add(Reference.ToString());
            ls.Add(Name);
            ls.Add("NO_RGB");
            ls.Add("BEAM"); // Type
            ls.Add(Property.ToString());
            ls.Add("0"); // Group
            foreach (int c in Connectivity)
                ls.Add(c.ToString());

            ls.Add("0"); // Orientation Node
            ls.Add(HelperFunctions.Get1DAngle(Axis).ToString());
            
            if (EndCondition1 != new SixVectorBool() || EndCondition2 != new SixVectorBool())
            {
                ls.Add("RLS");

                string end1 = "";

                end1 += EndCondition1.X ? "F" : "R";
                end1 += EndCondition1.Y ? "F" : "R";
                end1 += EndCondition1.Z ? "F" : "R";
                end1 += EndCondition1.XX ? "F" : "R";
                end1 += EndCondition1.YY ? "F" : "R";
                end1 += EndCondition1.ZZ ? "F" : "R";

                ls.Add(end1);

                string end2 = "";

                end2 += EndCondition2.X ? "F" : "R";
                end2 += EndCondition2.Y ? "F" : "R";
                end2 += EndCondition2.Z ? "F" : "R";
                end2 += EndCondition2.XX ? "F" : "R";
                end2 += EndCondition2.YY ? "F" : "R";
                end2 += EndCondition2.ZZ ? "F" : "R";

                ls.Add(end2);

            }
            else
                ls.Add("NO_RLS");

            ls.Add(Offset1.X.ToString()); // Offset x-start
            ls.Add(Offset2.X.ToString()); // Offset x-end

            ls.Add(Offset1.Y.ToString());
            ls.Add(Offset1.Z.ToString());

            //ls.Add("NORMAL"); // Action // TODO: EL.4 SUPPORT
            ls.Add(""); // Dummy

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

        private bool ParseEndRelease(char code, string[] pieces, ref int counter)
        {
            switch (code)
            {
                case 'F':
                    return true;
                case 'R':
                    return false;
                default:
                    // TODO
                    Status.AddError("Element end stiffness not supported. Only releases.");
                    counter++;
                    return false;
            }
        }
        #endregion
    }
}
