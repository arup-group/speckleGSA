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

    Task InitializeReceiver(string streamID, string documentName, string clientID = "", IProgress<double> totalProgress = null, IProgress<double> incrementProgress = null);
    List<SpeckleObject> GetObjects();
    void Dispose(); 
  }
}
