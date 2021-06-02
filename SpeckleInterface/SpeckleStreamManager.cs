﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SpeckleCore;


namespace SpeckleInterface
{
  /// <summary>
  /// Performs operations on Speckle streams.
  /// </summary>
  public static class SpeckleStreamManager
  {

    /// <summary>
    /// Returns streams associated with account.
    /// </summary>
    /// <param name="restApi">Server address</param>
    /// <param name="apiToken">API token for account</param>
    /// <returns>List of tuple containing the name and the streamID of each stream</returns>
    public static async Task<List<SpeckleStream>> GetStreams(string restApi, string apiToken, ISpeckleAppMessenger messenger)
    {
      SpeckleApiClient myClient = new SpeckleApiClient() { BaseUrl = restApi, AuthToken = apiToken };

      try
      {
        ResponseStream response = await myClient.StreamsGetAllAsync("limit=500&sort=-updatedAt&parent=&fields=name,streamId,parent");

        List<SpeckleStream> ret = new List<SpeckleStream>();

        foreach (SpeckleStream s in response.Resources.Where(r => r.Parent == null))
        {
          ret.Add(s);
        }

        return ret;
      }
      catch (Exception ex)
      {
        if (messenger != null)
        {
          messenger.Message(MessageIntent.Display, MessageLevel.Error, "Unable to access stream list information from the server");
          messenger.Message(MessageIntent.TechnicalLog, MessageLevel.Error, ex, "Unable to access stream list information",
            "BaseUrl=" + restApi);
        }
      }
      return null;
    }

    public static async Task<SpeckleStream> GetStream(string restApi, string apiToken, string streamId, ISpeckleAppMessenger messenger)
    {
      SpeckleApiClient myClient = new SpeckleApiClient() { BaseUrl = restApi, AuthToken = apiToken };

      try
      {
        var response = await myClient.StreamGetAsync(streamId, "fields=streamId,name");

        if (response != null && response.Success.HasValue && response.Success.Value
          && response.Resource != null && response.Resource.StreamId.Equals(streamId, StringComparison.InvariantCultureIgnoreCase))
        {
          return response.Resource;
        }
      }
      catch (Exception ex)
      {
        if (messenger != null)
        {
          messenger.Message(MessageIntent.Display, MessageLevel.Error, "Unable to access stream information for " + streamId);
          messenger.Message(MessageIntent.TechnicalLog, MessageLevel.Error, ex, "Unable to access stream information",
            "BaseUrl=" + restApi, "StreamId" + streamId);
          return null;
        }
      }
      return null;
    }

    public static async Task<string> GetClientName(string restApi, string apiToken, ISpeckleAppMessenger messenger)
    {
      SpeckleApiClient myClient = new SpeckleApiClient() { BaseUrl = restApi, AuthToken = apiToken };

      ResponseUser user = null;
      try
      {
        user = await myClient.UserGetAsync();
      }
      catch (Exception ex)
      {
        if (messenger != null)
        {
          messenger.Message(MessageIntent.Display, MessageLevel.Error, "Unable to get user's name from server");
          messenger.Message(MessageIntent.TechnicalLog, MessageLevel.Error, ex, "Unable to rename stream", "BaseUrl=" + restApi);
          return "";
        }
      }
      if (user.Resource != null)
      {
        return string.Join(" ", user.Resource.Name, user.Resource.Surname);
      }
      return "";
    }

    /// <summary>
    /// Clones the stream.
    /// </summary>
    /// <param name="restApi">Server address</param>
    /// <param name="apiToken">API token for account</param>
    /// <param name="streamId">Stream ID of stream to clone</param>
    /// <returns>Stream ID of the clone</returns>
    public static async Task<string> CloneStream(string restApi, string apiToken, string streamId, ISpeckleAppMessenger messenger)
    {
      SpeckleApiClient myClient = new SpeckleApiClient() { BaseUrl = restApi, AuthToken = apiToken };

      try
      {
        ResponseStreamClone response = await myClient.StreamCloneAsync(streamId);
        return response.Clone.StreamId;
      }
      catch (Exception ex)
      {
        if (messenger != null)
        {
          messenger.Message(MessageIntent.Display, MessageLevel.Error, "Unable to clone stream name for " + streamId);
          messenger.Message(MessageIntent.TechnicalLog, MessageLevel.Error, ex, "Unable to rename stream", "StreamId=" + streamId, "BaseUrl=" + restApi);
        }
      }
      return "";
    }

    public static async Task<bool> UpdateStreamName(string restApi, string apiToken, string streamId, string streamName, ISpeckleAppMessenger messenger)
    {
      SpeckleApiClient myClient = new SpeckleApiClient() { BaseUrl = restApi, AuthToken = apiToken };

      try
      {
        var response = await myClient.StreamUpdateAsync(streamId, new SpeckleStream() { Name = streamName });
        return (response.Success.HasValue && response.Success.Value);
      }
      catch (Exception ex)
      {
        if (messenger != null)
        {
          messenger.Message(MessageIntent.Display, MessageLevel.Error, "Unable to update stream name for " + streamId);
          messenger.Message(MessageIntent.TechnicalLog, MessageLevel.Error, ex, "Unable to rename stream", "StreamId=" + streamId, "BaseUrl=" + restApi, "StreamName=" + streamName);
        }
        return false;
      }
    }
  }
}

