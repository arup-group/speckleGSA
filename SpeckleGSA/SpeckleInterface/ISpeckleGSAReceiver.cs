using SpeckleCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA
{
  public interface ISpeckleGSAReceiver
  {
    string Units { get; }

    event EventHandler<EventArgs> UpdateGlobalTrigger;

    Task InitializeReceiver(string streamID, string clientID = "");
    List<SpeckleObject> GetObjects();
    void Dispose(); 
  }
}
