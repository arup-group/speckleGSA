using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    public static async Task<List<Tuple<string, string>>> GetStreams(string restApi, string apiToken)
    {
      SpeckleApiClient myClient = new SpeckleApiClient() { BaseUrl = restApi, AuthToken = apiToken };

      ResponseStream response = await myClient.StreamsGetAllAsync("fields=name,streamId");

      List<Tuple<string, string>> ret = new List<Tuple<string, string>>();

      foreach (SpeckleStream s in response.Resources)
        ret.Add(new Tuple<string, string>(s.Name, s.StreamId));

      return ret;
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

      ResponseStreamClone response = await myClient.StreamCloneAsync(streamID);

      return response.Clone.StreamId;
    }
  }

}
