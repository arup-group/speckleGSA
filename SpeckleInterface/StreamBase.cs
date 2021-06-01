using SpeckleCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SpeckleInterface
{
  public abstract class StreamBase
  {
    protected readonly ISpeckleAppMessenger messenger;

    public string StreamId { get => apiClient?.StreamId; }
    public string StreamName => apiClient?.Stream.Name;
    public string ClientId => apiClient?.ClientId;

    protected readonly SpeckleApiClient apiClient;
    protected readonly string apiToken;
    protected readonly bool verboseDisplayLog;

    public StreamBase(string serverAddress, string apiToken, ISpeckleAppMessenger messenger, bool verboseDisplayLog = false)
    {
      this.messenger = messenger;
      this.apiToken = apiToken;
      this.verboseDisplayLog = verboseDisplayLog;

      apiClient = new SpeckleApiClient() { BaseUrl = serverAddress.ToString() };
      LocalContext.Init();
    }

    public async Task<StreamBasicData> GetStream(string streamId)
    {
      try
      {
        var response = await apiClient.StreamGetAsync(streamId, "fields=streamId,name");

        if (response != null && response.Success.HasValue && response.Success.Value
          && response.Resource != null && response.Resource.StreamId.Equals(streamId, StringComparison.InvariantCultureIgnoreCase))
        {
          return new StreamBasicData(response.Resource.StreamId, response.Resource.Name, response.Resource.Owner);
        }
      }
      catch (Exception ex)
      {
        messenger.Message(MessageIntent.Display, MessageLevel.Error, "Unable to access stream information for " + streamId);
        messenger.Message(MessageIntent.TechnicalLog, MessageLevel.Error, ex, "Unable to access stream information",
          "BaseUrl=" + apiClient.BaseUrl, "StreamId" + streamId);
      }
      return null;
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
  }
}
