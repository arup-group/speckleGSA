using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA
{
    public class GSARegion : GSAObject
    {
        public GSARegion() : base("REGION")
        {

        }

        public override void ParseGWACommand(string command)
        {
            throw new NotImplementedException();
        }

        public override string GetGWACommand()
        {
            throw new NotImplementedException();
        }

        public override List<GSAObject> GetChildren()
        {
            throw new NotImplementedException();
        }
    }
}
