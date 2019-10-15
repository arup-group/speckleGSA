using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSAProxy.Test
{
  [Serializable]
  public class GwaRecord
  {
    public string ApplicationId { get; set; }
    public string GwaCommand { get; set; }

    public GwaRecord(string applicationId, string gwaCommand)
    {
      ApplicationId = applicationId;
      GwaCommand = gwaCommand;
    }
  }
}
