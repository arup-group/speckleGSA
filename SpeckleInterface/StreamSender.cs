﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SpeckleCore;

namespace SpeckleInterface
{
  /// <summary>
  /// Packages and sends objects as a stream.
  /// </summary>
  public class StreamSender : StreamBase, IStreamSender
  {
    const int DEFAULT_MAX_BUCKET_SIZE = 2000000;
    const int DEFAULT_API_TIMEOUT = 60000;
    const int DEFAULT_PARALLEL_API_REQUESTS = 5;

    private int maxPayloadBytes = DEFAULT_MAX_BUCKET_SIZE;
    private int apiTimeoutOverride = DEFAULT_API_TIMEOUT;
    private int numParallelApiRequests = DEFAULT_PARALLEL_API_REQUESTS;

    private IProgress<int> incrementProgress;
    private IProgress<int> totalProgress;

    /// <summary>
    /// Create SpeckleGSASender object.
    /// </summary>
    /// <param name="serverAddress">Server address</param>
    /// <param name="apiToken">API token</param>
    public StreamSender(string serverAddress, string apiToken, ISpeckleAppMessenger messenger) : base(serverAddress, apiToken, messenger)  {  }

    public async Task<bool> InitializeSender(string documentName, string streamName, BasePropertyUnits units, double tolerance, double angleTolerance, 
      IProgress<int> totalProgress, IProgress<int> incrementProgress)
    {
      this.totalProgress = totalProgress;
      this.incrementProgress = incrementProgress;

      tryCatchWithEvents(() =>
      {
        var streamToCreate = new SpeckleStream() { BaseProperties = CreateBaseProperties(units, tolerance, angleTolerance), Name = streamName };
        var streamResponse = apiClient.StreamCreateAsync(streamToCreate, timeoutMillisecondsOverride: apiTimeoutOverride).Result;
        apiClient.Stream = streamResponse.Resource;
        apiClient.StreamId = apiClient.Stream.StreamId;
      },
        "", "Unable to create stream on the server");

      tryCatchWithEvents(() =>
      {
        var clientResponse = apiClient.ClientCreateAsync(new AppClient()
        {
          DocumentName = documentName,
          DocumentType = "GSA",
          Role = "Sender",
          StreamId = this.StreamId,
          Online = true,
        }, timeoutMillisecondsOverride: apiTimeoutOverride).Result;
        apiClient.ClientId = clientResponse.Resource._id;
      }, "", "Unable to create client on the server");

      await InitialiseUser();

      ConnectWebSocket();

      return true;
    }

    public async Task<bool> InitializeSender(string documentName, string streamId, string clientId, IProgress<int> totalProgress, IProgress<int> incrementProgress)
    {
      this.totalProgress = totalProgress;
      this.incrementProgress = incrementProgress;

      tryCatchWithEvents(async () =>
      {
        var streamResponse = await apiClient.StreamGetAsync(streamId, null, timeoutMillisecondsOverride: DEFAULT_API_TIMEOUT);
        apiClient.Stream = streamResponse.Resource;
        apiClient.StreamId = streamId;
      }, "", "Unable to get stream response");

      tryCatchWithEvents(async () =>
      {
        var clientResponse = await apiClient.ClientUpdateAsync(clientId, new AppClient()
        {
          DocumentName = documentName,
          Online = true,
        }, timeoutMillisecondsOverride: apiTimeoutOverride);

        apiClient.ClientId = clientId;
      }, "", "Unable to update client on the server");

      await InitialiseUser();

      ConnectWebSocket();

      return true;
    }

    /// <summary>
    /// Update stream name.
    /// </summary>
    /// <param name="streamName">Stream name</param>
    public async Task<bool> UpdateName(string streamName)
    {
      apiClient.Stream.Name = streamName;
      var response = await apiClient.StreamUpdateAsync(apiClient.StreamId, apiClient.Stream);
      return ((response.Success.HasValue && response.Success.Value));
    }

