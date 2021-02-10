﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using SpeckleCore;
using SpeckleGSAInterfaces;
using SpeckleGSAProxy;

namespace SpeckleGSA
{
	/// <summary>
	/// Responsible for reading and writing Speckle streams.
	/// </summary>
	public class Receiver : BaseReceiverSender
	{
    public Dictionary<string, ISpeckleGSAReceiver> Receivers = new Dictionary<string, ISpeckleGSAReceiver>();

    //These need to be accessed using a lock
    private object currentObjectsLock = new object();
    private object traversedSerialisedLock = new object();
    private object traversedDeserialisedLock = new object();
    //private readonly List<Tuple<string, SpeckleObject>> currentObjects = new List<Tuple<string, SpeckleObject>>();
    private readonly List<Type> traversedSerialisedTypes = new List<Type>();
    private readonly List<Type> traversedDeserialisedTypes = new List<Type>();

    public readonly Dictionary<Type, IGSASpeckleContainer> dummyObjectDict = new Dictionary<Type, IGSASpeckleContainer>();

    /// <summary>
    /// Initializes receiver.
    /// </summary>
    /// <param name="restApi">Server address</param>
    /// <param name="apiToken">API token of account</param>
    /// <returns>Task</returns>
    public async Task<List<string>> Initialize(string restApi, string apiToken)
		{
			var statusMessages = new List<string>();

			if (IsInit) return statusMessages;

      var startTime = DateTime.Now;
			if (!GSA.IsInit)
			{
        GSA.GsaApp.gsaMessenger.Message(MessageIntent.Display, MessageLevel.Error, "GSA link not found.");
				return statusMessages;
			}

      //ExecuteWithLock(ref currentObjectsLock, () => currentObjects.Clear());
      ExecuteWithLock(ref traversedSerialisedLock, () => traversedSerialisedTypes.Clear());
      ExecuteWithLock(ref traversedDeserialisedLock, () => traversedDeserialisedTypes.Clear());

      GSA.GsaApp.gsaMessenger.Message(MessageIntent.Display, MessageLevel.Information, "Initialising receivers");

      Status.ChangeStatus("Reading GSA data into cache");

      var numRowsupdated = await UpdateCache();

      // Create receivers
      Status.ChangeStatus("Accessing streams");

			var nonBlankReceivers = GSA.ReceiverInfo.Where(r => !string.IsNullOrEmpty(r.Item1)).ToList();

      await nonBlankReceivers.ForEachAsync(async (streamInfo) =>
			{
				await Receivers[streamInfo.Item1].InitializeReceiver(streamInfo.Item1, streamInfo.Item2);
				Receivers[streamInfo.Item1].UpdateGlobalTrigger += Trigger;
			}, Environment.ProcessorCount);

      TimeSpan duration = DateTime.Now - startTime;
      GSA.GsaApp.gsaMessenger.Message(MessageIntent.Display, MessageLevel.Information, "Duration of initialisation: " + duration.ToString(@"hh\:mm\:ss"));
      GSA.GsaApp.gsaMessenger.Message(MessageIntent.Telemetry, MessageLevel.Information, "receive", "initialisation", "duration", duration.ToString(@"hh\:mm\:ss"));

      Status.ChangeStatus("Ready to receive");
			IsInit = true;

			return statusMessages;
		}

