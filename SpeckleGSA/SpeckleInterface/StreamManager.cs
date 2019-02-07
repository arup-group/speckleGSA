using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpeckleCore;


namespace SpeckleGSA
{
    public class StreamManager
    {
        private SpeckleApiClient myClient;

        public StreamManager(string serverAddress, string apiToken)
        {
            myClient = new SpeckleApiClient() { BaseUrl = serverAddress.ToString(), AuthToken = apiToken };
        }

        public async Task<List<Tuple<string, string>>> GetStreams()
        {
            ResponseStream response = await myClient.StreamsGetAllAsync("fields=name,streamId");

            List<Tuple<string, string>> ret = new List<Tuple<string, string>>();

            foreach (SpeckleStream s in response.Resources)
                ret.Add(new Tuple<string, string>(s.Name, s.StreamId));

            return ret;
        }

        public async Task<string> CloneStream(string streamID)
        {
            ResponseStreamClone response = await myClient.StreamCloneAsync(streamID);

            return response.Clone.StreamId;
        }
    }

}
