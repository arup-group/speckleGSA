using SpeckleCore;
using System;

namespace SpeckleInterface
{
  public abstract class StreamBase
  {
    protected readonly ISpeckleAppMessenger messenger;

    public string StreamId => apiClient?.StreamId;
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
