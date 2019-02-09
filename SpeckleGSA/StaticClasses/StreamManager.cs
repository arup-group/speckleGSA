using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpeckleCore;


namespace SpeckleGSA
{
    public static class StreamManager
    {
        public static async Task<List<Tuple<string, string>>> GetStreams(string restApi, string apiToken)
        {
            SpeckleApiClient myClient = new SpeckleApiClient() { BaseUrl = restApi, AuthToken = apiToken };

            ResponseStream response = await myClient.StreamsGetAllAsync("fields=name,streamId");

            List<Tuple<string, string>> ret = new List<Tuple<string, string>>();

            foreach (SpeckleStream s in response.Resources)
                ret.Add(new Tuple<string, string>(s.Name, s.StreamId));

            return ret;
        }

        public static async Task<string> CloneStream(string restApi, string apiToken, string streamID)
        {
            SpeckleApiClient myClient = new SpeckleApiClient() { BaseUrl = restApi, AuthToken = apiToken };

            ResponseStreamClone response = await myClient.StreamCloneAsync(streamID);

            return response.Clone.StreamId;
        }
    }

}
