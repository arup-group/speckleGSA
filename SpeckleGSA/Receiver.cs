using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SpeckleCore;
using SpeckleGSAInterfaces;
using SpeckleGSAProxy;

namespace SpeckleGSA
{
  /// <summary>
  /// Responsible for reading and writing Speckle streams.
  /// </summary>
  public class Receiver
  {
    public Dictionary<string, SpeckleGSAReceiver> Receivers = new Dictionary<string, SpeckleGSAReceiver>();
    public Dictionary<Type, List<Type>> TypePrerequisites = new Dictionary<Type, List<Type>>();
    public List<KeyValuePair<Type, List<Type>>> TypeCastPriority = new List<KeyValuePair<Type, List<Type>>>();

    public bool IsInit = false;
    public bool IsBusy = false;

		/// <summary>
		/// Initializes receiver.
		/// </summary>
		/// <param name="restApi">Server address</param>
		/// <param name="apiToken">API token of account</param>
		/// <returns>Task</returns>
		public async Task<List<string>> Initialize(string restApi, string apiToken)
		{
			if (IsInit) return null;

			if (!GSA.IsInit)
			{
				Status.AddError("GSA link not found.");
				return null;
			}

			var statusMessages = new List<string>();

			//var interfacePropertyToFind = typeof(ISpeckleInitializer).GetProperties().Where(p => p.DeclaringType == typeof(IGSAInterfacer));

			// Run initialize receiver method in interfacer
			var assemblies = SpeckleInitializer.GetAssemblies().Where(a => a.GetTypes().Any(t => t.GetInterfaces().Contains(typeof(ISpeckleInitializer))));

			foreach (var ass in assemblies)
			{
				//object gsaInterface;
				//object indexer;

				var types = ass.GetTypes();

				try
				{
					var gsaInterfaceType = types.FirstOrDefault(t => t.GetInterfaces().Contains(typeof(ISpeckleInitializer)) && t.GetProperties().Any(p => p.PropertyType == typeof(IGSAInterfacer)));
					if (gsaInterfaceType == null)
					{
						continue;
					}

					/*
					gsaInterface = gsaInterfaceType.GetProperty("GSA").GetValue(null);
					gsaInterface.GetType().GetMethod("InitializeSender").Invoke(gsaInterface, new object[] { GSA.GSAObject });

					indexer = gsaInterface.GetType().GetField("Indexer").GetValue(gsaInterface);
					*/
					((GSAInterfacer)GSA.Interfacer).InitializeReceiver();
					gsaInterfaceType.GetProperty("Interface").SetValue(null, GSA.Interfacer);
					gsaInterfaceType.GetProperty("Settings").SetValue(null, GSA.Settings);
				}
				catch
				{
					//Status.AddError("Unable to access kit. Try updating Speckle installation to a later release.");
					//throw new Exception("Unable to initialize");
					statusMessages.Add("Unable to access kit: " + ass.GetName());
					continue;
				}

				var attributeType = typeof(GSAObject);
				var interfaceType = typeof(IGSASpeckleContainer);
				/*
				// Grab GSA interface and attribute type
				var interfaceType = types.FirstOrDefault(t => t.FullName.Contains("IGSASpeckleContainer"));
				var attributeType = types.FirstOrDefault(t => t.FullName.Contains("GSAObject"));
				if (interfaceType == null || attributeType == null)
				{
					return;
				}

				// Grab all GSA related object
				var objTypesMatchingLayer = types.Where(t => interfaceType.IsAssignableFrom(t) && t != interfaceType && ObjectTypeMatchesLayer(t, attributeType));
				*/
				var objTypesMatchingLayer = types.Where(t => interfaceType.IsAssignableFrom(t) && t != interfaceType && ObjectTypeMatchesLayer(t, attributeType));

				//Pass one: for each type who has the correct layer attribute, record its prerequisites (some of which might not be the correct layer)
				foreach (var t in objTypesMatchingLayer)
				{
					TypePrerequisites[t] = (t.GetAttribute("WritePrerequisite", attributeType) == null)
						? new List<Type>()
						: ((Type[])t.GetAttribute("WritePrerequisite", attributeType)).Where(prereqT => ObjectTypeMatchesLayer(prereqT, attributeType)).ToList();
				}

				foreach (var kvp in TypePrerequisites)
				{
					try
					{
						var keywords = new List<string>() { (string)kvp.Key.GetAttribute("GSAKeyword", attributeType) };
						keywords.AddRange((string[])kvp.Key.GetAttribute("SubGSAKeywords", attributeType));

						foreach (string k in keywords)
						{
							//int highestRecord = (int)GSA.GSAObject.GwaCommand("HIGHEST\t" + k);
							int highestRecord = ((GSAInterfacer)GSA.Interfacer).HighestIndex(k);

							if (highestRecord > 0)
							{
								//indexer.GetType().GetMethod("ReserveIndices", new Type[] { typeof(string), typeof(List<int>) }).Invoke(indexer, new object[] { k, Enumerable.Range(1, highestRecord).ToList() });
								GSA.Interfacer.Indexer.ReserveIndices(k, Enumerable.Range(1, highestRecord));
							}
						}
					}
					catch { }
				}
				//indexer.GetType().GetMethod("SetBaseline").Invoke(indexer, null);
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
				TypeCastPriority = TypePrerequisites.ToList();
				TypeCastPriority.Sort((x, y) => x.Value.Count().CompareTo(y.Value.Count()));
			}

			Status.ChangeStatus("Ready to receive");
			IsInit = true;

			return statusMessages;
		}

