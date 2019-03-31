using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using SpeckleStructuresClasses;

namespace SpeckleGSA
{
    [GSAObject("EL.3", "elements", true, false, new Type[] { typeof(GSANode) }, new Type[] { typeof(GSA1DProperty) })]
    public class GSA1DElement : Structural1DElement, IGSAObject
    {
        public string GWACommand { get; set; } = "";
        public List<string> SubGWACommand { get; set; } = new List<string>();

        #region Sending Functions
        public static bool GetObjects(Dictionary<Type, List<IGSAObject>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType))
                dict[MethodBase.GetCurrentMethod().DeclaringType] = new List<IGSAObject>();

            List<GSA1DElement> elements = new List<GSA1DElement>();
            List<GSANode> nodes = dict[typeof(GSANode)].Cast<GSANode>().ToList();

            string keyword = MethodBase.GetCurrentMethod().DeclaringType.GetGSAKeyword();

            string[] lines = GSA.GetGWAGetCommands("GET_ALL," + keyword);
            string[] deletedLines = GSA.GetDeletedGWAGetCommands("GET_ALL," + keyword);

            // Remove deleted lines
            dict[typeof(GSA1DElement)].RemoveAll(l => deletedLines.Contains(l.GWACommand));
            foreach (KeyValuePair<Type, List<IGSAObject>> kvp in dict)
                kvp.Value.RemoveAll(l => l.SubGWACommand.Any(x => deletedLines.Contains(x)));

            // Filter only new lines
            string[] prevLines = dict[typeof(GSA1DElement)].Select(l => l.GWACommand).ToArray();
            string[] newLines = lines.Where(l => !prevLines.Contains(l)).ToArray();

            foreach (string p in newLines)
            {
                string[] pPieces = p.ListSplit(",");
                if (pPieces[4].ParseElementNumNodes() == 2)
                {
                    GSA1DElement element = ParseGWACommand(p, nodes);
                    elements.Add(element);
                }
            }

            dict[typeof(GSA1DElement)].AddRange(elements);

            if (elements.Count() > 0 || deletedLines.Length > 0) return true;

