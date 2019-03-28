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
    [GSAObject("MEMB.7", "elements", false, true, new Type[] { typeof(GSANode) }, new Type[] { typeof(GSA1DProperty) })]
    public class GSA1DMember : Structural1DElement, IGSAObject
    {
        public int Group;

        public string GWACommand { get; set; } = "";
        public List<string> SubGWACommand { get; set; } = new List<string>();

        #region Sending Functions
        public static bool GetObjects(Dictionary<Type, List<IGSAObject>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType))
                dict[MethodBase.GetCurrentMethod().DeclaringType] = new List<IGSAObject>();

            List<GSA1DMember> members = new List<GSA1DMember>();
            List<GSANode> nodes = dict[typeof(GSANode)].Cast<GSANode>().ToList();

            string keyword = MethodBase.GetCurrentMethod().DeclaringType.GetGSAKeyword();

            string[] lines = GSA.GetGWAGetCommands("GET_ALL," + keyword);
            string[] deletedLines = GSA.GetDeletedGWAGetCommands("GET_ALL," + keyword);

            // Remove deleted lines
            dict[typeof(GSA1DMember)].RemoveAll(l => deletedLines.Contains(l.GWACommand));
            foreach (KeyValuePair<Type, List<IGSAObject>> kvp in dict)
                kvp.Value.RemoveAll(l => l.SubGWACommand.Any(x => deletedLines.Contains(x)));

            // Filter only new lines
            string[] prevLines = dict[typeof(GSA1DMember)].Select(l => l.GWACommand).ToArray();
            string[] newLines = lines.Where(l => !prevLines.Contains(l)).ToArray();

            foreach (string p in newLines)
            {
                string[] pPieces = p.ListSplit(",");
                if (pPieces[4].MemberIs1D())
                {
                    GSA1DMember member = ParseGWACommand(p, nodes);
                    members.Add(member);
                }
            }

            dict[typeof(GSA1DMember)].AddRange(members);

            if (members.Count() > 0 || deletedLines.Length > 0) return true;

            return false;
        }

        public static GSA1DMember ParseGWACommand(string command, List<GSANode> nodes)
        {
            GSA1DMember ret = new GSA1DMember();

            ret.GWACommand = command;
            
            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier
            ret.StructuralId = pieces[counter++];
            ret.Name = pieces[counter++].Trim(new char[] { '"' });
            counter++; // Color

            string type = pieces[counter++];
            if (type == "BEAM")
                ret.ElementType = Structural1DElementType.Beam;
            else if (type == "COLUMN")
                ret.ElementType = Structural1DElementType.Column;
            else if (type == "CANTILEVER")
                ret.ElementType = Structural1DElementType.Cantilever;
            else
                ret.ElementType = Structural1DElementType.Generic;

            ret.PropertyRef = pieces[counter++];
            ret.Group = Convert.ToInt32(pieces[counter++]); // Keep group for load targetting

            ret.Value = new List<double>();
            string[] nodeRefs = pieces[counter++].ListSplit(" ");
            for (int i = 0; i < nodeRefs.Length; i++)
            {
                GSANode node = nodes.Where(n => n.StructuralId == nodeRefs[i]).FirstOrDefault();
                ret.Value.AddRange(node.Value);
                ret.SubGWACommand.Add(node.GWACommand);
            }

            string orientationNodeRef = pieces[counter++];
            double rotationAngle = Convert.ToDouble(pieces[counter++]);

            if (orientationNodeRef != "0")
            {
                GSANode node = nodes.Where(n => n.StructuralId == orientationNodeRef).FirstOrDefault();
                ret.ZAxis = HelperFunctions.Parse1DAxis(ret.Value.ToArray(),
                    rotationAngle, node.Value.ToArray()).Normal as StructuralVectorThree;
                ret.SubGWACommand.Add(node.GWACommand);
            }
            else
                ret.ZAxis = HelperFunctions.Parse1DAxis(ret.Value.ToArray(), rotationAngle).Normal as StructuralVectorThree;

            counter += 9; //Skip to end conditions

            ret.EndRelease = new List<StructuralVectorBoolSix>();
            ret.EndRelease.Add(ParseEndReleases(Convert.ToInt32(pieces[counter++])));
            ret.EndRelease.Add(ParseEndReleases(Convert.ToInt32(pieces[counter++])));

            // Skip to offsets at fifth to last
            counter = pieces.Length - 5;
            ret.Offset = new List<StructuralVectorThree>();
            ret.Offset.Add(new StructuralVectorThree(new double[3]));
            ret.Offset.Add(new StructuralVectorThree(new double[3]));

            ret.Offset[0].Value[0] = Convert.ToDouble(pieces[counter++]);
            ret.Offset[1].Value[0] = Convert.ToDouble(pieces[counter++]);

            ret.Offset[0].Value[1] = Convert.ToDouble(pieces[counter++]);
            ret.Offset[1].Value[1] = ret.Offset[0].Value[1];

            ret.Offset[0].Value[2] = Convert.ToDouble(pieces[counter++]);
            ret.Offset[1].Value[2] = ret.Offset[0].Value[2];

            return ret;
        }
        #endregion

        #region Receiving Functions
        public static void SetObjects(Dictionary<Type, List<IStructural>> dict)
        {
            if (!dict.ContainsKey(typeof(Structural1DElement))) return;

            foreach (IStructural obj in dict[typeof(Structural1DElement)])
            {
                Set(obj as Structural1DElement);
            }
        }

        public static void Set(Structural1DElement member, int group = 0)
        {
            if (member == null)
                return;

            string keyword = MethodBase.GetCurrentMethod().DeclaringType.GetGSAKeyword();

            int index = Indexer.ResolveIndex(MethodBase.GetCurrentMethod().DeclaringType, member);
            int propRef = 0;
            try
            {
                propRef = Indexer.LookupIndex(typeof(GSA1DProperty), member.PropertyRef).Value;
            }
            catch { }

            List<string> ls = new List<string>();

            ls.Add("SET");
            ls.Add(keyword);
            ls.Add(index.ToString());
            ls.Add(member.Name == null || member.Name == "" ? " " : member.Name);
            ls.Add("NO_RGB");
            if (member.ElementType == Structural1DElementType.Beam)
                ls.Add("BEAM");
            else if (member.ElementType == Structural1DElementType.Column)
                ls.Add("COLUMN");
            else if (member.ElementType == Structural1DElementType.Cantilever)
                ls.Add("CANTILEVER");
            else
                ls.Add("1D_GENERIC");
            ls.Add(propRef.ToString());
            ls.Add(group != 0 ? group.ToString() : index.ToString()); // TODO: This allows for targeting of elements from members group
            string topo = "";
            for (int i = 0; i < member.Value.Count(); i += 3)
                topo += GSA.NodeAt(member.Value[i], member.Value[i + 1], member.Value[i + 2]).ToString() + " ";
            ls.Add(topo);
            ls.Add("0"); // Orientation node
            try
            { 
                ls.Add(HelperFunctions.Get1DAngle(member.Value.ToArray(), member.ZAxis).ToString());
            }
            catch { ls.Add("0"); }
            ls.Add("1"); // Target mesh size
            ls.Add("MESH"); // TODO: What is this?
            ls.Add("BEAM"); // Element type
            ls.Add("0"); // Fire
            ls.Add("0"); // Time 1
            ls.Add("0"); // Time 2
            ls.Add("0"); // Time 3
            ls.Add("0"); // TODO: What is this?
            ls.Add("ACTIVE"); // Dummy

            try
            { 
                if (member.EndRelease[0].Equals(ParseEndReleases(1)))
                    ls.Add("1");
                else if (member.EndRelease[0].Equals(ParseEndReleases(2)))
                    ls.Add("2");
                else if (member.EndRelease[0].Equals(ParseEndReleases(3)))
                    ls.Add("3");
                else
                    ls.Add("2");
            }
            catch { ls.Add("2"); }

            try
            {
                if (member.EndRelease[1].Equals(ParseEndReleases(1)))
                    ls.Add("1");
                else if (member.EndRelease[1].Equals(ParseEndReleases(2)))
                    ls.Add("2");
                else if (member.EndRelease[1].Equals(ParseEndReleases(3)))
                    ls.Add("3");
                else
                    ls.Add("2");
            }
            catch { ls.Add("2"); }

            ls.Add("AUTOMATIC"); // Effective length option
            ls.Add("0"); // Pool
            ls.Add("0"); // Height
            ls.Add("MAN"); // Auto offset 1
            ls.Add("MAN"); // Auto offset 2
            ls.Add("NO"); // Internal auto offset

            try
            {
                List<string> subLs = new List<string>();
                subLs.Add(member.Offset[0].Value[0].ToString()); // Offset x-start
                subLs.Add(member.Offset[1].Value[0].ToString()); // Offset x-end

                subLs.Add(member.Offset[0].Value[1].ToString());
                subLs.Add(member.Offset[0].Value[2].ToString());

                ls.AddRange(subLs);
            }
            catch
            {
                ls.Add("0");
                ls.Add("0");
                ls.Add("0");
                ls.Add("0");
            }
            ls.Add("ALL"); // Exposure


            GSA.RunGWACommand(string.Join(",", ls));
        }
        #endregion

        #region Helper Functions
        public static StructuralVectorBoolSix ParseEndReleases(int option)
        {
            switch(option)
            {
                case 1:
                    // Pinned
                    return new StructuralVectorBoolSix(false, false, false, false, true, true);
                case 2:
                    // Fixed
                    return new StructuralVectorBoolSix(false, false, false, false, false, false);
                case 3:
                    // Free
                    return new StructuralVectorBoolSix(true, true, true, true, true, true);
                case 4:
                    // Full rotational
                    return new StructuralVectorBoolSix(false, false, false, false, false, false);
                case 5:
                    // Partial rotational
                    return new StructuralVectorBoolSix(false, false, false, false, true, true);
                case 6:
                    // Top flange lateral
                    return new StructuralVectorBoolSix(false, false, false, false, false, false);
                default:
                    // Pinned
                    return new StructuralVectorBoolSix(false, false, false, false, true, true);
            }
        }
        #endregion
    }
}