    /// <summary>
    /// Trigger to update stream. Is called automatically when update-global ws message is received on stream.
    /// </summary>
    public async void Trigger(object sender, EventArgs e)
    {
      if (IsBusy) return;
      if (!IsInit) return;

      IsBusy = true;
      GSA.UpdateUnits();

			/*
      // Run pre receiving method and inject!!!!
      var assemblies = SpeckleCore.SpeckleInitializer.GetAssemblies();
      foreach (var ass in assemblies)
      {
        var types = ass.GetTypes();
        foreach (var type in types)
        {
          if (type.GetInterfaces().Contains(typeof(SpeckleCore.ISpeckleInitializer)))
          {
            try
            {
              if (type.GetProperties().Select(p => p.Name).Contains("GSA"))
              {
								var gsaInterface = type.GetProperty("GSA").GetValue(null);
								gsaInterface.GetType().GetMethod("PreReceiving").Invoke(gsaInterface, new object[] { });
							}

              if (type.GetProperties().Select(p => p.Name).Contains("GSAUnits"))
                type.GetProperty("GSAUnits").SetValue(null, GSA.Units);
								
              if (type.GetProperties().Select(p => p.Name).Contains("GSACoincidentNodeAllowance"))
                type.GetProperty("GSACoincidentNodeAllowance").SetValue(null, Settings.CoincidentNodeAllowance);
								
              if (Settings.TargetDesignLayer)
                if (type.GetProperties().Select(p => p.Name).Contains("GSATargetDesignLayer"))
                  type.GetProperty("GSATargetDesignLayer").SetValue(null, true);

              if (Settings.TargetAnalysisLayer)
                if (type.GetProperties().Select(p => p.Name).Contains("GSATargetAnalysisLayer"))
                  type.GetProperty("GSATargetAnalysisLayer").SetValue(null, true);
            }
            catch
            {
              Status.AddError("Unable to access kit. Try updating Speckle installation to a later release.");
              throw new Exception("Unable to trigger");
            }
          }
        }
      }
			*/

			((GSAInterfacer)GSA.Interfacer).PreReceiving();

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
				catch { errors.Add("Unable to get stream " + kvp.Key); }
			});

			if (errors.Count() > 0)
			{
				foreach (var error in errors)
				{
					Status.AddError(error);
				}
			}

