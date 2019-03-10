using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using Interop.Gsa_10_0;
using SpeckleStructuresClasses;

namespace SpeckleGSA
{
    [GSAObject("EL.3", "elements", true, false, new Type[] { typeof(GSANode), typeof(GSA2DProperty) }, new Type[] { typeof(GSA2DElementMesh) })]
    public class GSA2DElement : Structural2DElement, IGSAObject
    {
        private List<int> Connectivity;
        public int MeshReference;

        public string GWACommand { get; set; }
        public List<string> SubGWACommand { get; set; }

        #region Contructors and Converters
        public GSA2DElement()
        {
            GWACommand = "";
            SubGWACommand = new List<string>();
            Connectivity = new List<int>();
            MeshReference = 0;
        }

        public GSA2DElement(Structural2DElement baseClass)
        {
            GWACommand = "";
            SubGWACommand = new List<string>();
            Connectivity = new List<int>();
            MeshReference = 0;

            foreach (FieldInfo f in baseClass.GetType().GetFields())
                f.SetValue(this, f.GetValue(baseClass));

            foreach (PropertyInfo p in baseClass.GetType().GetProperties())
                p.SetValue(this, p.GetValue(baseClass));
        }
        #endregion

        #region GSA Functions
        public static bool GetObjects(Dictionary<Type, List<object>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType))
                dict[MethodBase.GetCurrentMethod().DeclaringType] = new List<object>();

            if (!GSA.TargetAnalysisLayer) return false;

            List<object> e2Ds = new List<object>();

            string[] lines = GSA.GetGWAGetCommands("GET_ALL,EL");
            string[] deletedLines = GSA.GetDeletedGWAGetCommands("GET_ALL,EL");

            // Remove deleted lines
            dict[typeof(GSA2DElement)].RemoveAll(l => deletedLines.Contains(((IGSAObject)l).GWACommand));
            foreach (KeyValuePair<Type, List<object>> kvp in dict)
                kvp.Value.RemoveAll(l => ((IGSAObject)l).SubGWACommand.Any(x => deletedLines.Contains(x)));

            // Filter only new lines
            string[] prevLines = dict[typeof(GSA2DElement)].Select(l => ((GSA2DElement)l).GWACommand).ToArray();
            string[] newLines = lines.Where(l => !prevLines.Contains(l)).ToArray();

            List<object> nodes = dict[typeof(GSANode)];
            
            foreach (string p in newLines)
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
            }

            dict[typeof(GSA2DElement)].AddRange(e2Ds);

            if (e2Ds.Count() > 0 || deletedLines.Length > 0) return true;

            return false;
        }

        public static void WriteObjects(Dictionary<Type, List<StructuralObject>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType)) return;

            List<StructuralObject> e2Ds = dict[typeof(GSA2DElement)];

            double counter = 1;
            foreach (StructuralObject e in e2Ds)
            {
                int currRef = e.Reference;
                if (GSARefCounters.RefObject(e) != 0)
                {
                    e.Reference = 0;
                    GSARefCounters.RefObject(e);
                    if (dict.ContainsKey(typeof(GSA2DLoad)))
                    {
                        foreach (StructuralObject o in dict[typeof(GSA2DLoad)])
                        {
                            if ((o as GSA2DLoad).Elements.Contains(currRef))
                            {
                                (o as GSA2DLoad).Elements.Remove(currRef);
                                (o as GSA2DLoad).Elements.Add(e.Reference);
                            }
                        }
                    }
                }

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

        public void ParseGWACommand(string command, Dictionary<Type, List<object>> dict = null)
        {
            GWACommand = command;

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
                GSANode node = dict[typeof(GSANode)].Cast<GSANode>().Where(n => n.Reference == key).FirstOrDefault();
                Coordinates.Add(node.Coordinates);
                SubGWACommand.Add(node.GWACommand);
            }

            counter++; // Orientation node

            if (dict.ContainsKey(typeof(GSA2DProperty)))
            { 
                List<object> props = dict[typeof(GSA2DProperty)];
                GSA2DProperty prop = props.Cast<GSA2DProperty>().Where(p => p.Reference == Property).FirstOrDefault();
                Axis = HelperFunctions.Parse2DAxis(Coordinates.ToArray(),
                    Convert.ToDouble(pieces[counter++]),
                    prop == null ? false : (prop as GSA2DProperty).IsAxisLocal);
                SubGWACommand.Add(prop.GWACommand);
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
            ls.Add((string)this.GetAttribute("GSAKeyword"));
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
