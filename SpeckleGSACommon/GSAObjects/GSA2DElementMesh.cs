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
    public class GSA2DElementMesh : Structural2DElementMesh
    {
        public static readonly string GSAKeyword = "";
        public static readonly string Stream = "elements";

        public static readonly Type[] ReadPrerequisite = new Type[2] { typeof(GSA2DElement), typeof(GSA2DLoad) };
        public static readonly Type[] WritePrerequisite = new Type[0] { };

        public GSA2DElementMesh()
        {

        }

        public GSA2DElementMesh(double[] edgeCoordinates, string name = "", Structural2DElementType type = null, int property = 0, Axis axis = null, double offset = 0)
            : base (edgeCoordinates, name, type, property, axis, offset)
        {

        }

        public GSA2DElementMesh(GSA2DElement element)
            : base (element)
        {

        }

        public GSA2DElementMesh(Structural2DElementMesh baseClass)
        {
            foreach (FieldInfo f in baseClass.GetType().GetFields())
                f.SetValue(this, f.GetValue(baseClass));

            foreach (PropertyInfo p in baseClass.GetType().GetProperties())
                p.SetValue(this, p.GetValue(baseClass));
        }

        #region GSAObject Functions
        public static void GetObjects(Dictionary<Type, List<StructuralObject>> dict)
        {
            // Merges the 2D elements into mesh
            if (!GSA.TargetAnalysisLayer) return;

            if (!dict.ContainsKey(typeof(GSA2DElement))) return;

            List<StructuralObject> meshes = new List<StructuralObject>();

            foreach (StructuralObject e2D in dict[typeof(GSA2DElement)])
            {
                GSA2DElementMesh mesh = new GSA2DElementMesh(e2D as GSA2DElement);
                meshes.Add(mesh);
            }

            if (Settings.Merge2DElementsIntoMesh)
            {
                for (int i = 0; i < meshes.Count(); i++)
                {
                    List<StructuralObject> matches = meshes.Where((m, j) => (meshes[i] as GSA2DElementMesh).MeshMergeable(m as GSA2DElementMesh) & j != i).ToList();

                    foreach (StructuralObject m in matches)
                        (meshes[i] as GSA2DElementMesh).MergeMesh(m as GSA2DElementMesh);

                    foreach (StructuralObject m in matches)
                        meshes.Remove(m);

                    if (matches.Count() > 0) i--;

                    Status.ChangeStatus("Merging 2D elements", (double)(i + 1) / meshes.Count() * 100);
                }
            }

            dict.Remove(typeof(GSA2DElement));
            dict[typeof(GSA2DElementMesh)] = meshes;
        }

        public static void WriteObjects(Dictionary<Type, List<StructuralObject>> dict)
        {
            // Convert GSA2DElementMesh into other classes depending on the target

            if (!dict.ContainsKey(typeof(GSA2DElementMesh))) return;

            if (!GSA.TargetAnalysisLayer)
            {
                if (dict.ContainsKey(typeof(GSA2DMember)))
                    dict[typeof(GSA2DMember)].AddRange(dict[typeof(GSA2DElementMesh)].Cast<Structural2DElementMesh>()
                        .Select(o => new GSA2DMember(o)));
                else
                    dict[typeof(GSA2DMember)] = dict[typeof(GSA2DElementMesh)].Cast<Structural2DElementMesh>()
                        .Select(o => new GSA2DMember(o)).Cast<StructuralObject>().ToList();
            }
            else
            {
                List<StructuralObject> e2Ds = new List<StructuralObject>();

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

            dict.Remove(typeof(GSA2DElementMesh));
        }
        #endregion
    }
}
