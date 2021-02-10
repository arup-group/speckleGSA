using SpeckleCore;
using SpeckleGSA;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SpeckleGSAProxy.Test
{
  internal class TestSpeckleGSAReceiver : ISpeckleGSAReceiver
  {
    public string Units { get; private set; }

    public List<SpeckleObject> Objects { get; set; }

    public string StreamId { get; private set; }

    public string ServerAddress => "https://test.speckle.works";

    public event EventHandler<EventArgs> UpdateGlobalTrigger;

    public TestSpeckleGSAReceiver(string streamId, string units)
    {
      this.StreamId = (string.IsNullOrEmpty(streamId)) ? "TestStream" : streamId;
      this.Units = (string.IsNullOrEmpty(units)) ? "mm" : units;
    }

    public void Dispose()
    {
      Objects.Clear();
    }

    public List<SpeckleObject> GetObjects()
    {
      return Objects; 
    }

    public async Task InitializeReceiver(string streamID, string clientID = "")
    {
    }

    public string ObjectUrl(string id) => HelperFunctions.Combine(ServerAddress, "object/" + id);
  }
}
