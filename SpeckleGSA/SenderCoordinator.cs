using SpeckleCore;
using SpeckleGSAInterfaces;
using SpeckleInterface;
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

    // [ bucket, IStreamSender (which has stream ID and clientID) ]
    public Dictionary<string, IStreamSender> Senders = new Dictionary<string, IStreamSender>();

    //These need to be accessed using a lock
    private object traversedSerialisedLock = new object();
    private readonly List<Type> traversedSerialisedTypes = new List<Type>();

    private IProgress<MessageEventArgs> loggingProgress;
    private IProgress<string> statusProgress;
    private IProgress<SidSpeckleRecord> streamCreationProgress;
    private IProgress<SidSpeckleRecord> streamDeletionProgress;

    private ProgressEstimator progressEstimator;

    private double tolerance;
    private double angleTolerance;
    private string documentName;
    private string documentTitle;
    private List<SidSpeckleRecord> savedSenderSidRecords;
    private string restApi;
    private string apiToken;
    private Func<string, string, IStreamSender> gsaSenderCreator;
    private BasePropertyUnits basePropertyUnits;

    /// <summary>
    /// Initializes sender.
    /// </summary>
    /// <param name="restApi">Server address</param>
    /// <param name="apiToken">API token of account</param>
    /// <returns>Task</returns>
    public void Initialize(string restApi, string apiToken, List<SidSpeckleRecord> savedSenderStreamInfo, Func<string, string, IStreamSender> gsaSenderCreator,
      IProgress<MessageEventArgs> loggingProgress, IProgress<string> statusProgress, IProgress<double> percentageProgress,
      IProgress<SidSpeckleRecord> streamCreationProgress, IProgress<SidSpeckleRecord> streamDeletionProgress)
    {
      if (IsInit) return;

      if (!GSA.IsInit)
      {
        this.loggingProgress.Report(new MessageEventArgs(SpeckleGSAInterfaces.MessageIntent.Display, SpeckleGSAInterfaces.MessageLevel.Error, "GSA link not found."));
        return;
      }

      this.loggingProgress = loggingProgress;
      this.statusProgress = statusProgress;
      this.streamCreationProgress = streamCreationProgress;
      this.streamDeletionProgress = streamDeletionProgress;
      this.restApi = restApi;
      this.apiToken = apiToken;
      this.gsaSenderCreator = gsaSenderCreator;
      this.savedSenderSidRecords = savedSenderStreamInfo;
      this.documentName = Path.GetFileNameWithoutExtension(GSA.GsaApp.gsaProxy.FilePath);
      this.documentTitle = Path.GetFileNameWithoutExtension(GSA.GsaApp.gsaProxy.GetTitle());

      // Since this is sending, just use whatever is set in the opened GSA file
      GSA.GsaApp.gsaSettings.Units = GSA.GsaApp.gsaProxy.GetUnits();

      //Read properties in the opened GSA file
      var baseProps = GSA.GetBaseProperties();
      if (!Enum.TryParse(baseProps["units"].ToString(), true, out basePropertyUnits))
      {
        basePropertyUnits = BasePropertyUnits.Millimetres;
      }
      this.tolerance = Math.Round((double)baseProps["tolerance"], 8);
      this.angleTolerance = Math.Round((double)baseProps["angleTolerance"], 6);

      this.progressEstimator = new ProgressEstimator(percentageProgress, WorkPhase.CacheRead, 3, WorkPhase.CacheUpdate, 1, WorkPhase.Conversion, 20, WorkPhase.ApiCalls, 3);

      Senders.Clear();

      var startTime = DateTime.Now;

      Progress<int> incrementProgress = new Progress<int>();
      incrementProgress.ProgressChanged += IncorporateSendPayloadProgress;
      Progress<int> totalProgress = new Progress<int>();
      totalProgress.ProgressChanged += IncorporateNewNumPayloadsProgress;

      IsInit = true;

      return;
    }

    /// <summary>
    /// Trigger to update stream.
    /// </summary>
    public async Task Trigger()
    {
      if ((IsBusy) || (!IsInit)) return;

      IsBusy = true;
      //GSA.GsaApp.gsaSettings.Units = GSA.GsaApp.gsaProxy.GetUnits();

      #region update_cache
      var startTime = DateTime.Now;
      statusProgress.Report("Reading GSA data into cache");

      //Update cache
      var updatedCache = UpdateCache();
      if (!updatedCache)
      {
        this.loggingProgress.Report(new MessageEventArgs(SpeckleGSAInterfaces.MessageIntent.Display, SpeckleGSAInterfaces.MessageLevel.Error, "Error in communicating GSA - please check if the GSA file has been closed down"));
        return;
      }

      TimeSpan duration = DateTime.Now - startTime;
      loggingProgress.Report(new MessageEventArgs(SpeckleGSAInterfaces.MessageIntent.Display, SpeckleGSAInterfaces.MessageLevel.Information, "Duration of reading GSA model into cache: " + duration.ToString(@"hh\:mm\:ss")));
      loggingProgress.Report(new MessageEventArgs(SpeckleGSAInterfaces.MessageIntent.Telemetry, SpeckleGSAInterfaces.MessageLevel.Information, "send", "update-cache", "duration", duration.ToString(@"hh\:mm\:ss")));
      #endregion

      #region GSA_model_to_SpeckleObjects
      startTime = DateTime.Now;

      //Clear previously-sent objects
      GSA.ClearSenderDictionaries();

      lock (traversedSerialisedLock)
      {
        traversedSerialisedTypes.Clear();
      }

      var changeDetected = ProcessTxObjects();

      if (!changeDetected)
      {
        statusProgress.Report("No new or changed objects to send");
        IsBusy = false;
        return;
      }

      duration = DateTime.Now - startTime;
      loggingProgress.Report(new MessageEventArgs(SpeckleGSAInterfaces.MessageIntent.Display, SpeckleGSAInterfaces.MessageLevel.Information, "Duration of conversion to Speckle: " + duration.ToString(@"hh\:mm\:ss")));
      loggingProgress.Report(new MessageEventArgs(SpeckleGSAInterfaces.MessageIntent.Telemetry, SpeckleGSAInterfaces.MessageLevel.Information, "send", "conversion", "duration", duration.ToString(@"hh\:mm\:ss")));
      #endregion

      #region create_necessary_streams
      startTime = DateTime.Now;

      // Separate objects into streams
      var allBuckets = CreateStreamBuckets();

      var bucketsToCreate = allBuckets.Keys.Except(Senders.Keys).ToList();

      //Now check if any streams need to be created
      if (bucketsToCreate.Count() > 0)
      {
        var sidRecordByBucket = savedSenderSidRecords.ToDictionary(r => r.Bucket, r => r);
        if (savedSenderSidRecords != null && savedSenderSidRecords.Count() > 0)
        {
          var savedBuckets = sidRecordByBucket.Keys.ToList();
          var reuseBuckets = sidRecordByBucket.Keys.Where(ssn => bucketsToCreate.Any(sn => ssn.Equals(sn, StringComparison.InvariantCultureIgnoreCase))).ToList();
          var discardStreamNames = sidRecordByBucket.Keys.Except(reuseBuckets);
          foreach (var bucket in reuseBuckets)
          {
            var basicStreamData = SpeckleStreamManager.GetStream(restApi, apiToken, sidRecordByBucket[bucket].StreamId).Result;
            if (basicStreamData != null)
            {
              Senders.Add(bucket, gsaSenderCreator(restApi, apiToken));
              await Senders[bucket].InitializeSender(documentName, basePropertyUnits, tolerance, angleTolerance, sidRecordByBucket[bucket].StreamId, sidRecordByBucket[bucket].ClientId);

              bucketsToCreate.Remove(bucket);
            }
          }
          foreach (var dsn in discardStreamNames)
          {
            streamDeletionProgress.Report(sidRecordByBucket[dsn]);
          }
        }

        foreach (var sn in bucketsToCreate)
        {
          Senders.Add(sn, gsaSenderCreator(restApi, apiToken));
          var streamName = string.IsNullOrEmpty(documentTitle) ? "GSA " + sn : documentTitle + " (" + sn + ")";
          await Senders[sn].InitializeSender(documentName, basePropertyUnits, tolerance, angleTolerance, streamName: streamName);
          streamCreationProgress.Report(new SidSpeckleRecord(Senders[sn].StreamId, sn, Senders[sn].ClientId, streamName));
        }
      }

      #endregion

      #region send_to_server
      // Send package
      statusProgress.Report("Sending to Server");

      int numErrors = 0;
      var sendingTasks = new List<Task>();
      foreach (var k in Senders.Keys)
      {
        statusProgress.Report("Sending to stream: " + Senders[k].StreamId);
        numErrors += Senders[k].SendObjects(allBuckets[k]);
      }

      if (numErrors > 0)
      {
        loggingProgress.Report(new MessageEventArgs(SpeckleGSAInterfaces.MessageIntent.Display, SpeckleGSAInterfaces.MessageLevel.Error,
          numErrors + " errors found with sending to the server. Refer to the .txt log file(s) in " + AppDomain.CurrentDomain.BaseDirectory));
      }

      duration = DateTime.Now - startTime;
      loggingProgress.Report(new MessageEventArgs(SpeckleGSAInterfaces.MessageIntent.Display, SpeckleGSAInterfaces.MessageLevel.Information, "Duration of sending to Speckle: " + duration.ToString(@"hh\:mm\:ss")));
      loggingProgress.Report(new MessageEventArgs(SpeckleGSAInterfaces.MessageIntent.Telemetry, SpeckleGSAInterfaces.MessageLevel.Information, "send", "sending", "duration", duration.ToString(@"hh\:mm\:ss")));
      #endregion
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
        loggingProgress.Report(new MessageEventArgs(SpeckleGSAInterfaces.MessageIntent.Display, SpeckleGSAInterfaces.MessageLevel.Error,
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


      lock (traversedSerialisedLock)
      {
        traversedSerialisedTypes.AddRange(batch);
      }
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
      Senders.Keys.ToList().ForEach(s => Senders[s].Dispose());
      Senders.Clear();
    }


    private Dictionary<string, Dictionary<string, List<SpeckleObject>>> CreateStreamBuckets()
    {
      var buckets = new Dictionary<string, Dictionary<string, List<SpeckleObject>>>();

      var currentObjects = GSA.GetAllConvertedGsaObjectsByType();
      foreach (var t in currentObjects.Keys)
      {
        //var bucketName = GSA.GsaApp.gsaSettings.SeparateStreams ? StreamMap[kvp.Key] : "Full Model";
        var bucketName = (string)t.GetAttribute("Stream");

        foreach (IGSASpeckleContainer obj in currentObjects[t])
        {
          if (GSA.GsaApp.gsaSettings.SendOnlyMeaningfulNodes)
          {
            if (obj.GetType().Name == "GSANode" && !(bool)obj.GetType().GetField("ForceSend").GetValue(obj))
            {
              continue;
            }
          }
          var insideVal = (SpeckleObject) obj.SpeckleObject;

          insideVal.GenerateHash();

          if (!buckets.ContainsKey(bucketName))
          {
            buckets[bucketName] = new Dictionary<string, List<SpeckleObject>>();
          }

          if (buckets[bucketName].ContainsKey(insideVal.GetType().Name))
          {
            buckets[bucketName][insideVal.GetType().Name].Add(insideVal);
          }
          else
          {
            buckets[bucketName][insideVal.GetType().Name] = new List<SpeckleObject>() { insideVal };
          }
        }
      }
      return buckets;
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
          loggingProgress.Report(new MessageEventArgs(SpeckleGSAInterfaces.MessageIntent.Display, SpeckleGSAInterfaces.MessageLevel.Information,
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
