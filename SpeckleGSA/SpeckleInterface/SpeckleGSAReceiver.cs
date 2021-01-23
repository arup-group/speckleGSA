using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SpeckleCore;
using SpeckleGSAInterfaces;

namespace SpeckleGSA
{
	/// <summary>
	/// Receive objects from a stream.
	/// </summary>
	public class SpeckleGSAReceiver : ISpeckleGSAReceiver
  {
		//This was chosen to cause typical message payloads of round 100-300k to be sent from the server
    const int MAX_OBJ_REQUEST_COUNT = 1000;

    private SpeckleApiClient myReceiver;

    private readonly string apiToken;
    private readonly string serverAddress;

    public event EventHandler<EventArgs> UpdateGlobalTrigger;

    //public string StreamID { get => myReceiver == null ? null : myReceiver.StreamId; }
    //public string StreamName { get => myReceiver == null ? null : myReceiver.Stream.Name; }

    public string Units { get => myReceiver == null ? null : myReceiver.Stream.BaseProperties["units"]; }

		public string StreamId { get => myReceiver == null ? "" : myReceiver.Stream.StreamId; }

    /// <summary>
    /// Create SpeckleGSAReceiver object.
    /// </summary>
    /// <param name="serverAddress">Server address</param>
    /// <param name="apiToken">API token</param>
    public SpeckleGSAReceiver(string serverAddress, string apiToken)
    {
      this.apiToken = apiToken;
			this.serverAddress = serverAddress;
      myReceiver = new SpeckleApiClient() { BaseUrl = serverAddress.ToString() };

      //SpeckleInitializer.Initialize();
      LocalContext.Init();
    }

    /// <summary>
    /// Initializes receiver.
    /// </summary>
    /// <param name="streamID">Stream ID of stream</param>
    /// <returns>Task</returns>
    public async Task InitializeReceiver(string streamID, string clientID = "")
    {
      myReceiver.StreamId = streamID;
      myReceiver.AuthToken = apiToken;

      if (string.IsNullOrEmpty(clientID))
      {
				HelperFunctions.tryCatchWithEvents(() =>
				{
					var clientResponse = myReceiver.ClientCreateAsync(new AppClient()
					{
						DocumentName = Path.GetFileNameWithoutExtension(GSA.GsaApp.gsaProxy.FilePath),
						DocumentType = "GSA",
						Role = "Receiver",
						StreamId = streamID,
						Online = true,
					}).Result;

					myReceiver.ClientId = clientResponse.Resource._id;
				}, "", "Unable to create client on server");
      }
      else
      {
				HelperFunctions.tryCatchWithEvents(() =>
				{
					_ = myReceiver.ClientUpdateAsync(clientID, new AppClient()
					{
						DocumentName = Path.GetFileNameWithoutExtension(GSA.GsaApp.gsaProxy.FilePath),
						Online = true,
					}).Result;

					myReceiver.ClientId = clientID;
				}, "", "Unable to update client on server");
      }

      myReceiver.SetupWebsocket();
      myReceiver.JoinRoom("stream", streamID);

      myReceiver.OnWsMessage += OnWsMessage;
    }

		/// <summary>
		/// Return a list of SpeckleObjects from the stream.
		/// </summary>
		/// <returns>List of SpeckleObjects</returns>
		public List<SpeckleObject> GetObjects()
    {
      UpdateGlobal();

      return myReceiver.Stream.Objects.Where(o => o != null && !(o is SpecklePlaceholder)).ToList();
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
					GSA.GsaApp.gsaMessenger.Message(MessageIntent.Display, MessageLevel.Error, 
						"Unknown event: " + (string)e.EventObject.args.eventType);
          break;
      }
    }

    /// <summary>
    /// Update stream children.
    /// </summary>
    public void UpdateChildren()
    {
			HelperFunctions.tryCatchWithEvents(() =>
			{
				var result = myReceiver.StreamGetAsync(myReceiver.StreamId, "fields=children").Result;
				myReceiver.Stream.Children = result.Resource.Children;
			}, "", "Unable to get children of stream");
    }

		/// <summary>
		/// Force client to update to stream.
		/// </summary>
		public void UpdateGlobal()
		{
			// Try to get stream
			ResponseStream streamGetResult = null;

			var exceptionThrown = HelperFunctions.tryCatchWithEvents(() =>
			{
				streamGetResult = myReceiver.StreamGetAsync(myReceiver.StreamId, null).Result;
			}, "", "Unable to get stream info from server");

			if (!exceptionThrown && streamGetResult.Success == false)
			{
				GSA.GsaApp.gsaMessenger.Message(MessageIntent.Display, MessageLevel.Error, "Failed to receive " + myReceiver.Stream.Name + "stream.");
				return;
			}

			myReceiver.Stream = streamGetResult.Resource;

			// Store stream data in local DB
			HelperFunctions.tryCatchWithEvents(() =>
			{
				LocalContext.AddOrUpdateStream(myReceiver.Stream, myReceiver.BaseUrl);
			}, "", "Unable to add or update stream details into local database");

			string[] payload = myReceiver.Stream.Objects.Where(o => o.Type == "Placeholder").Select(o => o._id).ToArray();

			List<SpeckleObject> receivedObjects = new List<SpeckleObject>();

			// Get remaining objects from server
			for (int i = 0; i < payload.Length; i += MAX_OBJ_REQUEST_COUNT)
			{
				string[] partialPayload = payload.Skip(i).Take(MAX_OBJ_REQUEST_COUNT).ToArray();

				HelperFunctions.tryCatchWithEvents(() =>
				{
					ResponseObject response = myReceiver.ObjectGetBulkAsync(partialPayload, "omit=displayValue").Result;

					receivedObjects.AddRange(response.Resources);
				}, "", "Unable to get objects for stream in bulk");
			}

			foreach (SpeckleObject obj in receivedObjects)
			{
				int streamLoc = myReceiver.Stream.Objects.FindIndex(o => o._id == obj._id);
				try
				{
					myReceiver.Stream.Objects[streamLoc] = obj;
				}
				catch
				{ }
			}

			GSA.GsaApp.gsaMessenger.Message(MessageIntent.Display, MessageLevel.Information, 
				"Received " + myReceiver.Stream.Name + " stream with " + myReceiver.Stream.Objects.Count() + " objects.");
		}

    /// <summary>
    /// Dispose the receiver.
    /// </summary>
    public void Dispose()
    {
			HelperFunctions.tryCatchWithEvents(() =>
			{
				_ = myReceiver.ClientUpdateAsync(myReceiver.ClientId, new AppClient() { Online = false }).Result;
			}, "", "Unable to update client on server");
    }
  }
}