    /// <summary>
    /// Trigger to update stream. Is called automatically when update-global ws message is received on stream.
    /// </summary>
    public void Trigger(object sender, EventArgs e)
    {
      if ((IsBusy) || (!IsInit)) return;

      IsBusy = true;

      var startTime = DateTime.Now;

      GSA.GsaApp.gsaSettings.Units = GSA.GsaApp.gsaProxy.GetUnits();

      ExecuteWithLock(ref traversedSerialisedLock, () => traversedSerialisedTypes.Clear());
      ExecuteWithLock(ref traversedDeserialisedLock, () => traversedDeserialisedTypes.Clear());

      GSA.SenderDictionaries.Clear();

      // Read objects
      Status.ChangeStatus("Receiving streams");

      var streamIds = Receivers.Keys.ToList();

      //This list will contain ALL speckle objects received across all streams
      var rxObjs = new List<SpeckleObject>();
      var units = GSA.GsaApp.Settings.Units;

      foreach (var streamId in Receivers.Keys)
      {
        double factor = 1;
        if (Receivers[streamId].Units == null)
        {
          //Let the user know if any streams have no unit information
          GSA.GsaApp.gsaMessenger.CacheMessage(MessageIntent.Display, MessageLevel.Error, "Streams with no unit information", streamId);
        }
        else
        {
          factor = (1.0).ConvertUnit(Receivers[streamId].Units.ShortUnitName(), units);
        }

        foreach (var o in Receivers[streamId].GetObjects())
        {
          if (string.IsNullOrEmpty(o.ApplicationId))
          {
            GSA.GsaApp.gsaMessenger.CacheMessage(MessageIntent.Display, MessageLevel.Information, o.GetType().Name + ((string.IsNullOrEmpty(o.Name))
              ? " with no name nor ApplicationId (identified by hashes)" : " with no name nor ApplicationId (identified by hashes)"), o.Hash);
          }
          else
          {
            o.Properties.Add("StreamId", streamId);

            try
            {
              o.Scale(factor);
            }
            catch (Exception ex)
            {
              GSA.GsaApp.gsaMessenger.CacheMessage(MessageIntent.Display, MessageLevel.Error, "Scaling issue for objects with _ids on stream: " + streamId, o._id);
              GSA.GsaApp.gsaMessenger.CacheMessage(MessageIntent.TechnicalLog, MessageLevel.Error, ex, "Scaling issue", "StreamId=" + streamId, "_id=" + o._id);
            }

            //Populate the cache with stream IDs - review if this is needed anymroe
            GSA.GsaApp.gsaCache.SetStream(o.ApplicationId, streamId);
            rxObjs.Add(o);
          }
        }
      }

      GSA.GsaApp.gsaMessenger.Trigger();

      TimeSpan duration = DateTime.Now - startTime;
      GSA.GsaApp.gsaMessenger.Message(MessageIntent.Display, MessageLevel.Information, "Duration of reception from Speckle and scaling: " + duration.ToString(@"hh\:mm\:ss"));
      GSA.GsaApp.gsaMessenger.Message(MessageIntent.Telemetry, MessageLevel.Information, "receive", "reception and scaling", "duration", duration.ToString(@"hh\:mm\:ss"));

      if (rxObjs.Count() == 0)
      {
        GSA.GsaApp.gsaMessenger.Message(MessageIntent.Display, MessageLevel.Information, "No processing needed because the stream(s) contain(s) no objects");
        Status.ChangeStatus("Finished receiving", 100);
        IsBusy = false;
        return;
      }

      startTime = DateTime.Now;
      streamIds.ForEach(s => GSA.GsaApp.gsaCache.Snapshot(s));

      ProcessRxObjects(rxObjs);

      var toBeAddedGwa = GSA.GsaApp.gsaCache.GetNewGwaSetCommands();
      toBeAddedGwa.ForEach(tba => GSA.GsaApp.gsaProxy.SetGwa(tba));

      var toBeDeletedGwa = GSA.GsaApp.gsaCache.GetExpiredData();

      var setDeletes = toBeDeletedGwa.Where(t => t.Item4 == GwaSetCommandType.Set).ToList();
      setDeletes.ForEach(sd => GSA.GsaApp.gsaProxy.DeleteGWA(sd.Item1, sd.Item2, GwaSetCommandType.Set));

      var setAtDeletes = toBeDeletedGwa.Where(t => t.Item4 == GwaSetCommandType.SetAt).OrderByDescending(t => t.Item2).ToList();
      setAtDeletes.ForEach(sad => GSA.GsaApp.gsaProxy.DeleteGWA(sad.Item1, sad.Item2, GwaSetCommandType.SetAt));

      GSA.GsaApp.gsaProxy.Sync();

      GSA.GsaApp.gsaProxy.UpdateCasesAndTasks();
      GSA.GsaApp.gsaProxy.UpdateViews();

      duration = DateTime.Now - startTime;
      GSA.GsaApp.gsaMessenger.Message(MessageIntent.Display, MessageLevel.Information, "Duration of conversion from Speckle: " + duration.ToString(@"hh\:mm\:ss"));
      GSA.GsaApp.gsaMessenger.Message(MessageIntent.Telemetry, MessageLevel.Information, "receive", "conversion", "duration", duration.ToString(@"hh\:mm\:ss"));
      startTime = DateTime.Now;

      Status.ChangeStatus("Finished receiving", 100);
      IsBusy = false;
    }

