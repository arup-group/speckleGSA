using System;
using System.Collections.Generic;
using System.IO;
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

    public string Units { get => apiClient == null ? null : apiClient.Stream.BaseProperties["units"]; }

		public string StreamId { get => apiClient == null ? "" : apiClient.Stream.StreamId; }

		//public string ServerAddress { get => serverAddress; }

    /// <summary>
    /// Create SpeckleGSAReceiver object.
    /// </summary>
    /// <param name="serverAddress">Server address</param>
    /// <param name="apiToken">API token</param>
    public StreamReceiver(string serverAddress, string apiToken, ISpeckleAppMessenger messenger) : base(serverAddress, apiToken, messenger)
    {

    }

    /// <summary>
    /// Initializes receiver.
    /// </summary>
    /// <param name="streamID">Stream ID of stream</param>
    /// <returns>Task</returns>
    public async Task InitializeReceiver(string streamID, string documentName, string clientID = "")
    {
      apiClient.StreamId = streamID;
      apiClient.AuthToken = apiToken;

      if (string.IsNullOrEmpty(clientID))
      {
				tryCatchWithEvents(() =>
				{
					var clientResponse = apiClient.ClientCreateAsync(new AppClient()
					{
						DocumentName = documentName,
						DocumentType = "GSA",
						Role = "Receiver",
						StreamId = streamID,
						Online = true,
					}).Result;

					apiClient.ClientId = clientResponse.Resource._id;
				}, "", "Unable to create client on server");
      }
      else
      {
				tryCatchWithEvents(() =>
				{
					_ = apiClient.ClientUpdateAsync(clientID, new AppClient()
					{
						DocumentName = documentName,
						Online = true,
					}).Result;

					apiClient.ClientId = clientID;
				}, "", "Unable to update client on server");
      }

      apiClient.SetupWebsocket();
      apiClient.JoinRoom("stream", streamID);

      apiClient.OnWsMessage += OnWsMessage;
    }

		/// <summary>
		/// Return a list of SpeckleObjects from the stream.
		/// </summary>
		/// <returns>List of SpeckleObjects</returns>
		public List<SpeckleObject> GetObjects()
    {
      UpdateGlobal();

      return apiClient.Stream.Objects.Where(o => o != null && !(o is SpecklePlaceholder)).Distinct().ToList();
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

			var exceptionThrown = tryCatchWithEvents(() =>
			{
				streamGetResult = apiClient.StreamGetAsync(apiClient.StreamId, null).Result;
			}, "", "Unable to get stream info from server");

			if (!exceptionThrown && streamGetResult.Success == false)
			{
				messenger.Message(MessageIntent.Display, MessageLevel.Error, "Failed to receive " + apiClient.Stream.Name + "stream.");
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

			foreach (SpeckleObject obj in receivedObjects)
			{
				int streamLoc = apiClient.Stream.Objects.FindIndex(o => o._id == obj._id);
				try
				{
					apiClient.Stream.Objects[streamLoc] = obj;
				}
				catch
				{ }
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
    }
  }
}
