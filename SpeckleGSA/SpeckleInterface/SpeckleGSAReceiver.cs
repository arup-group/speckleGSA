﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using SpeckleCore;
using SpeckleStructuresClasses;

namespace SpeckleGSA
{
    /// <summary>
    /// Receive objects from a stream.
    /// </summary>
    public class SpeckleGSAReceiver
    {
        const int MAX_OBJ_REQUEST_COUNT = 20;

        private SpeckleApiClient myReceiver;

        private string apiToken;
        private string serverAddress;

        public event EventHandler<EventArgs> UpdateGlobalTrigger;

        public string StreamID { get => myReceiver == null? null : myReceiver.StreamId; }
        public string StreamName { get => myReceiver == null ? null : myReceiver.Stream.Name; }
        public string Units { get => myReceiver == null ? null : myReceiver.Stream.BaseProperties["units"]; }

        /// <summary>
        /// Create SpeckleGSAReceiver object.
        /// </summary>
        /// <param name="serverAddress">Server address</param>
        /// <param name="apiToken">API token</param>
        public SpeckleGSAReceiver(string serverAddress, string apiToken)
        {
            this.apiToken = apiToken;

            myReceiver = new SpeckleApiClient() { BaseUrl = serverAddress.ToString() };

            SpeckleInitializer.Initialize();
            LocalContext.Init();
        }

        /// <summary>
        /// Initializes receiver.
        /// </summary>
        /// <param name="streamID">Stream ID of stream</param>
        /// <returns>Task</returns>
        public async Task InitializeReceiver(string streamID)
        {
            await myReceiver.IntializeReceiver(streamID, "GSA", "GSA", "none", apiToken);

            myReceiver.OnWsMessage += OnWsMessage;
        }
        
        /// <summary>
        /// Return a list of SpeckleObjects from the stream.
        /// </summary>
        /// <returns>List of SpeckleObjects</returns>
        public List<SpeckleObject> GetStructuralObjects()
        {
            UpdateGlobal();

            List<SpeckleObject> structuralObjects = myReceiver.Stream.Objects.Select(o => ConvertToStructural(o)).Where(o => o != null).ToList();
            //List<SpeckleObject> structuralObjects = myReceiver.Stream.Objects.Where(o => o is IStructural).ToList();
            //structuralObjects.AddRange(Converter.Deserialise(myReceiver.Stream.Objects.Where(o => !(o is IStructural))).Cast<SpeckleObject>());

            return structuralObjects;
        }

        /// <summary>
        /// Handles web-socket messages.
        /// </summary>
        /// <param name="source">Source</param>
        /// <param name="e">Event argument</param>
        public void OnWsMessage( object source, SpeckleEventArgs e)
        {
            if (e == null) return;
            if (e.EventObject == null) return;
            switch ((string)e.EventObject.args.eventType)
            {
                case "update-global":
                    UpdateGlobalTrigger(null, null);
                    break;
                case "update-children":
                    UpdateChildren();
                    break;
                default:
                    Status.AddError("Unknown event: " + (string)e.EventObject.args.eventType);
                    break;
            }
        }

        /// <summary>
        /// Update stream children.
        /// </summary>
        public void UpdateChildren()
        {
            var result = myReceiver.StreamGetAsync(myReceiver.StreamId, "fields=children").Result;
            myReceiver.Stream.Children = result.Resource.Children;
        }

        /// <summary>
        /// Force client to update to stream.
        /// </summary>
        public void UpdateGlobal()
        {
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
                try
                {
                    LocalContext.AddOrUpdateStream(myReceiver.Stream, myReceiver.BaseUrl);
                }
                catch { }
                
                // Get cached objects
                try
                {
                    LocalContext.GetCachedObjects(myReceiver.Stream.Objects, myReceiver.BaseUrl);
                }
                catch { }

                string[] payload = myReceiver.Stream.Objects.Where(o => o.Type == "Placeholder").Select(o => o._id).ToArray();

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
                        try
                        {
                            LocalContext.AddCachedObject(obj, myReceiver.BaseUrl);
                        }
                        catch { }
                    }
                });

                Status.AddMessage("Received " + myReceiver.Stream.Name + " stream with " + myReceiver.Stream.Objects.Count() + " objects.");
            }
        }

        /// <summary>
        /// Convert SpeckleObject to Structural objects. Returns null if unable to do so.
        /// </summary>
        /// <param name="inputObject">SpeckleObject to convert</param>
        /// <returns>Structural object</returns>
        public SpeckleObject ConvertToStructural(SpeckleObject inputObject)
        {
            if (typeof(IStructural).IsAssignableFrom(inputObject.GetType()))
                return inputObject;

            List<string> pieces = inputObject.Type.Split(new char[] { '/' }).ToList();

            while (pieces.Count() > 0)
            {
                string subType = string.Join("/", pieces);
                try
                {
                    Type candidateType = null;
                    foreach (Type t in SpeckleInitializer.GetTypes())
                    {
                        if (t.Assembly.FullName == typeof(IStructural).Assembly.FullName)
                        { 
                            SpeckleObject convertedObject = (SpeckleObject)Activator.CreateInstance(t);
                            if (convertedObject.Type.Contains(subType))
                            {
                                candidateType = t;
                                break;
                            }
                        }
                    }
                    
                    if (candidateType != null)
                    {
                        SpeckleObject convertedObject = (SpeckleObject)Activator.CreateInstance(candidateType);

                        foreach (PropertyInfo p in convertedObject.GetType().GetProperties().Where(p => p.CanWrite))
                        {
                            PropertyInfo inputProperty = inputObject.GetType().GetProperty(p.Name);
                            if (inputProperty != null)
                                p.SetValue(convertedObject, inputProperty.GetValue(inputObject));
                        }
                        convertedObject.GenerateHash();

                        return convertedObject;
                    }
                }
                catch { }

                pieces.RemoveAt(pieces.Count() - 1);
            }
            return null;
        }

        /// <summary>
        /// Dispose the receiver.
        /// </summary>
        public void Dispose()
        {
            myReceiver.Dispose(true);
        }
    }
}
