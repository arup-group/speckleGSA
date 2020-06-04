﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SpeckleCore;

namespace SpeckleGSA
{
	/// <summary>
	/// Packages and sends objects as a stream.
	/// </summary>
	public class SpeckleGSASender : ISpeckleGSASender
  {
    const double MAX_BUCKET_SIZE = 1000000;
    const int SendPayloadApiRetries = 3;

    public string StreamName { get => mySender == null ? null : mySender.Stream.Name; }
    public string StreamID { get => mySender == null ? null : mySender.StreamId; }
    public string ClientID { get => mySender == null ? null : mySender.ClientId; }

    private SpeckleApiClient mySender;
    private string apiToken;

    /// <summary>
    /// Create SpeckleGSASender object.
    /// </summary>
    /// <param name="serverAddress">Server address</param>
    /// <param name="apiToken">API token</param>
    public SpeckleGSASender(string serverAddress, string apiToken)
    {
      this.apiToken = apiToken;

      mySender = new SpeckleApiClient() { BaseUrl = serverAddress.ToString() };

      //SpeckleInitializer.Initialize();
      LocalContext.Init();
    }

    /// <summary>
    /// Initializes sender.
    /// </summary>
    /// <param name="streamID">Stream ID of stream. If no stream ID is given, a new stream is created.</param>
    /// <param name="streamName">Stream name</param>
    /// <returns>Task</returns>
    public async Task InitializeSender(string streamID = "", string clientID = "", string streamName = "")
    {
      mySender.AuthToken = apiToken;

      if (string.IsNullOrEmpty(clientID))
      {
        var streamResponse = mySender.StreamCreateAsync(new SpeckleStream()).Result;

        mySender.Stream = streamResponse.Resource;
        mySender.StreamId = streamResponse.Resource.StreamId;

        var clientResponse = await mySender.ClientCreateAsync(new AppClient()
        {
          DocumentName = Path.GetFileNameWithoutExtension(GSA.gsaProxy.FilePath),
          DocumentType = "GSA",
          Role = "Sender",
          StreamId = this.StreamID,
          Online = true,
        });

        mySender.ClientId = clientResponse.Resource._id;
      }
      else
      {
        var streamResponse = mySender.StreamGetAsync(streamID, null).Result;

        mySender.Stream = streamResponse.Resource;
        mySender.StreamId = streamResponse.Resource.StreamId;

        var clientResponse = await mySender.ClientUpdateAsync(clientID, new AppClient()
        {
          DocumentName = Path.GetFileNameWithoutExtension(GSA.gsaProxy.FilePath),
          Online = true,
        });

        mySender.ClientId = clientID;
      }

      mySender.Stream.Name = streamName;

      mySender.SetupWebsocket();
      mySender.JoinRoom("stream", streamID);
    }

    /// <summary>
    /// Update stream name.
    /// </summary>
    /// <param name="streamName">Stream name</param>
    public void UpdateName(string streamName)
    {
      mySender.StreamUpdateAsync(mySender.StreamId, new SpeckleStream() { Name = streamName });

      mySender.Stream.Name = streamName;
    }

