﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpeckleCore;


namespace SpeckleInterface
{
  public class StreamBasicData
  {
    public string Name { get; set; }
    public string StreamId { get; set; }
    public string ClientId { get; set; }

    public StreamBasicData(string streamId, string name, string clientId)
    {
      this.Name = name;
      this.StreamId = streamId;
      this.ClientId = clientId;
    }
  }

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
    public static async Task<List<StreamBasicData>> GetStreams(string restApi, string apiToken)
    {
      SpeckleApiClient myClient = new SpeckleApiClient() { BaseUrl = restApi, AuthToken = apiToken };

      ResponseStream response = await myClient.StreamsGetAllAsync("limit=500&sort=-updatedAt&parent=&fields=name,streamId,parent");
      
      List<StreamBasicData> ret = new List<StreamBasicData>();

      foreach (SpeckleStream s in response.Resources.Where(r => r.Parent == null))
      {
        ret.Add(new StreamBasicData(s.StreamId, s.Name, s.Owner));
      }

      return ret;
    }

    public static async Task<StreamBasicData> GetStream(string restApi, string apiToken, string streamId)
    {
      SpeckleApiClient myClient = new SpeckleApiClient() { BaseUrl = restApi, AuthToken = apiToken };

      var response = await myClient.StreamGetAsync(streamId, "fields=streamId,name");

      if (response != null && response.Success.HasValue && response.Success.Value
        && response.Resource != null && response.Resource.StreamId.Equals(streamId, StringComparison.InvariantCultureIgnoreCase))
      {
        return new StreamBasicData(response.Resource.StreamId, response.Resource.Name, response.Resource.Owner);
      }
      return null;
    }

    /*
    public static async Task<bool> CheckStreamOnServer(string restApi, string apiToken, string streamId)
    {
      SpeckleApiClient myClient = new SpeckleApiClient() { BaseUrl = restApi, AuthToken = apiToken };

      var response = await myClient.StreamGetAsync(streamId, "fields=streamId");
      if (response != null && response.Success.HasValue && response.Success.Value
        && response.Resource != null && response.Resource.StreamId.Equals(streamId, StringComparison.InvariantCultureIgnoreCase))
      {
        return true;
      }
      return false;
    }

    public static async Task<string> GetStreamName(string restApi, string apiToken, string streamId)
    {
      SpeckleApiClient myClient = new SpeckleApiClient() { BaseUrl = restApi, AuthToken = apiToken };

      var response = await myClient.StreamGetAsync(streamId, "fields=name");
      if (response != null && response.Success.HasValue && response.Success.Value
        && response.Resource != null && response.Resource.StreamId.Equals(streamId, StringComparison.InvariantCultureIgnoreCase))
      {
        return response.Resource.Name;
      }
      return null;
    }
    */

    public static async Task<string> GetClientName(string restApi, string apiToken)
    {
      SpeckleApiClient myClient = new SpeckleApiClient() { BaseUrl = restApi, AuthToken = apiToken };

      var user = await myClient.UserGetAsync();
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
    /// <param name="streamID">Stream ID of stream to clone</param>
    /// <returns>Stream ID of the clone</returns>
    public static async Task<string> CloneStream(string restApi, string apiToken, string streamID)
    {
      SpeckleApiClient myClient = new SpeckleApiClient() { BaseUrl = restApi, AuthToken = apiToken };

      try
      {
        ResponseStreamClone response = await myClient.StreamCloneAsync(streamID);
        return response.Clone.StreamId;
      }
      catch (Exception ex)
      {
        return "";
      }
    }

    public static async Task<bool> UpdateStreamName(string restApi, string apiToken, string streamId, string streamName)
    {
      SpeckleApiClient myClient = new SpeckleApiClient() { BaseUrl = restApi, AuthToken = apiToken };

      try
      {
        var response = await myClient.StreamUpdateAsync(streamId, new SpeckleStream() { Name = streamName });
        return (response.Success.HasValue && response.Success.Value);
      }
      catch (Exception ex)
      {
        return false;
      }
    }
  }

}
