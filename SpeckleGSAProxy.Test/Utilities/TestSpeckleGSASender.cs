using SpeckleCore;
using SpeckleInterface;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SpeckleGSAProxy.Test
{
  class TestSpeckleGSASender : IStreamSender
  {
    private string clientId;
    private string streamId;
    private string streamName;
    public Dictionary<string, List<object>> sentObjects = new Dictionary<string, List<object>>();

    public string StreamId { get => streamId; }

    public string ClientId { get => clientId; }

    public void Dispose()
    {
    }

    //objects by layer name
    public int SendObjects(Dictionary<string, List<SpeckleObject>> value, int maxPayloadBytes = 0, int apiTimeoutOverride = 0, int numParallel = 0)
    {
      foreach (var key in value.Keys)
      {
        if (!sentObjects.ContainsKey(key))
        {
          sentObjects.Add(key, new List<object>());
        }
        sentObjects[key].AddRange(value[key]);
      }
      return 0;
    }

    public async Task<bool> InitializeSender(string documentName, string streamName, BasePropertyUnits units, double tolerance, double angleTolerance, 
      IProgress<int> totalProgress, IProgress<int> incrementProgress)
    {
      this.streamName = streamName;
      return true;
    }

    public async Task<bool> InitializeSender(string documentName, string streamId, string clientId, IProgress<int> totalProgress, IProgress<int> incrementProgress)
    {
      this.streamId = streamId;
      this.clientId = clientId;
      return true;
    }

    public Task<bool> UpdateName(string streamName)
    {
      this.streamName = streamName;
      return Task.FromResult(true);
    }
  }
}
