using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Interop.Gsa_10_0;

namespace SpeckleGSA
{
    public abstract class GSAObject
    {
        public abstract string Entity { get; set; }

        public int Reference { get; set; }
        public string Name { get; set; }

        public List<double> Coor;
        public object Color;
        
        public GSAObject()
        {
            Reference = 0;
            Name = "";
            Color = null;
            Coor = new List<double>();
        }

        public abstract void ParseGWACommand(string command, Dictionary<Type, object> dict = null);

        public abstract string GetGWACommand(Dictionary<Type, object> dict = null);

        public abstract List<GSAObject> GetChildren();

        public virtual void ScaleToGSAUnits(string originalUnit)
        {
            for(int i = 0; i < Coor.Count();i++)
                Coor[i] = Coor[i].ConvertUnit(originalUnit, GSA.Units);
        }
    }
}