    private Task<int> UpdateCache()
    {
      var keywords = GSA.Keywords;
      GSA.GsaApp.gsaCache.Clear();
      var data = GSA.GsaApp.gsaProxy.GetGwaData(keywords, false);
      for (int i = 0; i < data.Count(); i++)
      {
        GSA.GsaApp.gsaCache.Upsert(
          data[i].Keyword,
          data[i].Index,
          data[i].GwaWithoutSet,
          streamId: data[i].StreamId,
          //This needs to be revised as this logic is in the kit too
          applicationId: (string.IsNullOrEmpty(data[i].ApplicationId))
            ? ("gsa/" + data[i].Keyword + "_" + data[i].Index.ToString())
            : data[i].ApplicationId,
          gwaSetCommandType: data[i].GwaSetType);
      }

      var numRowsupdated = data.Count();
      if (numRowsupdated > 0)
      {
        GSA.GsaApp.gsaMessenger.Message(MessageIntent.Display, MessageLevel.Information, 
          "Read " + numRowsupdated + " GWA lines across " + keywords.Count() + " keywords into cache");
      }

      return Task.FromResult(numRowsupdated);
    }

    private Task ProcessRxObjects(List<SpeckleObject> rxObjs)
    {
      // Write objects

      int numErrors = 0;

      var rxTypePrereqs = GSA.RxTypeDependencies;
      var rxSpeckleTypes = rxObjs.Select(k => k.GetType()).Distinct().ToList();

      //build up dictionary of old schema (IGSASpeckleContainer) types and dummy instances
      rxTypePrereqs.Keys.Where(t => !dummyObjectDict.ContainsKey(t)).ToList()
        .ForEach(t => dummyObjectDict[t] = (IGSASpeckleContainer)Activator.CreateInstance(t));

      var rxGsaContainerTypes = rxTypePrereqs.Keys.Where(t => rxSpeckleTypes.Any(st => st == dummyObjectDict[t].SpeckleObject.GetType())).ToList();
      DiscoverToNativeMethods(rxGsaContainerTypes);

      //These are IGSASpeckleContainer types
      List<Type> currentTypeBatch;
      do
      {
        //A batch is a group of groups of objects by type
        currentTypeBatch = new List<Type>();
        ExecuteWithLock(ref traversedDeserialisedLock, () =>
        {
          currentTypeBatch.AddRange(rxTypePrereqs.Where(i => i.Value.Count(x => !traversedDeserialisedTypes.Contains(x)) == 0).Select(i => i.Key));
          currentTypeBatch.RemoveAll(i => traversedDeserialisedTypes.Contains(i));
        });

        var batchErrors = ProcessTypeBatch(currentTypeBatch, rxObjs);
        numErrors += batchErrors;

      } while (currentTypeBatch.Count > 0);

      if (numErrors > 0)
      {
        GSA.GsaApp.Messenger.Message(MessageIntent.Display, MessageLevel.Error, 
          numErrors + " processing errors found. Refer to the .txt log file(s) in " + AppDomain.CurrentDomain.BaseDirectory);
      }

      return Task.CompletedTask;
    }

    //Trigger the discovery and assignment of ToNative() methods within the SpeckleCore Converter static object
    //in preparation for their parallel use below.  The methods are stored in a Dictionary object, which is thread-safe
    //for reading. Because the calls to Deserialise below (of dummy objects) will alter the Dictionary object, it must be
    //done in serial on the one thread
    private void DiscoverToNativeMethods(List<Type> candidateTypes)
    {
      object toNativeMethodDictLock = new object();

      Parallel.ForEach(candidateTypes, t =>
      //foreach (var t in currentBatch)
      {
        var valueType = ((SpeckleObject)dummyObjectDict[t].SpeckleObject).GetType().ToString();
        bool discovered = false;
        lock (toNativeMethodDictLock)
        {
          discovered = Converter.toNativeMethods.ContainsKey(valueType.ToString());
        }
        if (!discovered)
        {
          try
          {
            Converter.Deserialise((SpeckleObject)dummyObjectDict[t].SpeckleObject);
          }
          catch { }
        }
      }
      );
    }

