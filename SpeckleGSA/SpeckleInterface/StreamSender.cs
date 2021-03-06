﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SpeckleCore;
using SpeckleGSAInterfaces;

namespace SpeckleGSA
{
	/// <summary>
	/// Packages and sends objects as a stream.
	/// </summary>
	public class StreamSender : IStreamSender
  {
    const double MAX_BUCKET_SIZE = 1000000;

    public string StreamID => (apiClient == null) ? null : apiClient.StreamId;
    public string StreamName => (apiClient == null) ? null : apiClient.Stream.Name;
    public string ClientID => (apiClient == null) ? null : apiClient.ClientId;

    private readonly SpeckleApiClient apiClient;
    private readonly string apiToken;

    /// <summary>
    /// Create SpeckleGSASender object.
    /// </summary>
    /// <param name="serverAddress">Server address</param>
    /// <param name="apiToken">API token</param>
    public StreamSender(string serverAddress, string apiToken)
    {
      this.apiToken = apiToken;

      apiClient = new SpeckleApiClient() { BaseUrl = serverAddress.ToString() };

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
      apiClient.AuthToken = apiToken;

      if (string.IsNullOrEmpty(clientID))
      {
        HelperFunctions.tryCatchWithEvents(() =>
        {
          var streamResponse = apiClient.StreamCreateAsync(new SpeckleStream()).Result;
          apiClient.Stream = streamResponse.Resource;
          apiClient.StreamId = streamResponse.Resource.StreamId;
        },
        "", "Unable to create stream on the server");

        HelperFunctions.tryCatchWithEvents(() =>
        {
          var clientResponse = apiClient.ClientCreateAsync(new AppClient()
          {
            DocumentName = Path.GetFileNameWithoutExtension(GSA.GsaApp.gsaProxy.FilePath),
            DocumentType = "GSA",
            Role = "Sender",
            StreamId = this.StreamID,
            Online = true,
          }).Result;
          apiClient.ClientId = clientResponse.Resource._id;
        }, "", "Unable to create client on the server");
      }
      else
      {
        HelperFunctions.tryCatchWithEvents(() =>
        {
          var streamResponse = apiClient.StreamGetAsync(streamID, null).Result;

          apiClient.Stream = streamResponse.Resource;
          apiClient.StreamId = streamResponse.Resource.StreamId;
        }, "", "Unable to get stream response");

        HelperFunctions.tryCatchWithEvents(() =>
        {
          var clientResponse = apiClient.ClientUpdateAsync(clientID, new AppClient()
          {
            DocumentName = Path.GetFileNameWithoutExtension(GSA.GsaApp.gsaProxy.FilePath),
            Online = true,
          }).Result;

          apiClient.ClientId = clientID;
        }, "", "Unable to update client on the server");
      }

      apiClient.Stream.Name = streamName;

      HelperFunctions.tryCatchWithEvents(() =>
      {
        apiClient.SetupWebsocket();
      }, "", "Unable to set up web socket");

      HelperFunctions.tryCatchWithEvents(() =>
      {
        apiClient.JoinRoom("stream", streamID);
      }, "", "Uable to join web socket");
    }

    /// <summary>
    /// Update stream name.
    /// </summary>
    /// <param name="streamName">Stream name</param>
    public void UpdateName(string streamName)
    {
      apiClient.StreamUpdateAsync(apiClient.StreamId, new SpeckleStream() { Name = streamName });

      apiClient.Stream.Name = streamName;
    }

