using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SpeckleCore;
using SpeckleGSAInterfaces;
using SpeckleGSAProxy;

namespace SpeckleGSA
{
	/// <summary>
	/// Responsible for reading and writing Speckle streams.
	/// </summary>
	public class ReceiverCoordinator
	{
    public bool IsInit = false;
    public bool IsBusy = false;

    public Dictionary<string, SpeckleInterface.IStreamReceiver> StreamReceivers = new Dictionary<string, SpeckleInterface.IStreamReceiver>();

    //These need to be accessed using a lock
    private object traversedSerialisedLock = new object();
    private object traversedDeserialisedLock = new object();

    private readonly List<Type> traversedSerialisedTypes = new List<Type>();
    private readonly List<Type> traversedDeserialisedTypes = new List<Type>();

    private IProgress<MessageEventArgs> loggingProgress;
    private IProgress<string> statusProgress;

    private ProgressEstimator progressEstimator;

    public readonly Dictionary<Type, IGSASpeckleContainer> dummyObjectDict = new Dictionary<Type, IGSASpeckleContainer>();

    /// <summary>
    /// Initializes receiver.
    /// </summary>
    /// <param name="restApi">Server address</param>
    /// <param name="apiToken">API token of account</param>
    /// <returns>Task</returns>
    public List<string> Initialize(string restApi, string apiToken, List<SidSpeckleRecord> receiverStreamInfo,
      Func<string, string, SpeckleInterface.IStreamReceiver> streamReceiverCreationFn, 
      IProgress<MessageEventArgs> loggingProgress, IProgress<string> statusProgress, IProgress<double> percentageProgress)
		{
      StreamReceivers.Clear();

      var statusMessages = new List<string>();

      this.loggingProgress = loggingProgress;
      this.statusProgress = statusProgress;

      this.progressEstimator = new ProgressEstimator(percentageProgress, WorkPhase.CacheRead, 3, WorkPhase.CacheUpdate, 1, WorkPhase.ApiCalls, 3, WorkPhase.Conversion, 20);

      if (IsInit) return statusMessages;

      var startTime = DateTime.Now;
			if (!GSA.IsInit)
			{
        this.loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Error, "GSA link not found."));
				return statusMessages;
			}

