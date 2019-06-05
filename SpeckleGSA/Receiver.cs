using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpeckleCore;

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
    public async Task Initialize(string restApi, string apiToken)
    {
      if (IsInit) return;

      if (!GSA.IsInit)
      {
        Status.AddError("GSA link not found.");
        return;
      }

      // Full clear cache
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
              var gsaInterface = type.GetProperty("GSA").GetValue(null);
              gsaInterface.GetType().GetMethod("FullClearCache").Invoke(gsaInterface, new object[0]);
            }
          }
        }
      }

      // Grab GSA interface and attribute type
      Type interfaceType = null;
      Type attributeType = null;
      foreach (var ass in assemblies)
      {
        var types = ass.GetTypes();
        foreach (var type in types)
        {
          if (type.FullName.Contains("IGSASpeckleContainer"))
          {
            interfaceType = type;
          }

          if (type.FullName.Contains("GSAObject"))
          {
            attributeType = type;
          }
        }
      }

      if (interfaceType == null)
        return;

      // Grab all GSA related object
      List<Type> objTypes = new List<Type>();
      foreach (var ass in assemblies)
      {
        var types = ass.GetTypes();
        foreach (var type in types)
        {
          if (interfaceType.IsAssignableFrom(type) && type != interfaceType)
          {
            objTypes.Add(type);
          }
        }
      }

      foreach (Type t in objTypes)
      {
        if (t.GetAttribute("AnalysisLayer", attributeType) != null)
          if (Settings.TargetAnalysisLayer && !(bool)t.GetAttribute("AnalysisLayer", attributeType)) continue;

        if (t.GetAttribute("DesignLayer", attributeType) != null)
          if (Settings.TargetDesignLayer && !(bool)t.GetAttribute("DesignLayer", attributeType)) continue;

        List<Type> prereq = new List<Type>();
        if (t.GetAttribute("WritePrerequisite", attributeType) != null)
          prereq = ((Type[])t.GetAttribute("WritePrerequisite", attributeType)).ToList();

        TypePrerequisites[t] = prereq;
      }

      // Remove wrong layer objects from prerequisites
      foreach (Type t in objTypes)
      {
        if (t.GetAttribute("AnalysisLayer", attributeType) != null)
          if (Settings.TargetAnalysisLayer && !(bool)t.GetAttribute("AnalysisLayer", attributeType))
            foreach (KeyValuePair<Type, List<Type>> kvp in TypePrerequisites)
              kvp.Value.Remove(t);

        if (t.GetAttribute("DesignLayer", attributeType) != null)
          if (Settings.TargetDesignLayer && !(bool)t.GetAttribute("DesignLayer", attributeType))
            foreach (KeyValuePair<Type, List<Type>> kvp in TypePrerequisites)
              kvp.Value.Remove(t);
      }

      // Generate which GSA object to cast for each type
      TypeCastPriority = TypePrerequisites.ToList();
      TypeCastPriority.Sort((x, y) => x.Value.Count().CompareTo(y.Value.Count()));

      // Get Indexer
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
              var gsaInterface = type.GetProperty("GSA").GetValue(null);

              indexer = gsaInterface.GetType().GetField("Indexer").GetValue(gsaInterface);
            }
          }
        }
      }


      // Add existing GSA file objects to counters
      foreach (KeyValuePair<Type, List<Type>> kvp in TypePrerequisites)
      {
        try
        {
          List<string> keywords = new List<string>() { (string)kvp.Key.GetAttribute("GSAKeyword", attributeType) };
          keywords.AddRange((string[])kvp.Key.GetAttribute("SubGSAKeywords", attributeType));

          foreach (string k in keywords)
          {
            int highestRecord = (int)GSA.GSAObject.GwaCommand("HIGHEST," + k);

            if (highestRecord > 0)
              indexer.GetType().GetMethod("ReserveIndices", new Type[] { typeof(string), typeof(List<int>) }).Invoke(indexer, new object[] { k, Enumerable.Range(1, highestRecord).ToList() });
          }
        }
        catch { }
      }
      indexer.GetType().GetMethod("SetBaseline").Invoke(indexer, new object[] { });

      // Create receivers
      Status.ChangeStatus("Accessing stream");

      foreach (string streamID in GSA.Receivers)
      {
        if (streamID == "")
          Status.AddMessage("No " + streamID + " stream specified.");
        else
        {
          Status.AddMessage("Creating receiver " + streamID);
          Receivers[streamID] = new SpeckleGSAReceiver(restApi, apiToken);
          await Receivers[streamID].InitializeReceiver(streamID);
          Receivers[streamID].UpdateGlobalTrigger += Trigger;
        }
      }

      Status.ChangeStatus("Ready to receive");
      IsInit = true;
    }

    /// <summary>
    /// Trigger to update stream. Is called automatically when update-global ws message is received on stream.
    /// </summary>
    public void Trigger(object sender, EventArgs e)
    {
      if (IsBusy) return;
      if (!IsInit) return;

      IsBusy = true;
      GSA.UpdateUnits();

      // Inject!!!!
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
              var gsaInterface = type.GetProperty("GSA").GetValue(null);

              gsaInterface.GetType().GetField("GSAObject").SetValue(gsaInterface, GSA.GSAObject);
              gsaInterface.GetType().GetField("CoincidentNodeAllowance").SetValue(gsaInterface, Settings.CoincidentNodeAllowance);
              gsaInterface.GetType().GetMethod("ClearCache").Invoke(gsaInterface, new object[] { });
            }

            if (type.GetProperties().Select(p => p.Name).Contains("GSAUnits"))
            {
              type.GetProperty("GSAUnits").SetValue(null, GSA.Units);
            }

            if (Settings.TargetDesignLayer)
            {
              if (type.GetProperties().Select(p => p.Name).Contains("GSATargetDesignLayer"))
                type.GetProperty("GSATargetDesignLayer").SetValue(null, true);
            }

            if (Settings.TargetAnalysisLayer)
            {
              if (type.GetProperties().Select(p => p.Name).Contains("GSATargetAnalysisLayer"))
                type.GetProperty("GSATargetAnalysisLayer").SetValue(null, true);
            }
          }
        }
      }

      List<SpeckleObject> objects = new List<SpeckleObject>();

      // Read objects
      Status.ChangeStatus("Receiving stream");
      foreach (KeyValuePair<string, SpeckleGSAReceiver> kvp in Receivers)
      {
        try
        {
          var receivedObjects = Receivers[kvp.Key].GetObjects();

          double scaleFactor = (1.0).ConvertUnit(Receivers[kvp.Key].Units.ShortUnitName(), GSA.Units);

          foreach (SpeckleObject o in receivedObjects)
            o.Scale(scaleFactor);

          objects.AddRange(receivedObjects);
        }
        catch { Status.AddError("Unable to get stream " + kvp.Key); }
      }


      // Get Indexer
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
              var gsaInterface = type.GetProperty("GSA").GetValue(null);

              indexer = gsaInterface.GetType().GetField("Indexer").GetValue(gsaInterface);
            }
          }
        }
      }

      // Add existing GSA file objects to counters
      indexer.GetType().GetMethod("ResetToBaseline").Invoke(indexer, new object[] { });

      // Write objects
      Status.ChangeStatus("Writing objects");
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

      // Blank files
      foreach (var ass in assemblies)
      {
        var types = ass.GetTypes();
        foreach (var type in types)
        {
          if (type.GetInterfaces().Contains(typeof(SpeckleCore.ISpeckleInitializer)))
          {
            if (type.GetProperties().Select(p => p.Name).Contains("GSA"))
            {
              var gsaInterface = type.GetProperty("GSA").GetValue(null);

              gsaInterface.GetType().GetMethod("BlankDepreciatedGWASetCommands").Invoke(gsaInterface, new object[] { });
            }
          }
        }
      }

      GSA.UpdateCasesAndTasks();
      GSA.UpdateViews();

      IsBusy = false;
      Status.ChangeStatus("Finished receiving", 100);
    }

    /// <summary>
    /// Dispose receiver.
    /// </summary>
    public void Dispose()
    {
      foreach (string streamID in GSA.Receivers)
      {
        Receivers[streamID].UpdateGlobalTrigger -= Trigger;
        Receivers[streamID].Dispose();
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
              var gsaInterface = type.GetProperty("GSA").GetValue(null);
              gsaInterface.GetType().GetMethod("DeleteSpeckleObjects").Invoke(gsaInterface, new object[0]);
            }
          }
        }
      }

      GSA.UpdateViews();
    }
  }
}
