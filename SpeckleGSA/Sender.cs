using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SpeckleCore;
using Interop.Gsa_9_0;

namespace SpeckleGSA
{
    public class Sender
    {
        private SpeckleApiClient mySender;
        private List<object> BucketObjects;

        private string apiToken { get; set; }
        private string serverAddress { get; set; }

        public string StreamID { get => mySender.StreamId; }

        public Sender(string serverAddress, string apiToken)
        {
            this.apiToken = apiToken;

            mySender = new SpeckleApiClient() { BaseUrl = serverAddress.ToString() };
        }

        public async Task InitializeSender()
        {
            await mySender.IntializeSender(apiToken, "GSA", "GSA", "none");
        }

        public void UpdateData(string name, GSAController gsa)
        {
            BucketObjects = gsa.ExportObjects();

            // Serialize objects to send
            var convertedObjects = Converter.Serialise(BucketObjects).ToList();
            
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

            List<ResponseObject> responses = new List<ResponseObject>();
            foreach (var payload in objectUpdatePayloads)
            {
                responses.Add(mySender.ObjectCreateAsync(payload).GetAwaiter().GetResult());
            }

            // create placeholders for stream update payload
            List<SpeckleObject> placeholders = new List<SpeckleObject>();
            foreach (var myResponse in responses)
                foreach (var obj in myResponse.Resources) placeholders.Add(new SpecklePlaceholder() { _id = obj._id });

            List<Layer> BucketLayers = new List<Layer>
            {
                new Layer(
                    "all",
                    Guid.NewGuid().ToString(),
                    "",
                    BucketObjects.Count,
                    0,
                    BucketObjects.Count-1)
            };

            SpeckleStream updateStream = new SpeckleStream()
            {
                Layers = BucketLayers,
                Name = name,
                Objects = placeholders
            };

            var response = mySender.StreamUpdateAsync(mySender.StreamId, updateStream).Result;
        }
    }
}
