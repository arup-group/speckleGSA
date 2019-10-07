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

			Status.AddMessage("Initialising receivers");

			GSA.Interfacer.InitializeReceiver();


			foreach (var kvp in FilteredTypePrerequisites)
			{
				try
				{
					var keywords = new List<string>() { (string)kvp.Key.GetAttribute("GSAKeyword") };
					keywords.AddRange((string[])kvp.Key.GetAttribute("SubGSAKeywords"));

					foreach (string k in keywords)
					{
						int highestRecord = GSA.Interfacer.HighestIndex(k);

						if (highestRecord > 0)
						{
							GSA.Interfacer.Indexer.ReserveIndices(k, Enumerable.Range(1, highestRecord));
						}
					}
				}
				catch { }
			}
			GSA.Interfacer.Indexer.SetBaseline();

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

			GSA.Settings.Units = GSA.Interfacer.GetUnits();

			GSA.Interfacer.PreReceiving();

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

			GSA.Interfacer.Indexer.ResetToBaseline();

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
          var targetObjects = objects.Where(o => o.GetType() == valueType);

          Converter.Deserialise(targetObjects);

          objects.RemoveAll(x => targetObjects.Any(o => x == o));

          traversedTypes.Add(t);
        }
      } while (currentBatch.Count > 0);

      // Write leftover
      Converter.Deserialise(objects);

			// Run post receiving method
			GSA.Interfacer.PostReceiving();

			GSA.UpdateCasesAndTasks();
			GSA.Interfacer.UpdateViews();

      IsBusy = false;
      Status.ChangeStatus("Finished receiving", 100);
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
			GSA.Interfacer.DeleteSpeckleObjects();

			GSA.Interfacer.UpdateViews();
    }
	}
}
