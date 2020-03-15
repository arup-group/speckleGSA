using SpeckleCore;
using SpeckleGSAInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SpeckleGSA
{
  /// <summary>
  /// Responsible for reading and sending GSA models.
  /// </summary>
  public class Sender : BaseReceiverSender
  {
    public Dictionary<Type, List<object>> SenderObjects = new Dictionary<Type, List<object>>();
    public Dictionary<string, SpeckleGSASender> Senders = new Dictionary<string, SpeckleGSASender>();
    public Dictionary<Type, string> StreamMap = new Dictionary<Type, string>();

    private Dictionary<Type, List<Type>> FilteredReadTypePrereqs = new Dictionary<Type, List<Type>>();

    /// <summary>
    /// Initializes sender.
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

      //Filter out Prereqs that are excluded by the layer selection
      // Remove wrong layer objects from Prereqs
      if (GSA.Settings.SendOnlyResults)
			{
				var stream = GSA.Settings.SendOnlyResults ? "results" : null;
				var streamLayerPrereqs = GSA.ReadTypePrereqs.Where(t => (string)t.Key.GetAttribute("Stream") == stream && ObjectTypeMatchesLayer(t.Key, GSA.Settings.TargetLayer));
				foreach (var kvp in streamLayerPrereqs)
				{
					FilteredReadTypePrereqs[kvp.Key] = kvp.Value.Where(l => ObjectTypeMatchesLayer(l, GSA.Settings.TargetLayer)
						&& (string)l.GetAttribute("Stream") == stream).ToList();
				}
			}
			else
			{
				var layerPrereqs = GSA.ReadTypePrereqs.Where(t => ObjectTypeMatchesLayer(t.Key, GSA.Settings.TargetLayer));
				foreach (var kvp in layerPrereqs)
				{
					FilteredReadTypePrereqs[kvp.Key] = kvp.Value.Where(l => ObjectTypeMatchesLayer(l, GSA.Settings.TargetLayer)).ToList();
				}
			}

      var keywords = GetFilteredKeywords();

      Status.ChangeStatus("Reading GSA data into cache");

      int numRowsUpdated = 0;
      var updatedCache = await Task.Run(() => UpdateCache(keywords, out numRowsUpdated));
      if (!updatedCache)
      {
        Status.AddError("Error in communicating GSA - please check if the GSA file has been closed down");
        return statusMessages;
      }

      Status.AddMessage("Read " + numRowsUpdated + " GWA lines across " + keywords.Count() + " keywords into cache");

      // Grab GSA interface type
      var interfaceType = typeof(IGSASpeckleContainer);

      // Grab all GSA related object
      Status.ChangeStatus("Preparing to read GSA Objects");

			// Run initialize sender method in interfacer
			var assemblies = SpeckleInitializer.GetAssemblies();
			var objTypes = new List<Type>();
      foreach (var ass in assemblies)
      {
        var types = ass.GetTypes();
				objTypes.AddRange(types.Where(t => interfaceType.IsAssignableFrom(t) && t != interfaceType));
      }

			foreach (Type t in objTypes)
			{
				var streamAttribute = t.GetAttribute("Stream");
				if (streamAttribute != null)
				{
					StreamMap[t] = (string)streamAttribute;
				}
			}

      // Create the streams
      Status.ChangeStatus("Creating streams");

			var streamNames = (GSA.Settings.SeparateStreams) ? objTypes.Select(t => (string)t.GetAttribute("Stream")).Distinct().ToList() : new List<string>() { "Full Model" };

      foreach (string streamName in streamNames)
      {
        Senders[streamName] = new SpeckleGSASender(restApi, apiToken);

        if (!GSA.Senders.ContainsKey(streamName))
        {
          Status.AddMessage(streamName + " sender not initialized. Creating new " + streamName + " sender.");
          await Senders[streamName].InitializeSender(null, null, streamName);
          GSA.Senders[streamName] = new Tuple<string, string> (Senders[streamName].StreamID, Senders[streamName].ClientID);
        }
        else
          await Senders[streamName].InitializeSender(GSA.Senders[streamName].Item1, GSA.Senders[streamName].Item2, streamName);
      }

      Status.ChangeStatus("Ready to stream");
      IsInit = true;

			return statusMessages;
    }

    /// <summary>
    /// Trigger to update stream.
    /// </summary>
    public void Trigger()
    {
      if ((IsBusy) || (!IsInit)) return;

      IsBusy = true;
			GSA.Settings.Units = GSA.gsaProxy.GetUnits();

      var gsaStaticObjects = GetAssembliesStaticTypes();

      //Clear previously-sent objects
      gsaStaticObjects.ForEach(dict => dict.Clear());

      // Read objects
      var currentBatch = new List<Type>();
      var traversedTypes = new List<Type>();

      bool changeDetected = false;
      do
      {
        currentBatch = FilteredReadTypePrereqs.Where(i => i.Value.Count(x => !traversedTypes.Contains(x)) == 0).Select(i => i.Key).ToList();
        currentBatch.RemoveAll(i => traversedTypes.Contains(i));

        foreach (var t in currentBatch)
        {
          if (changeDetected) // This will skip the first read but it avoids flickering
          {
            Status.ChangeStatus("Reading " + t.Name);
          }

					//The SpeckleStructural kit actually does serialisation (calling of ToSpeckle()) by type, not individual object.  This is due to
					//GSA offering bulk GET based on type.
					//So if the ToSpeckle() call for the type is successful it does all the objects of that type and returns SpeckleObject.
					//If there is an error, then the SpeckleCore Converter.Serialise will return SpeckleNull.  
					//The converted objects are stored in the kit in its own collection, not returned by Serialise() here.
          var dummyObject = Activator.CreateInstance(t);
          var result = Converter.Serialise(dummyObject);

					if (!(result is SpeckleNull))
					{
						changeDetected = true;
					}

					traversedTypes.Add(t);
        }
      } while (currentBatch.Count > 0);

			foreach (var dict in gsaStaticObjects)
			{
				//this item is the list of sender objects by type
				//var typeSenderObjects = tuple.Item4;
				foreach (var t in dict.Keys)
				{
					if (!SenderObjects.ContainsKey(t))
					{
						SenderObjects[t] = new List<object>();
					}
					SenderObjects[t].AddRange(dict[t]);
				}
			}

			if (!changeDetected)
      {
        Status.ChangeStatus("Finished sending", 100);
        IsBusy = false;
        return;
      }

      // Separate objects into streams
      var streamBuckets = new Dictionary<string, Dictionary<string, List<object>>>();

      foreach (var kvp in SenderObjects)
      {
        var targetStream = GSA.Settings.SeparateStreams ? StreamMap[kvp.Key] : "Full Model";

        foreach (object obj in kvp.Value)
        {
          if (GSA.Settings.SendOnlyMeaningfulNodes)
          {
            if (obj.GetType().Name == "GSANode" && !(bool)obj.GetType().GetField("ForceSend").GetValue(obj))
              continue;
          }
          object insideVal = obj.GetType().GetProperty("Value").GetValue(obj);

          ((SpeckleObject)insideVal).GenerateHash();

          if (!streamBuckets.ContainsKey(targetStream))
            streamBuckets[targetStream] = new Dictionary<string, List<object>>();

          if (streamBuckets[targetStream].ContainsKey(insideVal.GetType().Name))
            streamBuckets[targetStream][insideVal.GetType().Name].Add(insideVal);
          else
            streamBuckets[targetStream][insideVal.GetType().Name] = new List<object>() { insideVal };
        }
      }

      // Send package
      Status.ChangeStatus("Sending to Server");

      foreach (var kvp in streamBuckets)
      {
        Status.ChangeStatus("Sending to stream: " + Senders[kvp.Key].StreamID);

        var streamName = "";
				var title = GSA.gsaProxy.GetTitle();
				streamName = GSA.Settings.SeparateStreams ? title + "." + kvp.Key : title;

        Senders[kvp.Key].UpdateName(streamName);
        Senders[kvp.Key].SendGSAObjects(kvp.Value);
      }

			IsBusy = false;
      Status.ChangeStatus("Finished sending", 100);
    }

    /// <summary>
    /// Dispose receiver.
    /// </summary>
    public void Dispose()
    {
      foreach (KeyValuePair<string, Tuple<string, string>> kvp in GSA.Senders)
        Senders[kvp.Key].Dispose();
    }

    protected List<string> GetFilteredKeywords()
    {
      var keywords = new List<string>();
      keywords.AddRange(GetFilteredKeywords(FilteredReadTypePrereqs));

      return keywords;
    }

    private bool UpdateCache(List<string> keywords, out int numUpdated)
    {
      GSA.gsaCache.Clear();
      try
      {
        var data = GSA.gsaProxy.GetGwaData(keywords);
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
        numUpdated = data.Count();
        return true;
      }
      catch
      {
        numUpdated = 0;
        return false;
      }

    }
  }
}
