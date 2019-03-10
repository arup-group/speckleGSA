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
    [GSAObject("MEMB.7", "elements", true, true, new Type[] { typeof(GSA2DElement) }, new Type[] { })]
    public class GSA2DElementMesh : Structural2DElementMesh, IGSAObject
    {
        public string GWACommand { get; set; }
        public List<string> SubGWACommand { get; set; }
        
        #region Contructors and Converters
        public GSA2DElementMesh()
        {
            GWACommand = "";
            SubGWACommand = new List<string>();
        }
        
        public GSA2DElementMesh(GSA2DElement element)
            : base (element)
        {
            GWACommand = "";
            SubGWACommand = new List<string>();
        }

        public GSA2DElementMesh(Structural2DElementMesh baseClass)
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
        // Disable sending as mesh as its problematic for continuous sending
        //public static void GetObjects(Dictionary<Type, List<object>> dict)
        //{
        //    if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType))
        //        dict[MethodBase.GetCurrentMethod().DeclaringType] = new List<object>();

        //    // No need to run if targeting design layer since GSA2DMembers do not need mesh merging
        //    if (!GSA.TargetAnalysisLayer) return;
        //    if (!Settings.Merge2DElementsIntoMesh) return;

        //    List<object> meshes = new List<object>();

        //    // Create mesh from each element
        //    foreach (object e2D in dict[typeof(GSA2DElement)])
        //    {
        //        GSA2DElementMesh mesh = new GSA2DElementMesh(e2D as GSA2DElement);
        //        meshes.Add(mesh);
        //    }

        //    // Merge the mesh!
        //    for (int i = 0; i < meshes.Count(); i++)
        //    {
        //        List<object> matches = meshes.Where((m, j) => (meshes[i] as GSA2DElementMesh).MeshMergeable(m as GSA2DElementMesh) & j != i).ToList();

        //        foreach (StructuralObject m in matches)
        //            (meshes[i] as GSA2DElementMesh).MergeMesh(m as GSA2DElementMesh);

        //        foreach (StructuralObject m in matches)
        //            meshes.Remove(m);

        //        if (matches.Count() > 0) i--;

        //        Status.ChangeStatus("Merging 2D elements (#: " + meshes.Count().ToString() + ")");
        //    }

        //    dict[typeof(GSA2DElement)].Clear();
        //    dict[typeof(GSA2DElementMesh)].AddRange(meshes);
        //}

        public static void WriteObjects(Dictionary<Type, List<StructuralObject>> dict)
        {
            if (!dict.ContainsKey(MethodBase.GetCurrentMethod().DeclaringType)) return;

            if (GSA.TargetDesignLayer)
            {
                if (dict.ContainsKey(typeof(GSA2DMember)))
                    dict[typeof(GSA2DMember)].AddRange(dict[typeof(GSA2DElementMesh)].Cast<GSA2DElementMesh>()
                        .Select(o => new GSA2DMember(o.GetBase() as Structural2DElementMesh)));
                else
                    dict[typeof(GSA2DMember)] = dict[typeof(GSA2DElementMesh)].Cast<GSA2DElementMesh>()
                        .Select(o => new GSA2DMember(o.GetBase() as Structural2DElementMesh)).Cast<StructuralObject>().ToList();
            }
            else
            {
                List<StructuralObject> e2Ds = new List<StructuralObject>();

                // Break apart the mesh into its constitutive elements
                foreach (StructuralObject m in dict[typeof(GSA2DElementMesh)])
                {
                    List<StructuralObject> meshElements = (m as Structural2DElementMesh).Elements.Select(o => new GSA2DElement(o))
                        .Cast<StructuralObject>().ToList();

                    foreach (StructuralObject e in meshElements)
                    {
                        (e as GSA2DElement).Reference = 0;
                        (e as GSA2DElement).MeshReference = m.Reference;
                    }

                    e2Ds.AddRange(meshElements);
                }

                if (dict.ContainsKey(typeof(GSA2DElement)))
                    dict[typeof(GSA2DElement)].AddRange(e2Ds);
                else
                    dict[typeof(GSA2DElement)] = e2Ds;
            }
        }
        #endregion
    }
}
