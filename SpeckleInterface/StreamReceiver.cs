﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SpeckleCore;

namespace SpeckleInterface
{
  /// <summary>
  /// Receive objects from a stream.
  /// </summary>
  public class StreamReceiver : StreamBase, IStreamReceiver
  {
		//This was chosen to cause typical message payloads of round 100-300k to be sent from the server
    const int MAX_OBJ_REQUEST_COUNT = 1000;

    public event EventHandler<EventArgs> UpdateGlobalTrigger;

    public string Units { get => (apiClient == null || apiClient.Stream == null || apiClient.Stream.BaseProperties == null) ? null 
        : apiClient.Stream.BaseProperties["units"]; }

    private IProgress<int> incrementProgress;
    private IProgress<int> totalProgress;

    //public string ServerAddress { get => serverAddress; }

    /// <summary>
    /// Create SpeckleGSAReceiver object.
    /// </summary>
    /// <param name="serverAddress">Server address</param>
    /// <param name="apiToken">API token</param>
    public StreamReceiver(string serverAddress, string apiToken, ISpeckleAppMessenger messenger) : base(serverAddress, apiToken, messenger) { }

    public async Task<bool> InitializeReceiver(string streamId, string documentName, IProgress<int> totalProgress, IProgress<int> incrementProgress)
    {
      this.incrementProgress = incrementProgress;
      this.totalProgress = totalProgress;

      await apiClient.IntializeUser();

      //Check if the user has access to this stream in the first place
      if (!(await GetStream(streamId)))
      {
        return false;
      }

      if (!tryCatchWithEvents(() =>
      {
        var clientResponse = apiClient.ClientCreateAsync(new AppClient()
        {
          DocumentName = documentName,
          DocumentType = "GSA",
          Role = "Receiver",
          StreamId = streamId,
          Online = true,
        }).Result;

        apiClient.ClientId = clientResponse.Resource._id;
      }, "", "Unable to create client on server"))
      {
        return false;
      }

      await InitialiseUser();

      ConnectWebSocket();

      apiClient.OnWsMessage += OnWsMessage;

      return true;
    }

    public async Task<bool> InitializeReceiver(string streamId, string documentName, string clientId, IProgress<int> totalProgress, IProgress<int> incrementProgress)
    {
      this.incrementProgress = incrementProgress;
      this.totalProgress = totalProgress;

      await apiClient.IntializeUser();

      //Check if the user has access to this stream in the first place
      if (!(await GetStream(streamId)))
      {
        return false;
      }

      tryCatchWithEvents(() =>
      {
        _ = apiClient.ClientUpdateAsync(clientId, new AppClient()
        {
          DocumentName = documentName,
          Online = true,
        }).Result;

        apiClient.ClientId = clientId;
      }, "", "Unable to update client on server");

      await InitialiseUser();

      ConnectWebSocket();

      return true;
    }

    /// <summary>
    /// Return a list of SpeckleObjects from the stream.
    /// </summary>
    /// <returns>List of SpeckleObjects</returns>
    public List<SpeckleObject> GetObjects()
    {
      UpdateGlobal();
      return (apiClient == null || apiClient.Stream == null || apiClient.Stream.Objects == null) ? new List<SpeckleObject>() 
        : apiClient.Stream.Objects.Where(o => o != null && !(o is SpecklePlaceholder)).Distinct().ToList();
    }

    /// <summary>
    /// Handles web-socket messages.
    /// </summary>
    /// <param name="source">Source</param>
    /// <param name="e">Event argument</param>
    public void OnWsMessage(object source, SpeckleEventArgs e)
    {
      if (e == null) return;
      if (e.EventObject == null) return;
      switch ((string)e.EventObject.args.eventType)
      {
        case "update-global":
          UpdateGlobalTrigger?.Invoke(null, null);
          break;
        case "update-children":
          UpdateChildren();
          break;
        default:
					messenger.Message(MessageIntent.Display, MessageLevel.Error, 
						"Unknown event: " + (string)e.EventObject.args.eventType);
          break;
      }
    }

    /// <summary>
    /// Update stream children.
    /// </summary>
    public void UpdateChildren()
    {
			tryCatchWithEvents(() =>
			{
        var result = apiClient.StreamGetAsync(apiClient.StreamId, "fields=children").Result;
        apiClient.Stream.Children = result.Resource.Children;
			}, "", "Unable to get children of stream");
    }

		/// <summary>
		/// Force client to update to stream.
		/// </summary>
		public void UpdateGlobal()
		{
			// Try to get stream
			ResponseStream streamGetResult = null;

			var success = tryCatchWithEvents(() =>
			{
        streamGetResult = apiClient.StreamGetAsync(apiClient.StreamId, null).Result;
      }, "", "Unable to get stream info from server");

			if (!success || streamGetResult == null || (streamGetResult != null && streamGetResult.Success == false))
			{
				messenger.Message(MessageIntent.Display, MessageLevel.Error, "Unable to access " + apiClient.StreamId);
				return;
			}

      apiClient.Stream = streamGetResult.Resource;

      // Store stream data in local DB
      tryCatchWithEvents(() =>
			{
        LocalContext.AddOrUpdateStream(apiClient.Stream, apiClient.BaseUrl);
			}, "", "Unable to add or update stream details into local database");

      string[] payload = apiClient.Stream.Objects.Where(o => o.Type == "Placeholder").Select(o => o._id).ToArray();

      List<SpeckleObject> receivedObjects = new List<SpeckleObject>();

			// Get remaining objects from server
			for (int i = 0; i < payload.Length; i += MAX_OBJ_REQUEST_COUNT)
			{
				string[] partialPayload = payload.Skip(i).Take(MAX_OBJ_REQUEST_COUNT).ToArray();

				tryCatchWithEvents(() =>
				{
					ResponseObject response = apiClient.ObjectGetBulkAsync(partialPayload, "omit=displayValue").Result;

					receivedObjects.AddRange(response.Resources);
				}, "", "Unable to get objects for stream in bulk");
			}

      if (apiClient.Stream.Objects == null || apiClient.Stream.Objects.Count() == 0)
      {
        //This shouldn't happen as the apiClient.Stream.Objects should at least be filled with placeholders as a results of the ObjectGetBulkAsync call above,
        //but including this alternative just in case
        apiClient.Stream.Objects = receivedObjects;
      }
      else
      {
        //There's been a case where the same object has been added to the same stream twice, which in this case meant there were 2 objects with the same
        // _id value, so this line just takes the first one whenever that happens
        var currObjsDict = apiClient.Stream.Objects.GroupBy(o => o._id).ToDictionary(o => o.Key, o => o.First());
        foreach (SpeckleObject obj in receivedObjects)
        {
          if (currObjsDict.ContainsKey(obj._id))
          {
            currObjsDict[obj._id] = obj;
          }
          else
          {
            //This shouldn't happen either, for the same reasons as above, but including this anyway just in case, to be as defensive as possible
            currObjsDict.Add(obj._id, obj);
          }
        }
        apiClient.Stream.Objects = currObjsDict.Values.ToList();
      }

      messenger.Message(MessageIntent.Display, MessageLevel.Information, 
      	"Received " + apiClient.Stream.Name + " stream with " + apiClient.Stream.Objects.Count() + " objects.");


    }

    /// <summary>
    /// Dispose the receiver.
    /// </summary>
    public void Dispose()
    {
			tryCatchWithEvents(() =>
			{
				_ = apiClient.ClientUpdateAsync(apiClient.ClientId, new AppClient() { Online = false }).Result;
			}, "", "Unable to update client on server");

      //DisconnectWebSocket();
    }
  }
}
