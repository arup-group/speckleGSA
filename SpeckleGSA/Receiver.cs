using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SpeckleCore;
using SpeckleGSAInterfaces;

namespace SpeckleGSA
{
	/// <summary>
	/// Responsible for reading and writing Speckle streams.
	/// </summary>
	public class Receiver : BaseReceiverSender
	{
    public Dictionary<string, SpeckleGSAReceiver> Receivers = new Dictionary<string, SpeckleGSAReceiver>();
    
    public List<KeyValuePair<Type, List<Type>>> TypeCastPriority = new List<KeyValuePair<Type, List<Type>>>();


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

			var attributeType = typeof(GSAObject);

			//Filter out prerequisites that are excluded by the layer selection
			// Remove wrong layer objects from prerequisites
			var layerPrerequisites = GSA.WriteTypePrerequisites.Where(t => ObjectTypeMatchesLayer(t.Key));
			foreach (var kvp in layerPrerequisites)
			{
				FilteredTypePrerequisites[kvp.Key] = kvp.Value.Where(l => ObjectTypeMatchesLayer(l)).ToList();
			}

      //GSA.speckleObjectCache.ClearCache();

			Status.AddMessage("Initialising receivers");

			//GSA.Interfacer.InitializeReceiver();

      //Clear all indices first before creating a new baseline - this is to take in all the changes between the last reception and now
      //GSA.Interfacer.Indexer.Reset();

      //Get references to each assembly's sender objects dictionary
      var keywords = GetFilteredKeywords();

      /*
      foreach (string k in keywords)
      {
        GSA.Interfacer.RunGWACommand("GET_All\t" + k);
      }

      foreach (string k in keywords)
      {
        int highestRecord = GSA.Interfacer.HighestIndex(k);

        if (highestRecord > 0)
        {
          GSA.Interfacer.Indexer.ReserveIndices(k, Enumerable.Range(1, highestRecord));
        }
      }
      */

      GSA.gsaCache.Clear();
      var data = GSA.gsaProxy.GetGWAData(keywords);
      for (int i = 0; i < data.Count(); i++)
      {
        // <keyword, index, Application ID, GWA command (without SET or SET_AT), Set|Set At> tuples
        var keyword = data[i].Item1;
        var index = data[i].Item2;
        var applicationId = data[i].Item3;
        var gwa = data[i].Item4;
        var gwaSetCommandType = data[i].Item5;
        GSA.gsaCache.Upsert(keyword, index, gwa, applicationId, currentSession: false, gwaSetCommandType: gwaSetCommandType);
      }

      //GSA.Interfacer.Indexer.SetBaseline();

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

			// Generate which GSA object to cast for each type
			TypeCastPriority = FilteredTypePrerequisites.ToList();
			TypeCastPriority.Sort((x, y) => x.Value.Count().CompareTo(y.Value.Count()));

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

      //GSA.Settings.Units = GSA.Interfacer.GetUnits();
      GSA.Settings.Units = GSA.gsaProxy.GetUnits();

      //GSA.Interfacer.PreReceiving();

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

      //GSA.Interfacer.Indexer.ResetToBaseline();

      GSA.gsaCache.Snapshot();

      var senderDictionaries = new List<Dictionary<Type, List<object>>>();
      var gsaStaticObjects = GetAssembliesStaticTypes();
      foreach (var tuple in gsaStaticObjects)
      {
        senderDictionaries.Add(tuple.Item4);
      }