      this.loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Information, "Initialising receivers"));

      statusProgress.Report("Reading GSA data into cache");

      UpdateCache();
                                                
      // Create receivers
      statusProgress.Report("Accessing streams");

      receiverStreamInfo.Where(r => !string.IsNullOrEmpty(r.StreamId)).ToList().ForEach((streamInfo) =>
			{
        StreamReceivers.Add(streamInfo.StreamId, streamReceiverCreationFn(restApi, apiToken));
				StreamReceivers[streamInfo.StreamId].InitializeReceiver(streamInfo.StreamId, streamInfo.Bucket);
				StreamReceivers[streamInfo.StreamId].UpdateGlobalTrigger += Trigger;
			});

      TimeSpan duration = DateTime.Now - startTime;
      this.loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Information, "Duration of initialisation: " + duration.ToString(@"hh\:mm\:ss")));
      this.loggingProgress.Report(new MessageEventArgs(MessageIntent.Telemetry, MessageLevel.Information, "receive", "initialisation", "duration", duration.ToString(@"hh\:mm\:ss")));

      statusProgress.Report("Ready to receive");
			IsInit = true;

      GSA.App.LocalProxy.SetUnits(GSA.App.Settings.Units);

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

      //GSA.App.Settings.Units = GSA.App.Proxy.GetUnits();

      lock (traversedSerialisedLock)
      {
        traversedSerialisedTypes.Clear();
      }
      lock (traversedDeserialisedLock)
      {
        traversedDeserialisedTypes.Clear();
      }

      GSA.SenderDictionaries.Clear();

      // Read objects
      statusProgress.Report("Receiving streams");

      var streamIds = StreamReceivers.Keys.ToList();

      var rxObjsByStream = new Dictionary<string, List<SpeckleObject>>();

      foreach (var streamId in StreamReceivers.Keys)
      {
        rxObjsByStream.Add(streamId, StreamReceivers[streamId].GetObjects());  //This calls UpdateGlobal(), which is the trigger for pulilng information from the server
        progressEstimator.AppendCurrent(WorkPhase.ApiCalls, 1);
      }

      progressEstimator.UpdateTotal(WorkPhase.Conversion, rxObjsByStream.Keys.Sum(k => rxObjsByStream[k].Count()));

      //This list will contain ALL speckle objects received across all streams
      var rxObjs = new List<SpeckleObject>();
      var units = GSA.GsaApp.Settings.Units;
      foreach (var streamId in StreamReceivers.Keys)
      {
        double factor = 1;
        if (StreamReceivers[streamId].Units == null)
        {
          //Let the user know if any streams have no unit information
          this.loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Error, "Streams with no unit information", streamId));
        }
        else
        {
          factor = (1.0).ConvertUnit(StreamReceivers[streamId].Units.ShortUnitName(), units);
        }
        foreach (var o in rxObjsByStream[streamId])
        {
          if (string.IsNullOrEmpty(o.ApplicationId))
          {
            this.loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Information, o.GetType().Name + ((string.IsNullOrEmpty(o.Name))
              ? " with no name nor ApplicationId (identified by hashes)" : " with no name nor ApplicationId (identified by hashes)"), o.Hash));
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
              this.loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Error, "Scaling issue for objects with _ids on stream: " + streamId, o._id));
              this.loggingProgress.Report(new MessageEventArgs(MessageIntent.TechnicalLog, MessageLevel.Error, ex, "Scaling issue", "StreamId=" + streamId, "_id=" + o._id));
            }

            //Populate the cache with stream IDs - review if this is needed anymroe
            GSA.App.LocalCache.SetStream(o.ApplicationId, streamId);
            rxObjs.Add(o);
          }
        }
      }

      progressEstimator.UpdateTotal(WorkPhase.Conversion, rxObjs.Count());

      //GSA.App.Messenger.Trigger();

      TimeSpan duration = DateTime.Now - startTime;
      this.loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Information, "Duration of reception from Speckle and scaling: " + duration.ToString(@"hh\:mm\:ss")));
      this.loggingProgress.Report(new MessageEventArgs(MessageIntent.Telemetry, MessageLevel.Information, "receive", "reception and scaling", "duration", duration.ToString(@"hh\:mm\:ss")));

      if (rxObjs.Count() == 0)
      {
        this.loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Information, "No processing needed because the stream(s) contain(s) no objects"));
        statusProgress.Report("Finished receiving");
        IsBusy = false;
        return;
      }

      startTime = DateTime.Now;
      streamIds.ForEach(s => GSA.App.LocalCache.Snapshot(s));

      ProcessRxObjects(rxObjs);

      var toBeAddedGwa = GSA.App.LocalCache.GetNewGwaSetCommands();
      toBeAddedGwa.ForEach(tba => GSA.App.Proxy.SetGwa(tba));

      var toBeDeletedGwa = GSA.App.LocalCache.GetExpiredData();

      var setDeletes = toBeDeletedGwa.Where(t => t.Item4 == GwaSetCommandType.Set).ToList();
      setDeletes.ForEach(sd => GSA.App.Proxy.DeleteGWA(sd.Item1, sd.Item2, GwaSetCommandType.Set));

      var setAtDeletes = toBeDeletedGwa.Where(t => t.Item4 == GwaSetCommandType.SetAt).OrderByDescending(t => t.Item2).ToList();
      setAtDeletes.ForEach(sad => GSA.App.Proxy.DeleteGWA(sad.Item1, sad.Item2, GwaSetCommandType.SetAt));

      GSA.App.Proxy.Sync();

      GSA.App.Proxy.UpdateCasesAndTasks();
      GSA.App.Proxy.UpdateViews();

      duration = DateTime.Now - startTime;
      this.loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Information, "Duration of conversion from Speckle: " + duration.ToString(@"hh\:mm\:ss")));
      this.loggingProgress.Report(new MessageEventArgs(MessageIntent.Telemetry, MessageLevel.Information, "receive", "conversion", "duration", duration.ToString(@"hh\:mm\:ss")));
      startTime = DateTime.Now;

      statusProgress.Report("Finished receiving");
      IsBusy = false;
    }

    private bool UpdateCache()
    {
      var keywords = GSA.Keywords;
      GSA.App.LocalCache.Clear();
      //initial estimate
      progressEstimator.UpdateTotal(WorkPhase.CacheRead, keywords.Count());
      progressEstimator.UpdateTotal(WorkPhase.CacheUpdate, keywords.Count());
      progressEstimator.UpdateTotal(WorkPhase.Conversion, 10000); //Take wild guess at having 10,000 objects to convert
      progressEstimator.UpdateTotal(WorkPhase.ApiCalls, 10);       //Take wild guess at having 10 API calls to make

      try
      {
        var data = GSA.App.Proxy.GetGwaData(keywords, false);
        progressEstimator.UpdateTotal(WorkPhase.CacheRead, data.Count());
        progressEstimator.SetCurrentToTotal(WorkPhase.CacheRead); //Equalise the current and total in case the previous total estimate was wrong

        //Now that we have a better ideaof how many objects to update the cache with, and convert
        progressEstimator.UpdateTotal(WorkPhase.CacheUpdate, data.Count());
        progressEstimator.UpdateTotal(WorkPhase.Conversion, data.Count());

        for (int i = 0; i < data.Count(); i++)
        {
          GSA.App.Cache.Upsert(
            data[i].Keyword,
            data[i].Index,
            data[i].GwaWithoutSet,
            streamId: data[i].StreamId,
            //This needs to be revised as this logic is in the kit too
            applicationId: (string.IsNullOrEmpty(data[i].ApplicationId)) ? ("gsa/" + data[i].Keyword + "_" + data[i].Index.ToString()) : data[i].ApplicationId,
            gwaSetCommandType: data[i].GwaSetType);

          progressEstimator.AppendCurrent(WorkPhase.CacheRead, 1);
        }

        var numRowsupdated = data.Count();
        if (numRowsupdated > 0)
        {
          this.loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Information, 
            "Read " + numRowsupdated + " GWA lines across " + keywords.Count() + " keywords into cache"));
        }

        progressEstimator.SetCurrentToTotal(WorkPhase.CacheUpdate); //Equalise the current and total in case the previous total estimate was wrong

        return true;
      }
      catch
      {
        return false;
      }
    }

    private Dictionary<Type, List<SpeckleObject>> CollateRxObjectsByType(List<SpeckleObject> rxObjs)
    {
      var rxTypePrereqs = GSA.RxTypeDependencies;
      var rxSpeckleTypes = rxObjs.Select(k => k.GetType()).Distinct().ToList();

      ///[ GSA type , [ SpeckleObjects ]]
      var d = new Dictionary<Type, List<SpeckleObject>>();
      foreach (var o in rxObjs)
      {
        var speckleType = o.GetType();

        var matchingGsaTypes = rxTypePrereqs.Keys.Where(t => dummyObjectDict[t].SpeckleObject.GetType() == speckleType);
        if (matchingGsaTypes.Count() == 0)
        {
          matchingGsaTypes = rxTypePrereqs.Keys.Where(t => speckleType.IsSubclassOf(dummyObjectDict[t].SpeckleObject.GetType()));
        }

        if (matchingGsaTypes.Count() == 0)
        {
          continue;
        }

        var gsaType = matchingGsaTypes.First();
        if (!d.ContainsKey(gsaType))
        {
          d.Add(gsaType, new List<SpeckleObject>());
        }
        d[gsaType].Add(o);
      }

      return d;
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

      var rxObjsByType = CollateRxObjectsByType(rxObjs);

      //These are IGSASpeckleContainer types
      List<Type> currentTypeBatch;
      do
      {
        //A batch is a group of groups of objects by type
        currentTypeBatch = new List<Type>();
        lock (traversedDeserialisedLock)
        {
          currentTypeBatch.AddRange(rxTypePrereqs.Where(i => i.Value.Count(x => !traversedDeserialisedTypes.Contains(x)) == 0).Select(i => i.Key));
          currentTypeBatch.RemoveAll(i => traversedDeserialisedTypes.Contains(i));
        };

        //var batchErrors = ProcessTypeBatch(currentTypeBatch, rxObjsByType);
        //numErrors += batchErrors;
        ProcessTypeBatch(currentTypeBatch, rxObjsByType);

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

    //private int ProcessTypeBatch(List<Type> currentBatch, List<SpeckleObject> rxObjs, out List<SpeckleObject> processedObjs)
    private void ProcessTypeBatch(List<Type> currentBatch, Dictionary<Type, List<SpeckleObject>> rxObjsByType)
    {
      var streamIds = StreamReceivers.Keys.ToList();

      //This method assumes it's not run in parallel
      //GSA.App.Messenger.ResetLoggedMessageCount();

      //Create new dictionary instance in case the original ever gets modified
      var batchRxObsByType = rxObjsByType.Keys.Where(t => currentBatch.Contains(t)).ToDictionary(t => t, t => rxObjsByType[t]);
      var batchLock = new object();

      //Commented this out to enable debug tests for preserving order
#if DEBUG
      foreach (var t in currentBatch)
#else
      Parallel.ForEach(currentBatch, t =>
#endif
      {
        var dummyObject = dummyObjectDict[t];
        var valueType = t.GetProperty("Value").GetValue(dummyObject).GetType();

        List<SpeckleObject> currentObjects = null;
        lock (batchLock)
        {
          currentObjects = batchRxObsByType.ContainsKey(t) ? rxObjsByType[t] : null;
        }

        if (currentObjects != null && currentObjects.Count() > 0)
        {
          statusProgress.Report("Writing " + t.Name);

          var keyword = dummyObject.GetAttribute("GSAKeyword").ToString();

          var speckleTypeName = ((SpeckleObject)(dummyObject).SpeckleObject).Type;

          //First serialise all relevant objects into sending dictionary so that merging can happen
          var typeAppIds = currentObjects.Where(bo => bo.ApplicationId != null).Select(bo => bo.ApplicationId).ToList();
          if (typeAppIds.Any(i => GSA.App.LocalCache.ApplicationIdExists(keyword, i)))
          {
            //Serialise all objects of this type and update traversedSerialised list
            lock (traversedSerialisedLock)
            {
              if (!traversedSerialisedTypes.Contains(t))
              {
                SerialiseUpdateCacheForGSAType(keyword, t, dummyObject);
              }
            }
          }

#if !DEBUG
          if (GSA.RxParallelisableTypes.ContainsKey(valueType))
          {
            var numErrorLock = new object();

            keyword = GSA.RxParallelisableTypes[valueType];
            foreach (var co in currentObjects)
            {
              GSA.App.Cache.ReserveIndex(keyword, co.ApplicationId);
            }
            Parallel.ForEach(currentObjects, o =>
            {
              MergeAndDeserialseObject(o, speckleTypeName, keyword, t);
            });
          }
          else
#endif
          {
            currentObjects.ForEach(o =>
            {
              MergeAndDeserialseObject(o, speckleTypeName, keyword, t);
            });
          }
        }

        lock (traversedDeserialisedLock)
        {
          traversedDeserialisedTypes.Add(t);
        }
      }
#if !DEBUG
      );                                                                                                                                                                                            
#endif

      //Outside of any parallisation, process any cached messages from the conversion code.
      //These should be mostly technical log but may include some display messages
      GSA.App.LocalMessenger.Trigger();

      return;
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

      var existingList = GSA.App.LocalCache.GetSpeckleObjects(speckleTypeName, targetObject.ApplicationId, streamId: streamId);

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
        GSA.App.LocalCache.MarkAsPrevious(keyword, targetObject.ApplicationId);
      }
      else
      {
        var existing = existingList.First();  //There should just be one instance of each Application ID per type

        try
        {
          targetObject = GSA.App.Merger.Merge(targetObject, existing);
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
      if (!string.IsNullOrEmpty(targetObject.ApplicationId))
      {
        GSA.App.LocalMessenger.Append(new[] { targetObject.ApplicationId }, errContext);
      }

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

      progressEstimator.AppendCurrent(WorkPhase.Conversion, 1);
      return;
    }
     
    private bool GwaToCache(string gwaCommand, string streamId, SpeckleObject targetObject)
    {
      //At this point the SID will be filled with the application ID
      GSAProxy.ParseGeneralGwa(gwaCommand, out string keyword, out int? foundIndex, out string foundStreamId, out string foundApplicationId, out string gwaWithoutSet, out GwaSetCommandType? gwaSetCommandType, true);

      var originalSid = GSA.App.Proxy.FormatSidTags(foundStreamId, foundApplicationId);
      var newSid = GSA.App.Proxy.FormatSidTags(streamId, foundApplicationId);

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
      return GSA.App.LocalCache.Upsert(keyword.Split('.').First(),
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
              GSA.App.LocalCache.AssignSpeckleObject(prereqKeyword, applicationId, prereqSerialisedObjects[k]);
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
          GSA.App.LocalCache.AssignSpeckleObject(keyword, serialisedObjects[j].ApplicationId, serialisedObjects[j]);
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
      foreach (var streamId in StreamReceivers.Keys)
      {
        StreamReceivers[streamId].UpdateGlobalTrigger -= Trigger;
        StreamReceivers[streamId].Dispose();
      }
    }

		public void DeleteSpeckleObjects()
    {
			var gwaToDelete = GSA.App.LocalCache.GetDeletableData();
      
      for (int i = 0; i < gwaToDelete.Count(); i++)
      {
        var keyword = gwaToDelete[i].Item1;
        var index = gwaToDelete[i].Item2;
        var gwaSetCommandType = gwaToDelete[i].Item4;

        GSA.App.Proxy.DeleteGWA(keyword, index, gwaSetCommandType);
      }

      GSA.App.Proxy.UpdateViews();
    }
  }
}
