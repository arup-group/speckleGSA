using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Interop.Gsa_9_0;

namespace SpeckleGSA
{
    public abstract class GSAObject
    {
        public int Reference { get; set; }
        public string Name { get; set; }
        public List<int> Connectivity { get; set; }

        public List<double> Coor;
        public object Color;
        public ComAuto gsa;

        public GSAObject()
        {
            Reference = 0;
            Name = "";
            Color = null;
            Coor = new List<double>();
            Connectivity = new List<int>();

            gsa = null;
        }

        public abstract void ParseGWACommand(string command, GSAObject[] children = null);

        public abstract string GetGWACommand(Dictionary<Type, object> dict = null);

        public abstract List<GSAObject> GetChildren();
        
        public object RunGWACommand(string command)
        {
            if (gsa == null) return null;

            return gsa.GwaCommand(command);
        }
    }
}
