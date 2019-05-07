using Interop.Gsa_10_0;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using SpeckleStructuresClasses;
using System.Reflection;

namespace SpeckleGSA
{
    [GSAObject("MEMB.7", new string[] { "NODE.2", "PROP_2D.5" }, "elements", false, true, new Type[] { typeof(GSANode), typeof(GSA2DProperty) }, new Type[] { typeof(GSA2DProperty) })]
    public class GSA2DMember : Structural2DElementMesh, IGSAObject
    {
        public int Group;

        public string GWACommand { get; set; } = "";
        public List<string> SubGWACommand { get; set; } = new List<string>();

        #region Sending Functions
        public static bool GetObjects(Dictionary<Type, List<IGSAObject>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType))
                dict[MethodBase.GetCurrentMethod().DeclaringType] = new List<IGSAObject>();

            List<GSA2DMember> members = new List<GSA2DMember>();
            List<GSANode> nodes = dict[typeof(GSANode)].Cast<GSANode>().ToList();
            List<GSA2DProperty> props = dict[typeof(GSA2DProperty)].Cast<GSA2DProperty>().ToList();

            string keyword = MethodBase.GetCurrentMethod().DeclaringType.GetGSAKeyword();
            string[] subKeywords = MethodBase.GetCurrentMethod().DeclaringType.GetSubGSAKeyword();

            string[] lines = GSA.GetGWARecords("GET_ALL," + keyword);
            List<string> deletedLines = GSA.GetDeletedGWARecords("GET_ALL," + keyword).ToList();
            foreach (string k in subKeywords)
                deletedLines.AddRange(GSA.GetDeletedGWARecords("GET_ALL," + k));

            // Remove deleted lines
            dict[typeof(GSA2DMember)].RemoveAll(l => deletedLines.Contains(l.GWACommand));
            foreach (KeyValuePair<Type, List<IGSAObject>> kvp in dict)
                kvp.Value.RemoveAll(l => l.SubGWACommand.Any(x => deletedLines.Contains(x)));

            // Filter only new lines
            string[] prevLines = dict[typeof(GSA2DMember)].Select(l => l.GWACommand).ToArray();
            string[] newLines = lines.Where(l => !prevLines.Contains(l)).ToArray();

            foreach (string p in newLines)
            {
                string[] pPieces = p.ListSplit(",");
                if (pPieces[4].MemberIs2D())
                {
                    // Check if dummy
                    if (pPieces[pPieces.Length - 4] == "ACTIVE")
                    {
                        GSA2DMember member = ParseGWACommand(p, nodes, props);
                        members.Add(member);
                    }
                }
            }

            dict[typeof(GSA2DMember)].AddRange(members);

            if (members.Count() > 0 || deletedLines.Count() > 0) return true;

