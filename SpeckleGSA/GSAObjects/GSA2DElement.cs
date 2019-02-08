using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using Interop.Gsa_10_0;
using SpeckleStructures;

namespace SpeckleGSA
{
    public class GSA2DElement : Structural2DElement
    {
        public static readonly string GSAKeyword = "EL";
        public static readonly string Stream = "elements";

        public static readonly Type[] ReadPrerequisite = new Type[2] { typeof(GSANode), typeof(GSA2DProperty) };
        public static readonly Type[] WritePrerequisite = new Type[1] { typeof(GSA2DElementMesh) };
        public static readonly bool AnalysisLayer = true;
        public static readonly bool DesignLayer = false;

        private List<int> Connectivity;
        public int MeshReference;

        #region Contructors and Converters
        public GSA2DElement()
        {
            Connectivity = new List<int>();
            MeshReference = 0;
        }

        public GSA2DElement(Structural2DElement baseClass)
        {
            Connectivity = new List<int>();
            MeshReference = 0;

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

            if (!GSA.TargetAnalysisLayer) return;

            List<StructuralObject> e2Ds = new List<StructuralObject>();

            string res = (string)GSA.RunGWACommand("GET_ALL,EL");

            if (res == "")
                return;

            string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            List<StructuralObject> nodes = dict[typeof(GSANode)];

            double counter = 1;
            foreach (string p in pieces)
            {
                string[] pPieces = p.ListSplit(",");
                int numConnectivity = pPieces[4].ParseElementNumNodes();
                // TODO: Only supports QUAD4 and TRI3. Throw out everything else since they're useless.
                if (pPieces[4] == "QUAD4" || pPieces[4] == "TRI3")
                {
                    GSA2DElement e2D = new GSA2DElement();
                    e2D.ParseGWACommand(p, dict);

                    e2Ds.Add(e2D);
                }
                Status.ChangeStatus("Reading 2D elements", counter++ / pieces.Length * 100);
            }

            dict[typeof(GSA2DElement)].AddRange(e2Ds);
        }

        public static void WriteObjects(Dictionary<Type, List<StructuralObject>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType)) return;

            List<StructuralObject> e2Ds = dict[typeof(GSA2DElement)];

            double counter = 1;
            foreach (StructuralObject e in e2Ds)
            {
                GSARefCounters.RefObject(e);

                List<StructuralObject> eNodes = (e as GSA2DElement).GetChildren();

                // Ensure no coincident nodes
                if (!dict.ContainsKey(typeof(GSANode)))
                    dict[typeof(GSANode)] = new List<StructuralObject>();

                dict[typeof(GSANode)] = HelperFunctions.CollapseNodes(dict[typeof(GSANode)].Cast<GSANode>().ToList(), eNodes.Cast<GSANode>().ToList()).Cast<StructuralObject>().ToList();

                (e as GSA2DElement).Connectivity = eNodes.Select(n => n.Reference).ToList();

                GSA.RunGWACommand((e as GSA2DElement).GetGWACommand());
                Status.ChangeStatus("Writing 2D elements", counter++ / e2Ds.Count() * 100);
            }
        }

        public void ParseGWACommand(string command, Dictionary<Type, List<StructuralObject>> dict = null)
        {
            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier
            Reference = Convert.ToInt32(pieces[counter++]);
            Name = pieces[counter++].Trim(new char[] { '"' });
            Color = pieces[counter++].ParseGSAColor(); // Color
            string type = pieces[counter++];
            Type = Structural2DElementType.GENERIC;
            Property = Convert.ToInt32(pieces[counter++]);
            counter++; // Group

            Coordinates = new Coordinates();
            for (int i = 0; i < type.ParseElementNumNodes(); i++)
            {
                int key = Convert.ToInt32(pieces[counter++]);
                Coordinates.Add(dict[typeof(GSANode)].Cast<GSANode>().Where(n => n.Reference == key).FirstOrDefault().Coordinates);
            }

            counter++; // Orientation node

            if (dict.ContainsKey(typeof(GSA2DProperty)))
            { 
                List<StructuralObject> props = dict[typeof(GSA2DProperty)];
                GSA2DProperty prop = props.Cast<GSA2DProperty>().Where(p => p.Reference == Property).FirstOrDefault();
                Axis = HelperFunctions.Parse2DAxis(Coordinates.ToArray(),
                    Convert.ToDouble(pieces[counter++]),
                    prop == null ? false : (prop as GSA2DProperty).IsAxisLocal);
            }
            else
            {
                Axis = HelperFunctions.Parse2DAxis(Coordinates.ToArray(),
                    Convert.ToDouble(pieces[counter++]),
                    false);
            }

            if (pieces[counter++] != "NO_RLS")
            {
                string start = pieces[counter++];
                string end = pieces[counter++];

                counter += start.Split('K').Length - 1 + end.Split('K').Length - 1;
            }
            
            counter++; //Ofsset x-start
            counter++; //Ofsset x-end
            counter++; //Ofsset y

            Offset = GetGSATotalElementOffset(Property,Convert.ToDouble(pieces[counter++]));

            //counter++; // Action // TODO: EL.4 SUPPORT
            counter++; // Dummy
        }

        public string GetGWACommand(Dictionary<Type, List<StructuralObject>> dict = null)
        {
            // Error check type and number of coordinates
            if (Coordinates.Count() != 3 && Coordinates.Count() != 4) return "";

            List<string> ls = new List<string>();

            ls.Add("SET");
            ls.Add(GSAKeyword);
            ls.Add(Reference.ToString());
            ls.Add(Name);
            ls.Add(Color == null ? "NO_RGB" : Color.ToString());
            ls.Add(Coordinates.Count() == 3 ? "TRI3" : "QUAD4");
            ls.Add(Property.ToString());
            ls.Add("0"); // Group
            foreach (int c in Connectivity)
                ls.Add(c.ToString());
            ls.Add("0"); //Orientation node
            ls.Add(HelperFunctions.Get2DAngle(Coordinates.ToArray(),Axis).ToString());
            ls.Add("NO_RLS");

            ls.Add("0"); // Offset x-start
            ls.Add("0"); // Offset x-end
            ls.Add("0"); // Offset y
            ls.Add(Offset.ToString());

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
        
        private double GetGSATotalElementOffset(int prop, double insertionPointOffset)
        {
            double materialInsertionPointOffset = 0;
            double zMaterialOffset = 0;
            double materialThickness = 0;

            string res = (string)GSA.RunGWACommand("GET,PROP_2D," + prop);

            if (res == null || res == "")
                return insertionPointOffset;

            string[] pieces = res.ListSplit(",");

            materialThickness = Convert.ToDouble(pieces[10]);
            switch (pieces[11])
            {
                case "TOP_CENTRE":
                    materialInsertionPointOffset = -materialThickness / 2;
                    break;
                case "BOT_CENTRE":
                    materialInsertionPointOffset = materialThickness / 2;
                    break;
                default:
                    materialInsertionPointOffset = 0;
                    break;
            }

            zMaterialOffset = -Convert.ToDouble(pieces[12]);
            return insertionPointOffset + zMaterialOffset + materialInsertionPointOffset;
        }
        #endregion
    }
}
