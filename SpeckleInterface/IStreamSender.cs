using SpeckleCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SpeckleInterface
{
  public interface IStreamSender
  {
    string StreamId { get; }
    string ClientId { get; }

    Task InitializeSender(string documentName, BasePropertyUnits units, double tolerance, double angleTolerance, string streamID = "", 
      string clientID = "", string streamName = "", IProgress<int> totalProgress = null, IProgress<int> incrementProgress = null);
    Task UpdateName(string streamName);
    int SendObjects(Dictionary<string, List<SpeckleObject>> value, int maxPayloadBytes = 0, int apiTimeoutOverrideMilliseconds = 0, int numParallel = 0);
    Task<StreamBasicData> GetStream(string streamId);
    void Dispose();
  }
}