    private int ProcessTypeBatch(List<Type> currentBatch, List<SpeckleObject> rxObjs)
    {
      var streamIds = Receivers.Keys.ToList();

      //This method assumes it's not run in parallel
      GSA.GsaApp.gsaMessenger.ResetTriggeredMessageCount();

      //Commented this out to enable debug tests for preserving order
#if DEBUG
      foreach (var t in currentBatch)
#else
      Parallel.ForEach(currentBatch, t =>
#endif
      {
        var dummyObject = dummyObjectDict[t];
        var valueType = t.GetProperty("Value").GetValue(dummyObject).GetType();

        var currentObjects = rxObjs.Where(o => o.GetType() == valueType).ToList();

        if (currentObjects.Count() > 0)
        {
          Status.ChangeStatus("Writing " + t.Name);

          var keyword = dummyObject.GetAttribute("GSAKeyword").ToString();

          var speckleTypeName = ((SpeckleObject)(dummyObject).SpeckleObject).Type;

          //First serialise all relevant objects into sending dictionary so that merging can happen
          var typeAppIds = currentObjects.Where(bo => bo.ApplicationId != null).Select(bo => bo.ApplicationId).ToList();
          if (typeAppIds.Any(i => GSA.GsaApp.gsaCache.ApplicationIdExists(keyword, i)))
          {
            //Serialise all objects of this type and update traversedSerialised list
            ExecuteWithLock(ref traversedSerialisedLock, () =>
            {
              if (!traversedSerialisedTypes.Contains(t))
              {
                SerialiseUpdateCacheForGSAType(keyword, t, dummyObject);
              }
            });
          }

          GSA.GsaApp.gsaMessenger.Trigger();
          
          if (GSA.RxParallelisableTypes.ContainsKey(valueType))
          {
            var numErrorLock = new object();

            keyword = GSA.RxParallelisableTypes[valueType];
            foreach (var co in currentObjects)
            {
              GSA.GsaApp.gsaCache.ReserveIndex(keyword, co.ApplicationId);
            }
            Parallel.ForEach(currentObjects, o =>
            {
              MergeAndDeserialseObject(o, speckleTypeName, keyword, t);
            });
          }
          else
          {
            currentObjects.ForEach(o =>
            {
              MergeAndDeserialseObject(o, speckleTypeName, keyword, t);
            });
          }

          //Process any cached messages from the conversion code - should be mostly technical log but may include some display messages
          GSA.GsaApp.gsaMessenger.Trigger();
        }

        ExecuteWithLock(ref traversedDeserialisedLock, () => traversedDeserialisedTypes.Add(t));
      }
#if !DEBUG
      );
#endif

      return GSA.GsaApp.gsaMessenger.TriggeredMessageCount;
    }

