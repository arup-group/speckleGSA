using SpeckleCore;
using SpeckleGSA;
using SpeckleInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SpeckleGSAUI.Test
{
  internal class TestSpeckleGSAReceiver : IStreamReceiver
  {
    public string Units { get; private set; }

    public List<SpeckleObject> Objects { get; set; }

    public string StreamId { get; private set; }
    public string Token { get; set; }

    public string ServerAddress = "https://test.speckle.works";

    public event EventHandler<EventArgs> UpdateGlobalTrigger;

    public TestSpeckleGSAReceiver(string url, string units)
    {
      if (!string.IsNullOrEmpty(url))
      {
        this.ServerAddress = url;
      }
      this.Units = (string.IsNullOrEmpty(units)) ? "mm" : units;
    }

    public void Dispose()
    {
      Objects.Clear();
    }

    public List<SpeckleObject> GetObjects()
    {
      if (Objects == null || Objects.Count == 0)
      {
        Objects = new List<SpeckleObject>()
        {
          new SpeckleObject() { ApplicationId = StreamId + "_1" },
          new SpeckleObject() { ApplicationId = StreamId + "_2" }
        };
      }
      UpdateGlobalTrigger?.Invoke(null, null);
      return Objects;
    }

    public string ObjectUrl(string id) => HelperFunctions.Combine(ServerAddress, "object/" + id);

    public Task<bool> InitializeReceiver(string streamId, string documentName, string clientID, IProgress<int> totalProgress, IProgress<int> incrementProgress)
    {
      this.StreamId = streamId;
      return Task.FromResult(true);
    }

    public Task<bool> InitializeReceiver(string streamId, string documentName, IProgress<int> totalProgress, IProgress<int> incrementProgress)
    {
      this.StreamId = streamId;
      return Task.FromResult(true);
    }
  }
}