    /// <summary>
    /// Send objects to stream.
    /// </summary>
    /// <param name="payloadObjects">Dictionary of lists of objects indexed by layer name</param>
    public int SendObjects(Dictionary<string, List<SpeckleObject>> payloadObjects, int maxPayloadBytes = 0, int apiTimeoutOverride = 0, int numParallel = 0)
    {
      var baseUrl = apiClient.BaseUrl;

      foreach (var o in payloadObjects.Keys.SelectMany(k => payloadObjects[k]))
      {
        if (o.Hash == null)
        {
          o.GenerateHash();
        }
      }

      if (maxPayloadBytes > 0)
      {
        this.maxPayloadBytes = maxPayloadBytes;
      }
      if (apiTimeoutOverride > 0)
      {
        this.apiTimeoutOverride = apiTimeoutOverride;
      }
      if (numParallel > 0)
      {
        this.numParallelApiRequests = numParallel;
      }

      GroupIntoLayers(payloadObjects, out List<Layer> layers, out List<SpeckleObject> bucketObjects);

      messenger.Message(MessageIntent.Display, MessageLevel.Information, "Successfully grouped " + bucketObjects.Count() + " objects into " + layers.Count() + " layers");

      DetermineObjectsToBeSent(bucketObjects, baseUrl, out List<SpeckleObject> changedObjects);

      int numErrors = 0;

      var numChanged = changedObjects.Count();
      var numUnchanged = bucketObjects.Count() - numChanged;
      if (numChanged == 0)
      {
        messenger.Message(MessageIntent.Display, MessageLevel.Information, "All " + numUnchanged + " objects are unchanged on the server for stream " + StreamId);
      }
      else
      {
        CreateObjectsOnServer(changedObjects, baseUrl, ref numErrors);

        messenger.Message(MessageIntent.Display, MessageLevel.Information,
          "Created/updated " + numChanged + " on the server for stream " + StreamId 
          + ((numUnchanged > 0) ? " (the other " + numUnchanged + " were pre-existing/unchanged)" : ""));
      }
      messenger.Message(MessageIntent.TechnicalLog, MessageLevel.Information, "Sent stream", "StreamId=" + StreamId,
        "NumCreated=" + (numChanged - numErrors), "NumErrors=" + numErrors, "NumFoundInSentCache=" + numUnchanged);

      UpdateStreamWithIds(layers, bucketObjects, ref numErrors);

      BroadcastStreamUpdate(ref numErrors);

      CloneStream(ref numErrors);

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
            tryCatchWithEvents(() =>
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
          tryCatchWithEvents(() =>
          {
            objAsJson = JsonConvert.SerializeObject(obj);
          }, "", "Unable to serialise object into JSON");

          tryCatchWithEvents(() =>
          {
            size = (objAsJson == "") ? Converter.getBytes(obj).Length : Converter.getBytes(objAsJson).Length;
          }, "", "Unable to get bytes from object or its JSON representation");
        }

        if (size > maxPayloadBytes || (currentBucketSize + size) > maxPayloadBytes)
        {
          payloadDict.Add(new Tuple<long, List<SpeckleObject>>(currentBucketSize, currentBucketObjects));
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
        messenger.Message(MessageIntent.TechnicalLog, MessageLevel.Debug,
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
      tryCatchWithEvents(() =>
      {
        _ = apiClient.ClientUpdateAsync(apiClient.ClientId, new AppClient() { Online = false }, apiTimeoutOverride).Result;
      },
        "", "Unable to update client on server with offline status");

      //DisconnectWebSocket();
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

    private void GroupIntoLayers(Dictionary<string, List<SpeckleObject>> payloadObjects, out List<Layer> layers, out List<SpeckleObject> bucketObjects)
    {
      // Convert and set up layers
      bucketObjects = new List<SpeckleObject>();
      layers = new List<Layer>();

      int objectCounter = 0;
      int orderIndex = 0;

      foreach (KeyValuePair<string, List<SpeckleObject>> kvp in payloadObjects.Where(kvp => kvp.Value.Count() > 0))
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

      apiClient.Stream.Objects = placeholders;
      apiClient.Stream.Layers = layers;
      apiClient.ClientType = "GSA";

      try
      {
        _ = apiClient.StreamUpdateAsync(StreamId, apiClient.Stream, apiTimeoutOverride).Result;
        messenger.Message(MessageIntent.Display, MessageLevel.Information, "Updated the stream's object list on the server", StreamId);
      }
      catch (Exception ex)
      {
        numErrors++;
        messenger.Message(MessageIntent.Display, MessageLevel.Error, "Updating the stream's object list on the server", StreamId);
        var speckleExceptionContext = ExtractSpeckleExceptionContext(ex);
        var errContext = speckleExceptionContext.Concat(new[] { "Endpoint=StreamUpdateAsync", "Error updating the stream's object list on the server", "StreamId=" + StreamId,
          "NumPlaceHolders=" + placeholders.Count(), "PayloadBytes=" + Converter.getBytes(apiClient.Stream.Objects).Length });
        messenger.Message(MessageIntent.TechnicalLog, MessageLevel.Error, ex, errContext.ToArray());
      }

      return;
    }

    private void CreateObjectsOnServer(List<SpeckleObject> bucketObjects, string baseUrl, ref int numErrors)
    {
      // Separate objects into sizeable payloads
      var payloads = CreatePayloads(bucketObjects);

      if (totalProgress != null)
      {
        totalProgress.Report(payloads.Count());
      }

      if (bucketObjects.Count(o => o.Type == "Placeholder") == bucketObjects.Count)
      {
        numErrors = 0;
        return;
      }

      // Send objects which are in payload and add to local DB with updated IDs
      for (var j = 0; j < payloads.Count(); j++)
      {
        ResponseObject res = null;
        //Make a copy so that the task below doesn't use the j variable, which could change (by looping) by the time the task is run
        var payload = payloads[j].ToList();   

        try
        {
          res = apiClient.ObjectCreateAsync(payload, apiTimeoutOverride).Result;
          if (incrementProgress != null)
          {
            incrementProgress.Report(1);
          }
        }
        catch (Exception ex)
        {
          numErrors++;
          var speckleExceptionContext = ExtractSpeckleExceptionContext(ex);
          var errContext = speckleExceptionContext.Concat(new[] { "StreamId=" + StreamId, "Endpoint=ObjectCreateAsync", 
            "PayloadBytes=" + Converter.getBytes(payload).Length, "Error in updating the server with a payload of " + payload.Count() + " objects" }); ;
          messenger.Message(MessageIntent.TechnicalLog, MessageLevel.Error, ex, errContext.ToArray());
        }

        if (res != null && res.Resources.Count() > 0)
        {
          for (int i = 0; i < payload.Count(); i++)
          {
            payload[i]._id = res.Resources[i]._id;
          }
        }


        Task.Run(() =>
        {
          //Don't save results to the hard disk as they take up a huge amount of space and are likely to change very often
          foreach (SpeckleObject obj in payload.Where(o => o.Hash != null && o._id != null && !o.Type.Contains("Result")))
          {
            tryCatchWithEvents(() => LocalContext.AddSentObject(obj, baseUrl), "", "Error in updating local db");
          }
        });
      }

      int successfulPayloads = payloads.Count() - numErrors;
      messenger.Message(MessageIntent.Display, MessageLevel.Information,
          "Successfully sent " + successfulPayloads + "/" + payloads.Count() + " payloads to the server");
      messenger.Message(MessageIntent.TechnicalLog, MessageLevel.Information, "Sent payloads to server", 
        "NumSuccessful=" + successfulPayloads, "NumErrored=" + numErrors);
    }

    private void BroadcastStreamUpdate(ref int numErrors)
    {
      try
      {
        apiClient.BroadcastMessage("stream", StreamId, new { eventType = "update-global" });
      }
      catch (Exception ex)
      {
        numErrors++;
        var speckleExceptionContext = ExtractSpeckleExceptionContext(ex);
        var errContext = speckleExceptionContext.Concat(new[] { "Failed to broadcast update-global message on stream", "StreamId=" + StreamId });
        messenger.Message(MessageIntent.TechnicalLog, MessageLevel.Error, ex, errContext.ToArray());
      }
    }

    private void CloneStream(ref int numErrors)
    {
      try
      {
        _ = apiClient.StreamCloneAsync(StreamId).Result;
      }
      catch (Exception ex)
      {
        numErrors++;
        var speckleExceptionContext = ExtractSpeckleExceptionContext(ex);
        var errContext = speckleExceptionContext.Concat(new[] { "Failed to clone", "StreamId=" + StreamId });
        messenger.Message(MessageIntent.TechnicalLog, MessageLevel.Error, ex, errContext.ToArray());
      }
    }
  }
}
