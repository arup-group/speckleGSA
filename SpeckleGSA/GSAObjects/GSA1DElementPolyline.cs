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
    [GSAObject("MEMB.7", new string[] { }, "elements", true, true, new Type[] { typeof(GSA1DElement) }, new Type[] { })]
    public class GSA1DElementPolyline : Structural1DElementPolyline, IGSAObject
    {
        public string GWACommand { get; set; } = "";
        public List<string> SubGWACommand { get; set; } = new List<string>();

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
        }
        #endregion
    }
}
