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

    Task<bool> InitializeSender(string documentName, string streamName, BasePropertyUnits units, double tolerance, double angleTolerance, IProgress<int> totalProgress, IProgress<int> incrementProgress);
    Task<bool> InitializeSender(string documentName, string streamId, string clientId, IProgress<int> totalProgress, IProgress<int> incrementProgress);
    Task<bool> UpdateName(string streamName);
    int SendObjects(Dictionary<string, List<SpeckleObject>> value, int maxPayloadBytes = 0, int apiTimeoutOverrideMilliseconds = 0, int numParallel = 0);
    void Dispose();
  }
}
