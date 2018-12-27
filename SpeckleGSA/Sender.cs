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
        const double MAX_BUCKET_SIZE = 5e5;

        private SpeckleApiClient mySender;

        private string apiToken { get; set; }
        private string serverAddress { get; set; }

        public string StreamID { get => mySender == null ? null : mySender.StreamId; }

        public Sender(string serverAddress, string apiToken)
        {
            this.apiToken = apiToken;

            mySender = new SpeckleApiClient() { BaseUrl = serverAddress.ToString() };

        }

        public async Task InitializeSender(string streamName)
        {
            await mySender.IntializeSender(apiToken, "GSA", "GSA", "none");
            mySender.Stream.Name = streamName;
        }

        public void SendGSAObjects(Dictionary<string, List<object>> payloadObjects)
        {
            ConverterHack n = new ConverterHack();

            // Convert and set up layers
            List<SpeckleObject> bucketObjects = new List<SpeckleObject>();
            List<Layer> layers = new List<Layer>();

            int objectCounter = 0;
            int orderIndex = 0;
            foreach (KeyValuePair<string, List<object>> kvp in payloadObjects)
            {
                List <SpeckleObject> convertedObjects = SpeckleCore.Converter.Serialise(kvp.Value);

                if (kvp.Value.Count == 0)
                    continue;

                layers.Add(new Layer()
                {
                    Name = kvp.Key,
                    Guid = kvp.Key,
                    ObjectCount = convertedObjects.Count,
                    StartIndex = objectCounter,
                    OrderIndex = orderIndex++,
                    Properties = new LayerProperties()
                    {
                        Color = new SpeckleBaseColor()
                    }
                });

                bucketObjects.AddRange(convertedObjects);
                objectCounter += convertedObjects.Count;
            }

            // Prune objects with placeholders using local DB
            LocalContext.PruneExistingObjects(bucketObjects, mySender.BaseUrl);
            
            // Store IDs of objects to add to stream
            List<string> objectsInStream = new List<string>();
            
            // Seperate objects into sizeable payloads
            List<List<SpeckleObject>> payloads = CreatePayloads(bucketObjects);

            if (bucketObjects.Count(o => o.Type == SpeckleObjectType.Placeholder) < bucketObjects.Count)
            {
                // Send objects which are in payload and add to local DB with updated IDs
                foreach (List<SpeckleObject> payload in payloads)
                {
                    ResponseObject res = mySender.ObjectCreateAsync(payload).Result;

                    for (int i = 0; i < payload.Count(); i++)
                    {
                        payload[i]._id = res.Resources[i]._id;
                        objectsInStream.Add(payload[i]._id);
                    }

                    Task.Run(() =>
                    {
                        foreach (SpeckleObject obj in payload)
                            LocalContext.AddSentObject(obj, mySender.BaseUrl);
                    });
                }
            }
            else
                objectsInStream = bucketObjects.Select(o => o._id).ToList();

            // Update stream with payload
            List<SpeckleObject> placeholders = new List<SpeckleObject>();

            foreach (string id in objectsInStream)
                placeholders.Add(new SpecklePlaceholder() { _id = id });

            SpeckleStream updateStream = new SpeckleStream();
            updateStream.Layers = layers;
            updateStream.Objects = placeholders;
            updateStream.Name = mySender.Stream.Name;

            var response = mySender.StreamUpdateAsync(mySender.Stream.StreamId, updateStream).Result;

            mySender.Stream.Layers = updateStream.Layers.ToList();
            mySender.Stream.Objects = placeholders;
        }

        public List<List<SpeckleObject>> CreatePayloads(List<SpeckleObject> bucketObjects)
        {
            // Seperate objects into sizable payloads
            long totalBucketSize = 0;
            long currentBucketSize = 0;
            List<List<SpeckleObject>> objectUpdatePayloads = new List<List<SpeckleObject>>();
            List<SpeckleObject> currentBucketObjects = new List<SpeckleObject>();
            List<SpeckleObject> allObjects = new List<SpeckleObject>();

            foreach (SpeckleObject obj in bucketObjects)
            {
                long size = Converter.getBytes(obj).Length;
                currentBucketSize += size;
                totalBucketSize += size;
                currentBucketObjects.Add(obj);

                if (currentBucketSize > MAX_BUCKET_SIZE)
                {
                    Console.WriteLine("Reached payload limit. Making a new one, current  #: " + objectUpdatePayloads.Count);
                    objectUpdatePayloads.Add(currentBucketObjects);
                    currentBucketObjects = new List<SpeckleObject>();
                    currentBucketSize = 0;
                }
            }

            // add in the last bucket 
            if (currentBucketObjects.Count > 0)
                objectUpdatePayloads.Add(currentBucketObjects);

            return objectUpdatePayloads;
        }
    }
}
