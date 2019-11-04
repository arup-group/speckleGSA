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
    
    //public List<KeyValuePair<Type, List<Type>>> TypeCastPriority = new List<KeyValuePair<Type, List<Type>>>();


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

			Status.AddMessage("Initialising receivers");

      //Get references to each assembly's sender objects dictionary
      var keywords = GetFilteredKeywords();

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
			//TypeCastPriority = FilteredTypePrerequisites.ToList();
			//TypeCastPriority.Sort((x, y) => x.Value.Count().CompareTo(y.Value.Count()));

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
          var keyword = "";
          try
          {
            keyword = dummyObject.GetAttribute("GSAKeyword").ToString();
          }
          catch { }
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
              //This check is intended to match objects in the GSA model that were loaded into the cache previously, not any other matches within this same
              //reception event
              if (GSA.gsaCache.Exists(keyword, applicationId, true, false))
              {
                if (!GSA.gsaCache.ContainsType(speckleTypeName))
                {
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

                  for (int j = 0; j < senderDictionaries.Count(); j++)
                  {
                    senderDictionaries[j].Clear();
                  }
                }

                //If so but the type doesn't appear alongside it as one that was loaded, then load it now by calling ToSpeckle with a dummy version of the GSA corresponding type
                var existingList = GSA.gsaCache.GetSpeckleObjects(speckleTypeName, applicationId);

                if (existingList != null && existingList.Count() > 0)
                {
                  //There should just be one instance of each Application ID per type
                  var existing = existingList.First();

                  //Merge objects to form the resulting one
                  targetObjects[i] = GSA.Merger.Merge(targetObjects[i], existing);
                }
              }
              else
              {
                //The application Id doesn't exist yet in the model - but the deserialisation will add it in
              }

              var gwaCommands = ((string)Converter.Deserialise(targetObjects[i])).Split(new[] { '\n' }).Where(c => c.Length > 0).ToList();

              for (int j = 0; j < gwaCommands.Count(); j++)
              {
                ProcessDeserialiseReturnObject(gwaCommands[j], out keyword, out int index, out string gwa, out GwaSetCommandType gwaSetCommandType);
                var itemApplicationId = gwaCommands[j].ExtractApplicationId();

                GSA.gsaProxy.SetGWA(gwaCommands[j]);

                //Only cache the object against, the top-level GWA command, not the sub-commands
                GSA.gsaCache.Upsert(keyword, index, gwa, itemApplicationId, (itemApplicationId == applicationId) ? targetObjects[i] : null); 
              }
            }
            catch (Exception ex)
            {
              // TO DO:
              Status.AddMessage(ex.Message);
            }
            finally
            {
              objects.RemoveAll(x => targetObjects.Any(o => x.Type.Equals(o.Type) && x.ApplicationId.Equals(o.ApplicationId)));
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

      var toBeDeletedGwa = GSA.gsaCache.GetToBeDeletedGwa();
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
			var gwaToDelete = GSA.gsaCache.GetCurrentSessionGwa();

      //TO DO: blank or delete each line

			GSA.gsaProxy.UpdateViews();
    }
	}
}