			// Get Indexer
			/*
			object indexer = null;
      foreach (var ass in assemblies)
      {
        var types = ass.GetTypes();
        foreach (var type in types)
        {
          if (type.GetInterfaces().Contains(typeof(SpeckleCore.ISpeckleInitializer)))
          {
            if (type.GetProperties().Select(p => p.Name).Contains("GSA"))
            {
              try
              {
                var gsaInterface = type.GetProperty("GSA").GetValue(null);
                indexer = gsaInterface.GetType().GetField("Indexer").GetValue(gsaInterface);
              }
              catch
              {
                Status.AddError("Unable to access kit. Try updating Speckle installation to a later release.");
                throw new Exception("Unable to trigger");
              }
            }
          }
        }
      }
			*/

			// Add existing GSA file objects to counters
			//indexer.GetType().GetMethod("ResetToBaseline").Invoke(indexer, new object[] { });
			GSA.Interfacer.Indexer.ResetToBaseline();

			// Write objects
			List<Type> currentBatch = new List<Type>();
      List<Type> traversedTypes = new List<Type>();
      do
      {
        currentBatch = TypePrerequisites.Where(i => i.Value.Count(x => !traversedTypes.Contains(x)) == 0).Select(i => i.Key).ToList();
        currentBatch.RemoveAll(i => traversedTypes.Contains(i));

        foreach (Type t in currentBatch)
        {
          Status.ChangeStatus("Writing " + t.Name);

          object dummyObject = Activator.CreateInstance(t);

          Type valueType = t.GetProperty("Value").GetValue(dummyObject).GetType();
          var targetObjects = objects.Where(o => o.GetType() == valueType);
          Converter.Deserialise(targetObjects);

          objects.RemoveAll(x => targetObjects.Any(o => x == o));

          traversedTypes.Add(t);
        }
      } while (currentBatch.Count > 0);

      // Write leftover
      Converter.Deserialise(objects);

			/*
      // Run post receiving method
      foreach (var ass in assemblies)
      {
        var types = ass.GetTypes();
        foreach (var type in types)
        {
          if (type.GetInterfaces().Contains(typeof(SpeckleCore.ISpeckleInitializer)))
          {
            if (type.GetProperties().Select(p => p.Name).Contains("GSA"))
            {
              try
              {
                var gsaInterface = type.GetProperty("GSA").GetValue(null);
                gsaInterface.GetType().GetMethod("PostReceiving").Invoke(gsaInterface, new object[] { });
              }
              catch
              {
                Status.AddError("Unable to access kit. Try updating Speckle installation to a later release.");
                throw new Exception("Unable to trigger");
              }
            }
          }
        }
      }
			*/

			((GSAInterfacer)GSA.Interfacer).PostReceiving();

			GSA.UpdateCasesAndTasks();
      ((GSAInterfacer)GSA.Interfacer).UpdateViews();

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
      var assemblies = SpeckleCore.SpeckleInitializer.GetAssemblies();
      foreach (var ass in assemblies)
      {
        var types = ass.GetTypes();
        foreach (var type in types)
        {
          if (type.GetInterfaces().Contains(typeof(SpeckleCore.ISpeckleInitializer)))
          {
            if (type.GetProperties().Select(p => p.Name).Contains("GSA"))
            {
              try
              {
                var gsaInterface = type.GetProperty("GSA").GetValue(null);
                gsaInterface.GetType().GetMethod("DeleteSpeckleObjects").Invoke(gsaInterface, new object[0]);
              }
              catch
              {
                Status.AddError("Unable to access kit. Try updating Speckle installation to a later release.");
                throw new Exception("Unable to delete expired objects");
              }
            }
          }
        }
      }

      ((GSAInterfacer)GSA.Interfacer).UpdateViews();
    }

		private bool ObjectTypeMatchesLayer(Type t, Type attributeType)
		{
			var analysisLayerAttribute = t.GetAttribute("AnalysisLayer", attributeType);
			var designLayerAttribute = t.GetAttribute("DesignLayer", attributeType);

			//If an object type has a layer attribute exists and its boolean value doesn't match the settings target layer, then it doesn't match.  This could be reviewed and simplified.
			if ((analysisLayerAttribute != null && GSA.Settings.TargetAnalysisLayer && !(bool)analysisLayerAttribute)
				|| (designLayerAttribute != null && GSA.Settings.TargetDesignLayer && !(bool)designLayerAttribute))
			{
				return false;
			}
			return true;
		}
	}
}
