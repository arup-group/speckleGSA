using SpeckleCore;
using SpeckleGSA;
using SpeckleInterface;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
      ;
    }

    public Task InitializeSender(string documentName, BasePropertyUnits units, double tolerance, double angleTolerance, string streamID = "",
      string clientID = "", string streamName = "", IProgress<int> totalProgress = null, IProgress<int> incrementProgress = null)
    {
      this.streamId = streamID;
      this.clientId = clientID;
      this.streamName = streamName;
      return Task.CompletedTask;
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

    public void UpdateName(string streamName)
    {
      this.streamName = streamName;
    }
  }
}