            return false;
        }

        public static GSA1DElement ParseGWACommand(string command, List<GSANode> nodes)
        {
            GSA1DElement ret = new GSA1DElement();

            ret.GWACommand = command;

            string[] pieces = command.ListSplit(",");

            int counter = 1; // Skip identifier
            ret.StructuralId = pieces[counter++];
            ret.Name = pieces[counter++].Trim(new char[] { '"' });
            counter++; // Colour
            counter++; // Type
            ret.PropertyRef = pieces[counter++];
            counter++; // Group

            ret.Value = new List<double>();
            for (int i = 0; i < 2; i++)
            {
                string key = pieces[counter++];
                GSANode node = nodes.Where(n => n.StructuralId == key).FirstOrDefault();
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


            if (pieces[counter++] != "NO_RLS")
            {
                string start = pieces[counter++];
                string end = pieces[counter++];

                ret.EndRelease = new List<StructuralVectorBoolSix>();
                ret.EndRelease.Add(new StructuralVectorBoolSix(new bool[6]));
                ret.EndRelease.Add(new StructuralVectorBoolSix(new bool[6]));

                ret.EndRelease[0].Value[0] = ParseEndRelease(start[0], pieces, ref counter);
                ret.EndRelease[0].Value[1] = ParseEndRelease(start[1], pieces, ref counter);
                ret.EndRelease[0].Value[2] = ParseEndRelease(start[2], pieces, ref counter);
                ret.EndRelease[0].Value[3] = ParseEndRelease(start[3], pieces, ref counter);
                ret.EndRelease[0].Value[4] = ParseEndRelease(start[4], pieces, ref counter);
                ret.EndRelease[0].Value[5] = ParseEndRelease(start[5], pieces, ref counter);

                ret.EndRelease[1].Value[0] = ParseEndRelease(start[0], pieces, ref counter);
                ret.EndRelease[1].Value[1] = ParseEndRelease(start[1], pieces, ref counter);
                ret.EndRelease[1].Value[2] = ParseEndRelease(start[2], pieces, ref counter);
                ret.EndRelease[1].Value[3] = ParseEndRelease(start[3], pieces, ref counter);
                ret.EndRelease[1].Value[4] = ParseEndRelease(start[4], pieces, ref counter);
                ret.EndRelease[1].Value[5] = ParseEndRelease(start[5], pieces, ref counter);
            }

            ret.Offset = new List<StructuralVectorThree>();
            ret.Offset.Add(new StructuralVectorThree(new double[3]));
            ret.Offset.Add(new StructuralVectorThree(new double[3]));

            ret.Offset[0].Value[0] = Convert.ToDouble(pieces[counter++]);
            ret.Offset[1].Value[0] = Convert.ToDouble(pieces[counter++]);

            ret.Offset[0].Value[1] = Convert.ToDouble(pieces[counter++]);
            ret.Offset[1].Value[1] = ret.Offset[0].Value[1];

            ret.Offset[0].Value[2] = Convert.ToDouble(pieces[counter++]);
            ret.Offset[1].Value[2] = ret.Offset[0].Value[2];

            //counter++; // Action // TODO: EL.4 SUPPORT
            counter++; // Dummy

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

        public static void Set(Structural1DElement element, int group = 0)
        {
            if (element == null)
                return;

            string keyword = MethodBase.GetCurrentMethod().DeclaringType.GetGSAKeyword();

            int index = Indexer.ResolveIndex(MethodBase.GetCurrentMethod().DeclaringType, element);
            int propRef = 0;
            try
            {
                propRef = Indexer.LookupIndex(typeof(GSA1DProperty), element.PropertyRef).Value;
            }
            catch { }

            List<string> ls = new List<string>();

            ls.Add("SET");
            ls.Add(keyword);
            ls.Add(index.ToString());
            ls.Add(element.Name == null || element.Name == "" ? " " : element.Name);
            ls.Add("NO_RGB");
            ls.Add("BEAM"); // Type
            ls.Add(propRef.ToString());
            ls.Add(group.ToString());
            for (int i = 0; i < element.Value.Count(); i += 3)
                ls.Add(GSA.NodeAt(element.Value[i], element.Value[i+1], element.Value[i+2]).ToString());
            ls.Add("0"); // Orientation Node
            try
            { 
                ls.Add(HelperFunctions.Get1DAngle(element.Value.ToArray(), element.ZAxis).ToString());
            } catch { ls.Add("0"); }
            try
            {
                List<string> subLs = new List<string>();
                if (element.EndRelease[0].Value.Any(x => x) || element.EndRelease[1].Value.Any(x => x))
                {
                    subLs.Add("RLS");

                    string end1 = "";

                    end1 += element.EndRelease[0].Value[0] ? "R" : "F";
                    end1 += element.EndRelease[0].Value[1] ? "R" : "F";
                    end1 += element.EndRelease[0].Value[2] ? "R" : "F";
                    end1 += element.EndRelease[0].Value[3] ? "R" : "F";
                    end1 += element.EndRelease[0].Value[4] ? "R" : "F";
                    end1 += element.EndRelease[0].Value[5] ? "R" : "F";

                    subLs.Add(end1);

                    string end2 = "";

                    end1 += element.EndRelease[1].Value[0] ? "R" : "F";
                    end1 += element.EndRelease[1].Value[1] ? "R" : "F";
                    end1 += element.EndRelease[1].Value[2] ? "R" : "F";
                    end1 += element.EndRelease[1].Value[3] ? "R" : "F";
                    end1 += element.EndRelease[1].Value[4] ? "R" : "F";
                    end1 += element.EndRelease[1].Value[5] ? "R" : "F";

                    subLs.Add(end2);

                    ls.AddRange(subLs);
                }
                else
                    ls.Add("NO_RLS");
            }
            catch { ls.Add("NO_RLS"); }

            try
            {
                List<string> subLs = new List<string>();
                subLs.Add(element.Offset[0].Value[0].ToString()); // Offset x-start
                subLs.Add(element.Offset[1].Value[0].ToString()); // Offset x-end

                subLs.Add(element.Offset[0].Value[1].ToString());
                subLs.Add(element.Offset[0].Value[2].ToString());

                ls.AddRange(subLs);
            }
            catch
            {
                ls.Add("0");
                ls.Add("0");
                ls.Add("0");
                ls.Add("0");
            }

            //ls.Add("NORMAL"); // Action // TODO: EL.4 SUPPORT
            ls.Add(""); // Dummy

            GSA.RunGWACommand(string.Join(",", ls));
        }
        #endregion

        #region Helper Functions
        private static bool ParseEndRelease(char code, string[] pieces, ref int counter)
        {
            switch (code)
            {
                case 'F':
                    return false;
                case 'R':
                    return true;
                default:
                    // TODO
                    Status.AddError("Element end stiffness not supported. Only releases.");
                    counter++;
                    return true;
            }
        }
        #endregion
    }
}
