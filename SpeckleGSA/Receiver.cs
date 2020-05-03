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

    private readonly Dictionary<GSATargetLayer, Dictionary<Type, List<Type>>> FilteredWriteTypePrereqs = new Dictionary<GSATargetLayer, Dictionary<Type, List<Type>>>();
    private readonly Dictionary<GSATargetLayer, Dictionary<Type, List<Type>>> FilteredReadTypePrereqs = new Dictionary<GSATargetLayer, Dictionary<Type, List<Type>>>();
    private readonly List<IGSASenderDictionary> senderDictionaries = new List<IGSASenderDictionary>();

    //These need to be accessed using a lock
    private object currentObjectsLock = new object();
    private object traversedSerialisedLock = new object();
    private object traversedDeserialisedLock = new object();
    private readonly List<Tuple<string, SpeckleObject>> currentObjects = new List<Tuple<string, SpeckleObject>>();
    private readonly List<Type> traversedSerialisedTypes = new List<Type>();
    private readonly List<Type> traversedDeserialisedTypes = new List<Type>();

    private readonly Dictionary<Type, object> dummyObjectDict = new Dictionary<Type, object>();

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

			if (!GSA.IsInit)
			{
				Status.AddError("GSA link not found.");
				return statusMessages;
			}
      ExecuteWithLock(ref currentObjectsLock, () => currentObjects.Clear());
      ExecuteWithLock(ref traversedSerialisedLock, () => traversedSerialisedTypes.Clear());
      ExecuteWithLock(ref traversedDeserialisedLock, () => traversedDeserialisedTypes.Clear());

      senderDictionaries.Clear();

      Status.AddMessage("Initialising receivers");

      var attributeType = typeof(GSAObject);

      //The layer for this receive event is stored in GSA.Settings.TargetLayer

      var layers = new[] { GSATargetLayer.Design, GSATargetLayer.Analysis };
      foreach (var layer in layers)
      {
        FilteredWriteTypePrereqs.Add(layer, new Dictionary<Type, List<Type>>());

        //Filter out Prereqs that are excluded by the layer selection
        // Remove wrong layer objects from Prereqs
        var layerPrereqs = GSA.WriteTypePrereqs.Where(t => ObjectTypeMatchesLayer(t.Key, layer));
        foreach (var kvp in layerPrereqs)
        {
          FilteredWriteTypePrereqs[layer][kvp.Key] = kvp.Value.Where(l => ObjectTypeMatchesLayer(l, layer)).ToList();
        }

        FilteredReadTypePrereqs.Add(layer, new Dictionary<Type, List<Type>>());

        //The receiver needs the read type Prereqs too as it might need to serialise objects for merging
        layerPrereqs = GSA.ReadTypePrereqs.Where(t => ObjectTypeMatchesLayer(t.Key, layer));
        foreach (var kvp in layerPrereqs)
        {
          FilteredReadTypePrereqs[layer][kvp.Key] = kvp.Value.Where(l => ObjectTypeMatchesLayer(l, layer)).ToList();
        }
      }

      //Get references to each assembly's sender objects dictionary
      var keywords = GetFilteredKeywords();

      Status.ChangeStatus("Reading GSA data into cache");

      var numRowsupdated = await UpdateCache(keywords);

      if (numRowsupdated > 0)
      {
        Status.AddMessage("Read " + numRowsupdated + " GWA lines across " + keywords.Count() + " keywords into cache");
      }

      // Create receivers
      Status.ChangeStatus("Accessing streams");

			var nonBlankReceivers = GSA.Receivers.Where(r => !string.IsNullOrEmpty(r.Item1)).ToList();

      await nonBlankReceivers.ForEachAsync(async (streamInfo) =>
			{
				await Receivers[streamInfo.Item1].InitializeReceiver(streamInfo.Item1, streamInfo.Item2);
				Receivers[streamInfo.Item1].UpdateGlobalTrigger += Trigger;
			}, Environment.ProcessorCount);

			Status.ChangeStatus("Ready to receive");
			IsInit = true;

			return statusMessages;
		}

		/// <summary>
		/// Trigger to update stream. Is called automatically when update-global ws message is received on stream.
		/// </summary>
		public async void Trigger(object sender, EventArgs e)
    {
      if ((IsBusy) || (!IsInit)) return;

      IsBusy = true;

      GSA.Settings.Units = GSA.gsaProxy.GetUnits();

      ExecuteWithLock(ref currentObjectsLock, () => currentObjects.Clear());
      ExecuteWithLock(ref traversedSerialisedLock, () => traversedSerialisedTypes.Clear());
      ExecuteWithLock(ref traversedDeserialisedLock, () => traversedDeserialisedTypes.Clear());

      senderDictionaries.Clear();

      // Read objects
      Status.ChangeStatus("Receiving streams");
			var errors = new ConcurrentBag<string>();

			Parallel.ForEach(Receivers.Keys, key =>
			{
				try
				{
					var receivedObjects = Receivers[key].GetObjects().Distinct();
					double scaleFactor = (1.0).ConvertUnit(Receivers[key].Units.ShortUnitName(), GSA.Settings.Units);
					foreach (var o in receivedObjects)
					{
						try
						{
							o.Scale(scaleFactor);
						}
						catch { }

            ExecuteWithLock(ref currentObjectsLock, () => currentObjects.Add(new Tuple<string, SpeckleObject>(key, o)));
          }
				}
				catch (Exception ex) 
        {
					errors.Add("stream " + key + ": " + ((ex.InnerException == null) ? ex.Message : ex.InnerException.Message));
				}
			});

			if (errors.Count() > 0)
			{
				foreach (var error in errors)
				{
					Status.AddError(error);
				}
			}

      var streamIds = GSA.Receivers.Select(r => r.Item1).ToList();
      for (int i = 0; i < streamIds.Count(); i++)
      {
        GSA.gsaCache.Snapshot(streamIds[i]);
      }
      
      var gsaStaticObjects = GetAssembliesStaticTypes();

      foreach (var dict in gsaStaticObjects)
      {
        senderDictionaries.Add(dict);
      }

      //These sender dictionaries will be used in merging and they need to be clear in order for the merging code to work
      for (int j = 0; j < senderDictionaries.Count(); j++)
      {
        senderDictionaries[j].Clear();
      }

      //Process on the basis of writing to the chosen layer first.  After this call, the only objects left are those which can only be written to the other layer
      await ProcessObjectsForLayer(GSA.Settings.TargetLayer);

      //For any objects still left in the collection, process on the basis of writing to the other layer
      var otherLayer = GSA.Settings.TargetLayer == GSATargetLayer.Design ? GSATargetLayer.Analysis : GSATargetLayer.Design;
      await ProcessObjectsForLayer(otherLayer);

      var toBeAddedGwa = GSA.gsaCache.GetNewlyAddedGwa();
      for (int i = 0; i < toBeAddedGwa.Count(); i++)
      {
        GSA.gsaProxy.SetGwa(toBeAddedGwa[i]);
      }

      var toBeDeletedGwa = GSA.gsaCache.GetExpiredData();

      var setDeletes = toBeDeletedGwa.Where(t => t.Item4 == GwaSetCommandType.Set).ToList();
      for (int i = 0; i < setDeletes.Count(); i++)  
      {
        var keyword = setDeletes[i].Item1;
        var index = setDeletes[i].Item2;
        GSA.gsaProxy.DeleteGWA(keyword, index, GwaSetCommandType.Set);
      }

      var setAtDeletes = toBeDeletedGwa.Where(t => t.Item4 == GwaSetCommandType.SetAt).OrderByDescending(t => t.Item2).ToList();
      for (int i = 0; i < setAtDeletes.Count(); i++)
      {
        var keyword = setAtDeletes[i].Item1;
        var index = setAtDeletes[i].Item2;
        GSA.gsaProxy.DeleteGWA(keyword, index, GwaSetCommandType.SetAt);
      }

      GSA.gsaProxy.Sync();

      GSA.gsaProxy.UpdateCasesAndTasks();
			GSA.gsaProxy.UpdateViews();

      Status.ChangeStatus("Finished receiving", 100);
      IsBusy = false;
    }

    private Task<int> UpdateCache(List<string> keywords)
    {
      GSA.gsaCache.Clear();
      var data = GSA.gsaProxy.GetGwaData(keywords, true);
      for (int i = 0; i < data.Count(); i++)
      {
        GSA.gsaCache.Upsert(
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
      return Task.FromResult(data.Count());
    }

    private Task ProcessObjectsForLayer(GSATargetLayer layer)
    {
      // Write objects
      var currentBatch = new List<Type>();

      do
      {
        ExecuteWithLock(ref traversedDeserialisedLock, () =>
        {
          currentBatch = FilteredWriteTypePrereqs[layer].Where(i => i.Value.Count(x => !traversedDeserialisedTypes.Contains(x)) == 0).Select(i => i.Key).ToList();
          currentBatch.RemoveAll(i => traversedDeserialisedTypes.Contains(i));
        });

        //Trigger the discovery and assignment of ToNative() methods within the SpeckleCore Converter static object
        //in preparation for their parallel use below.  The methods are stored in a Dictionary object, which is thread-safe
        //for reading. Because the calls to Deserialise below (of dummy objects) will alter the Dictionary object, it must be
        //done in serial on the one thread
        foreach (var t in currentBatch)
        {
          if (!dummyObjectDict.ContainsKey(t))
          {
            var dummyObject = Activator.CreateInstance(t);
            dummyObjectDict[t] = dummyObject;
          }
          var valueType = t.GetProperty("Value").GetValue(dummyObjectDict[t]).GetType();
          if (!Converter.toNativeMethods.ContainsKey(valueType.ToString()))
          {
            try
            {
              Converter.Deserialise((SpeckleObject)((IGSASpeckleContainer)dummyObjectDict[t]).Value);
            }
            catch { }
          }
        }

        Debug.WriteLine("Ran through all types in batch to populate SpeckleCore's ToNative list");

        Parallel.ForEach(currentBatch, t =>
        {
          Status.ChangeStatus("Writing " + t.Name);

          Debug.WriteLine("Processing " + t.Name + " on thread " + Thread.CurrentThread.ManagedThreadId);

          var dummyObject = dummyObjectDict[t];
          var keyword = dummyObject.GetAttribute("GSAKeyword").ToString();

          var valueType = t.GetProperty("Value").GetValue(dummyObject).GetType();
          var targetObjects = ExecuteWithLock(ref currentObjectsLock, () => currentObjects.Where(o => o.Item2.GetType() == valueType).ToList());

          var speckleTypeName = ((SpeckleObject)((IGSASpeckleContainer)dummyObject).Value).Type;

          //First serialise all relevant objects into sending dictionary so that merging can happen
          var typeAppIds = targetObjects.Where(o => o.Item2.ApplicationId != null).Select(o => o.Item2.ApplicationId).ToList();
          if (typeAppIds.Any(i => GSA.gsaCache.ApplicationIdExists(keyword, i)))
          {
            //Serialise all objects of this type and update traversedSerialised list
            ExecuteWithLock(ref traversedSerialisedLock, () =>
            {
              if (!traversedSerialisedTypes.Contains(t))
              {
                SerialiseUpdateCacheForGSAType(layer, keyword, t, dummyObject);
              }
            });
          }

          Parallel.ForEach(targetObjects, tuple =>
          {
            dummyObject = Activator.CreateInstance(t);
            var streamId = tuple.Item1;
            var obj = tuple.Item2;

            Debug.WriteLine("Processing " + t.Name + " " + " AppId " + obj.ApplicationId + " on thread " + Thread.CurrentThread.ManagedThreadId);

            var applicationId = obj.ApplicationId;

            //Check if this application appears in the cache at all
            if (!string.IsNullOrEmpty(applicationId))
            {
              try
              {
                ProcessObject(obj, speckleTypeName, keyword, t, dummyObject, streamId, layer);
              }
              catch (Exception ex)
              {
                // TO DO:
                Status.AddMessage("Processing error for " + speckleTypeName + " with ApplicationId = " + applicationId + " - " + ex.Message);
              }
              finally
              {
                ExecuteWithLock(ref currentObjectsLock, () => currentObjects.Remove(tuple));
              }
            }
          });
          ExecuteWithLock(ref traversedDeserialisedLock, () => traversedDeserialisedTypes.Add(t));
        });

      } while (currentBatch.Count > 0);

      return Task.CompletedTask;
    }

    private SpeckleObject ProcessObject(SpeckleObject targetObject, string speckleTypeName, string keyword, Type t, object dummyObject, string streamId, GSATargetLayer layer)
    {
      var existingList = GSA.gsaCache.GetSpeckleObjects(speckleTypeName, targetObject.ApplicationId, streamId: streamId);

      if (existingList == null || existingList.Count() == 0)
      {
        //The serialisation for this object didn't work (a notable example is ASSEMBLY when type is ELEMENT when Design layer is targeted)
        //so mark it as previous as there is clearly an update from the stream.  For these cases, merging isn't possible.
        GSA.gsaCache.MarkAsPrevious(keyword, targetObject.ApplicationId);
      }
      else
      {
        var existing = existingList.First();  //There should just be one instance of each Application ID per type
        targetObject = GSA.Merger.Merge(targetObject, existing);
      }

      List<string> gwaCommands = new List<string>();
      try
      {
        var deserialiseReturn = Converter.Deserialise(targetObject);
        if (deserialiseReturn.GetType() != typeof(string))
        {
          //TO DO
        }
        var linesToAdd = ((string)deserialiseReturn).Split(new[] { '\n' }).Where(c => c.Length > 0);
        gwaCommands.AddRange(linesToAdd);
      }
      catch (Exception ex)
      {
        //TO DO
      }

      //TO DO - parallelise
      for (int j = 0; j < gwaCommands.Count(); j++)
      {
        //At this point the SID will be filled with the application ID
        GSA.gsaProxy.ParseGeneralGwa(gwaCommands[j], out keyword, out int? foundIndex, out string foundStreamId, out string foundApplicationId, out string gwaWithoutSet, out GwaSetCommandType? gwaSetCommandType);

        var originalSid = GSA.gsaProxy.FormatSidTags(foundStreamId,foundApplicationId);
        var newSid = GSA.gsaProxy.FormatSidTags(streamId, foundApplicationId);

        //If the SID tag has been set then update it with the stream
        if (string.IsNullOrEmpty(originalSid))
        {
          gwaCommands[j] = gwaCommands[j].Replace(keyword, keyword + ":" + newSid);
          gwaWithoutSet = gwaWithoutSet.Replace(keyword, keyword + ":" + newSid);
        }
        else
        {
          gwaCommands[j] = gwaCommands[j].Replace(originalSid, newSid);
          gwaWithoutSet = gwaWithoutSet.Replace(originalSid, newSid);
        }

        //Only cache the object against, the top-level GWA command, not the sub-commands - this is what the SID value comparision is there for
        GSA.gsaCache.Upsert(keyword, 
          foundIndex.Value, 
          gwaWithoutSet, 
          foundApplicationId,
          so: (foundApplicationId != null && targetObject.ApplicationId.EqualsWithoutSpaces(foundApplicationId)) ? targetObject : null, 
          gwaSetCommandType: gwaSetCommandType.HasValue ? gwaSetCommandType.Value : GwaSetCommandType.Set, 
          streamId: streamId);
      }
      return targetObject;
    }

    //Note: this is called while the traversedSerialisedLock is in place
    private void SerialiseUpdateCacheForGSAType(GSATargetLayer layer, string keyword, Type t, object dummyObject)
    {
      var readPrereqs = GetPrereqs(t, FilteredReadTypePrereqs[layer]);

      //The way the readPrereqs are constructed (one linear list, not grouped by generations/batches), this cannot be parallelised
      for (int j = 0; j < readPrereqs.Count(); j++)
      {
        var prereqDummyObject = Activator.CreateInstance(readPrereqs[j]);
        var prereqKeyword = prereqDummyObject.GetAttribute("GSAKeyword").ToString();

        if (!traversedSerialisedTypes.Contains(readPrereqs[j]))
        {
          var prereqResult = Converter.Serialise(prereqDummyObject);
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
              GSA.gsaCache.AssignSpeckleObject(prereqKeyword, applicationId, prereqSerialisedObjects[k]);
            }
          }
          traversedSerialisedTypes.Add(readPrereqs[j]);
        }
      }

      //This ensures the sender objects are filled within the assembly which contains the corresponding "ToSpeckle" method
      var result = Converter.Serialise(dummyObject);
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
          GSA.gsaCache.AssignSpeckleObject(keyword, serialisedObjects[j].ApplicationId, serialisedObjects[j]);
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
      for (int i = 0; i < senderDictionaries.Count(); i++)
      {
        var allObjects = senderDictionaries[i].GetAll();
        serialised.AddRange(allObjects.SelectMany(kvp => kvp.Value).Select(o => (SpeckleObject)((IGSASpeckleContainer)o).Value));
      }
      return serialised;
    }

    /// <summary>
    /// Dispose receiver.
    /// </summary>
    public void Dispose()
    {
      foreach (Tuple<string,string> streamInfo in GSA.Receivers)
      {
        Receivers[streamInfo.Item1].UpdateGlobalTrigger -= Trigger;
        Receivers[streamInfo.Item1].Dispose();
      }
    }

		public void DeleteSpeckleObjects()
    {
			var gwaToDelete = GSA.gsaCache.GetDeletableData();
      
      for (int i = 0; i < gwaToDelete.Count(); i++)
      {
        var keyword = gwaToDelete[i].Item1;
        var index = gwaToDelete[i].Item2;
        var gwa = gwaToDelete[i].Item3;
        var gwaSetCommandType = gwaToDelete[i].Item4;

        GSA.gsaProxy.DeleteGWA(keyword, index, gwaSetCommandType);
      }

      GSA.gsaProxy.UpdateViews();
    }

    protected List<string> GetFilteredKeywords()
    {
      var keywords = new List<string>();
      keywords.AddRange(GetFilteredKeywords(FilteredWriteTypePrereqs[GSATargetLayer.Design]));
      keywords.AddRange(GetFilteredKeywords(FilteredWriteTypePrereqs[GSATargetLayer.Analysis]));
      keywords.AddRange(GetFilteredKeywords(FilteredReadTypePrereqs[GSATargetLayer.Design]));
      keywords.AddRange(GetFilteredKeywords(FilteredReadTypePrereqs[GSATargetLayer.Analysis]));

      return keywords.Distinct().ToList();
    }
  }
}
