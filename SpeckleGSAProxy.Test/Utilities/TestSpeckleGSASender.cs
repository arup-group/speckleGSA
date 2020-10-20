using SpeckleGSA;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSAProxy.Test
{
  class TestSpeckleGSASender : ISpeckleGSASender
  {
    private string clientId;
    private string streamId;
    private string streamName;
    public Dictionary<string, List<object>> sentObjects = new Dictionary<string, List<object>>();

    public string StreamID { get => streamId; }

    public string ClientID { get => clientId; }

    public void Dispose()
    {
      ;
    }

    public Task InitializeSender(string streamID = "", string clientID = "", string streamName = "")
    {
      this.streamId = streamID;
      this.clientId = clientID;
      this.streamName = streamName;

      return Task.CompletedTask;
    }

    //objects by layer name
    public void SendGSAObjects(Dictionary<string, List<object>> value)
    {
      foreach (var key in value.Keys)
      {
        if (!sentObjects.ContainsKey(key))
        {
          sentObjects.Add(key, new List<object>());
        }
        sentObjects[key].AddRange(value[key]);
      } 
    }

    public void UpdateName(string streamName)
    {
      this.streamName = streamName;
    }
  }
}