    /// <summary>
    /// Send objects to stream.
    /// </summary>
    /// <param name="payloadObjects">Dictionary of lists of objects indexed by layer name</param>
    public int SendGSAObjects(Dictionary<string, List<object>> payloadObjects)
    {
      var baseUrl = apiClient.BaseUrl;

      GroupIntoLayers(payloadObjects, out List<Layer> layers, out List<SpeckleObject> bucketObjects);

      GSA.GsaApp.gsaMessenger.Message(MessageIntent.Display, MessageLevel.Information,
        "Successfully grouped " + bucketObjects.Count() + " objects into " + layers.Count() + " layers");

      DetermineObjectsToBeSent(bucketObjects, baseUrl, out List<SpeckleObject> changedObjects);

      int numErrors = 0;

      var numChanged = changedObjects.Count();
      var numUnchanged = bucketObjects.Count() - numChanged;
      if (numChanged == 0)
      {
        GSA.GsaApp.gsaMessenger.Message(MessageIntent.Display, MessageLevel.Information,
          "All " + numUnchanged + " objects are unchanged on the server for stream " + StreamID);
      }
      else
      {
        CreateObjectsOnServer(changedObjects, baseUrl, ref numErrors);

        GSA.GsaApp.gsaMessenger.Message(MessageIntent.Display, MessageLevel.Information,
          "Created/updated " + numChanged + " on the server for stream " + StreamID + " (the other " + numUnchanged + " were pre-existing/unchanged)");
      }
      GSA.GsaApp.gsaMessenger.CacheMessage(MessageIntent.TechnicalLog, MessageLevel.Information, "Sent stream", "StreamId=" + StreamID,
        "NumCreated=" + (numChanged - numErrors), "NumErrors=" + numErrors, "NumFoundInSentCache=" + numUnchanged);

      UpdateStreamWithIds(layers, bucketObjects, ref numErrors);

      BroadcastStreamUpdate(ref numErrors);

      CloneStream(ref numErrors);

      GSA.GsaApp.gsaMessenger.Trigger();

      return numErrors;
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
            HelperFunctions.tryCatchWithEvents(() =>
            {
              placeholderSize = Converter.getBytes(JsonConvert.SerializeObject(obj)).Length;
            }, "", "Unable to determine size of placeholder object");
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
          HelperFunctions.tryCatchWithEvents(() =>
          {
            objAsJson = JsonConvert.SerializeObject(obj);
          }, "", "Unable to serialise object into JSON");

          HelperFunctions.tryCatchWithEvents(() =>
          {
            size = (objAsJson == "") ? Converter.getBytes(obj).Length : Converter.getBytes(objAsJson).Length;
          }, "", "Unable to get bytes from object or its JSON representation");
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

      for (var i = 0; i < payloadDict.Count(); i++)
      {
        var groupedByType = payloadDict[i].Item2.GroupBy(o => o.Type).ToDictionary(o => o.Key, o => o.ToList());
        var numByTypeSummaries = groupedByType.Keys.Select(k => string.Join(":", k, groupedByType[k].Count())).ToList();
        GSA.GsaApp.gsaMessenger.CacheMessage(MessageIntent.TechnicalLog, MessageLevel.Debug,
          "Details for payload #" + (i + 1) + "/" + payloadDict.Count(), "TotalEstimatedSize=" + payloadDict[i].Item1,
          "NumObjectsByType=" + string.Join(";", numByTypeSummaries));
      }

      return payloadDict.Select(d => d.Item2).ToList();
    }

    /// <summary>
    /// Dispose the sender.
    /// </summary>
    public void Dispose()
    {
      HelperFunctions.tryCatchWithEvents(() =>
      {
        //lock (apiClientLock)
        {
          _ = apiClient.ClientUpdateAsync(apiClient.ClientId, new AppClient() { Online = false }).Result;
        }
      },
        "", "Unable to update client on server with offline status");
    }

    private List<string> ExtractSpeckleExceptionContext(Exception ex)
    {
      var speckleExceptionContext = new List<string>();
      if (ex.InnerException != null && ex.InnerException is SpeckleException)
      {
        var se = (SpeckleException)ex.InnerException;
        speckleExceptionContext.AddRange(new[] {"Response=" + se.Response, "StatusCode=" + se.StatusCode });
      }
      return speckleExceptionContext;
    }

    private void GroupIntoLayers(Dictionary<string, List<object>> payloadObjects, out List<Layer> layers, out List<SpeckleObject> bucketObjects)
    {
      // Convert and set up layers
      bucketObjects = new List<SpeckleObject>();
      layers = new List<Layer>();

      int objectCounter = 0;
      int orderIndex = 0;

      foreach (KeyValuePair<string, List<object>> kvp in payloadObjects.Where(kvp => kvp.Value.Count() > 0))
      {
        var convertedObjects = kvp.Value.Select(v => (SpeckleObject)v).ToList();

        if (convertedObjects != null && convertedObjects.Count() > 0)
        {
          layers.Add(new Layer()
          {
            Name = kvp.Key,
            Guid = Guid.NewGuid().ToString(),
            ObjectCount = convertedObjects.Count,
            StartIndex = objectCounter,
            OrderIndex = orderIndex++,
            Topology = ""
          });
        }

        bucketObjects.AddRange(convertedObjects);
        objectCounter += convertedObjects.Count;
      }
    }

    private void DetermineObjectsToBeSent(List<SpeckleObject> bucketObjects, string baseUrl, out List<SpeckleObject> toBeSentObjects)
    {
      //There is the possibility that placeholders with null database IDs are saved to the sent objects database
      //(the reason for this is not certain yet).  When this happens, PruneExistingObjects returns some placeholders which have
      //neither database ID nor hash.  As the method removes the link between Hashes and database IDs, there is no way to determine 
      //which objects were represented in the database with null database IDs.

      //To figure this out, run the method one object at a time.
      toBeSentObjects = new List<SpeckleObject>();
      foreach (var bo in bucketObjects)
      {
        // Prune objects with placeholders using local DB
        var foundInSentCache = false;
        try
        {
          var singleItemList = new List<SpeckleObject>() { bo };
          LocalContext.PruneExistingObjects(singleItemList, baseUrl);
          foundInSentCache = (singleItemList.First()._id != null);
          if (foundInSentCache)
          {
            bo._id = singleItemList.First()._id;
          }
        }
        catch { }

        if (!foundInSentCache)
        {
          toBeSentObjects.Add(bo);
        }
      }
    }

