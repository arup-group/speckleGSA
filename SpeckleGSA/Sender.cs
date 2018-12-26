using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using SpeckleCore;

namespace SpeckleGSA
{
    public class Sender
    {
        private SpeckleApiClient mySender;

        private string apiToken { get; set; }
        private string serverAddress { get; set; }

        public string StreamID { get => mySender == null ? null : mySender.StreamId; }

        public Sender(string serverAddress, string apiToken)
        {
            this.apiToken = apiToken;

            mySender = new SpeckleApiClient() { BaseUrl = serverAddress.ToString() };

        }

        public async Task InitializeSender()
        {
            string res = await mySender.IntializeSender(apiToken, "GSA", "GSA", "none");
            Console.WriteLine(res);
        }

        public async Task<Dictionary<string,string[]>> UpdateDataAsync(string streamName, Dictionary<string, List<object>> bucketObjects)
        {
            Dictionary<string, string[]> objectIDs = new Dictionary<string, string[]>();

            List<Layer> bucketLayers = new List<Layer>();
            List<SpeckleObject> payload = new List<SpeckleObject>();

            int objectCount = 0;

            foreach (KeyValuePair<string, List<object>> layer in bucketObjects)
            {
                await GetPayloadAsync(layer.Value).ContinueWith(res =>
                {
                    bucketLayers.Add(new Layer
                    (
                        layer.Key,
                        Guid.NewGuid().ToString(),
                        "",
                        res.Result.Count(),
                        objectCount,
                        0
                    ));

                    objectCount += res.Result.Count();

                    payload.AddRange(res.Result);

                    objectIDs[layer.Key] = payload.Select(p => p._id).ToArray();
                });
            }

            SpeckleStream updateStream = new SpeckleStream()
            {
                Layers = bucketLayers,
                Name = streamName,
                Objects = payload,
            };

            var response = mySender.StreamUpdateAsync(mySender.StreamId, updateStream).Result;
            if (response.Success == false)
                throw new Exception(response.Message);

            return objectIDs;
        }

        public async Task<List<SpeckleObject>> GetPayloadAsync(List<object> bucketObjects)
        {
            ConverterHack n = new ConverterHack();

            // Serialize objects to send
            var convertedObjects = Converter.Serialise(bucketObjects).ToList();

            // Seperate objects into sizable payloads
            long totalBucketSize = 0;
            long currentBucketSize = 0;
            List<List<SpeckleObject>> objectUpdatePayloads = new List<List<SpeckleObject>>();
            List<SpeckleObject> currentBucketObjects = new List<SpeckleObject>();
            List<SpeckleObject> allObjects = new List<SpeckleObject>();

            foreach (SpeckleObject convertedObject in convertedObjects)
            {
                long size = Converter.getBytes(convertedObject).Length;
                currentBucketSize += size;
                totalBucketSize += size;
                currentBucketObjects.Add(convertedObject);

                if (currentBucketSize > 5e5) // restrict max to ~500kb; should it be user config? anyway these functions should go into core. at one point. 
                {
                    Console.WriteLine("Reached payload limit. Making a new one, current  #: " + objectUpdatePayloads.Count);
                    objectUpdatePayloads.Add(currentBucketObjects);
                    currentBucketObjects = new List<SpeckleObject>();
                    currentBucketSize = 0;
                }
            }

            // add  the last bucket 
            if (currentBucketObjects.Count > 0)
                objectUpdatePayloads.Add(currentBucketObjects);

            // create placeholders for stream update payload
            List<SpeckleObject> placeholders = new List<SpeckleObject>();

            // create and update
            List<ResponseObject> responses = new List<ResponseObject>();
            foreach (var payload in objectUpdatePayloads)
                await mySender.ObjectCreateAsync(payload).ContinueWith(res =>
                    responses.Add(res.Result));

            foreach (var myResponse in responses)
                foreach (var obj in myResponse.Resources)
                    placeholders.Add(new SpecklePlaceholder() { _id = obj._id });

            return placeholders;
        }
    }
}
