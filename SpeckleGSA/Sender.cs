﻿using SpeckleCore;
using SpeckleGSAProxy;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA
{
  /// <summary>
  /// Responsible for reading and sending GSA models.
  /// </summary>
  public class Sender
  {
    public Dictionary<Type, List<object>> SenderObjects = new Dictionary<Type, List<object>>();
    public Dictionary<string, SpeckleGSASender> Senders = new Dictionary<string, SpeckleGSASender>();
    public Dictionary<Type, List<Type>> TypePrerequisites = new Dictionary<Type, List<Type>>();
    public Dictionary<Type, string> StreamMap = new Dictionary<Type, string>();

    public bool IsInit = false;
    public bool IsBusy = false;

    /// <summary>
    /// Initializes sender.
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

			
      // Run initialize sender method in interfacer
      var assemblies = SpeckleCore.SpeckleInitializer.GetAssemblies();
			/*
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
                gsaInterface.GetType().GetMethod("InitializeSender").Invoke(gsaInterface, new object[] { GSA.GSAObject });
              }
              catch
              {
                Status.AddError("Unable to access kit. Try updating Speckle installation to a later release.");
                throw new Exception("Unable to initialize");
              }
            }
          }
        }
      }
			*/
			((GSAInterfacer)GSA.Interfacer).InitializeSender();

      // Grab GSA interface type
      Type interfaceType = null;
      Type attributeType = null;
      foreach (var ass in assemblies)
      {
        var types = ass.GetTypes();
        foreach (var type in types)
        {
          if (type.FullName.Contains("IGSASpeckleContainer"))
            interfaceType = type;

          if (type.FullName.Contains("GSAObject"))
            attributeType = type;
        }
      }

      if (interfaceType == null)
        return;

      // Grab all GSA related object
      Status.ChangeStatus("Preparing to read GSA Objects");

      List<Type> objTypes = new List<Type>();
      foreach (var ass in assemblies)
      {
        var types = ass.GetTypes();
        foreach (var type in types)
          if (interfaceType.IsAssignableFrom(type) && type != interfaceType)
            objTypes.Add(type);
      }

      foreach (Type t in objTypes)
      {
        if (t.GetAttribute("Stream", attributeType) != null)
          StreamMap[t] = (string)t.GetAttribute("Stream", attributeType);
        else
          continue;

        if (t.GetAttribute("AnalysisLayer", attributeType) != null)
          if (GSA.Settings.TargetAnalysisLayer && !(bool)t.GetAttribute("AnalysisLayer", attributeType)) continue;

        if (t.GetAttribute("DesignLayer", attributeType) != null)
          if (GSA.Settings.TargetDesignLayer && !(bool)t.GetAttribute("DesignLayer", attributeType)) continue;

        if (t.GetAttribute("Stream", attributeType) != null)
          if (((Settings)GSA.Settings).SendOnlyResults && t.GetAttribute("Stream", attributeType) as string != "results") continue;

        List<Type> prereq = new List<Type>();
        if (t.GetAttribute("ReadPrerequisite", attributeType) != null)
          prereq = ((Type[])t.GetAttribute("ReadPrerequisite", attributeType)).ToList();

        TypePrerequisites[t] = prereq;
      }

      // Remove wrong layer objects from prerequisites
      foreach (Type t in objTypes)
      {
        if (t.GetAttribute("AnalysisLayer", attributeType) != null)
          if (GSA.Settings.TargetAnalysisLayer && !(bool)t.GetAttribute("AnalysisLayer", attributeType))
            foreach (KeyValuePair<Type, List<Type>> kvp in TypePrerequisites)
              kvp.Value.Remove(t);

        if (t.GetAttribute("DesignLayer", attributeType) != null)
          if (GSA.Settings.TargetDesignLayer && !(bool)t.GetAttribute("DesignLayer", attributeType))
            foreach (KeyValuePair<Type, List<Type>> kvp in TypePrerequisites)
              kvp.Value.Remove(t);

        if (t.GetAttribute("Stream", attributeType) != null)
          if (((Settings)GSA.Settings).SendOnlyResults && t.GetAttribute("Stream", attributeType) as string != "results")
            foreach (KeyValuePair<Type, List<Type>> kvp in TypePrerequisites)
              kvp.Value.Remove(t);
      }

      // Create the streams
      Status.ChangeStatus("Creating streams");

      List<string> streamNames = new List<string>();

      if (((Settings)GSA.Settings).SeparateStreams)
      {
        foreach (Type t in objTypes)
          streamNames.Add((string)t.GetAttribute("Stream", attributeType));
        streamNames = streamNames.Distinct().ToList();
      }
      else
        streamNames.Add("Full Model");

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
    }

    /// <summary>
    /// Trigger to update stream.
    /// </summary>
    public void Trigger()
    {
      if (IsBusy) return;
      if (!IsInit) return;

      IsBusy = true;
      GSA.UpdateUnits();

			/*
      // Run pre sending method and inject!!!!
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

                gsaInterface.GetType().GetMethod("PreSending").Invoke(gsaInterface, new object[] { });
              }

              if (type.GetProperties().Select(p => p.Name).Contains("GSASenderObjects"))
                type.GetProperty("GSASenderObjects").SetValue(null, SenderObjects);

              if (type.GetProperties().Select(p => p.Name).Contains("GSAUnits"))
                type.GetProperty("GSAUnits").SetValue(null, GSA.Settings.Units);

              if (GSA.Settings.TargetDesignLayer)
                if (type.GetProperties().Select(p => p.Name).Contains("GSATargetDesignLayer"))
                  type.GetProperty("GSATargetDesignLayer").SetValue(null, true);

              if (GSA.Settings.TargetAnalysisLayer)
                if (type.GetProperties().Select(p => p.Name).Contains("GSATargetAnalysisLayer"))
                  type.GetProperty("GSATargetAnalysisLayer").SetValue(null, true);

              if (Settings.SendResults)
              {
                if (type.GetProperties().Select(p => p.Name).Contains("GSAEmbedResults"))
                  type.GetProperty("GSAEmbedResults").SetValue(null, Settings.EmbedResults);

                if (type.GetProperties().Select(p => p.Name).Contains("GSANodalResults"))
                  type.GetProperty("GSANodalResults").SetValue(null, Settings.ChosenNodalResult);

                if (type.GetProperties().Select(p => p.Name).Contains("GSAElement1DResults"))
                  type.GetProperty("GSAElement1DResults").SetValue(null, Settings.ChosenElement1DResult);

                if (type.GetProperties().Select(p => p.Name).Contains("GSAElement2DResults"))
                  type.GetProperty("GSAElement2DResults").SetValue(null, Settings.ChosenElement2DResult);

                if (type.GetProperties().Select(p => p.Name).Contains("GSAMiscResults"))
                  type.GetProperty("GSAMiscResults").SetValue(null, Settings.ChosenMiscResult);
              }

              if (type.GetProperties().Select(p => p.Name).Contains("GSAResultCases"))
                type.GetProperty("GSAResultCases").SetValue(null, Settings.ResultCases);

              if (type.GetProperties().Select(p => p.Name).Contains("GSAResultInLocalAxis"))
                type.GetProperty("GSAResultInLocalAxis").SetValue(null, Settings.ResultInLocalAxis);

              if (type.GetProperties().Select(p => p.Name).Contains("GSAResult1DNumPosition"))
                type.GetProperty("GSAResult1DNumPosition").SetValue(null, Settings.Result1DNumPosition);
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
			((GSAInterfacer)GSA.Interfacer).PreSending();

			// Read objects
			List<Type> currentBatch = new List<Type>();
      List<Type> traversedTypes = new List<Type>();

      bool changeDetected = false;
      do
      {
        currentBatch = TypePrerequisites.Where(i => i.Value.Count(x => !traversedTypes.Contains(x)) == 0).Select(i => i.Key).ToList();
        currentBatch.RemoveAll(i => traversedTypes.Contains(i));

        foreach (Type t in currentBatch)
        {
          if (changeDetected) // This will skip the first read but it avoids flickering
            Status.ChangeStatus("Reading " + t.Name);

          object dummyObject = Activator.CreateInstance(t);
          var result = Converter.Serialise(dummyObject);

          if (!(result is SpeckleNull))
            changeDetected = true;

          traversedTypes.Add(t);
        }
      } while (currentBatch.Count > 0);

      if (!changeDetected)
      {
        Status.ChangeStatus("Finished sending", 100);
        IsBusy = false;
        return;
      }

      // Separate objects into streams
      Dictionary<string, Dictionary<string, List<object>>> streamBuckets = new Dictionary<string, Dictionary<string, List<object>>>();

      foreach (KeyValuePair<Type, List<object>> kvp in SenderObjects)
      {
        string targetStream = ((Settings)GSA.Settings).SeparateStreams ? StreamMap[kvp.Key] : "Full Model";

        foreach (object obj in kvp.Value)
        {
          if (((Settings)GSA.Settings).SendOnlyMeaningfulNodes)
          {
            if (obj.GetType().Name == "GSANode" && !(bool)obj.GetType().GetField("ForceSend").GetValue(obj))
              continue;
          }
          object insideVal = obj.GetType().GetProperty("Value").GetValue(obj);

          (insideVal as SpeckleObject).GenerateHash();

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

      foreach (KeyValuePair<string, Dictionary<string, List<object>>> kvp in streamBuckets)
      {
        Status.ChangeStatus("Sending to stream: " + Senders[kvp.Key].StreamID);

        string streamName = "";
				string title = ((GSAInterfacer)GSA.Interfacer).GetTitle();
				streamName = (((Settings)GSA.Settings).SeparateStreams) ? title + "." + kvp.Key : title;

        Senders[kvp.Key].UpdateName(streamName);
        Senders[kvp.Key].SendGSAObjects(kvp.Value);
      }

			/*
			// Run post sending method
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
                gsaInterface.GetType().GetMethod("PostSending").Invoke(gsaInterface, new object[] { });
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
			((GSAInterfacer)GSA.Interfacer).PostSending();

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
  }
}
