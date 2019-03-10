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
    [GSAObject("MEMB.7", "elements", true, true, new Type[] { typeof(GSA1DElement) }, new Type[] { })]
    public class GSA1DElementPolyline : Structural1DElementPolyline, IGSAObject
    {
        public string GWACommand { get; set; }
        public List<string> SubGWACommand { get; set; }

        #region Contructors and Converters
        public GSA1DElementPolyline()
        {
            GWACommand = "";
            SubGWACommand = new List<string>();
        }
        
        public GSA1DElementPolyline(GSA1DElement element)
            : base (element)
        {
            GWACommand = "";
            SubGWACommand = new List<string>();
        }

        public GSA1DElementPolyline(Structural1DElementPolyline baseClass)
        {
            GWACommand = "";
            SubGWACommand = new List<string>();

            foreach (FieldInfo f in baseClass.GetType().GetFields())
                f.SetValue(this, f.GetValue(baseClass));

            foreach (PropertyInfo p in baseClass.GetType().GetProperties())
                p.SetValue(this, p.GetValue(baseClass));
        }
        #endregion

        #region GSA Functions
        // Disable sending as polyline as its problematic for continuous sending
        //public static void GetObjects(Dictionary<Type, List<object>> dict)
        //{
        //    if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType))
        //        dict[MethodBase.GetCurrentMethod().DeclaringType] = new List<object>();
            
        //    // No need to run if targeting design layer since GSA1DMembers do not need polyline merging
        //    if (!GSA.TargetAnalysisLayer) return;
        //    if (!Settings.Merge1DElementsIntoPolyline) return;

        //    List<object> polylines = new List<object>();

        //    // Create polyline from each element
        //    foreach (object e1D in dict[typeof(GSA1DElement)])
        //    {
        //        GSA1DElementPolyline poly = new GSA1DElementPolyline(e1D as GSA1DElement);
        //        polylines.Add(poly);
        //    }
            
        //    // Merge the polylines!
        //    for (int i = 0; i < polylines.Count(); i++)
        //    {
        //        List<object> matches = polylines.Where((p, j) => (polylines[i] as GSA1DElementPolyline).PolylineMergeable(p as GSA1DElementPolyline) & j != i).ToList();

        //        foreach (StructuralObject m in matches)
        //            (polylines[i] as GSA1DElementPolyline).MergePolyline(m as GSA1DElementPolyline);

        //        foreach (StructuralObject m in matches)
        //            polylines.Remove(m);

        //        if (matches.Count() > 0) i--;

        //        Status.ChangeStatus("Merging 1D elements (#: " + polylines.Count().ToString() + ")");
        //    }

        //    dict[typeof(GSA1DElement)].Clear();
        //    dict[typeof(GSA1DElementPolyline)].AddRange(polylines);
        //}

        public static void WriteObjects(Dictionary<Type, List<StructuralObject>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType)) return;

            List<StructuralObject> e1Ds = new List<StructuralObject>();

            // Break apart the polyline into its constitutive elements
            foreach (StructuralObject p in dict[typeof(GSA1DElementPolyline)])
            {
                if (GSA.TargetAnalysisLayer)
                {
                    List<StructuralObject> polyElements = (p as Structural1DElementPolyline).Elements.Select(o => new GSA1DElement(o))
                        .Cast<StructuralObject>().ToList();

                    foreach (StructuralObject e in polyElements)
                    {
                        (e as GSA1DElement).Reference = 0;
                        (e as GSA1DElement).PolylineReference = p.Reference;
                    }
                    e1Ds.AddRange(polyElements);
                }
                else
                {
                    List<StructuralObject> polyElements = (p as Structural1DElementPolyline).Elements.Select(o => new GSA1DMember(o))
                        .Cast<StructuralObject>().ToList();

                    foreach (StructuralObject e in polyElements)
                    {
                        (e as GSA1DMember).Reference = 0;
                        (e as GSA1DMember).PolylineReference = p.Reference;
                    }
                    e1Ds.AddRange(polyElements);
                }
            }

            if (GSA.TargetAnalysisLayer)
            {
                if (dict.ContainsKey(typeof(GSA1DElement)))
                    dict[typeof(GSA1DElement)].AddRange(e1Ds);
                else
                    dict[typeof(GSA1DElement)] = e1Ds;
            }
            else
            {
                if (dict.ContainsKey(typeof(GSA1DMember)))
                    dict[typeof(GSA1DMember)].AddRange(e1Ds);
                else
                    dict[typeof(GSA1DMember)] = e1Ds;
            }
        }
        #endregion
    }
}
