using SpeckleCore;
using SpeckleGSA;
using SpeckleInterface;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SpeckleGSAProxy.Test
{
  internal class TestSpeckleGSAReceiver : IStreamReceiver
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

    public string ObjectUrl(string id) => HelperFunctions.Combine(ServerAddress, "object/" + id);

    public Task InitializeReceiver(string streamID, string documentName, string clientID = "", IProgress<double> totalProgress = null, IProgress<double> incrementProgress = null)
    {
      return Task.CompletedTask;
    }
  }
}
