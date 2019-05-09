using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpeckleStructuresClasses;
using System.Reflection;

namespace SpeckleGSA.GSAObjects
{

    [GSAObject("MEMB.7", new string[] { "NODE.2" }, "elements", false, true, new Type[] { typeof(GSANode) }, new Type[] { })]
    public class GSA2DVoid : Structural2DVoid, IGSAObject
    {
        public string GWACommand { get; set; } = "";
        public List<string> SubGWACommand { get; set; } = new List<string>();

        #region Sending Functions
        public static bool GetObjects(Dictionary<Type, List<IGSAObject>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType))
                dict[MethodBase.GetCurrentMethod().DeclaringType] = new List<IGSAObject>();

            List<GSA2DVoid> voids = new List<GSA2DVoid>();
            List<GSANode> nodes = dict[typeof(GSANode)].Cast<GSANode>().ToList();

            string keyword = MethodBase.GetCurrentMethod().DeclaringType.GetGSAKeyword();
            string[] subKeywords = MethodBase.GetCurrentMethod().DeclaringType.GetSubGSAKeyword();

            string[] lines = GSA.GetGWARecords("GET_ALL," + keyword);
            List<string> deletedLines = GSA.GetDeletedGWARecords("GET_ALL," + keyword).ToList();
            foreach (string k in subKeywords)
                deletedLines.AddRange(GSA.GetDeletedGWARecords("GET_ALL," + k));

            // Remove deleted lines
            dict[typeof(GSA2DVoid)].RemoveAll(l => deletedLines.Contains(l.GWACommand));
            foreach (KeyValuePair<Type, List<IGSAObject>> kvp in dict)
                kvp.Value.RemoveAll(l => l.SubGWACommand.Any(x => deletedLines.Contains(x)));

            // Filter only new lines
            string[] prevLines = dict[typeof(GSA2DVoid)].Select(l => l.GWACommand).ToArray();
            string[] newLines = lines.Where(l => !prevLines.Contains(l)).ToArray();

            foreach (string p in newLines)
            {
                string[] pPieces = p.ListSplit(",");
                if (pPieces[4].MemberIs2D())
                {
                    // Check if dummy
                    if (pPieces[pPieces.Length - 4] == "DUMMY")
                    {
                        GSA2DVoid v = ParseGWACommand(p, nodes);
                        voids.Add(v);
                    }
                }
            }

            dict[typeof(GSA2DVoid)].AddRange(voids);

            if (voids.Count() > 0 || deletedLines.Count() > 0) return true;

            return false;
        }

        public static GSA2DVoid ParseGWACommand(string command, List<GSANode> nodes)
        {
            GSA2DVoid ret = new GSA2DVoid();

            ret.GWACommand = command;

            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier
            ret.StructuralId = pieces[counter++];
            ret.Name = pieces[counter++].Trim(new char[] { '"' });
            var color = pieces[counter++].ParseGSAColor();

            counter++; // Type
            counter++; // Property
            counter++; // Group

            List<double> coordinates = new List<double>();
            string[] nodeRefs = pieces[counter++].ListSplit(" ");
            for (int i = 0; i < nodeRefs.Length; i++)
            {
                GSANode node = nodes.Where(n => n.StructuralId == nodeRefs[i]).FirstOrDefault();
                coordinates.AddRange(node.Value);
                ret.SubGWACommand.Add(node.GWACommand);
            }

            Structural2DVoid temp = new Structural2DVoid(
                coordinates.ToArray(),
                color.HexToArgbColor());

            ret.Vertices = temp.Vertices;
            ret.Faces = temp.Faces;
            ret.Colors = temp.Colors;
            
            return ret;
        }
        #endregion

        #region Receiving Functions
        public static void SetObjects(Dictionary<Type, List<IStructural>> dict)
        {
            if (!dict.ContainsKey(typeof(Structural2DVoid))) return;

            foreach (IStructural obj in dict[typeof(Structural2DVoid)])
            {
                Set(obj as Structural2DVoid);
            }
        }

        public static void Set(Structural2DVoid v)
        {
            if (v == null)
                return;

            string keyword = MethodBase.GetCurrentMethod().DeclaringType.GetGSAKeyword();

            int index = Indexer.ResolveIndex(MethodBase.GetCurrentMethod().DeclaringType, v);

            List<string> ls = new List<string>();

            ls.Add("SET");
            ls.Add(keyword);
            ls.Add(index.ToString());
            ls.Add(v.Name == null || v.Name == "" ? " " : v.Name);
            ls.Add(v.Colors == null || v.Colors.Count() < 1 ? "NO_RGB" : v.Colors[0].ArgbToHexColor().ToString());
            ls.Add("2D_VOID_CUTTER");
            ls.Add("1"); // Property reference
            ls.Add("0"); // Group
            string topo = "";
            List<int[]> connectivities = v.Edges();
            List<double> coor = new List<double>();
            foreach (int[] conn in connectivities)
                foreach (int c in conn)
                {
                    coor.AddRange(v.Vertices.Skip(c * 3).Take(3));
                    topo += GSA.NodeAt(v.Vertices[c * 3], v.Vertices[c * 3 + 1], v.Vertices[c * 3 + 2]).ToString() + " ";
                }
            ls.Add(topo);
            ls.Add("0"); // Orientation node
            ls.Add("0"); // Angles
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
            ls.Add("0"); // Offset z
            ls.Add("ALL"); // Exposure

            GSA.RunGWACommand(string.Join("\t", ls));
        }
        #endregion
    }
}
