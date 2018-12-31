using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA
{
    public class GSAMember : GSAObject
    {
        public static readonly string Stream = "elements";
        public static readonly int ReadPriority = 3;
        public static readonly int WritePriority = 3;

        // TODO: TARGET GSA 10
        public GSAMember()
        {
        }

        public override void ParseGWACommand(string command, GSAObject[] children = null)
        {
            throw new NotImplementedException();
        }

        public override string GetGWACommand(GSAObject[] children = null)
        {
            throw new NotImplementedException();
        }

        public override List<GSAObject> GetChildren()
        {
            throw new NotImplementedException();
        }
    }
}