    private void UpdateStreamWithIds(List<Layer> layers, List<SpeckleObject> bucketObjects, ref int numErrors)
    {
      // Update stream with payload
      var placeholders = new List<SpeckleObject>();

      foreach (string id in bucketObjects.Select(bo => bo._id))
      {
        placeholders.Add(new SpecklePlaceholder() { _id = id });
      }

      SpeckleStream updateStream = new SpeckleStream
      {
        Layers = layers,
        Objects = placeholders,
        Name = StreamName,
        BaseProperties = GSA.GetBaseProperties()
      };

      try
      {
        _ = apiClient.StreamUpdateAsync(StreamID, updateStream).Result;
        apiClient.Stream.Layers = updateStream.Layers.ToList();
        apiClient.Stream.Objects = placeholders;
        GSA.GsaApp.gsaMessenger.CacheMessage(MessageIntent.Display, MessageLevel.Information, "Updated the stream's object list on the server", StreamID);
      }
      catch (Exception ex)
      {
        numErrors++;
        GSA.GsaApp.gsaMessenger.CacheMessage(MessageIntent.Display, MessageLevel.Error, "Updating the stream's object list on the server", StreamID);
        var speckleExceptionContext = ExtractSpeckleExceptionContext(ex);
        var errContext = speckleExceptionContext.Concat(new[] { "Error updating the stream's object list on the server", "StreamId=" + StreamID });
        GSA.GsaApp.gsaMessenger.CacheMessage(MessageIntent.TechnicalLog, MessageLevel.Error, ex, errContext.ToArray());
      }

      return;
    }

    private void CreateObjectsOnServer(List<SpeckleObject> bucketObjects, string baseUrl, ref int numErrors)
    {
      // Separate objects into sizeable payloads
      var payloads = CreatePayloads(bucketObjects);

      if (bucketObjects.Count(o => o.Type == "Placeholder") == bucketObjects.Count)
      {
        numErrors = 0;
        return;
      }

      var payloadTasks = payloads.Select(p => apiClient.ObjectCreateAsync(p, 30000)).ToArray();

      // Send objects which are in payload and add to local DB with updated IDs
      //foreach (List<SpeckleObject> payload in payloads)
      for (var j = 0; j < payloads.Count(); j++)
      {
        ResponseObject res = null;
        try
        {
          res = payloadTasks[j].Result;
        }
        catch (Exception ex)
        {
          numErrors++;
          var speckleExceptionContext = ExtractSpeckleExceptionContext(ex);
          var errContext = speckleExceptionContext.Concat(new[] { "StreamId=" + StreamID,
                "Error in updating the server with a payload of " + payloads[j].Count() + " objects" });
          GSA.GsaApp.gsaMessenger.Message(MessageIntent.TechnicalLog, MessageLevel.Error, ex, errContext.ToArray());
        }

        if (res != null && res.Resources.Count() > 0)
        {
          for (int i = 0; i < payloads[j].Count(); i++)
          {
            payloads[j][i]._id = res.Resources[i]._id;
          }
        }

        Task.Run(() =>
        {
          foreach (SpeckleObject obj in payloads[j].Where(o => o.Hash != null && o._id != null))
          {
            HelperFunctions.tryCatchWithEvents(() => LocalContext.AddSentObject(obj, baseUrl), "", "Error in updating local db");
          }
        });
      }

      int successfulPayloads = payloads.Count() - numErrors;
      GSA.GsaApp.gsaMessenger.Message(MessageIntent.Display, MessageLevel.Information,
          "Successfully sent " + successfulPayloads + "/" + payloads.Count() + " payloads to the server");
      GSA.GsaApp.gsaMessenger.CacheMessage(MessageIntent.TechnicalLog, MessageLevel.Information, "Sent payloads to server", 
        "NumSuccessful=" + successfulPayloads, "NumErrored=" + numErrors);
    }

    private void BroadcastStreamUpdate(ref int numErrors)
    {
      try
      {
        apiClient.BroadcastMessage("stream", StreamID, new { eventType = "update-global" });
      }
      catch (Exception ex)
      {
        numErrors++;
        var speckleExceptionContext = ExtractSpeckleExceptionContext(ex);
        var errContext = speckleExceptionContext.Concat(new[] { "Failed to broadcast update-global message on stream", "StreamId=" + StreamID });
        GSA.GsaApp.gsaMessenger.CacheMessage(MessageIntent.TechnicalLog, MessageLevel.Error, ex, errContext.ToArray());
      }
    }

    private void CloneStream(ref int numErrors)
    {
      try
      {
        _ = apiClient.StreamCloneAsync(StreamID).Result;
      }
      catch (Exception ex)
      {
        numErrors++;
        var speckleExceptionContext = ExtractSpeckleExceptionContext(ex);
        var errContext = speckleExceptionContext.Concat(new[] { "Failed to clone", "StreamId=" + StreamID });
        GSA.GsaApp.gsaMessenger.CacheMessage(MessageIntent.TechnicalLog, MessageLevel.Error, ex, errContext.ToArray());
      }
    }
  }
}
