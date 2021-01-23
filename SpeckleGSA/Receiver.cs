using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

    //private readonly Dictionary<Type, List<Type>> rxTypePrereqs;
    //private readonly Dictionary<Type, List<Type>> txTypePrereqs;
    //private readonly List<IGSASenderDictionary> senderDictionaries = new List<IGSASenderDictionary>();

    //These need to be accessed using a lock
    private object currentObjectsLock = new object();
    private object traversedSerialisedLock = new object();
    private object traversedDeserialisedLock = new object();
    private readonly List<Tuple<string, SpeckleObject>> currentObjects = new List<Tuple<string, SpeckleObject>>();
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

      //GSA.CollateRxParallelisableTypes();

      ExecuteWithLock(ref currentObjectsLock, () => currentObjects.Clear());
      ExecuteWithLock(ref traversedSerialisedLock, () => traversedSerialisedTypes.Clear());
      ExecuteWithLock(ref traversedDeserialisedLock, () => traversedDeserialisedTypes.Clear());

      GSA.GsaApp.gsaMessenger.Message(MessageIntent.Display, MessageLevel.Information, "Initialising receivers");


      //Get references to each assembly's sender objects dictionary
      //var keywords = GetFilteredKeywords();

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

      GSA.GsaApp.Settings.Units = GSA.GsaApp.gsaProxy.GetUnits();

      ExecuteWithLock(ref currentObjectsLock, () => currentObjects.Clear());
      ExecuteWithLock(ref traversedSerialisedLock, () => traversedSerialisedTypes.Clear());
      ExecuteWithLock(ref traversedDeserialisedLock, () => traversedDeserialisedTypes.Clear());

      GSA.SenderDictionaries.Clear();

      // Read objects
      Status.ChangeStatus("Receiving streams");

      ScaleReceivedObjects();

      TimeSpan duration = DateTime.Now - startTime;
      GSA.GsaApp.gsaMessenger.Message(MessageIntent.Display, MessageLevel.Information, "Duration of reception from Speckle and scaling: " + duration.ToString(@"hh\:mm\:ss"));
      GSA.GsaApp.gsaMessenger.Message(MessageIntent.Telemetry, MessageLevel.Information, "receive", "reception and scaling", "duration", duration.ToString(@"hh\:mm\:ss"));

      if (currentObjects.Count() == 0)
      {
        GSA.GsaApp.gsaMessenger.Message(MessageIntent.Display, MessageLevel.Information, "No processing needed because the stream(s) contain(s) no objects");
      }
      else
      {
        startTime = DateTime.Now;

        var streamIds = GSA.ReceiverInfo.Select(r => r.Item1).ToList();
        for (int i = 0; i < streamIds.Count(); i++)
        {
          GSA.GsaApp.gsaCache.Snapshot(streamIds[i]);
        }

        ProcessObjectsForLayer();

        var toBeAddedGwa = GSA.GsaApp.gsaCache.GetNewGwaSetCommands();
        for (int i = 0; i < toBeAddedGwa.Count(); i++)
        {
          GSA.GsaApp.gsaProxy.SetGwa(toBeAddedGwa[i]);
        }

        var toBeDeletedGwa = GSA.GsaApp.gsaCache.GetExpiredData();

        var setDeletes = toBeDeletedGwa.Where(t => t.Item4 == GwaSetCommandType.Set).ToList();
        for (int i = 0; i < setDeletes.Count(); i++)
        {
          var keyword = setDeletes[i].Item1;
          var index = setDeletes[i].Item2;
          GSA.GsaApp.gsaProxy.DeleteGWA(keyword, index, GwaSetCommandType.Set);
        }

        var setAtDeletes = toBeDeletedGwa.Where(t => t.Item4 == GwaSetCommandType.SetAt).OrderByDescending(t => t.Item2).ToList();
        for (int i = 0; i < setAtDeletes.Count(); i++)
        {
          var keyword = setAtDeletes[i].Item1;
          var index = setAtDeletes[i].Item2;
          GSA.GsaApp.gsaProxy.DeleteGWA(keyword, index, GwaSetCommandType.SetAt);
        }

        GSA.GsaApp.gsaProxy.Sync();

        GSA.GsaApp.gsaProxy.UpdateCasesAndTasks();
        GSA.GsaApp.gsaProxy.UpdateViews();

        duration = DateTime.Now - startTime;
        GSA.GsaApp.gsaMessenger.Message(MessageIntent.Display, MessageLevel.Information, "Duration of conversion from Speckle: " + duration.ToString(@"hh\:mm\:ss"));
        GSA.GsaApp.gsaMessenger.Message(MessageIntent.Telemetry, MessageLevel.Information, "receive", "conversion", "duration", duration.ToString(@"hh\:mm\:ss"));
        startTime = DateTime.Now;
      }
      Status.ChangeStatus("Finished receiving", 100);
      IsBusy = false;
    }

    private bool ScaleReceivedObjects()
    {
      //var errors = new ConcurrentBag<string>();
      int numErrors = 0;
#if DEBUG
      foreach (var key in Receivers.Keys)
#else
      Parallel.ForEach(Receivers.Keys, key =>
#endif
      {
        try
        {
          var receivedObjects = Receivers[key].GetObjects().Distinct();
          if (receivedObjects.Count() > 0)
          {
            if (Receivers[key].Units == null)
            {
              GSA.GsaApp.gsaMessenger.CacheMessage(MessageIntent.Display, MessageLevel.Error, "stream " + key + ": No unit information could be found");
              numErrors++;
            }
            else
            {
              double scaleFactor = (1.0).ConvertUnit(Receivers[key].Units.ShortUnitName(), GSA.GsaApp.Settings.Units);
              foreach (var o in receivedObjects)
              {
                if (!string.IsNullOrEmpty(o.ApplicationId))
                {
                  GSA.GsaApp.gsaCache.SetStream(o.ApplicationId, Receivers[key].StreamId);
                }
                if (scaleFactor != 1)
                {
                  try
                  {
                    o.Scale(scaleFactor);
                  }
                  catch { }
                }
                ExecuteWithLock(ref currentObjectsLock, () => currentObjects.Add(new Tuple<string, SpeckleObject>(key, o)));
              }
            }
          }
        }
        catch (Exception ex)
        {
          GSA.GsaApp.gsaMessenger.CacheMessage(MessageIntent.Display, MessageLevel.Error, 
            "stream " + key + ": " + ((ex.InnerException == null) ? ex.Message : ex.InnerException.Message));
          numErrors++;
        }
      }
#if !DEBUG
      );
#endif
      GSA.GsaApp.gsaMessenger.Trigger();
      /*
      if (errors.Count() > 0)
      {
        foreach (var error in errors)
        {
          GSA.GsaApp.gsaMessager.AddError(error);
        }
        return false;
      }
      */
      return (numErrors == 0);
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

    //Trigger the discovery and assignment of ToNative() methods within the SpeckleCore Converter static object
    //in preparation for their parallel use below.  The methods are stored in a Dictionary object, which is thread-safe
    //for reading. Because the calls to Deserialise below (of dummy objects) will alter the Dictionary object, it must be
    //done in serial on the one thread
    private void DiscoverToNativeMethods(List<Type> currentBatch)
    {
      currentBatch.Where(t => !dummyObjectDict.ContainsKey(t)).ToList()
        .ForEach(t => dummyObjectDict[t] = (IGSASpeckleContainer) Activator.CreateInstance(t));
      
      foreach (var t in currentBatch)
      {
        var valueType = ((SpeckleObject) dummyObjectDict[t].SpeckleObject).GetType().ToString();
        if (!Converter.toNativeMethods.ContainsKey(valueType.ToString()))
        {
          try
          {
            Converter.Deserialise((SpeckleObject)dummyObjectDict[t].SpeckleObject);
          }
          catch { }
        }
      }
    }

    private List<Type> GetNewCurrentBatch()
    {
      var rxTypePrereqs = GSA.RxTypeDependencies;
      List<Type> batch = new List<Type>();
      ExecuteWithLock(ref traversedDeserialisedLock, () =>
      {
        batch.AddRange(rxTypePrereqs.Where(i => i.Value.Count(x => !traversedDeserialisedTypes.Contains(x)) == 0).Select(i => i.Key));
        batch.RemoveAll(i => traversedDeserialisedTypes.Contains(i));
      });
      return batch;
    }

    private void ProcessTargetObject(Tuple<string, SpeckleObject> tuple, string speckleTypeName, Type t, string keyword)
    {
      var streamId = tuple.Item1;
      var obj = tuple.Item2;
      var applicationId = obj.ApplicationId;

      if (string.IsNullOrEmpty(applicationId))
      {
        GSA.GsaApp.Messenger.Message(MessageIntent.Display, MessageLevel.Information, speckleTypeName +
          ((string.IsNullOrEmpty(obj.Name)) ? " with no name nor ApplicationId (identified by hashes)" : " with no name nor ApplicationId (identified by hashes)"),
          obj.Hash);
      }
      //Check if this application appears in the cache at all
      else
      {
        HelperFunctions.tryCatchWithEvents(() => 
        
          MergeAndDeserialseObject(obj, speckleTypeName, keyword, t, streamId),
        
          "", "Processing error for " + speckleTypeName + " with ApplicationId = " + applicationId);

        ExecuteWithLock(ref currentObjectsLock, () => currentObjects.Remove(tuple));
      }
    }

    private void ProcessObjectBatch(List<Type> currentBatch)
    {
      //Commented this out to enable debug tests for preserving order
#if DEBUG
      foreach (var t in currentBatch)
#else
      Parallel.ForEach(currentBatch, t =>
#endif
      {
        Status.ChangeStatus("Writing " + t.Name);

        var dummyObject = dummyObjectDict[t];
        var keyword = dummyObject.GetAttribute("GSAKeyword").ToString();

        var valueType = t.GetProperty("Value").GetValue(dummyObject).GetType();
        var targetObjects = ExecuteWithLock(ref currentObjectsLock, () => currentObjects.Where(o => o.Item2.GetType() == valueType).ToList());

        var speckleTypeName = ((SpeckleObject)(dummyObject).SpeckleObject).Type;

        //First serialise all relevant objects into sending dictionary so that merging can happen
        var typeAppIds = targetObjects.Where(o => o.Item2.ApplicationId != null).Select(o => o.Item2.ApplicationId).ToList();
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

        if (GSA.RxParallelisableTypes.ContainsKey(valueType))
        {
          keyword = GSA.RxParallelisableTypes[valueType];
          foreach (var tuple in targetObjects)
          {
            GSA.GsaApp.gsaCache.ReserveIndex(keyword, tuple.Item2.ApplicationId);
          }
          Parallel.ForEach(targetObjects, tuple => { ProcessTargetObject(tuple, speckleTypeName, t, keyword); });
        }
        else
        {
          targetObjects.ForEach(tuple => { ProcessTargetObject(tuple, speckleTypeName, t, keyword); });
        }

        //Process any cached messages from the conversion code
        GSA.GsaApp.gsaMessenger.Trigger();

        ExecuteWithLock(ref traversedDeserialisedLock, () => traversedDeserialisedTypes.Add(t));
      }
#if !DEBUG
      );
#endif
    }

    private Task ProcessObjectsForLayer()
    {
      // Write objects
      List<Type> currentBatch;

      do
      {
        currentBatch = GetNewCurrentBatch();
        DiscoverToNativeMethods(currentBatch);

        Debug.WriteLine("Ran through all types in batch to populate SpeckleCore's ToNative list");

        //A batch is a group of groups of objects by type
        //So each batch has a group of StructuralX objects + StructuralY objects, etc

        ProcessObjectBatch(currentBatch);

      } while (currentBatch.Count > 0);

      return Task.CompletedTask;
    }

    private SpeckleObject MergeAndDeserialseObject(SpeckleObject targetObject, string speckleTypeName, string keyword, Type t, string streamId)
    {
      var existingList = GSA.GsaApp.gsaCache.GetSpeckleObjects(speckleTypeName, targetObject.ApplicationId, streamId: streamId);

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
        catch
        {
          //Add for summary messaging at the end of processing
          GSA.GsaApp.Messenger.Message(MessageIntent.Display, MessageLevel.Error, 
            "Unable to merge " + t.Name + " with existing objects", targetObject.ApplicationId);
        }
      }

      List<string> gwaCommands = new List<string>();
      List<string> linesToAdd = null;

      HelperFunctions.tryCatchWithEvents(() =>
        {
          var deserialiseReturn = Converter.Deserialise(targetObject);
          if (deserialiseReturn.GetType() != typeof(string))
          {
            throw new Exception("Converting to native didn't produce a string output");
          }
          linesToAdd = ((string)deserialiseReturn).Split(new[] { '\n' }).Where(c => c.Length > 0).ToList();
        }, "", "Unable to convert object to GWA");

      if (linesToAdd != null)
      {
        gwaCommands.AddRange(linesToAdd);

        //TO DO - parallelise
        for (int j = 0; j < gwaCommands.Count(); j++)
        {
          GwaToCache(gwaCommands[j], streamId, targetObject);
        }
      }
      return targetObject;
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
          _ = Converter.Serialise(prereqDummyObject);
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
      _ = Converter.Serialise(dummyObject);
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
