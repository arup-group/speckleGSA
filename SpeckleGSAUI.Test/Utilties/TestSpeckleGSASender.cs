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
    
    public Task InitializeSender(string documentName, BasePropertyUnits units, double tolerance, double angleTolerance, string streamId = "",
      string clientId = "", string streamName = "", IProgress<int> totalProgress = null, IProgress<int> incrementProgress = null)
    {
      if (string.IsNullOrEmpty(clientId))
      {
        //Just like the real thing, this simulates the creation of a stream if the client ID is not present
        this.StreamId = RandomString(8);
      }
      else
      {
        this.StreamId = streamId;
      }
      this.clientId = clientId;
      this.StreamName = streamName;
      return Task.CompletedTask;
    }

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

    public Task UpdateName(string streamName)
    {
      this.StreamName = streamName;
      return Task.CompletedTask;
    }

    public async Task<StreamBasicData> GetStream(string streamId)
    {
      return new StreamBasicData(streamId, "", "");
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

  }
}
