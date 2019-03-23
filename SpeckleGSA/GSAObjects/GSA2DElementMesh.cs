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
    [GSAObject("MEMB.7", "elements", true, false, new Type[] { typeof(GSA2DElement) }, new Type[] { })]
    public class GSA2DElementMesh : Structural2DElementMesh, IGSAObject
    {
        public string GWACommand { get; set; }
        public List<string> SubGWACommand { get; set; }
        
        #region Receiving Functions
        public static void SetObjects(Dictionary<Type, List<IStructural>> dict)
        {
            if (!dict.ContainsKey(typeof(Structural2DElementMesh))) return;

            foreach (IStructural obj in dict[typeof(Structural2DElementMesh)])
            {
                Set(obj as Structural2DElementMesh);
            }
        }

        public static void Set(Structural2DElementMesh mesh)
        {
            if (mesh == null)
                return;

            int group = Indexer.ResolveIndex(MethodBase.GetCurrentMethod().DeclaringType, mesh);

            Structural2DElementMesh[] elements = mesh.Explode();

            foreach (Structural2DElementMesh element in elements)
            {
                GSA2DElement.Set(element, group);
            }
        }
        #endregion
    }
}
