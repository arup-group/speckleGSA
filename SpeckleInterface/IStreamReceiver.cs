using SpeckleCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SpeckleInterface
{
  public interface IStreamReceiver
  {
    string Units { get; }
    string StreamId { get; }

    event EventHandler<EventArgs> UpdateGlobalTrigger;

    Task InitializeReceiver(string streamID, string documentName, string clientID = "");
    List<SpeckleObject> GetObjects();
    void Dispose(); 
  }
}