    //Return number of errors
    private void MergeAndDeserialseObject(SpeckleObject targetObject, string speckleTypeName, string keyword, Type t)
    {
      if (targetObject == null)
      {
        return;
      }

      var streamId = "";
      try
      {
        streamId = targetObject.Properties["StreamId"].ToString();
      }
      catch { }

      var existingList = GSA.GsaApp.gsaCache.GetSpeckleObjects(speckleTypeName, targetObject.ApplicationId, streamId: streamId);

      //In case of error
      var errContext = new List<string>() { "StreamId=" + streamId, "SpeckleType=" + speckleTypeName,
        "ApplicationId=" + (string.IsNullOrEmpty(targetObject.ApplicationId) ? "" : targetObject.ApplicationId),
        "_id=" + targetObject._id,
        "Url=" + GSA.GsaApp.Settings.ObjectUrl(targetObject._id) };

      if (existingList == null || existingList.Count() == 0)
      {
        //Either this is the first reception event, or it's not in the cache for another reason, like:
        //The ToSpeckle for this Application ID didn't work (a notable example is ASSEMBLY when type is ELEMENT when Design layer is targeted)
        //so mark it as previous as there is clearly an update from the stream.  For these cases, merging isn't possible.
        GSA.GsaApp.gsaCache.MarkAsPrevious(keyword, targetObject.ApplicationId);
      }
      else
      {
        var existing = existingList.First();  //There should just be one instance of each Application ID per type

        try
        {
          targetObject = GSA.GsaApp.Merger.Merge(targetObject, existing);
        }
        catch (Exception ex)
        {
          GSA.GsaApp.Messenger.Message(MessageIntent.TechnicalLog, MessageLevel.Error, ex,
            HelperFunctions.Combine("Error with merging received with existing counterpart", errContext).ToArray());
        }
      }

      //SpeckleCore swallows exceptions on the Deserialise call, so no need for a try..catch block here.  Need to rely on the messages
      //cached in the messenger
      var deserialiseReturn = Converter.Deserialise(targetObject);

      if (deserialiseReturn is Exception)
      {
        GSA.GsaApp.Messenger.Message(MessageIntent.TechnicalLog, MessageLevel.Error, (Exception)deserialiseReturn, errContext.ToArray());
      }
      else
      {
        var linesToAdd = ((string)deserialiseReturn).Split(new[] { '\n' }).Where(c => c.Length > 0).ToList();
        if (linesToAdd != null)
        {
          //TO DO - parallelise
          for (int j = 0; j < linesToAdd.Count(); j++)
          {
            try
            {
              if (!GwaToCache(linesToAdd[j], streamId, targetObject))
              {
                throw new Exception("Error in updating cache with GWA");
              }
            }
            catch (Exception ex)
            {
              GSA.GsaApp.Messenger.Message(MessageIntent.TechnicalLog, MessageLevel.Error, ex, errContext.ToArray());
            }
          }
        }
      }
      return;
    }

    private bool GwaToCache(string gwaCommand, string streamId, SpeckleObject targetObject)
    {
      //At this point the SID will be filled with the application ID
      GSA.GsaApp.gsaProxy.ParseGeneralGwa(gwaCommand, out string keyword, out int? foundIndex, out string foundStreamId, out string foundApplicationId, out string gwaWithoutSet, out GwaSetCommandType? gwaSetCommandType, true);

      var originalSid = GSA.GsaApp.gsaProxy.FormatSidTags(foundStreamId, foundApplicationId);
      var newSid = GSA.GsaApp.gsaProxy.FormatSidTags(streamId, foundApplicationId);

      //If the SID tag has been set then update it with the stream
      if (string.IsNullOrEmpty(originalSid))
      {
        gwaWithoutSet = gwaWithoutSet.Replace(keyword, keyword + ":" + newSid);
      }
      else
      {
        gwaWithoutSet = gwaWithoutSet.Replace(originalSid, newSid);
      }

      //Only cache the object against, the top-level GWA command, not the sub-commands - this is what the SID value comparision is there for
      return GSA.GsaApp.gsaCache.Upsert(keyword.Split('.').First(),
        foundIndex.Value,
        gwaWithoutSet,
        foundApplicationId,
        so: (foundApplicationId != null
          && targetObject.ApplicationId != null
          && targetObject.ApplicationId.EqualsWithoutSpaces(foundApplicationId))
            ? targetObject
            : null,
        gwaSetCommandType: gwaSetCommandType ?? GwaSetCommandType.Set,
        streamId: streamId);
    }

