using SpeckleCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SpeckleInterface
{
  public abstract class StreamBase
  {
    protected readonly ISpeckleAppMessenger messenger;

    protected readonly SpeckleApiClient apiClient;

    public string StreamId { get => (apiClient == null || apiClient.Stream == null) ? null : apiClient.Stream.StreamId; }
    public string StreamName => (apiClient == null || apiClient.Stream == null) ? null : apiClient.Stream.Name;
    public string ClientId => apiClient?.ClientId;

    protected readonly bool verboseDisplayLog;

    public StreamBase(string serverAddress, string apiToken, ISpeckleAppMessenger messenger, bool verboseDisplayLog = false)
    {
      this.messenger = messenger;
      this.verboseDisplayLog = verboseDisplayLog;

      apiClient = new SpeckleApiClient() { BaseUrl = serverAddress.ToString(), AuthToken = apiToken };
      LocalContext.Init();
    }

    public async Task<bool> GetStream(string streamId)
    {
      var queryString = "fields=streamId,baseProperties";
      try
      {
        var response = await apiClient.StreamGetAsync(streamId, queryString);

        if (response != null && response.Success.HasValue && response.Success.Value
          && response.Resource != null && response.Resource.StreamId.Equals(streamId, StringComparison.InvariantCultureIgnoreCase))
        {
          apiClient.Stream = response.Resource;
          apiClient.StreamId = StreamId;  //It is a bit strange that there's another streamId here; I think it's necessary to be set here for a server API call later
          return true;
        }
      }
      catch (SpeckleException se)
      {
        if (messenger != null)
        {
          messenger.Message(MessageIntent.Display, MessageLevel.Error, "Unable to access stream list information from the server");
          var context = new List<string>() { "Unable to access stream list information from the server",
            "StatusCode=" + se.StatusCode, "ResponseData=" + se.Response, "Message=" + se.Message,
            "Endpoint=StreamsGetAllAsync", "QueryString=\"" + queryString + "\"" };
          if (se is SpeckleException<ResponseBase> && ((SpeckleException<ResponseBase>)se).Result != null)
          {
            var responseJson = ((SpeckleException<ResponseBase>)se).Result.ToJson();
            context.Add("ResponseJson=" + responseJson);
          }
          messenger.Message(MessageIntent.TechnicalLog, MessageLevel.Error, se, context.ToArray());
        }
      }
      catch (Exception ex)
      {
        messenger.Message(MessageIntent.Display, MessageLevel.Error, "Unable to access stream information for " + streamId);
        messenger.Message(MessageIntent.TechnicalLog, MessageLevel.Error, ex, "Unable to access stream information",
          "BaseUrl=" + apiClient.BaseUrl, "StreamId" + streamId);
      }
      return false;
    }

    protected bool tryCatchWithEvents(Action action, string msgSuccessful, string msgFailure)
    {
      bool success = false;
      try
      {
        action();
        success = true;
      }
      catch (Exception ex)
      {
        if (!string.IsNullOrEmpty(msgFailure))
        {
          messenger.Message(MessageIntent.Display, MessageLevel.Error, msgFailure, this.verboseDisplayLog ? ex.Message : null);
          messenger.Message(MessageIntent.TechnicalLog, MessageLevel.Error, ex, msgFailure);
        }
      }
      if (success)
      {
        if (!string.IsNullOrEmpty(msgSuccessful))
        {
          messenger.Message(MessageIntent.Display, MessageLevel.Information, msgSuccessful);
        }
      }
      return success;
    }

    protected Dictionary<string, object> CreateBaseProperties(BasePropertyUnits units, double tolerance, double angleTolerance)
    {
      var unitsMap = new Dictionary<BasePropertyUnits, string>()
      {
        { BasePropertyUnits.Centimetres, "Centimeters" },
        { BasePropertyUnits.Meters, "Meters" },
        { BasePropertyUnits.Millimetres, "Millimeters" },
        { BasePropertyUnits.Feet, "Feet" },
        { BasePropertyUnits.Inches, "Inches" }
      };

      return new Dictionary<string, object>()
      {
        { "units", unitsMap[units] },
        { "tolerance", tolerance },
        { "angleTolerance", angleTolerance }
      };
    }

    protected void ConnectWebSocket()
    {
      tryCatchWithEvents(() =>
      {
        apiClient.SetupWebsocket();
      }, "", "Unable to set up web socket");

      tryCatchWithEvents(() =>
      {
        apiClient.JoinRoom("stream", apiClient.StreamId);
      }, "", "Unable to join web socket");
    }

    protected void DisconnectWebSocket()
    {
      tryCatchWithEvents(() =>
      {
        apiClient.LeaveRoom("stream", apiClient.Stream.StreamId);
      }, "", "Unable to leave web socket");
    }
  }
}
