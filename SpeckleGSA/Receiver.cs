using System;
using System.Collections.Concurrent;
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
	public class Receiver : BaseReceiverSender
	{
    public Dictionary<string, SpeckleGSAReceiver> Receivers = new Dictionary<string, SpeckleGSAReceiver>();

    private Dictionary<GSATargetLayer, Dictionary<Type, List<Type>>> FilteredWriteTypePrereqs = new Dictionary<GSATargetLayer, Dictionary<Type, List<Type>>>();
    private Dictionary<GSATargetLayer, Dictionary<Type, List<Type>>> FilteredReadTypePrereqs = new Dictionary<GSATargetLayer, Dictionary<Type, List<Type>>>();

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

      GSA.gsaCache.Clear();
      var data = GSA.gsaProxy.GetGWAData(keywords);
      for (int i = 0; i < data.Count(); i++)
      {
        data[i].Sid.ParseSid(out string streamId, out string applicationId);
        //This needs to be revised as this logic is in the kit too
        applicationId = (string.IsNullOrEmpty(applicationId)) ? ("gsa/" + data[i].Keyword + "_" + data[i].Index.ToString()) : applicationId;
        GSA.gsaCache.Upsert(data[i].Keyword, data[i].Index, data[i].GwaWithoutSet, applicationId: applicationId, gwaSetCommandType: data[i].GwaSetType, streamId: streamId);
      }
      Status.AddMessage("Read " + data.Count() + " GWA lines across " + keywords.Count()  + " keywords into cache");

      // Create receivers
      Status.ChangeStatus("Accessing streams");

			var nonBlankReceivers = GSA.Receivers.Where(r => !string.IsNullOrEmpty(r.Item1)).ToList();

			foreach (var streamInfo in nonBlankReceivers)
			{
				Status.AddMessage("Creating receiver " + streamInfo.Item1);
				Receivers[streamInfo.Item1] = new SpeckleGSAReceiver(restApi, apiToken);
			}

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

      var objects = new SynchronizedCollection<Tuple<string, SpeckleObject>>();
      
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

            objects.Add(new Tuple<string, SpeckleObject>(key, o));
          }
				}
				catch {
					errors.Add("Unable to get stream " + key);
				}
			});

			if (errors.Count() > 0)
			{
				foreach (var error in errors)
				{
					Status.AddError(error);
				}
			}

      GSA.gsaCache.Snapshot();

      var senderDictionaries = new List<Dictionary<Type, List<object>>>();
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

      var traversedSerialisedTypes = new List<Type>();
      
      //Process on the basis of writing to the chosen layer first.  After this call, the only objects left are those which can only be written to the other layer
      ProcessObjectsForLayer(GSA.Settings.TargetLayer, ref traversedSerialisedTypes, ref objects, ref senderDictionaries);

      //For any objects still left in the collection, process on the basis of writing to the other layer
      var otherLayer = GSA.Settings.TargetLayer == GSATargetLayer.Design ? GSATargetLayer.Analysis : GSATargetLayer.Design;
      ProcessObjectsForLayer(otherLayer, ref traversedSerialisedTypes, ref objects, ref senderDictionaries);

      var toBeDeletedGwa = GSA.gsaCache.GetExpiredData();
      for (int i = 0; i < toBeDeletedGwa.Count(); i++)
      {
        var keyword = toBeDeletedGwa[i].Item1;
        var index = toBeDeletedGwa[i].Item2;
        var gwa = toBeDeletedGwa[i].Item3;
        var gwaSetCommandType = toBeDeletedGwa[i].Item4;
        GSA.gsaProxy.DeleteGWA(keyword, index, gwaSetCommandType);
      }
      GSA.gsaProxy.Sync();

      GSA.gsaProxy.UpdateCasesAndTasks();
			GSA.gsaProxy.UpdateViews();

      IsBusy = false;
      Status.ChangeStatus("Finished receiving", 100);
    }

    private void ProcessObjectsForLayer(GSATargetLayer layer, ref List<Type> traversedSerialisedTypes, ref SynchronizedCollection<Tuple<string, SpeckleObject>> objects, ref List<Dictionary<Type, List<object>>> senderDictionaries)
    {
      // Write objects
      var currentBatch = new List<Type>();
      var traversedTypes = new List<Type>();      

      do
      {
        currentBatch = FilteredWriteTypePrereqs[layer].Where(i => i.Value.Count(x => !traversedTypes.Contains(x)) == 0).Select(i => i.Key).ToList();
        currentBatch.RemoveAll(i => traversedTypes.Contains(i));

        foreach (Type t in currentBatch)
        {
          Status.ChangeStatus("Writing " + t.Name);

          var dummyObject = Activator.CreateInstance(t);
          var keyword = dummyObject.GetAttribute("GSAKeyword").ToString();

          var valueType = t.GetProperty("Value").GetValue(dummyObject).GetType();
          var targetObjects = objects.Where(o => o.Item2.GetType() == valueType).ToList();

          var speckleTypeName = ((SpeckleObject)((IGSASpeckleContainer)dummyObject).Value).Type;

          for (int i = 0; i < targetObjects.Count(); i++)
          {
            var streamId = targetObjects[i].Item1;
            var obj = targetObjects[i].Item2;
            var applicationId = obj.ApplicationId;
            //Check if this application appears in the cache at all
            if (string.IsNullOrEmpty(applicationId)) continue;

            try
            {
              ProcessObject(obj, speckleTypeName, keyword, t, dummyObject, streamId, layer, ref traversedSerialisedTypes, ref senderDictionaries);
            }
            catch (Exception ex)
            {
              // TO DO:
              Status.AddMessage("Processing error for " + speckleTypeName + " with ApplicationId = " + applicationId + " - " + ex.Message);
            }
            finally
            {
              objects.Remove(targetObjects[i]);
            }
          }

          traversedTypes.Add(t);
        }

      } while (currentBatch.Count > 0);
    }

    private SpeckleObject ProcessObject(SpeckleObject targetObject, string speckleTypeName, string keyword, Type t, object dummyObject, string streamId, GSATargetLayer layer, ref List<Type> traversedSerialisedTypes, ref List<Dictionary<Type, List<object>>> senderDictionaries)
    {
      //Hold value for later upserting into cache
      var applicationId = targetObject.ApplicationId;

      //Check if merging needs to be considered
      if (GSA.gsaCache.ApplicationIdExists(keyword, targetObject.ApplicationId))
      {
        if (!traversedSerialisedTypes.Contains(t))
        {
          var readPrereqs = GetPrereqs(t, FilteredReadTypePrereqs[layer]);
          SerialiseUpdateCacheForGSAType(readPrereqs, keyword, t, dummyObject, ref traversedSerialisedTypes, ref senderDictionaries);
        }

        //If so but the type doesn't appear alongside it as one that was loaded, then load it now by calling ToSpeckle with a dummy version of the GSA corresponding type
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
      }
      else
      {
        //The application Id doesn't exist yet in the model - but the deserialisation will add it in
      }

      var gwaCommands = ((string)Converter.Deserialise(targetObject)).Split(new[] { '\n' }).Where(c => c.Length > 0).ToList();

      for (int j = 0; j < gwaCommands.Count(); j++)
      {
        gwaCommands[j].ExtractKeywordApplicationId(out keyword, out int? foundIndex, out string foundApplicationId, out string gwaWithoutSet, out GwaSetCommandType? gwaSetCommandType);

        //If the SID tag has been set then update it with the stream
        if (!string.IsNullOrEmpty(foundApplicationId))
        {
          var sid = streamId + "|" + foundApplicationId;
          gwaCommands[j] = gwaCommands[j].Replace(foundApplicationId + "}", sid + "}");
          gwaWithoutSet = gwaWithoutSet.Replace(foundApplicationId + "}", sid + "}");
        }

        GSA.gsaProxy.SetGWA(gwaCommands[j]);

        //Only cache the object against, the top-level GWA command, not the sub-commands - this is what the SID value comparision is there for
        GSA.gsaCache.Upsert(keyword, foundIndex.Value, gwaWithoutSet, foundApplicationId, 
          so: (foundApplicationId != null && targetObject.ApplicationId.SidValueCompare(foundApplicationId)) ? targetObject : null, streamId: streamId);
      }
      return targetObject;
    }

    private void SerialiseUpdateCacheForGSAType(List<Type> readPrereqs, string keyword, Type t, object dummyObject, ref List<Type> traversedSerialisedTypes, ref List<Dictionary<Type, List<object>>> senderDictionaries)
    {
      for (int j = 0; j < readPrereqs.Count(); j++)
      {
        var prereqDummyObject = Activator.CreateInstance(readPrereqs[j]);
        var prereqKeyword = prereqDummyObject.GetAttribute("GSAKeyword").ToString();

        if (!traversedSerialisedTypes.Contains(readPrereqs[j]))
        {
          var prereqResult = Converter.Serialise(prereqDummyObject);
          var prereqSerialisedObjects = CollateSerialisedObjects(senderDictionaries, readPrereqs[j]);
          for (int k = 0; k < prereqSerialisedObjects.Count; k++)
          {
            //The GWA will contain stream ID, which needs to be stripped off for merging and sending purposes
            var sid = prereqSerialisedObjects[k].ApplicationId;
            //Only objects which have application IDs can be merged
            if (!string.IsNullOrEmpty(sid))
            {
              sid.ParseSid(out string streamId, out string applicationId);
              prereqSerialisedObjects[k].ApplicationId = applicationId;

              //The SpeckleTypeName of this cache entry is automatically created by the assignment of the object
              GSA.gsaCache.AssignSpeckleObject(prereqKeyword, applicationId, prereqSerialisedObjects[k], streamId);
            }
          }
          traversedSerialisedTypes.Add(readPrereqs[j]);
        }
      }

      //This ensures the sender objects are filled within the assembly which contains the corresponding "ToSpeckle" method
      var result = Converter.Serialise(dummyObject);
      var serialisedObjects = CollateSerialisedObjects(senderDictionaries, t);

      //For these serialised objects, there should already be a match in the cache, as it was read during initialisation and updated
      //during previous reception Trigger calls

      for (int j = 0; j < serialisedObjects.Count; j++)
      {
        var sid = serialisedObjects[j].ApplicationId;

        //Only objects which have application IDs can be merged
        if (!string.IsNullOrEmpty(sid))
        {
          //The GWA will contain stream ID, which needs to be stripped off for merging and sending purposes
          sid.ParseSid(out string streamId, out string applicationId);
          serialisedObjects[j].ApplicationId = applicationId;

          //The SpeckleTypeName of this cache entry is automatically created by the assignment of the object
          GSA.gsaCache.AssignSpeckleObject(keyword, serialisedObjects[j].ApplicationId, serialisedObjects[j], streamId);
        }
      }

      traversedSerialisedTypes.Add(t);
    }

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

      return prereqs;
    }

    private List<SpeckleObject> CollateSerialisedObjects(List<Dictionary<Type, List<object>>> dictionaryList, Type t)
    {
      var serialised = new List<SpeckleObject>();
      for (int i = 0; i < dictionaryList.Count(); i++)
      {
        if (dictionaryList[i].ContainsKey(t))
        {
          serialised.AddRange(dictionaryList[i][t].Select(o => (SpeckleObject)((IGSASpeckleContainer)o).Value));
        }
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
