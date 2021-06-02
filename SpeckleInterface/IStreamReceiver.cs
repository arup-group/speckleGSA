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

    //Task<bool> InitializeReceiver(string streamID, string documentName, string clientID = "", IProgress<double> totalProgress = null, IProgress<double> incrementProgress = null);
    Task<bool> InitializeReceiver(string streamId, string documentName, IProgress<int> totalProgress, IProgress<int> incrementProgress);
    Task<bool> InitializeReceiver(string streamId, string documentName, string clientId, IProgress<int> totalProgress, IProgress<int> incrementProgress);
    List<SpeckleObject> GetObjects();
    void Dispose();
  }
}
