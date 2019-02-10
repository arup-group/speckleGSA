using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Interop.Gsa_10_0;
using SpeckleStructures;

namespace SpeckleGSA
{
    public class GSA1DElementPolyline : Structural1DElementPolyline
    {
        public static readonly string GSAKeyword = "";
        public static readonly string Stream = "elements";

        public static readonly Type[] ReadPrerequisite = new Type[1] { typeof(GSA1DElement) };
        public static readonly Type[] WritePrerequisite = new Type[0] { };
        public static readonly bool AnalysisLayer = true;
        public static readonly bool DesignLayer = true;

        #region Contructors and Converters
        public GSA1DElementPolyline()
        {

        }
        
        public GSA1DElementPolyline(GSA1DElement element)
            : base (element)
        {

        }

        public GSA1DElementPolyline(Structural1DElementPolyline baseClass)
        {
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
            
            // No need to run if targeting design layer since GSA1DMembers do not need polyline merging
            if (!GSA.TargetAnalysisLayer) return;
            if (!Settings.Merge1DElementsIntoPolyline) return;

            List<StructuralObject> polylines = new List<StructuralObject>();

            // Create polyline from each element
            foreach (StructuralObject e1D in dict[typeof(GSA1DElement)])
            {
                GSA1DElementPolyline poly = new GSA1DElementPolyline(e1D as GSA1DElement);
                polylines.Add(poly);
            }
            
            // Merge the polylines!
            for (int i = 0; i < polylines.Count(); i++)
            {
                List<StructuralObject> matches = polylines.Where((p, j) => (polylines[i] as GSA1DElementPolyline).PolylineMergeable(p as GSA1DElementPolyline) & j != i).ToList();

                foreach (StructuralObject m in matches)
                    (polylines[i] as GSA1DElementPolyline).MergePolyline(m as GSA1DElementPolyline);

                foreach (StructuralObject m in matches)
                    polylines.Remove(m);

                if (matches.Count() > 0) i--;

                Status.ChangeStatus("Merging 1D elements (#: " + polylines.Count().ToString() + ")");
            }

            dict[typeof(GSA1DElement)].Clear();
            dict[typeof(GSA1DElementPolyline)].AddRange(polylines);
        }

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
