using SpeckleCore;
using SpeckleGSAInterfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SpeckleGSA
{
  /// <summary>
  /// Responsible for reading and sending GSA models.
  /// </summary>
  public class SenderCoordinator
  {
    public bool IsInit = false;
    public bool IsBusy = false;

    private Dictionary<Type, string> StreamMap;

    private Dictionary<string, SpeckleInterface.IStreamSender> Senders;

    //These need to be accessed using a lock
    private object traversedSerialisedLock = new object();
    private readonly List<Type> traversedSerialisedTypes = new List<Type>();

    private IProgress<MessageEventArgs> loggingProgress;
    private IProgress<string> statusProgress;

    private ProgressEstimator progressEstimator;


    /// <summary>
    /// Initializes sender.
    /// </summary>
    /// <param name="restApi">Server address</param>
    /// <param name="apiToken">API token of account</param>
    /// <returns>Task</returns>
    public void Initialize(string restApi, string apiToken, Func<string, string, SpeckleInterface.IStreamSender> gsaSenderCreator,
      IProgress<MessageEventArgs> loggingProgress, IProgress<string> statusProgress, IProgress<double> percentageProgress)
    {
      this.loggingProgress = loggingProgress;
      this.statusProgress = statusProgress;

      this.progressEstimator = new ProgressEstimator(percentageProgress, WorkPhase.CacheRead, 3, WorkPhase.CacheUpdate, 1, WorkPhase.Conversion, 20, WorkPhase.ApiCalls, 3);

      if (IsInit) return;

      if (!GSA.IsInit)
      {
        this.loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Error, "GSA link not found."));
        return;
      }

      var startTime = DateTime.Now;
      statusProgress.Report("Reading GSA data into cache");

      //Update cache
      var updatedCache = UpdateCache();
      if (!updatedCache)
      {
        this.loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Error, "Error in communicating GSA - please check if the GSA file has been closed down"));
        return;
      }

      // Grab all GSA related object
      statusProgress.Report("Preparing to read GSA Objects");

      // Run initialize sender method in interfacer
      var objTypes = GetAssembliesTypes();
      var streamNames = GetStreamNames(objTypes);

      StreamMap = new Dictionary<Type, string>();
      foreach (Type t in objTypes)
      {
        var streamAttribute = t.GetAttribute("Stream");
        if (streamAttribute != null)
        {
          StreamMap[t] = (string)streamAttribute;
        }
      }

      // Create the streams
      statusProgress.Report("Creating streams");

      // The units are key for the stream
      GSA.GsaApp.gsaSettings.Units = GSA.GsaApp.gsaProxy.GetUnits();

      CreateInitialiseSenders(streamNames, gsaSenderCreator, restApi, apiToken);

      TimeSpan duration = DateTime.Now - startTime;
      this.loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Information, "Duration of initialisation: " + duration.ToString(@"hh\:mm\:ss")));
      this.loggingProgress.Report(new MessageEventArgs(MessageIntent.Telemetry, MessageLevel.Information, "send", "initialisation", "duration", duration.ToString(@"hh\:mm\:ss")));
      statusProgress.Report("Ready to stream");
      IsInit = true;

      return;
    }

    /// <summary>
    /// Trigger to update stream.
    /// </summary>
    public void Trigger()
    {
      if ((IsBusy) || (!IsInit)) return;

      var startTime = DateTime.Now;

      IsBusy = true;
			GSA.GsaApp.gsaSettings.Units = GSA.GsaApp.gsaProxy.GetUnits();

      //Clear previously-sent objects
      GSA.ClearSenderDictionaries();

      var changeDetected = ProcessTxObjects();

      if (!changeDetected)
      {
        statusProgress.Report("Finished sending");
        IsBusy = false;
        return;
      }
        
      // Separate objects into streams
      var streamBuckets = CreateStreamBuckets();

      TimeSpan duration = DateTime.Now - startTime;
      loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Information, "Duration of conversion to Speckle: " + duration.ToString(@"hh\:mm\:ss")));
      loggingProgress.Report(new MessageEventArgs(MessageIntent.Telemetry, MessageLevel.Information, "send", "conversion", "duration", duration.ToString(@"hh\:mm\:ss")));
      startTime = DateTime.Now;

      // Send package
      statusProgress.Report("Sending to Server");

      int numErrors = 0;
      var sendingTasks = new List<Task>();
      foreach (var k in streamBuckets.Keys.Where(k => Senders.ContainsKey(k)))
      {
        statusProgress.Report("Sending to stream: " + Senders[k].StreamId);

        var title = GSA.GsaApp.gsaProxy.GetTitle();
        var streamName = GSA.GsaApp.gsaSettings.SeparateStreams ? title + "." + k : title;

        Senders[k].UpdateName(streamName);
        numErrors += Senders[k].SendObjects(streamBuckets[k]);
        //GSA.GsaApp.gsaMessenger.Trigger();
      }

      if (numErrors > 0)
      {
        loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Error,
          numErrors + " errors found with sending to the server. Refer to the .txt log file(s) in " + AppDomain.CurrentDomain.BaseDirectory));
      }

      duration = DateTime.Now - startTime;
      loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Information, "Duration of sending to Speckle: " + duration.ToString(@"hh\:mm\:ss")));
      loggingProgress.Report(new MessageEventArgs(MessageIntent.Telemetry, MessageLevel.Information, "send", "sending", "duration", duration.ToString(@"hh\:mm\:ss")));

      IsBusy = false;
      statusProgress.Report("Finished sending");
    }

    private bool ProcessTxObjects()
    {
      int numErrors = 0;

      var txTypePrereqs = GSA.TxTypeDependencies;

      // Read objects
      var batch = new List<Type>();

      bool anyChangeDetected = false;

      do
      {
        batch = new List<Type>();
        lock (traversedSerialisedLock)
        {
          batch = txTypePrereqs.Where(i => i.Value.Count(x => !traversedSerialisedTypes.Contains(x)) == 0).Select(i => i.Key).ToList();
          batch.RemoveAll(i => traversedSerialisedTypes.Contains(i));
        }

        ProcessTypeBatch(batch, out bool changeDetected);
        if (changeDetected)
        {
          anyChangeDetected = true;
        }
        //numErrors += batchErrors;

      } while (batch.Count > 0);

      if (numErrors > 0)
      {
        loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Error,
          numErrors + " processing errors found. Refer to the .txt log file(s) in " + AppDomain.CurrentDomain.BaseDirectory));
      }

      return anyChangeDetected;
    }

    private void ProcessTypeBatch(List<Type> batch, out bool changeDetected)
    {
      //This method assumes it's not run in parallel
      //GSA.GsaApp.gsaMessenger.ResetLoggedMessageCount();

#if DEBUG
      changeDetected = false;

      foreach (var t in batch)
      {
        SerialiseType(t, ref changeDetected);

        if (changeDetected) // This will skip the first read but it avoids flickering
        {
          statusProgress.Report("Reading " + t.Name);
        }

        //Nodes are a special case because they're the main type of records that would be cached but not actually sent
        var numObjects = t.GetProperties().Any(p => p.Name.ToLower().Contains("forcesend")) ? 0 : GSA.SenderDictionaries.Sum(d => d.Count(t));
        progressEstimator.AppendCurrent(WorkPhase.Conversion, numObjects);
      }
#else
      var changeLock = new object();
      var parallelChangeDetected = false;
      Parallel.ForEach(batch, t =>
      {
        bool changed = false;
        SerialiseType(t, ref changed);

        if (changed) // This will skip the first read but it avoids flickering
        {
          lock (changeLock)
          {
            parallelChangeDetected = true;
          }
          statusProgress.Report("Reading " + t.Name);          
        }
      }      
      );
      foreach (var t in batch)
      {
        progressEstimator.AppendCurrent(WorkPhase.Conversion, GSA.SenderDictionaries.Sum(d => d.Count(t)));
      }
      changeDetected = parallelChangeDetected;
#endif

      //Process any cached messages from the conversion code
      //GSA.GsaApp.gsaMessenger.Trigger();

      lock (traversedSerialisedLock)
      {
        traversedSerialisedTypes.AddRange(batch);
      }

      //return GSA.GsaApp.gsaMessenger.LoggedMessageCount;
    }

    private void SerialiseType(Type t, ref bool changeDetected)
    {
      try
      {
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
      }
      catch { }
    }

    /// <summary>
    /// Dispose receiver.
    /// </summary>
    public void Dispose()
    {
      foreach (KeyValuePair<string, SidSpeckleRecord> kvp in GSA.SenderInfo)
      {
        Senders[kvp.Key].Dispose();
      }
    }

    private void CreateInitialiseSenders(List<string> streamNames, Func<string, string, SpeckleInterface.IStreamSender> GSASenderCreator, string restApi, string apiToken)
    {
      GSA.RemoveUnusedStreamInfo(streamNames);

      Senders = new Dictionary<string, SpeckleInterface.IStreamSender>();

      progressEstimator.UpdateTotal(WorkPhase.ApiCalls, 0);//Set it to zero here so that it will be updated as each sender works out its number of payloads - to be revised with a better way soon

      Progress<int> incrementProgress = new Progress<int>();
      incrementProgress.ProgressChanged += IncorporateSendPayloadProgress;
      Progress<int> totalProgress = new Progress<int>();
      totalProgress.ProgressChanged += IncorporateNewNumPayloadsProgress;

      var baseProps = GSA.GetBaseProperties();
      if (!Enum.TryParse(baseProps["units"].ToString(), true, out SpeckleInterface.BasePropertyUnits basePropertyUnits))
      {
        basePropertyUnits = SpeckleInterface.BasePropertyUnits.Millimetres;
      }
      var tolerance = Math.Round((double)baseProps["tolerance"], 8);
      var angleTolerance = Math.Round((double)baseProps["angleTolerance"], 6);
      var documentName = Path.GetFileNameWithoutExtension(GSA.GsaApp.gsaProxy.FilePath);

      foreach (string streamName in streamNames)
      {
        Senders[streamName] = GSASenderCreator(restApi, apiToken);

        if (!GSA.SenderInfo.ContainsKey(streamName))
        {
          loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Information, "Creating new sender for " + streamName));

          Senders[streamName].InitializeSender(documentName, basePropertyUnits, tolerance, angleTolerance, streamName: streamName, 
            totalProgress: totalProgress, incrementProgress: incrementProgress);

          GSA.SenderInfo[streamName] = new SidSpeckleRecord(Senders[streamName].StreamId, streamName, Senders[streamName].ClientId);
        }
        else
        {
          Senders[streamName].InitializeSender(documentName, basePropertyUnits, tolerance, angleTolerance, GSA.SenderInfo[streamName].StreamId, GSA.SenderInfo[streamName].ClientId, streamName);
        }
      }
    }

    private Dictionary<string, Dictionary<string, List<SpeckleObject>>> CreateStreamBuckets()
    {
      var streamBuckets = new Dictionary<string, Dictionary<string, List<SpeckleObject>>>();

      var currentObjects = GSA.GetAllConvertedGsaObjectsByType();
      foreach (var kvp in currentObjects)
      {
        var targetStream = GSA.GsaApp.gsaSettings.SeparateStreams ? StreamMap[kvp.Key] : "Full Model";

        foreach (IGSASpeckleContainer obj in kvp.Value)
        {
          if (GSA.GsaApp.gsaSettings.SendOnlyMeaningfulNodes)
          {
            if (obj.GetType().Name == "GSANode" && !(bool)obj.GetType().GetField("ForceSend").GetValue(obj))
            {
              continue;
            }
          }
          //var insideVal = (SpeckleObject)obj.GetType().GetProperty("Value").GetValue(obj);
          var insideVal = (SpeckleObject) obj.SpeckleObject;

          insideVal.GenerateHash();

          if (!streamBuckets.ContainsKey(targetStream))
          {
            streamBuckets[targetStream] = new Dictionary<string, List<SpeckleObject>>();
          }

          if (streamBuckets[targetStream].ContainsKey(insideVal.GetType().Name))
          {
            streamBuckets[targetStream][insideVal.GetType().Name].Add(insideVal);
          }
          else
          {
            streamBuckets[targetStream][insideVal.GetType().Name] = new List<SpeckleObject>() { insideVal };
          }
        }
      }
      return streamBuckets;
    }

    private List<Type> GetAssembliesTypes()
    {
      // Grab GSA interface type
      var interfaceType = typeof(IGSASpeckleContainer);

      var assemblies = SpeckleInitializer.GetAssemblies();
      var objTypes = new List<Type>();
      foreach (var ass in assemblies)
      {
        var types = ass.GetTypes();
        objTypes.AddRange(types.Where(t => interfaceType.IsAssignableFrom(t) && t != interfaceType && !t.IsAbstract));
      }
      return objTypes;
    }

    private List<string> GetStreamNames(List<Type> objTypes)
    {
      var streamNames = (GSA.GsaApp.gsaSettings.SendOnlyResults) ? new List<string> { "results" }
       : (GSA.GsaApp.gsaSettings.SeparateStreams)
         ? objTypes.Select(t => (string)t.GetAttribute("Stream")).Distinct().ToList()
         : new List<string>() { "Full Model" };
      return streamNames.Where(sn => !string.IsNullOrEmpty(sn)).ToList();
    }

    private bool UpdateCache()
    {
      var progress = new Progress<int>();
      progress.ProgressChanged += IncorporateCacheProgress;

      var keywords = GSA.Keywords;
      GSA.GsaApp.gsaCache.Clear();

      //initial estimate
      progressEstimator.UpdateTotal(WorkPhase.CacheRead, keywords.Count());
      progressEstimator.UpdateTotal(WorkPhase.CacheUpdate, keywords.Count());
      progressEstimator.UpdateTotal(WorkPhase.Conversion, 10000); //Take wild guess at having 10,000 objects to convert
      progressEstimator.UpdateTotal(WorkPhase.ApiCalls, 10);       //Take wild guess at having 10 API calls to make

      try
      {
        var data = GSA.GsaApp.gsaProxy.GetGwaData(keywords, false, progress);
        progressEstimator.UpdateTotal(WorkPhase.CacheRead, data.Count());
        progressEstimator.SetCurrentToTotal(WorkPhase.CacheRead); //Equalise the current and total in case the previous total estimate was wrong

        //Now that we have a better ideaof how many objects to update the cache with, and convert
        progressEstimator.UpdateTotal(WorkPhase.CacheUpdate, data.Count());
        progressEstimator.UpdateTotal(WorkPhase.Conversion, data.Count());

        for (int i = 0; i < data.Count(); i++)
        {
          var applicationId = (string.IsNullOrEmpty(data[i].ApplicationId)) ? null : data[i].ApplicationId;
          GSA.GsaApp.gsaCache.Upsert(
            data[i].Keyword,
            data[i].Index,
            data[i].GwaWithoutSet,
            streamId: data[i].StreamId,
            applicationId: applicationId,
            gwaSetCommandType: data[i].GwaSetType);

          progressEstimator.AppendCurrent(WorkPhase.CacheRead, 1);
        }

        int numRowsupdated = data.Count();
        if (numRowsupdated > 0)
        {
          loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Information,
            "Read " + numRowsupdated + " GWA lines across " + keywords.Count() + " keywords into cache"));
        }

        progressEstimator.SetCurrentToTotal(WorkPhase.CacheUpdate); //Equalise the current and total in case the previous total estimate was wrong

        return true;
      }
      catch
      {
        return false;
      }
    }

    private void IncorporateCacheProgress(object sender, int e)
    {
      progressEstimator.AppendCurrent(WorkPhase.CacheRead, e);
    }

    private void IncorporateSendPayloadProgress(object sender, int e)
    {
      progressEstimator.AppendCurrent(WorkPhase.ApiCalls, e);
    }

    private void IncorporateNewNumPayloadsProgress(object sender, int e)
    {
      progressEstimator.AppendTotal(WorkPhase.ApiCalls, e);
    }
  }
}