      // Write objects
      var currentBatch = new List<Type>();
      var traversedTypes = new List<Type>();
      do
      {
        currentBatch = FilteredTypePrerequisites.Where(i => i.Value.Count(x => !traversedTypes.Contains(x)) == 0).Select(i => i.Key).ToList();
        currentBatch.RemoveAll(i => traversedTypes.Contains(i));

        foreach (Type t in currentBatch)
        {
          Status.ChangeStatus("Writing " + t.Name);

          var dummyObject = Activator.CreateInstance(t);

          var valueType = t.GetProperty("Value").GetValue(dummyObject).GetType();
          var speckleType = ((object)((IGSASpeckleContainer)dummyObject).Value).GetType();

          var targetObjects = objects.Where(o => o.GetType() == valueType).ToList();

          for (int i = 0; i < targetObjects.Count(); i++)
          {
            var applicationId = targetObjects[i].ApplicationId;
            //Check if this application appears in the cache at all
            if (string.IsNullOrEmpty(applicationId)) continue;

            try
            {
              //if (GSA.Interfacer.ExistsInModel(applicationId))
              if (GSA.gsaCache.Exists(applicationId))
              {
                //if (!GSA.speckleObjectCache.ContainsType(speckleType))
                if (!GSA.gsaCache.ContainsType(speckleType))
                {
                  //This ensures the sender objects are filled within the assembly which contains the corresponding "ToSpeckle" method
                  var result = Converter.Serialise(dummyObject);
                  var serialisedObjects = CollateSerialisedObjects(senderDictionaries);

                  //For these serialised objects, there should already be a match in the cache, as it was read during initialisation and updated
                  //during previous reception Trigger calls

                  foreach (var serialisedType in serialisedObjects.Keys)
                  {
                    //GSA.speckleObjectCache.AddList(type, serialisedObjects[type]);
                    for (int j = 0; j < serialisedObjects[serialisedType].Count; j++)
                    {
                      GSA.gsaCache.AssignSpeckleObject(serialisedType, serialisedObjects[serialisedType][j].ApplicationId, serialisedObjects[serialisedType][j]);
                    }
                  }
                }

                //If so but the type doesn't appear alongside it as one that was loaded, then load it now by calling ToSpeckle with a dummy version of the GSA corresponding type
                //var existing = GSA.speckleObjectCache.GetCachedSpeckleObject(speckleType, applicationId);
                var existing = GSA.gsaCache.GetSpeckleObjects(speckleType, applicationId);

                if (existing != null && existing.Count() > 0)
                {
                  //Merge objects to form the resulting one
                  targetObjects[i] = GSA.Merger.Merge(targetObjects[i], existing.First());

                  //Add merged object back into the Speckle Objects cache
                  //GSA.speckleObjectCache.Add(targetObjects[i], speckleType);
                }
              }
              else
              {
                //The application Id doesn't exist yet in the model - but the deserialisation will add it in
              }

              var gwaCommand = (string)Converter.Deserialise(targetObjects[i]);

              ProcessDeserialiseReturnObject(gwaCommand, out string keyword, out int index, out string gwa, out GwaSetCommandType gwaSetCommandType);

              GSA.gsaProxy.SetGWA(gwaCommand);
              GSA.gsaCache.Upsert(keyword, index, gwa, applicationId, targetObjects[i]);
            }
            catch (Exception ex)
            {
              // TO DO:
            }
            finally
            {
              objects.RemoveAll(x => targetObjects.Any(o => x == o));
            }
          }

          traversedTypes.Add(t);
        }
      } while (currentBatch.Count > 0);

      // Write leftover
      if (objects.Count() > 0)
      {
        var targetObjects = objects.ToList();        
        for (int i = 0; i < objects.Count(); i++)
        {
          var applicationId = targetObjects[i].ApplicationId;
          if (string.IsNullOrEmpty(applicationId)) continue;

          var gwaCommand = (string)Converter.Deserialise(targetObjects[i]);
          ProcessDeserialiseReturnObject(gwaCommand, out string keyword, out int index, out string gwa, out GwaSetCommandType gwaSetCommandType);

          GSA.gsaProxy.SetGWA(gwaCommand);
          GSA.gsaCache.Upsert(keyword, index, gwa, applicationId, targetObjects[i]);
        }
      }
      //Converter.Deserialise(objects);

			// Run post receiving method
			//GSA.Interfacer.PostReceiving();

			GSA.gsaProxy.UpdateCasesAndTasks();
			GSA.gsaProxy.UpdateViews();

      IsBusy = false;
      Status.ChangeStatus("Finished receiving", 100);
    }

    private void ProcessDeserialiseReturnObject(object deserialiseReturnObject, out string keyword, out int index, out string gwa, out GwaSetCommandType gwaSetCommandType)
    {
      keyword = "";
      index = 0;
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

        int.TryParse(pieces[1], out index);

      gwa = string.Join("\t", pieces);

      return;
    }

    private List<SpeckleObject> ExtractSenderObjects(List<Dictionary<Type, List<object>>> dictionaryList, Type type, string applicationId)
    {
      var matchingList = new List<SpeckleObject>();
      for (int i = 0; i < dictionaryList.Count(); i++)
      {
        if (dictionaryList[i].ContainsKey(type))
        {
          var speckleObjects = dictionaryList[i][type].Select(o => (SpeckleObject)o).Where(so => so.ApplicationId == applicationId).ToList();
          if (speckleObjects.Count() > 0)
          {
            matchingList.AddRange(speckleObjects);
          }
        }
      }
      return matchingList;
    }

    private Dictionary<Type, List<SpeckleObject>> CollateSerialisedObjects(List<Dictionary<Type, List<object>>> dictionaryList)
    {
      var serialisedDict = new Dictionary<Type, List<SpeckleObject>>();
      for (int i = 0; i < dictionaryList.Count(); i++)
      {
        foreach (var key in dictionaryList[i].Keys)
        {
          if (!serialisedDict.ContainsKey(key))
          {
            serialisedDict[key] = new List<SpeckleObject>();
          }
          serialisedDict[key].AddRange(dictionaryList[i][key].Select(o => (SpeckleObject)((IGSASpeckleContainer)o).Value));
        }        
      }
      return serialisedDict;
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
			var gwaToDelete = GSA.gsaCache.GetCurrentSessionGwa();

      //TO DO: blank or delete each line

			GSA.gsaProxy.UpdateViews();
    }
	}
}
