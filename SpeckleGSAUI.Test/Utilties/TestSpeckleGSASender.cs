using SpeckleCore;
using SpeckleInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SpeckleGSAUI.Test
{
  class TestSpeckleGSASender : IStreamSender
  {
    public string StreamName { get; private set; }

    public Dictionary<string, List<object>> SentObjects = new Dictionary<string, List<object>>();

    public string StreamId { get; private set; }
    public string Token { get; set; }

    public string ServerAddress = "https://test.speckle.works";

    public string ClientId { get => clientId; }

    private string clientId;

    private static Random random = new Random();
   

    //objects by layer name
    public int SendObjects(Dictionary<string, List<SpeckleObject>> value, int maxPayloadBytes = 0, int apiTimeoutOverride = 0, int numParallel = 0)
    {
      foreach (var key in value.Keys)
      {
        if (!SentObjects.ContainsKey(key))
        {
          SentObjects.Add(key, new List<object>());
        }
        SentObjects[key].AddRange(value[key]);
      }
      return 0;
    }

    public Task<bool> UpdateName(string streamName)
    {
      this.StreamName = streamName;
      return Task.FromResult(true);
    }

    private string RandomString(int length)
    {
      const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
      return new string(Enumerable.Repeat(chars, length)
        .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    public void Dispose()
    {

    }

    public bool InitializeSender(string documentName, string streamName, BasePropertyUnits units, double tolerance, double angleTolerance, IProgress<int> totalProgress, IProgress<int> incrementProgress)
    {
      //Just like the real thing, this simulates the creation of a stream if the client ID is not present
      this.StreamId = RandomString(8);
      this.StreamName = streamName;
      return true;
    }

    public bool InitializeSender(string documentName, string streamId, string clientId, IProgress<int> totalProgress, IProgress<int> incrementProgress)
    {
      this.StreamId = streamId;
      this.clientId = clientId;
      return true;
    }
  }
}
