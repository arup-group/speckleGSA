using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SpeckleCore;

namespace SpeckleGSA
{
    public class SpeckleGSASender
    {
        const double MAX_BUCKET_SIZE = 5e5;

        public string StreamName { get => mySender == null ? null : mySender.Stream.Name; }
        public string StreamID { get => mySender == null ? null : mySender.StreamId; }

        private SpeckleApiClient mySender;
        private string apiToken { get; set; }
        
        public SpeckleGSASender(string serverAddress, string apiToken)
        {
            this.apiToken = apiToken;

            mySender = new SpeckleApiClient() { BaseUrl = serverAddress.ToString() };
        }

        public async Task InitializeSender()
        {
            await mySender.IntializeSender(apiToken, "GSA", "GSA", "none");
        }

        public void UpdateName(string streamName)
        {
            mySender.Stream.Name = streamName;
        }

        public void SendGSAObjects(Dictionary<string, List<object>> payloadObjects)
        {
            ConverterHack hack = new ConverterHack();

            // Convert and set up layers
            List<SpeckleObject> bucketObjects = new List<SpeckleObject>();
            List<Layer> layers = new List<Layer>();

            int objectCounter = 0;
            int orderIndex = 0;
            foreach (KeyValuePair<string, List<object>> kvp in payloadObjects)
            {
                if (kvp.Value.Count() == 0)
                    continue;

                List<SpeckleObject> convertedObjects = SpeckleCore.Converter.Serialise(kvp.Value).Where(o => o != null).ToList();

                layers.Add(new Layer()
                {
                    Name = kvp.Key,
                    Guid = Guid.NewGuid().ToString(),
                    ObjectCount = convertedObjects.Count,
                    StartIndex = objectCounter,
                    OrderIndex = orderIndex++,
                    Topology = ""
                });

                bucketObjects.AddRange(convertedObjects);
                objectCounter += convertedObjects.Count;
            }

            Status.AddMessage("Succesfully converted: " + bucketObjects.Count() + " objects.");

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
                    try
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
                    catch
                    {
                        Status.AddError("Failed to send payload.");
                    }
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

            try
            { 
                var response = mySender.StreamUpdateAsync(mySender.Stream.StreamId, updateStream).Result;
                mySender.Stream.Layers = updateStream.Layers.ToList();
                mySender.Stream.Objects = placeholders;
                
                Status.AddMessage("Succesfully sent " + mySender.Stream.Name + " stream with " + updateStream.Objects.Count() + " objects.");
            }
            catch
            {
                Status.AddError("Failed to send " + mySender.Stream.Name + " stream.");
            }
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
                    Status.AddMessage("Reached payload limit. Making a new one, current  #: " + objectUpdatePayloads.Count);
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
