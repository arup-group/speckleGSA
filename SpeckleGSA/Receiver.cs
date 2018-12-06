using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SpeckleCore;

namespace SpeckleGSA
{
    public class Receiver
    {
        private SpeckleApiClient myReceiver;
        private List<object> ConvertedObjects;
        private List<SpeckleObject> SpeckleObjects;
        private Dictionary<string, SpeckleObject> ObjectCache = new Dictionary<string, SpeckleObject>();

        private string apiToken { get; set; }
        private string serverAddress { get; set; }

        public string StreamID { get => myReceiver == null? null : myReceiver.StreamId; }
        public string StreamName { get => myReceiver == null ? null : myReceiver.Stream.Name; }

        public Receiver(string serverAddress, string apiToken)
        {
            this.apiToken = apiToken;

            myReceiver = new SpeckleApiClient() { BaseUrl = serverAddress.ToString() };
        }

        public async Task InitializeReceiver(string streamID)
        {
            await myReceiver.IntializeReceiver(streamID, "GSA", "GSA", "none", apiToken);
            ObjectCache = new Dictionary<string, SpeckleObject>();
            SpeckleObjects = new List<SpeckleObject>();
            ConvertedObjects = new List<object>();
        }

        public async Task<List<object>> UpdateDataAsync()
        {
            List<object> convertedObjects = new List<object>();

            ConverterHack n = new ConverterHack();

            var getStream = await myReceiver.StreamGetAsync(myReceiver.StreamId, null);

            var payload = getStream.Resource.Objects.Select(obj => obj._id).ToArray();

            await myReceiver.ObjectGetBulkAsync(payload, "omit=displayValue").ContinueWith(res=>
            {
                // Add objects to cache
                foreach (var x in res.Result.Resources)
                    ObjectCache[x._id] = x;

                // Get real objects
                SpeckleObjects.Clear();
                foreach (var obj in getStream.Resource.Objects)
                    SpeckleObjects.Add(ObjectCache[obj._id]);

                // Convert
                convertedObjects = SpeckleCore.Converter.Deserialise(SpeckleObjects);
            });

            return convertedObjects;
        }

        public List<object> UpdateData()
        {
            ConverterHack n = new ConverterHack();

            var getStream = myReceiver.StreamGetAsync(myReceiver.StreamId, null);
            getStream.Wait();

            var payload = getStream.Result.Resource.Objects.Select(obj => obj._id).ToArray();

            ResponseObject response = myReceiver.ObjectGetBulkAsync(payload, "omit=displayValue").GetAwaiter().GetResult();

            // Add objects to cache
            foreach (var x in response.Resources)
                ObjectCache[x._id] = x;

            // Get real objects
            SpeckleObjects.Clear();
            foreach (var obj in getStream.Result.Resource.Objects)
                SpeckleObjects.Add(ObjectCache[obj._id]);

            // Convert
            ConvertedObjects = SpeckleCore.Converter.Deserialise(SpeckleObjects);

            return ConvertedObjects;
        }
    }
}
