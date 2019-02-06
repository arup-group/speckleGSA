using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpeckleStructures;

namespace SpeckleGSA
{
    public class GSAConverterHack { /*makes sure the assembly is loaded*/  public GSAConverterHack() { } }

    public static class SpeckleGSAConverter
    {
        public static object ToSpeckle(this GSAMaterial material)
        {
            return (material as StructuralMaterial).ToSpeckle();
        }

        public static object ToSpeckle(this GSA1DProperty prop)
        {
            return (prop as Structural1DProperty).ToSpeckle();
        }

        public static object ToSpeckle(this GSA2DProperty prop)
        {
            return (prop as Structural2DProperty).ToSpeckle();
        }

        public static object ToSpeckle(this GSA0DLoad load)
        {
            return (load as Structural0DLoad).ToSpeckle();
        }

        public static object ToSpeckle(this GSA2DLoad load)
        {
            return (load as Structural2DLoad).ToSpeckle();
        }

        public static object ToSpeckle(this GSANode node)
        {
            return (node as StructuralNode).ToSpeckle();
        }

        public static object ToSpeckle(this GSA1DElement element)
        {
            return (element as Structural1DElement).ToSpeckle();
        }

        public static object ToSpeckle(this GSA1DMember member)
        {
            return (member as Structural1DElement).ToSpeckle();
        }

        public static object ToSpeckle(this GSA2DElement element)
        {
            return (element as Structural2DElement).ToSpeckle();
        }

        public static object ToSpeckle(this GSA2DElementMesh mesh)
        {
            return (mesh as Structural2DElementMesh).ToSpeckle();
        }

        public static object ToSpeckle(this GSA2DMember member)
        {
            return (member as Structural2DElementMesh).ToSpeckle();
        }
    }
}