    /// <summary>
    /// Send objects to stream.
    /// </summary>
    /// <param name="payloadObjects">Dictionary of lists of objects indexed by layer name</param>
    public void SendGSAObjects(Dictionary<string, List<object>> payloadObjects)
    {
      // Convert and set up layers
      List<SpeckleObject> bucketObjects = new List<SpeckleObject>();
      List<Layer> layers = new List<Layer>();

      int objectCounter = 0;
      int orderIndex = 0;
      foreach (KeyValuePair<string, List<object>> kvp in payloadObjects)
      {
        if (kvp.Value.Count() == 0)
          continue;

        List<SpeckleObject> convertedObjects = Converter.Serialise(kvp.Value).Where(o => o != null).ToList();

        layers.Add(new Layer()
        {
          Name = kvp.Key,
          Guid = Guid.NewGuid().ToString(),
          ObjectCount = convertedObjects.Count,
          StartIndex = objectCounter,
          OrderIndex = orderIndex++,
          Topology = ""
        });

        bucketObjects.AddRange(convertedObjects);
        objectCounter += convertedObjects.Count;
      }

      //Status.AddMessage("Successfully converted: " + bucketObjects.Count() + " objects.");

      // Prune objects with placeholders using local DB
      try
      {
        LocalContext.PruneExistingObjects(bucketObjects, mySender.BaseUrl);
      }
      catch { }

      // Store IDs of objects to add to stream
      List<string> objectsInStream = new List<string>();

      // Separate objects into sizeable payloads
      List<List<SpeckleObject>> payloads = CreatePayloads(bucketObjects);

      if (bucketObjects.Count(o => o.Type == "Placeholder") < bucketObjects.Count)
      {
        // Send objects which are in payload and add to local DB with updated IDs
        foreach (List<SpeckleObject> payload in payloads)
        {
          var SendPayloadApiRetries = 3;
          do
          {
            try
            {
              ResponseObject res = mySender.ObjectCreateAsync(payload).Result;
              SendPayloadApiRetries = 0;

              for (int i = 0; i < payload.Count(); i++)
              {
                payload[i]._id = res.Resources[i]._id;
                objectsInStream.Add(payload[i]._id);
              }

              Task.Run(() =>
              {
                foreach (SpeckleObject obj in payload)
                {
                  try
                  {
                    LocalContext.AddSentObject(obj, mySender.BaseUrl);
                  }
                  catch { }
                }
              });
            }
            catch (Exception ex)
            {
              SendPayloadApiRetries--;
              if (SendPayloadApiRetries == 0)
              {
                if (ex is AggregateException && ex.InnerException != null && ex.InnerException.Message.Contains("task was canceled"))
                {
                  Status.AddError("Failed to send payload, likely due to timeout with server");
                }
                else
                {
                  Status.AddError("Failed to send payload. " + ex.Message);
                }
              }
              else
              {
                Status.AddError("Retrying payload");
              }
            }
          } while (SendPayloadApiRetries > 0);
        }
      }
      else
      {
        objectsInStream = bucketObjects.Select(o => o._id).ToList();
      }

      // Update stream with payload
      List<SpeckleObject> placeholders = new List<SpeckleObject>();

      foreach (string id in objectsInStream)
        placeholders.Add(new SpecklePlaceholder() { _id = id });

			SpeckleStream updateStream = new SpeckleStream
			{
				Layers = layers,
				Objects = placeholders,
				Name = StreamName,
				BaseProperties = GSA.GetBaseProperties()
			};

			try
      {
        var response = mySender.StreamUpdateAsync(StreamID, updateStream).Result;
        mySender.Stream.Layers = updateStream.Layers.ToList();
        mySender.Stream.Objects = placeholders;

        mySender.BroadcastMessage("stream", StreamID, new { eventType = "update-global" });

        var responseClone = mySender.StreamCloneAsync(StreamID).Result;

        Status.AddMessage("Successfully sent " + StreamName + " stream with " + updateStream.Objects.Count() + " objects.");
      }
      catch
      {
        Status.AddError("Failed to send " + StreamName + " stream.");
      }
    }

    /// <summary>
    /// Create payloads.
    /// </summary>
    /// <param name="bucketObjects">List of SpeckleObjects to separate into payloads</param>
    /// <returns>List of list of SpeckleObjects separated into payloads</returns>
    public List<List<SpeckleObject>> CreatePayloads(List<SpeckleObject> bucketObjects)
    {
      // Seperate objects into sizable payloads
      long totalBucketSize = 0;
      long currentBucketSize = 0;
      var payloadDict = new List<Tuple<long, List<SpeckleObject>>>();
      var currentBucketObjects = new List<SpeckleObject>();

      long size = 0;
      long placeholderSize = 0;

      foreach (SpeckleObject obj in bucketObjects)
      {
        size = 0;

        if (obj is SpecklePlaceholder)
        {
          if (placeholderSize == 0)
          {
            try
            {
              placeholderSize = Converter.getBytes(JsonConvert.SerializeObject(obj)).Length;
            }
            catch { }
          }
          size = placeholderSize;
        }
        else if (obj.Type.ToLower().Contains("result"))
        {
          size = Converter.getBytes(obj).Length;
        }

        if (size == 0)
        {
          string objAsJson = "";
          try
          {
            objAsJson = JsonConvert.SerializeObject(obj);
          }
          catch { }

          try
          {
            size = (objAsJson == "") ? Converter.getBytes(obj).Length : Converter.getBytes(objAsJson).Length;
          }
          catch { }
        }

        if (size > MAX_BUCKET_SIZE || (currentBucketSize + size) > MAX_BUCKET_SIZE)
        {
          payloadDict.Add(new Tuple<long, List<SpeckleObject>>(size, currentBucketObjects));
          currentBucketObjects = new List<SpeckleObject>();
          currentBucketSize = 0;
        }

        currentBucketObjects.Add(obj);
        currentBucketSize += size;
        totalBucketSize += size;
      }

      // add in the last bucket 
      if (currentBucketObjects.Count > 0)
      {
        payloadDict.Add(new Tuple<long, List<SpeckleObject>>(size, currentBucketObjects));
      }

      payloadDict = payloadDict.OrderByDescending(o => o.Item1).ToList();
      return payloadDict.Select(d => d.Item2).ToList();
    }

    /// <summary>
    /// Dispose the sender.
    /// </summary>
    public void Dispose()
    {
      mySender.ClientUpdateAsync(mySender.ClientId, new AppClient() { Online = false });
    }
  }
}