    //Note: this is called while the traversedSerialisedLock is in place
    private void SerialiseUpdateCacheForGSAType(string keyword, Type t, object dummyObject)
    {
      var readPrereqs = GetPrereqs(t, GSA.TxTypeDependencies);

      //The way the readPrereqs are constructed (one linear list, not grouped by generations/batches), this cannot be parallelised
      for (int j = 0; j < readPrereqs.Count(); j++)
      {
        var prereqDummyObject = Activator.CreateInstance(readPrereqs[j]);
        var prereqKeyword = prereqDummyObject.GetAttribute("GSAKeyword").ToString();

        if (!traversedSerialisedTypes.Contains(readPrereqs[j]))
        {
          try
          {
            _ = Converter.Serialise(prereqDummyObject);
          }
          catch { }

          var prereqSerialisedObjects = CollateSerialisedObjects(readPrereqs[j]);

          for (int k = 0; k < prereqSerialisedObjects.Count; k++)
          {
            //The GWA will contain stream ID, which needs to be stripped off for merging and sending purposes
            var applicationId = prereqSerialisedObjects[k].ApplicationId;
            //Only objects which have application IDs can be merged
            if (!string.IsNullOrEmpty(applicationId))
            {
              prereqSerialisedObjects[k].ApplicationId = applicationId;

              //The SpeckleTypeName of this cache entry is automatically created by the assignment of the object
              GSA.GsaApp.gsaCache.AssignSpeckleObject(prereqKeyword, applicationId, prereqSerialisedObjects[k]);
            }
          }
          traversedSerialisedTypes.Add(readPrereqs[j]);
        }
      }

      //This ensures the sender objects are filled within the assembly which contains the corresponding "ToSpeckle" method
      try
      {
        _ = Converter.Serialise(dummyObject);
      }
      catch { }

      var serialisedObjects = CollateSerialisedObjects(t);

      //For these serialised objects, there should already be a match in the cache, as it was read during initialisation and updated
      //during previous reception Trigger calls

      for (int j = 0; j < serialisedObjects.Count; j++)
      {
        var applicationId = serialisedObjects[j].ApplicationId;

        //Only objects which have application IDs can be merged
        if (!string.IsNullOrEmpty(applicationId))
        {
          serialisedObjects[j].ApplicationId = applicationId;

          //The SpeckleTypeName of this cache entry is automatically created by the assignment of the object
          GSA.GsaApp.gsaCache.AssignSpeckleObject(keyword, serialisedObjects[j].ApplicationId, serialisedObjects[j]);
        }
      }

      traversedSerialisedTypes.Add(t);
    }


    //These are not grouped by generation - so should be treated as a linear list
    //Note: this is called while traversedSerialisedLock is in place 
    private List<Type> GetPrereqs(Type t, Dictionary<Type, List<Type>> allPrereqs)
    {
      var prereqs = new List<Type>();
      var latestGen = new List<Type>();
      latestGen.AddRange(allPrereqs[t]);

      while (latestGen != null && latestGen.Count() > 0)
      {
        prereqs.AddRange(latestGen.Where(lg => !prereqs.Any(pr => pr == lg)));
        latestGen = latestGen.SelectMany(lg => allPrereqs[lg]).ToList();
      }

      prereqs.Reverse();

      return prereqs;
    }

    private List<SpeckleObject> CollateSerialisedObjects(Type t)
    {
      var serialised = new List<SpeckleObject>();
      for (int i = 0; i < GSA.SenderDictionaries.Count(); i++)
      {
        var allObjects = GSA.SenderDictionaries[i].GetAll();
        serialised.AddRange(allObjects.SelectMany(kvp => kvp.Value).Select(o => (SpeckleObject) ((IGSASpeckleContainer)o).SpeckleObject));
      }
      return serialised;
    }

    /// <summary>
    /// Dispose receiver.
    /// </summary>
    public void Dispose()
    {
      foreach (Tuple<string,string> streamInfo in GSA.ReceiverInfo)
      {
        Receivers[streamInfo.Item1].UpdateGlobalTrigger -= Trigger;
        Receivers[streamInfo.Item1].Dispose();
      }
    }

		public void DeleteSpeckleObjects()
    {
			var gwaToDelete = GSA.GsaApp.gsaCache.GetDeletableData();
      
      for (int i = 0; i < gwaToDelete.Count(); i++)
      {
        var keyword = gwaToDelete[i].Item1;
        var index = gwaToDelete[i].Item2;
        var gwaSetCommandType = gwaToDelete[i].Item4;

        GSA.GsaApp.gsaProxy.DeleteGWA(keyword, index, gwaSetCommandType);
      }

      GSA.GsaApp.gsaProxy.UpdateViews();
    }
  }
}
