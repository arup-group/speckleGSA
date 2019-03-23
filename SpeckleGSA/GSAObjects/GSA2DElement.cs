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
    public class GSA2DElement : Structural2DElementMesh, IGSAObject
    {
        public string GWACommand { get; set; }
        public List<string> SubGWACommand { get; set; }
        
        #region Sending Functions
        public static bool GetObjects(Dictionary<Type, List<IGSAObject>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType))
                dict[MethodBase.GetCurrentMethod().DeclaringType] = new List<IGSAObject>();

            List<GSA2DElement> elements = new List<GSA2DElement>();
            List<GSANode> nodes = dict[typeof(GSANode)].Cast<GSANode>().ToList();
            List<GSA2DProperty> props = dict[typeof(GSA2DProperty)].Cast<GSA2DProperty>().ToList();

            string keyword = MethodBase.GetCurrentMethod().DeclaringType.GetGSAKeyword();

            string[] lines = GSA.GetGWAGetCommands("GET_ALL," + keyword);
            string[] deletedLines = GSA.GetDeletedGWAGetCommands("GET_ALL," + keyword);

            // Remove deleted lines
            dict[typeof(GSA2DElement)].RemoveAll(l => deletedLines.Contains(l.GWACommand));
            foreach (KeyValuePair<Type, List<IGSAObject>> kvp in dict)
                kvp.Value.RemoveAll(l => l.SubGWACommand.Any(x => deletedLines.Contains(x)));

            // Filter only new lines
            string[] prevLines = dict[typeof(GSA2DElement)].Select(l => l.GWACommand).ToArray();
            string[] newLines = lines.Where(l => !prevLines.Contains(l)).ToArray();

            foreach (string p in newLines)
            {
                string[] pPieces = p.ListSplit(",");
                if (pPieces[4].ParseElementNumNodes() == 2)
                {
                    GSA2DElement element = ParseGWACommand(p, nodes, props);
                    elements.Add(element);
                }
            }

            dict[typeof(GSA2DElement)].AddRange(elements);

            if (elements.Count() > 0 || deletedLines.Length > 0) return true;

            return false;
        }

        public static GSA2DElement ParseGWACommand(string command, List<GSANode> nodes, List<GSA2DProperty> props)
        {
            GSA2DElement ret = new Structural2DElementMesh() as GSA2DElement;

            ret.GWACommand = command;

            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier
            ret.StructuralID = pieces[counter++];
            ret.Name = pieces[counter++].Trim(new char[] { '"' });
            var color = pieces[counter++].ParseGSAColor();
            string type = pieces[counter++];
            if (color != null)
                ret.Colors = Enumerable.Repeat(color.Value, type.ParseElementNumNodes()).ToList();
            ret.ElementType = Structural2DElementType.Generic;
            ret.PropertyRef = pieces[counter++];
            counter++; // Group

            ret.Vertices = new List<double>();
            ret.Faces = new List<int>() { type.ParseElementNumNodes() - 3 };

            for (int i = 0; i < type.ParseElementNumNodes(); i++)
            {
                string key = pieces[counter++];
                GSANode node = nodes.Where(n => n.StructuralID == key).FirstOrDefault();
                ret.Vertices.AddRange(node.Value);
                ret.Faces.Add(i);
                ret.SubGWACommand.Add(node.GWACommand);
            }

            counter++; // Orientation node

            GSA2DProperty prop = props.Where(p => p.StructuralID == ret.PropertyRef).FirstOrDefault();
            ret.Axis = HelperFunctions.Parse2DAxis(ret.Vertices.ToArray(),
                Convert.ToDouble(pieces[counter++]),
                prop == null ? false : (prop as GSA2DProperty).IsAxisLocal);
            ret.SubGWACommand.Add(prop.GWACommand);

            if (pieces[counter++] != "NO_RLS")
            {
                string start = pieces[counter++];
                string end = pieces[counter++];

                counter += start.Split('K').Length - 1 + end.Split('K').Length - 1;
            }

            counter++; //Ofsset x-start
            counter++; //Ofsset x-end
            counter++; //Ofsset y

            ret.Offset = GetGSATotalElementOffset(Convert.ToInt32(ret.PropertyRef), Convert.ToDouble(pieces[counter++]));

            //counter++; // Action // TODO: EL.4 SUPPORT
            counter++; // Dummy

            return ret;
        }
        #endregion

        #region Receiving Functions
        public static void SetObjects(Dictionary<Type, List<IStructural>> dict)
        {
            // Set from GSA2DElementMesh
            return;
        }

        public static void Set(Structural2DElementMesh mesh, int group = 0)
        {
            if (mesh == null)
                return;

            string keyword = MethodBase.GetCurrentMethod().DeclaringType.GetGSAKeyword();

            int index = Indexer.ResolveIndex(MethodBase.GetCurrentMethod().DeclaringType, mesh);
            int propRef = Indexer.ResolveIndex(typeof(GSA2DProperty), mesh.PropertyRef);


            List<string> ls = new List<string>();

            ls.Add("SET");
            ls.Add(keyword);
            ls.Add(index.ToString());
            ls.Add(mesh.Name);
            ls.Add(mesh.Colors == null ? "NO_RGB" : mesh.Colors[0].ToHexColor().ToString());
            ls.Add(mesh.Vertices.Count() / 3 == 3 ? "TRI3" : "QUAD4");
            ls.Add(propRef.ToString());
            ls.Add(group.ToString()); // Group
            int numVertices = mesh.Faces[0] + 3;
            List<double> coor = new List<double>();
            for (int i = 1; i < mesh.Faces.Count(); i++)
            {
                coor.AddRange(mesh.Vertices.Skip(mesh.Faces[i] * 3).Take(3));
                ls.Add(GSA.NodeAt(mesh.Vertices[mesh.Faces[i] * 3], mesh.Vertices[mesh.Faces[i] * 3 + 1], mesh.Vertices[mesh.Faces[i] * 3 + 2]).ToString());
            }
            ls.Add("0"); //Orientation node
            ls.Add(HelperFunctions.Get2DAngle(coor.ToArray(), mesh.Axis).ToString());
            ls.Add("NO_RLS");

            ls.Add("0"); // Offset x-start
            ls.Add("0"); // Offset x-end
            ls.Add("0"); // Offset y
            ls.Add(mesh.Offset.ToString());

            //ls.Add("NORMAL"); // Action // TODO: EL.4 SUPPORT
            ls.Add(""); // Dummy

            GSA.RunGWACommand(string.Join(",", ls));
        }
        #endregion

        #region Helper Functions
        private static double GetGSATotalElementOffset(int propIndex, double insertionPointOffset)
        {
            double materialInsertionPointOffset = 0;
            double zMaterialOffset = 0;
            double materialThickness = 0;

            string res = (string)GSA.RunGWACommand("GET,PROP_2D," + propIndex.ToString());

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
