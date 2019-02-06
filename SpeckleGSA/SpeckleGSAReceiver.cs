using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using SpeckleCore;
using SpeckleStructures;

namespace SpeckleGSA
{
    public class SpeckleGSAReceiver
    {
        const int MAX_OBJ_REQUEST_COUNT = 20;

        private SpeckleApiClient myReceiver;

        private string apiToken { get; set; }
        private string serverAddress { get; set; }

        public string StreamID { get => myReceiver == null? null : myReceiver.StreamId; }
        public string StreamName { get => myReceiver == null ? null : myReceiver.Stream.Name; }
        public string Units { get => myReceiver == null ? null : myReceiver.Stream.BaseProperties["units"]; }

        public SpeckleGSAReceiver(string serverAddress, string apiToken)
        {
            this.apiToken = apiToken;

            myReceiver = new SpeckleApiClient() { BaseUrl = serverAddress.ToString() };
        }

        public async Task InitializeReceiver(string streamID)
        {
            await myReceiver.IntializeReceiver(streamID, "GSA", "GSA", "none", apiToken);
        }
        
        public List<object> GetGSAObjects()
        {
            UpdateDataAsync();
            
            return Converter.Deserialise(myReceiver.Stream.Objects);
        }

        public void UpdateDataAsync()
        {
            GSAConverterHack hack1 = new GSAConverterHack();
            StructuresConverterHack hack2 = new StructuresConverterHack();

            // Try to get stream
            ResponseStream streamGetResult = myReceiver.StreamGetAsync(myReceiver.StreamId, null).Result;

            if (streamGetResult.Success == false)
            {
                Status.AddError("Failed to receive " + myReceiver.Stream.Name + "stream.");
            }
            else
            {
                myReceiver.Stream = streamGetResult.Resource;

                // Store stream data in local DB
                LocalContext.AddOrUpdateStream(myReceiver.Stream, myReceiver.BaseUrl);

                // Get cached objects
                LocalContext.GetCachedObjects(myReceiver.Stream.Objects, myReceiver.BaseUrl);

                string[] payload = myReceiver.Stream.Objects.Where(o => o.Type == SpeckleObjectType.Placeholder).Select(o => o._id).ToArray();

                List<SpeckleObject> receivedObjects = new List<SpeckleObject>();

                // Get remaining objects from server
                for (int i=0; i < payload.Length; i += MAX_OBJ_REQUEST_COUNT)
                {
                    string[] partialPayload = payload.Skip(i).Take(MAX_OBJ_REQUEST_COUNT).ToArray();

                    ResponseObject response = myReceiver.ObjectGetBulkAsync(partialPayload, "omit=displayValue").Result;

                    receivedObjects.AddRange(response.Resources);
                }

                foreach(SpeckleObject obj in receivedObjects)
                {
                    int streamLoc = myReceiver.Stream.Objects.FindIndex(o => o._id == obj._id);
                    try
                    {
                        myReceiver.Stream.Objects[streamLoc] = obj;
                    }
                    catch
                    { }
                }

                Task.Run( () =>
                {
                    foreach (SpeckleObject obj in receivedObjects)
                    {
                        LocalContext.AddCachedObject(obj, myReceiver.BaseUrl);
                    }
                });

                Status.AddMessage("Received " + myReceiver.Stream.Name + " stream with " + myReceiver.Stream.Objects.Count() + " objects.");
            }
        }
    }
}
