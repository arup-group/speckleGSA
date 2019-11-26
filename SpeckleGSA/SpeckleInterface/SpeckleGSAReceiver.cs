using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SpeckleCore;

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

    private string apiToken;
    private string serverAddress;

    public event EventHandler<EventArgs> UpdateGlobalTrigger;

    //public string StreamID { get => myReceiver == null ? null : myReceiver.StreamId; }
    //public string StreamName { get => myReceiver == null ? null : myReceiver.Stream.Name; }

    public string Units { get => myReceiver == null ? null : myReceiver.Stream.BaseProperties["units"]; }

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
				var task = myReceiver.ClientCreateAsync(new AppClient()
				{
					DocumentName = Path.GetFileNameWithoutExtension(GSA.gsaProxy.FilePath),
					DocumentType = "GSA",
					Role = "Receiver",
					StreamId = streamID,
					Online = true,
				});
				await task;
				var clientResponse = task.Result;

        myReceiver.ClientId = clientResponse.Resource._id;
      }
      else
      {
				var task = myReceiver.ClientUpdateAsync(clientID, new AppClient()
				{
					DocumentName = Path.GetFileNameWithoutExtension(GSA.gsaProxy.FilePath),
					Online = true,
				});
				await task;
				var clientResponse = task.Result;

        myReceiver.ClientId = clientID;
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
          Status.AddError("Unknown event: " + (string)e.EventObject.args.eventType);
          break;
      }
    }

    /// <summary>
    /// Update stream children.
    /// </summary>
    public void UpdateChildren()
    {
      var result = myReceiver.StreamGetAsync(myReceiver.StreamId, "fields=children").Result;
      myReceiver.Stream.Children = result.Resource.Children;
    }

		/// <summary>
		/// Force client to update to stream.
		/// </summary>
		public void UpdateGlobal()
		{
			// Try to get stream
			ResponseStream streamGetResult = myReceiver.StreamGetAsync(myReceiver.StreamId, null).Result;

			if (streamGetResult.Success == false)
			{
				Status.AddError("Failed to receive " + myReceiver.Stream.Name + "stream.");
			}
			else
			{
				myReceiver.Stream = streamGetResult.Resource;

				// Store stream data in local DB
				try
				{
					LocalContext.AddOrUpdateStream(myReceiver.Stream, myReceiver.BaseUrl);
				}
				catch { }

				// Get cached objects
				try
				{
					LocalContext.GetCachedObjects(myReceiver.Stream.Objects, myReceiver.BaseUrl);
				}
				catch { }

				string[] payload = myReceiver.Stream.Objects.Where(o => o.Type == "Placeholder").Select(o => o._id).ToArray();

				List<SpeckleObject> receivedObjects = new List<SpeckleObject>();

				// Get remaining objects from server
				for (int i = 0; i < payload.Length; i += MAX_OBJ_REQUEST_COUNT)
				{
					string[] partialPayload = payload.Skip(i).Take(MAX_OBJ_REQUEST_COUNT).ToArray();

					ResponseObject response = myReceiver.ObjectGetBulkAsync(partialPayload, "omit=displayValue").Result;

					receivedObjects.AddRange(response.Resources);
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

				Task.Run(() =>
				{
					foreach (SpeckleObject obj in receivedObjects)
					{
						try
						{
							LocalContext.AddCachedObject(obj, myReceiver.BaseUrl);
						}
						catch { }
					 }
				 });

				Status.AddMessage("Received " + myReceiver.Stream.Name + " stream with " + myReceiver.Stream.Objects.Count() + " objects.");
			}
		}

    /// <summary>
    /// Dispose the receiver.
    /// </summary>
    public void Dispose()
    {
      myReceiver.ClientUpdateAsync(myReceiver.ClientId, new AppClient() { Online = false });
    }
  }
}