            return false;
        }

        public static GSA2DMember ParseGWACommand(string command, List<GSANode> nodes, List<GSA2DProperty> props)
        {
            GSA2DMember ret = new GSA2DMember();

            ret.GWACommand = command;

            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier
            ret.StructuralId = pieces[counter++];
            ret.Name = pieces[counter++].Trim(new char[] { '"' });
            var color = pieces[counter++].ParseGSAColor();

            string type = pieces[counter++];
            if (type == "SLAB")
                ret.ElementType = Structural2DElementType.Slab;
            else if (type == "WALL")
                ret.ElementType = Structural2DElementType.Wall;
            else
                ret.ElementType = Structural2DElementType.Generic;

            ret.PropertyRef = pieces[counter++];
            ret.Group = Convert.ToInt32(pieces[counter++]); // Keep group for load targetting

            List<double> coordinates = new List<double>();
            string[] nodeRefs = pieces[counter++].ListSplit(" ");
            for (int i = 0; i < nodeRefs.Length; i++)
            {
                GSANode node = nodes.Where(n => n.StructuralId == nodeRefs[i]).FirstOrDefault();
                coordinates.AddRange(node.Value);
                ret.SubGWACommand.Add(node.GWACommand);
            }

            Structural2DElementMesh temp = new Structural2DElementMesh(
                coordinates.ToArray(),
                color.HexToArgbColor(),
                ret.ElementType, ret.PropertyRef, null, 0);

            ret.Vertices = temp.Vertices;
            ret.Faces = temp.Faces;
            ret.Colors = temp.Colors;
            
            counter++; // Orientation node
            
            GSA2DProperty prop = props.Where(p => p.StructuralId == ret.PropertyRef).FirstOrDefault();
            ret.Axis = HelperFunctions.Parse2DAxis(coordinates.ToArray(),
                Convert.ToDouble(pieces[counter++]),
                prop == null ? false : (prop as GSA2DProperty).IsAxisLocal);
            if (prop != null)
                ret.SubGWACommand.Add(prop.GWACommand);

            // Skip to offsets at second to last
            counter = pieces.Length - 2;
            ret.Offset = Convert.ToDouble(pieces[counter++]);

            return ret;
        }
        #endregion

        #region Receiving Functions
        public static void SetObjects(Dictionary<Type, List<IStructural>> dict)
        {
            if (!dict.ContainsKey(typeof(Structural2DElementMesh))) return;

            foreach (IStructural obj in dict[typeof(Structural2DElementMesh)])
            {
                Set(obj as Structural2DElementMesh);
            }
        }

        public static void Set(Structural2DElementMesh mesh, int group = 0)
        {
            if (mesh == null)
                return;

            string keyword = MethodBase.GetCurrentMethod().DeclaringType.GetGSAKeyword();

            int index = Indexer.ResolveIndex(MethodBase.GetCurrentMethod().DeclaringType, mesh);
            int propRef = 0;
            try
            {
                propRef = Indexer.LookupIndex(typeof(GSA2DProperty), mesh.PropertyRef).Value;
            }
            catch { }

            List<string> ls = new List<string>();

            ls.Add("SET");
            ls.Add(keyword);
            ls.Add(index.ToString());
            ls.Add(mesh.Name == null || mesh.Name == "" ? " " : mesh.Name);
            ls.Add(mesh.Colors == null || mesh.Colors.Count() < 1 ? "NO_RGB" : mesh.Colors[0].ArgbToHexColor().ToString());
            if (mesh.ElementType == Structural2DElementType.Slab)
                ls.Add("SLAB");
            else if (mesh.ElementType == Structural2DElementType.Wall)
                ls.Add("WALL");
            else
                ls.Add("2D_GENERIC");
            ls.Add(propRef.ToString());
            ls.Add(group != 0 ? group.ToString() : index.ToString()); // TODO: This allows for targeting of elements from members group
            string topo = "";
            List<int[]> connectivities = mesh.Edges();
            List<double> coor = new List<double>();
            foreach (int[] conn in connectivities)
                foreach (int c in conn)
                {
                    coor.AddRange(mesh.Vertices.Skip(c * 3).Take(3));
                    topo += GSA.NodeAt(mesh.Vertices[c * 3], mesh.Vertices[c * 3 + 1], mesh.Vertices[c * 3 + 2]).ToString() + " ";
                }
            ls.Add(topo);
            ls.Add("0"); // Orientation node
            try
            { 
                ls.Add(HelperFunctions.Get2DAngle(coor.ToArray(), mesh.Axis).ToString());
            }
            catch { ls.Add("0"); }
            ls.Add("1"); // Target mesh size
            ls.Add("MESH"); // TODO: What is this?
            ls.Add("LINEAR"); // Element type
            ls.Add("0"); // Fire
            ls.Add("0"); // Time 1
            ls.Add("0"); // Time 2
            ls.Add("0"); // Time 3
            ls.Add("0"); // TODO: What is this?
            ls.Add("ACTIVE"); // Dummy
            ls.Add("NO"); // Internal auto offset
            ls.Add(mesh.Offset.ToString()); // Offset z
            ls.Add("ALL"); // Exposure

            GSA.RunGWACommand(string.Join(",", ls));
        }
        #endregion
    }
}
