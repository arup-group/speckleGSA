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
        // <keyword, index, Application ID, GWA command (without SET or SET_AT), Set|Set At> tuples
        var keyword = data[i].Item1;
        var index = data[i].Item2;
        //var applicationId = data[i].Item3;
        //This needs to be revised as this logic is in the kit too
        var applicationId = (string.IsNullOrEmpty(data[i].Item3)) ? ("gsa/" + keyword + "_" + index.ToString()) : data[i].Item3;
        var gwa = data[i].Item4;
        var gwaSetCommandType = data[i].Item5;
        GSA.gsaCache.Upsert(keyword, index, gwa, applicationId, gwaSetCommandType: gwaSetCommandType);
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

      var objects = new List<SpeckleObject>();

			// Read objects
			Status.ChangeStatus("Receiving streams");
			var errors = new ConcurrentBag<string>();
			Parallel.ForEach(Receivers, (kvp) =>
			{
				try
				{
					var receivedObjects = Receivers[kvp.Key].GetObjects().Distinct();
					double scaleFactor = (1.0).ConvertUnit(Receivers[kvp.Key].Units.ShortUnitName(), GSA.Settings.Units);
					foreach (var o in receivedObjects)
					{
						try
						{
							o.Scale(scaleFactor);
						}
						catch { }
					}
					objects.AddRange(receivedObjects);
				}
				catch {
					errors.Add("Unable to get stream " + kvp.Key);
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

    private void ProcessObjectsForLayer(GSATargetLayer layer, ref List<Type> traversedSerialisedTypes, ref List<SpeckleObject> objects, ref List<Dictionary<Type, List<object>>> senderDictionaries)
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
          var targetObjects = objects.Where(o => o.GetType() == valueType).ToList();

          var speckleTypeName = ((SpeckleObject)((IGSASpeckleContainer)dummyObject).Value).Type;

          for (int i = 0; i < targetObjects.Count(); i++)
          {
            var applicationId = targetObjects[i].ApplicationId;
            //Check if this application appears in the cache at all
            if (string.IsNullOrEmpty(applicationId)) continue;

            try
            {
              targetObjects[i] = ProcessObject(targetObjects[i], speckleTypeName, keyword, t, dummyObject, layer, ref traversedSerialisedTypes, ref senderDictionaries);
            }
            catch (Exception ex)
            {
              // TO DO:
              Status.AddMessage(ex.Message);
            }
            finally
            {
              objects.RemoveAll(x => targetObjects.Any(o => x.Type.Equals(o.Type) && x.ApplicationId.SidValueCompare(o.ApplicationId)));
            }
          }

          traversedTypes.Add(t);
        }

      } while (currentBatch.Count > 0);
    }

    private SpeckleObject ProcessObject(SpeckleObject targetObject, string speckleTypeName, string keyword, Type t, object dummyObject, GSATargetLayer layer, ref List<Type> traversedSerialisedTypes, ref List<Dictionary<Type, List<object>>> senderDictionaries)
    {
      //Check if merging needs to be considered
      if (GSA.gsaCache.Exists(keyword, targetObject.ApplicationId))
      {
        if (!traversedSerialisedTypes.Contains(t))
        {
          var readPrereqs = GetPrereqs(t, FilteredReadTypePrereqs[layer]);
          SerialiseUpdateCacheForGSAType(readPrereqs, keyword, t, dummyObject, ref traversedSerialisedTypes, ref senderDictionaries);
        }

        //If so but the type doesn't appear alongside it as one that was loaded, then load it now by calling ToSpeckle with a dummy version of the GSA corresponding type
        var existingList = GSA.gsaCache.GetSpeckleObjects(speckleTypeName, targetObject.ApplicationId);

        if (existingList != null && existingList.Count() > 0)
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
        ProcessDeserialiseReturnObject(gwaCommands[j], out keyword, out int index, out string gwa, out GwaSetCommandType gwaSetCommandType);
        var itemApplicationId = gwaCommands[j].ExtractApplicationId();

        GSA.gsaProxy.SetGWA(gwaCommands[j]);

        //Only cache the object against, the top-level GWA command, not the sub-commands
        GSA.gsaCache.Upsert(keyword, index, gwa, itemApplicationId, (itemApplicationId.SidValueCompare(targetObject.ApplicationId)) ? targetObject : null);
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
            //The SpeckleTypeName of this cache entry is automatically created by the assignment of the object
            GSA.gsaCache.AssignSpeckleObject(prereqKeyword, prereqSerialisedObjects[k].ApplicationId, prereqSerialisedObjects[k]);
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
        //The SpeckleTypeName of this cache entry is automatically created by the assignment of the object
        GSA.gsaCache.AssignSpeckleObject(keyword, serialisedObjects[j].ApplicationId, serialisedObjects[j]);
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

    private void ProcessDeserialiseReturnObject(object deserialiseReturnObject, out string keyword, out int index, out string gwa, out GwaSetCommandType gwaSetCommandType)
    {
      index = 0;
      keyword = "";
      gwa = "";
      gwaSetCommandType = GwaSetCommandType.Set;

      if (!(deserialiseReturnObject is string))
      {
        return;
      }

      var fullGwa = (string) deserialiseReturnObject;

      var pieces = fullGwa.ListSplit("\t").ToList();
      if (pieces.Count() < 2)
      {
        return;
      }

      if (pieces[0].StartsWith("set_at", StringComparison.InvariantCultureIgnoreCase))
      {
        gwaSetCommandType = GwaSetCommandType.SetAt;
        pieces.Remove(pieces[0]);
      }
      else if (pieces[0].StartsWith("set", StringComparison.InvariantCultureIgnoreCase))
      {
        gwaSetCommandType = GwaSetCommandType.Set;
        pieces.Remove(pieces[0]);
      }

      gwa = string.Join("\t", pieces);
      gwa.ExtractKeywordApplicationId(out keyword, out int? foundIndex, out string applicationId, out string gwaWithoutSet);
      if (foundIndex.HasValue)
      {
        index = foundIndex.Value;
      }
      return;
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
