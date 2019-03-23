using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Interop.Gsa_10_0;
using SpeckleStructuresClasses;

namespace SpeckleGSA
{
    // Keyword set as MEMB to not clash with grouping of members
    [GSAObject("MEMB.7", "elements", true, true, new Type[] { typeof(GSA1DElement) }, new Type[] { })]
    public class GSA1DElementPolyline : Structural1DElementPolyline, IGSAObject
    {
        public string GWACommand { get; set; }
        public List<string> SubGWACommand { get; set; }
        
        #region Receiving Functions
        public static void SetObjects(Dictionary<Type, List<IStructural>> dict)
        {
            if (!dict.ContainsKey(typeof(Structural1DElementPolyline))) return;

            foreach (IStructural obj in dict[typeof(Structural1DElementPolyline)])
            {
                Set(obj as Structural1DElementPolyline);
            }
        }

        public static void Set(Structural1DElementPolyline poly)
        {
            if (poly == null)
                return;
            
            int group = Indexer.ResolveIndex(MethodBase.GetCurrentMethod().DeclaringType, poly);

            Structural1DElement[] elements = poly.Explode();

            foreach (Structural1DElement element in elements)
            {
                if (GSA.TargetAnalysisLayer)
                    GSA1DElement.Set(element, group);
                else
                    GSA1DMember.Set(element, group);
            }
            //for (int i = 0; i < poly.Value.Count() - 3; i += 3)
            //{
            //    int index = 0;
            //    if (GSA.TargetAnalysisLayer)
            //        index = Indexer.ResolveIndex(typeof(GSA1DElement));
            //    else
            //        index = Indexer.ResolveIndex(typeof(GSA1DMember));

            //    List<string> ls = new List<string>();

            //    if (GSA.TargetAnalysisLayer)
            //    {
            //        ls.Add("SET");
            //        ls.Add(keyword);
            //        ls.Add(index.ToString());
            //        ls.Add(poly.Name);
            //        ls.Add("NO_RGB");
            //        ls.Add("BEAM"); // Type
            //        ls.Add(propRef.ToString());
            //        ls.Add(group.ToString()); // Group
            //        List<double> coor = poly.Value.Skip(i * 3).Take(6).ToList();
            //        ls.Add(GSA.NodeAt(coor[0], coor[1], coor[2]).ToString());
            //        ls.Add(GSA.NodeAt(coor[3], coor[5], coor[6]).ToString());
            //        ls.Add("0"); // Orientation Node

            //        try
            //        { 
            //            ls.Add(HelperFunctions.Get1DAngle(coor.ToArray(), poly.ZAxis[i]).ToString());
            //        }
            //        catch { ls.Add("0"); }

            //        try
            //        {
            //            List<string> subLs = new List<string>();
            //            if (poly.EndRelease[i * 3 / 2].Value.Any(x => x) || poly.EndRelease[i * 3 / 2 + 1].Value.Any(x => x))
            //            {
            //                subLs.Add("RLS");

            //                string end1 = "";

            //                end1 += poly.EndRelease[i * 3 / 2].Value[0] ? "R" : "F";
            //                end1 += poly.EndRelease[i * 3 / 2].Value[1] ? "R" : "F";
            //                end1 += poly.EndRelease[i * 3 / 2].Value[2] ? "R" : "F";
            //                end1 += poly.EndRelease[i * 3 / 2].Value[3] ? "R" : "F";
            //                end1 += poly.EndRelease[i * 3 / 2].Value[4] ? "R" : "F";
            //                end1 += poly.EndRelease[i * 3 / 2].Value[5] ? "R" : "F";

            //                subLs.Add(end1);

            //                string end2 = "";

            //                end1 += poly.EndRelease[i * 3 / 2 + 1].Value[0] ? "R" : "F";
            //                end1 += poly.EndRelease[i * 3 / 2 + 1].Value[1] ? "R" : "F";
            //                end1 += poly.EndRelease[i * 3 / 2 + 1].Value[2] ? "R" : "F";
            //                end1 += poly.EndRelease[i * 3 / 2 + 1].Value[3] ? "R" : "F";
            //                end1 += poly.EndRelease[i * 3 / 2 + 1].Value[4] ? "R" : "F";
            //                end1 += poly.EndRelease[i * 3 / 2 + 1].Value[5] ? "R" : "F";

            //                subLs.Add(end2);

            //                ls.AddRange(subLs);
            //            }
            //            else
            //                ls.Add("NO_RLS");
            //        }
            //        catch { ls.Add("NO_RLS"); }

            //        try
            //        {
            //            List<string> subLs = new List<string>();
            //            subLs.Add(poly.Offset[i * 3 / 2].Value[0].ToString()); // Offset x-start
            //            subLs.Add(poly.Offset[i * 3 / 2 + 1].Value[0].ToString()); // Offset x-end

            //            subLs.Add(poly.Offset[i * 3 / 2].Value[1].ToString());
            //            subLs.Add(poly.Offset[i * 3 / 2].Value[2].ToString());

            //            ls.AddRange(subLs);
            //        }
            //        catch
            //        {
            //            ls.Add("0");
            //            ls.Add("0");
            //            ls.Add("0");
            //            ls.Add("0");
            //        }

            //        //ls.Add("NORMAL"); // Action // TODO: EL.4 SUPPORT
            //        ls.Add(""); // Dummy

            //        GSA.RunGWACommand(string.Join(",", ls));
            //    }

            //    if (GSA.TargetDesignLayer)
            //    {
            //        ls.Add("SET");
            //        ls.Add(keyword);
            //        ls.Add(index.ToString());
            //        ls.Add(poly.Name);
            //        ls.Add("NO_RGB");
            //        if (poly.ElementType == Structural1DElementType.Beam)
            //            ls.Add("BEAM");
            //        else if (poly.ElementType == Structural1DElementType.Column)
            //            ls.Add("COLUMN");
            //        else if (poly.ElementType == Structural1DElementType.Cantilever)
            //            ls.Add("CANTILEVER");
            //        else
            //            ls.Add("1D_GENERIC");
            //        ls.Add(propRef.ToString());
            //        ls.Add(group.ToString()); // TODO: This allows for targeting of elements from members group
            //        string topo = "";
            //        List<double> coor = poly.Value.Skip(i * 3).Take(6).ToList();
            //        topo += GSA.NodeAt(coor[0], coor[1], coor[2]).ToString() + " ";
            //        topo += GSA.NodeAt(coor[3], coor[5], coor[6]).ToString() + " ";
            //        ls.Add(topo);
            //        ls.Add("0"); // Orientation node
            //        try
            //        {
            //            ls.Add(HelperFunctions.Get1DAngle(coor.ToArray(), poly.ZAxis[i]).ToString());
            //        }
            //        catch { ls.Add("0"); }
            //        ls.Add("1"); // Target mesh size
            //        ls.Add("MESH"); // TODO: What is this?
            //        ls.Add("BEAM"); // Element type
            //        ls.Add("0"); // Fire
            //        ls.Add("0"); // Time 1
            //        ls.Add("0"); // Time 2
            //        ls.Add("0"); // Time 3
            //        ls.Add("0"); // TODO: What is this?
            //        ls.Add("ACTIVE"); // Dummy

            //        try
            //        {
            //            if (poly.EndRelease[i * 3 / 2].Equals(GSA1DMember.ParseEndReleases(1)))
            //                ls.Add("1");
            //            else if (poly.EndRelease[i * 3 / 2].Equals(GSA1DMember.ParseEndReleases(2)))
            //                ls.Add("2");
            //            else if (poly.EndRelease[i * 3 / 2].Equals(GSA1DMember.ParseEndReleases(3)))
            //                ls.Add("3");
            //            else
            //                ls.Add("2");
            //        }
            //        catch { ls.Add("2"); }


            //        try
            //        {
            //            if (poly.EndRelease[i * 3 / 2 + 1].Equals(GSA1DMember.ParseEndReleases(1)))
            //                ls.Add("1");
            //            else if (poly.EndRelease[i * 3 / 2 + 1].Equals(GSA1DMember.ParseEndReleases(2)))
            //                ls.Add("2");
            //            else if (poly.EndRelease[i * 3 / 2 + 1].Equals(GSA1DMember.ParseEndReleases(3)))
            //                ls.Add("3");
            //            else
            //                ls.Add("2");
            //        }
            //        catch { ls.Add("2"); }

            //        ls.Add("AUTOMATIC"); // Effective length option
            //        ls.Add("0"); // Pool
            //        ls.Add("0"); // Height
            //        ls.Add("MAN"); // Auto offset 1
            //        ls.Add("MAN"); // Auto offset 2

            //        try
            //        {
            //            List<string> subLs = new List<string>();
            //            subLs.Add(poly.Offset[i * 3 / 2].Value[0].ToString()); // Offset x-start
            //            subLs.Add(poly.Offset[i * 3 / 2 + 1].Value[0].ToString()); // Offset x-end

            //            subLs.Add(poly.Offset[i * 3 / 2].Value[1].ToString());
            //            subLs.Add(poly.Offset[i * 3 / 2].Value[2].ToString());

            //            ls.AddRange(subLs);
            //        }
            //        catch
            //        {
            //            ls.Add("0");
            //            ls.Add("0");
            //            ls.Add("0");
            //            ls.Add("0");
            //        }
            //        ls.Add("ALL"); // Exposure


            //        GSA.RunGWACommand(string.Join(",", ls));
            //    }
            //}
        }
        #endregion
    }
}
