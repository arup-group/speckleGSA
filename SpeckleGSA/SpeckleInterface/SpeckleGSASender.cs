using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SpeckleCore;
using SpeckleGSAProxy;

namespace SpeckleGSA
{
  /// <summary>
  /// Packages and sends objects as a stream.
  /// </summary>
  public class SpeckleGSASender
  {
    const double MAX_BUCKET_SIZE = 5e5;

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

      SpeckleInitializer.Initialize();
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

        var clientResponse = mySender.ClientCreateAsync(new AppClient()
        {
          DocumentName = Path.GetFileNameWithoutExtension(((GSAInterfacer)GSA.Interfacer).FilePath),
          DocumentType = "GSA",
          Role = "Sender",
          StreamId = this.StreamID,
          Online = true,
        }).Result;

        mySender.ClientId = clientResponse.Resource._id;
      }
      else
      {
        var streamResponse = mySender.StreamGetAsync(streamID, null).Result;

        mySender.Stream = streamResponse.Resource;
        mySender.StreamId = streamResponse.Resource.StreamId;

        var clientResponse = mySender.ClientUpdateAsync(clientID, new AppClient()
        {
          DocumentName = Path.GetFileNameWithoutExtension(((GSAInterfacer)GSA.Interfacer).FilePath),
          Online = true,
        }).Result;

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

        List<SpeckleObject> convertedObjects = SpeckleCore.Converter.Serialise(kvp.Value).Where(o => o != null).ToList();

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
          try
          {
            ResponseObject res = mySender.ObjectCreateAsync(payload).Result;

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
          catch
          {
            Status.AddError("Failed to send payload.");
          }
        }
      }
      else
        objectsInStream = bucketObjects.Select(o => o._id).ToList();

      // Update stream with payload
      List<SpeckleObject> placeholders = new List<SpeckleObject>();

      foreach (string id in objectsInStream)
        placeholders.Add(new SpecklePlaceholder() { _id = id });

      SpeckleStream updateStream = new SpeckleStream();
      updateStream.Layers = layers;
      updateStream.Objects = placeholders;
      updateStream.Name = StreamName;
      updateStream.BaseProperties = GSA.GetBaseProperties();

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
      // Separate objects into sizable payloads
      long totalBucketSize = 0;
      long currentBucketSize = 0;
      List<List<SpeckleObject>> objectUpdatePayloads = new List<List<SpeckleObject>>();
      List<SpeckleObject> currentBucketObjects = new List<SpeckleObject>();
      List<SpeckleObject> allObjects = new List<SpeckleObject>();

      foreach (SpeckleObject obj in bucketObjects)
      {
        long size = Converter.getBytes(obj).Length;
        currentBucketSize += size;
        totalBucketSize += size;
        currentBucketObjects.Add(obj);

        if (currentBucketSize > MAX_BUCKET_SIZE)
        {
          //Status.AddMessage("Reached payload limit. Making a new one, current  #: " + objectUpdatePayloads.Count);
          objectUpdatePayloads.Add(currentBucketObjects);
          currentBucketObjects = new List<SpeckleObject>();
          currentBucketSize = 0;
        }
      }

      // add in the last bucket 
      if (currentBucketObjects.Count > 0)
        objectUpdatePayloads.Add(currentBucketObjects);

      return objectUpdatePayloads;
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
