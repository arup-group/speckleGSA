using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SpeckleCore;
using SpeckleGSAInterfaces;

namespace SpeckleGSAProxy
{
  public class GSACache : IGSACache, IGSACacheForKit, IGSACacheForTesting
  {
    private readonly object syncLock = new object();

    private T ExecuteWithLock<T>(Func<T> f)
    {
      lock (syncLock)
      {
        var ret = f();
        return ret;
      }
    }

    private void ExecuteWithLock(Action a)
    {
      lock (syncLock)
      {
        a();
      }
    }

    private readonly IGSACacheRecordCollection recordCollection = new GsaCacheRecordCollection();

    // < keyword , { < index, app_id >, < index, app_id >, ... } >
    private readonly Dictionary<string, IPairCollection<int, string>> provisionals = new Dictionary<string, IPairCollection<int, string>>();

    private readonly List<string> streams = new List<string>();
    private readonly Dictionary<string, int> streamIndexByApplicationId = new Dictionary<string, int>();

    //Hardcoded for now to use current 10.1 keywords - to be reviewed
    private static readonly string analKeyword = "ANAL";
    private static readonly string comboKeyword = "COMBINATION";
    //These are hardcoded for queries of the cache that are exportable from the UI
    private static readonly string sectionKeyword = "PROP_SEC";
    private static readonly string memberKeyword = "MEMB";
    private static readonly string elementKeyword = "EL";

    public int NumRecords { get => recordCollection.NumRecords; }

    public List<string> KeywordsForLoadCaseExpansion { get => new List<string> { analKeyword, comboKeyword }; }

    public Dictionary<int, object> GetIndicesSpeckleObjects(string speckleTypeName)
      => ExecuteWithLock(() => recordCollection.GetSpeckleObjectsByTypeName(speckleTypeName.ChildType()));

    public List<SpeckleObject> GetSpeckleObjects(string speckleTypeName, string applicationId, bool? latest = true, string streamId = null)
      => ExecuteWithLock(() => recordCollection.GetSpeckleObjects(speckleTypeName.ChildType(), applicationId, latest, streamId));

    public bool ApplicationIdExists(string keyword, string applicationId)
      => ExecuteWithLock(() => ValidAppId(applicationId, out string appId) && recordCollection.ContainsKeyword(keyword.Split('.').First(), appId));

    public bool ContainsType(string speckleTypeName)
      => ExecuteWithLock(() => recordCollection.ContainsSpeckleType(speckleTypeName.ChildType()));

    //Used by the ToSpeckle methods in the kit; either the previous needs to be serialised for merging purposes during reception, or newly-arrived GWA needs to be serialised for transmission
    public Dictionary<int, string> GetGwaToSerialise(string keyword) => ExecuteWithLock(() => recordCollection.GetIndexedGwa(keyword.Split('.').First()));

    public List<string> GetNewGwaSetCommands() => ExecuteWithLock(() =>
      {
        var retList = new List<string>();
        var latestRecordsByKeyword = recordCollection.GetLatestRecordsByKeyword();
        foreach (var kw in latestRecordsByKeyword.Keys)
        {
          foreach (var r in latestRecordsByKeyword[kw])
          {
            retList.Add((r.GwaSetCommandType == GwaSetCommandType.SetAt)
            ? string.Join(GSAProxy.GwaDelimiter.ToString(), new[] { "SET_AT", r.Index.ToString(), r.Gwa })
            : r.Gwa);
          }
        }
        return retList;
      });

    //To review: does this need to be latest records only?
    public List<string> GetGwa(string keyword, int index) => ExecuteWithLock(() =>
      {
        var kw = keyword.Split('.').First();
        return recordCollection.GetRecord(kw, index);
      });

    public List<string> GetGwa(string keyword) => ExecuteWithLock(() =>
      {
        var kw = keyword.Split('.').First();
        return recordCollection.GetAllRecordsByKeyword(kw);
      });

    public List<string> GetCurrentGwa() => ExecuteWithLock(() => recordCollection.GetLatestGwa());

    public void Clear()
    {
      ExecuteWithLock(() =>
      {
        recordCollection.Clear();
        provisionals.Clear();
        streamIndexByApplicationId.Clear();
        streams.Clear();
      });
    }

    //For results
    public bool GetKeywordRecordsSummary(string keyword, out List<string> gwa, out List<int> indices, out List<string> applicationIds)
    {
      var kw = keyword.Split('.').First();
      return recordCollection.GetRecordSummaries(kw, out gwa, out indices, out applicationIds);
    }

    //For testing
    public List<string> GetGwaSetCommands() => ExecuteWithLock(() => recordCollection.GetGwaCommandsWithSet());

    public void MarkAsPrevious(string keyword, string applicationId)
    {
      ExecuteWithLock(() =>
      {
        var kw = keyword.Split('.').First();
        if (ValidAppId(applicationId, out string appId))
        {
          recordCollection.MarkPrevious(kw, appId);
        }
      });
    }

    public bool Upsert(string keyword, int index, string gwaWithoutSet, string streamId, string applicationId, GwaSetCommandType gwaSetCommandType)
    {
      return Upsert(keyword, index, gwaWithoutSet, applicationId, null, gwaSetCommandType, streamId: streamId);
    }

    #region methods_used_within_lock_by_Upsert
    private void RemoveFromProvisional(string keyword, int index)
    {
      if (provisionals.ContainsKey(keyword))
      {
        if (provisionals[keyword].ContainsLeft(index))
        {
          provisionals[keyword].RemoveLeft(index);
        }
        if (provisionals[keyword].Count() == 0)
        {
          provisionals.Remove(keyword);
        }
      }
    }
    #endregion

    //Not every record has stream IDs (like generated nodes)
    public bool Upsert(string keyword, int index, string gwa, string applicationId = "", SpeckleObject so = null, GwaSetCommandType gwaSetCommandType = GwaSetCommandType.Set, bool? latest = true, string streamId = null)
    {
      var kw = keyword.Split('.').First();

      try
      {
        var matchingRecords = new List<GSACacheRecord>();
        ExecuteWithLock(() =>
        {
          matchingRecords = recordCollection.GetAllRecords(kw, index);
        });

        if (matchingRecords.Count() > 0)
        {
          var gwaFormatted = gwa.GwaForComparison();
          var matchingGwaRecords = matchingRecords.Where(r => r.Gwa.GwaForComparison().Equals(gwaFormatted, StringComparison.InvariantCultureIgnoreCase)).ToList();
          if (matchingGwaRecords.Count() > 1)
          {
            throw new Exception("Unexpected multiple matches found in upsert of cache records");
          }
          else if (matchingGwaRecords.Count() == 1)
          {
            //There should just be one matching record

            //There is no change to the GWA but it clearly means it's part of the latest
            if (latest.HasValue)
            {
              ExecuteWithLock(() => matchingGwaRecords.First().Latest = latest.Value);
            }

            return true;
          }
          else
          {
            //These will be return at the next call to GetToBeDeletedGwa() and removed at the next call to Snapshot()
            foreach (var r in matchingRecords)
            {
              ExecuteWithLock(() => r.Latest = false);
            }
          }
        }

        ExecuteWithLock(() =>
        {
          recordCollection.Upsert(kw, index, gwa, streamId, applicationId ?? "", true, so, gwaSetCommandType);
          RemoveFromProvisional(kw, index);
        });
        return true;
      }
      catch
      {
        return false;
      }
    }

    public bool AssignSpeckleObject(string keyword, string applicationId, SpeckleObject so, string streamId = null) => ExecuteWithLock(() =>
       (ValidAppId(applicationId, out string appId) && recordCollection.AssignSpeckleObject(keyword.Split('.').First(), appId, so, streamId))
    );

    public void Snapshot(string streamId) => ExecuteWithLock(() => { recordCollection.Snapshot(streamId); });

    #region methods_used_within_lock_by_UpsertProvisional
    private int? HighestProvisional(string keyword)
    {
      if (!provisionals.ContainsKey(keyword) || provisionals[keyword] == null || provisionals[keyword].Count() == 0)
      {
        return null;
      }

      return provisionals[keyword].MaxLeft();
    }

    private void UpsertProvisional(string keyword, int index)
    {
      if (!provisionals.ContainsKey(keyword))
      {
        provisionals.Add(keyword, new PairCollection<int, string>());
      }
      provisionals[keyword].Add(index, null);
    }

    private void UpsertProvisional(string keyword, int index, string applicationId)
    {
      if (!provisionals.ContainsKey(keyword))
      {
        provisionals.Add(keyword, new PairCollection<int, string>());
      }
      provisionals[keyword].Add(index, applicationId);
    }

    private bool FindProvisionalIndex(string keyword, string applicationId, out int? provisionalIndex)
    {
      if (provisionals.ContainsKey(keyword) && provisionals[keyword].ContainsRight(applicationId) && provisionals[keyword].FindLeft(applicationId, out int index))
      {
        provisionalIndex = index;
        return true;
      }
      provisionalIndex = null;
      return false;
    }

    private bool ProvisionalContains(string keyword, int index)
    {
      if (!provisionals.ContainsKey(keyword) || provisionals[keyword] == null || provisionals[keyword].Count() == 0)
      {
        return false;
      }
      return provisionals[keyword].ContainsLeft(index);
    }
    #endregion

    public bool ReserveIndex(string keyword, string applicationId)
    {
      if (ValidAppId(applicationId, out string appId))
      {
        //ResolveIndex applies a lock
        return (ResolveIndex(keyword, appId) > 0);
      }
      return false;
    }

    public void RemoveFromProvisional(string keyword, string applicationId)
    {
      ExecuteWithLock(() =>
      {
        if (ValidAppId(applicationId, out string appId))
        {
          var kw = keyword.Split('.').First();
          if (provisionals.ContainsKey(kw) && provisionals[kw].ContainsRight(appId))
          {
            provisionals[kw].RemoveRight(appId);
          }
        }
      });
    }

    public int ResolveIndex(string keyword, string applicationId = "")
    {
      return ExecuteWithLock(() =>
      {
        var kw = keyword.Split('.').First();
        if (ValidAppId(applicationId, out string appId))
        {
          var matchingRecords = recordCollection.GetAllRecords(kw, appId);

          if (matchingRecords.Count() == 0)
          {
            if (FindProvisionalIndex(kw, appId, out int? provisionalIndex))
            {
              return provisionalIndex.Value;
            }
            //No matches in either previous or latest
            //var indices = GetIndices(kw);
            var indices = recordCollection.GetRecordIndexHashSet(kw);
            var highestProvisional = HighestProvisional(kw);
            var highestIndex = Math.Max((indices.Count() == 0) ? 0 : indices.Max(), highestProvisional ?? 0);
            for (int i = 1; i <= highestIndex; i++)
            {
              if (!indices.Contains(i) && !ProvisionalContains(kw, i))
              {
                UpsertProvisional(kw, i, appId);
                return i;
              }
            }
            UpsertProvisional(kw, highestIndex + 1, appId);
            return highestIndex + 1;
          }
          else
          {
            //There should be only at most one previous and one latest for this type and applicationID
            var existingPrevious = matchingRecords.Where(r => r.Previous && !r.Latest);
            var existingLatest = matchingRecords.Where(r => r.Latest);

            return (existingLatest.Count() > 0) ? existingLatest.First().Index : existingPrevious.First().Index;
          }
        }
        else
        {
          //Application ID is empty or null
          var indices = recordCollection.GetRecordIndexHashSet(kw);
          var highestProvisional = HighestProvisional(kw);
          var highestIndex = Math.Max((indices.Count() == 0) ? 0 : indices.Max(), highestProvisional ?? 0);
          for (int i = 1; i <= highestIndex; i++)
          {
            if (!indices.Contains(i) && !ProvisionalContains(kw, i))
            {
              UpsertProvisional(kw, i);
              return i;
            }
          }

          UpsertProvisional(kw, highestIndex + 1);
          return highestIndex + 1;
        }
      });
    }

    public int? LookupIndex(string keyword, string applicationId)
      => ExecuteWithLock(() => (ValidAppId(applicationId, out string appId)) ? recordCollection.GetRecordIndex(keyword.Split('.').First(), appId) : null);

    public List<int?> LookupIndices(string keyword, IEnumerable<string> applicationIds) => ExecuteWithLock(()
       => (ValidAppIds(applicationIds, out List<string> appIds)) ? recordCollection.GetRecordIndices(keyword.Split('.').First(), appIds) : new List<int?>()
    );

    public List<int?> LookupIndices(string keyword)
      => ExecuteWithLock(() => recordCollection.GetRecordIndices(keyword.Split('.').First()).Select(k => (int?)k).ToList());

    public List<Tuple<string, int, string, GwaSetCommandType>> GetExpiredData()
      => ExecuteWithLock(() => recordCollection.GetExpiredData());

    public List<Tuple<string, int, string, GwaSetCommandType>> GetDeletableData()
      => ExecuteWithLock(() => recordCollection.GetDeletableData());

    public List<string> ExpandLoadCasesAndCombinations(string loadCaseString)
    {
      var retList = new List<string>();

      if (string.IsNullOrEmpty(loadCaseString) || !ProcessLoadCaseCombinationSpec(loadCaseString, out List<string> aParts, out List<string> cParts))
      {
        return retList;
      }

      var cachedAnalIndices = ExecuteWithLock(() => recordCollection.GetRecordIndices(analKeyword));
      var cachedComboIndices = ExecuteWithLock(() => recordCollection.GetRecordIndices(comboKeyword));
      var tasks = new List<Task>();
      var retListLock = new object();

      if (aParts.Count() > 0)
      {
#if !DEBUG
        tasks.Add(Task.Run(() =>
#endif
        {
          var aSpecs = ExpandLoadCasesAndCombinationSubset(aParts, "A", cachedAnalIndices);
          if (aSpecs != null && aSpecs.Count() > 0)
          {
            lock (retListLock)
            {
              retList.AddRange(aSpecs);
            }
          }
        }
#if !DEBUG
        ));
#endif
      }

      if (cParts.Count() > 0)
      {
#if !DEBUG
        tasks.Add(Task.Run(() =>
#endif
        {
          var cSpecs = ExpandLoadCasesAndCombinationSubset(cParts, "C", cachedComboIndices);
          if (cSpecs != null && cSpecs.Count() > 0)
          {
            lock (retListLock)
            {
              retList.AddRange(cSpecs);
            }
          }
        }
#if !DEBUG
        ));
#endif
      }

#if !DEBUG
      Task.WaitAll(tasks.ToArray());
#endif

      return retList;
    }

    private List<string> ExpandLoadCasesAndCombinationSubset(List<string> listParts, string marker, SortedSet<int> cachedIndices)
    {
      var specs = new List<string>();
      if (listParts.All(sp => IsMarkerPattern(sp)))
      {
        var aPartsDistinct = listParts.Distinct();
        foreach (var a in aPartsDistinct)
        {
          if (a.Length > 1 && int.TryParse(a.Substring(1), out int specIndex))
          {
            if (cachedIndices.Contains(specIndex))
            {
              specs.Add(a);
            }
          }
        }
      }
      else
      {
        specs = (listParts[0].ToLower() == "all")
          ? cachedIndices.Select(i => marker + i).ToList()
          : ExpandSubsetViaProxy(cachedIndices.ToList(), listParts, marker);
      }
      return specs;
    }

    #region load_case_conversion

    //Since EntitiesInList doesn't offer load cases/combinations as a GsaEntity type, a dummy GSA instance is 
    //created where a node is created for every load case/combination in the specification.  This is done separately for load cases and combinations.
    private List<string> ExpandSubsetViaProxy(List<int> existingIndices, List<string> specParts, string marker)
    {
      var items = new List<string>();
      var gsaProxy = new GSAProxy();

      try
      {
        gsaProxy.NewFile(false);

        for (int i = 0; i < existingIndices.Count(); i++)
        {
          var indexStr = existingIndices[i].ToString();
          gsaProxy.SetGwa(string.Join(GSAProxy.GwaDelimiter.ToString(), new[] { "SET", "NODE.3", indexStr, indexStr, "NO_RGB", "0", "0", "0" }));
        }
        gsaProxy.Sync();
        var tempSpec = string.Join(" ", specParts.Select(a => RemoveMarker(a)));
        items.AddRange(gsaProxy.GetNodeEntitiesInList(tempSpec).Select(e => marker + e.ToString()));
      }
      catch { }
      finally
      {
        gsaProxy.Close();
        gsaProxy = null;
      }

      return items;
    }

    private bool IsMarkerPattern(string item)
    {
      return (item.Length >= 2 && char.IsLetter(item[0]) && item.Substring(1).All(c => char.IsDigit(c)));
    }

    private string RemoveMarker(string item)
    {
      return (IsMarkerPattern(item) ? item.Substring(1) : item);
    }

    private bool ProcessLoadCaseCombinationSpec(string spec, out List<string> aParts, out List<string> cParts)
    {
      aParts = new List<string>();
      cParts = new List<string>();
      var formattedSpec = spec.ToLower().Trim();

      if (formattedSpec.StartsWith("all"))
      {
        aParts.Add("All");
        cParts.Add("All");
        return true;
      }

      var stage1Parts = new List<string>();
      //break up the string by any A<number> and C<number> substrings
      var inCurrSpec = false;
      var currSpec = "";
      var bnSpec = "";  //Between spec, could be any string
      for (int i = 0; i < formattedSpec.Length; i++)
      {
        if (Char.IsDigit(formattedSpec[i]))
        {
          if (i == 0)
          {
            bnSpec += formattedSpec[i];
          }
          else
          {
            if (formattedSpec[i - 1] == 'a' || formattedSpec[i - 1] == 'c')
            {
              //Previous is A or C and current is a number
              inCurrSpec = true;
              currSpec = spec[i - 1].ToString() + spec[i].ToString();
              bnSpec = bnSpec.Substring(0, bnSpec.Length - 1);
              if (bnSpec.Length > 0)
              {
                stage1Parts.Add(bnSpec);
              }
              bnSpec = "";
            }
            else if (Char.IsNumber(formattedSpec[i - 1]))
            {
              //Previous is not A or C but current is a number - assume continuation of previous state
              if (inCurrSpec)
              {
                currSpec += spec[i].ToString();
              }
              else
              {
                bnSpec += spec[i].ToString();
              }
            }
          }
        }
        else if (Char.IsLetter(formattedSpec[i]))
        {
          //it's not a number, so close off new part if relevant
          if (inCurrSpec)
          {
            stage1Parts.Add(currSpec);
            currSpec = "";
          }

          inCurrSpec = false;
          bnSpec += spec[i].ToString();
        }
        else
        {
          if (inCurrSpec)
          {
            stage1Parts.Add(currSpec);
            currSpec = "";
            inCurrSpec = false;
          }
          else if (bnSpec.Length > 0)
          {
            stage1Parts.Add(bnSpec);
            bnSpec = "";
          }
        }
      }

      if (inCurrSpec)
      {
        stage1Parts.Add(currSpec);
      }
      else
      {
        stage1Parts.Add(bnSpec);
      }

      //Now break up these items into groups, delimited by a switch between an A_ and C_ mention, or an all-number item and an A_ or C_ mention
      var partsAorC = stage1Parts.Select(p => GetAorC(p)).ToList();

      if (partsAorC.All(p => p == 0))
      {
        return false;
      }
      int? firstViableIndex = null;
      for (int i = 0; i < partsAorC.Count(); i++)
      {
        if (partsAorC[i] > 0)
        {
          firstViableIndex = i;
          break;
        }
      }
      if (!firstViableIndex.HasValue)
      {
        return false;
      }

      int currAorC = GetAorC(stage1Parts[firstViableIndex.Value]); // A = 1, C = 2
      if (currAorC == 1)
      {
        aParts.Add(stage1Parts[firstViableIndex.Value]);
      }
      else
      {
        cParts.Add(stage1Parts[firstViableIndex.Value]);
      }

      for (int i = (firstViableIndex.Value + 1); i < stage1Parts.Count(); i++)
      {
        var itemAorC = GetAorC(stage1Parts[i]);

        if (itemAorC == 0 || itemAorC == currAorC)
        {
          //Continue on
          if (currAorC == 1)
          {
            aParts.Add(stage1Parts[i]);
          }
          else
          {
            cParts.Add(stage1Parts[i]);
          }
        }
        else if (itemAorC != currAorC)
        {
          if (currAorC == 1)
          {
            RemoveTrailingLettersOnlyItems(ref aParts);

            cParts.Add(stage1Parts[i]);
          }
          else if (currAorC == 2)
          {
            RemoveTrailingLettersOnlyItems(ref cParts);

            aParts.Add(stage1Parts[i]);
          }
          currAorC = itemAorC;
        }
      }

      return (aParts.Count > 0 || cParts.Count > 0);
    }

    private static void RemoveTrailingLettersOnlyItems(ref List<string> parts)
    {
      var found = true;

      //First remove any all-letter items from the last state
      var index = (parts.Count - 1);

      do
      {
        if (parts[index].All(p => (char.IsLetter(p))))
        {
          parts.RemoveAt(index);
          index--;
        }
        else
        {
          found = false;
        }
      } while (found);
    }

    private static int GetAorC(string part)
    {
      if (string.IsNullOrEmpty(part) || part.Length < 2 || !(Char.IsLetter(part[0]) && Char.IsDigit(part[1])))
      {
        return 0;
      }
      return (char.ToLowerInvariant(part[0]) == 'a') ? 1 : (char.ToLowerInvariant(part[0]) == 'c') ? 2 : 0;
    }
    #endregion

    #region applicationIdLookup
    public string GetApplicationId(string keyword, int index)
      => ExecuteWithLock(() => recordCollection.GetApplicationId(keyword.Split('.').First(), index));

    public bool SetApplicationId(string keyword, int index, string applicationId)
      => ExecuteWithLock(() => 
      {
        if (ValidAppId(applicationId, out string appId))
        {
          recordCollection.AssignApplicationId(keyword.Split('.').First(), index, appId);
          return true;
        }
        return false; 
      });
    
    #endregion


    public bool SetStream(string applicationId, string streamId)
    {
      if (!ValidAppId(applicationId, out string appId))
      {
        return false;
      }
      return ExecuteWithLock(() =>
      {
        int streamIndex;
        if (!streams.Contains(streamId))
        {
          streamIndex = streams.Count();
          streams.Add(streamId);
        }
        else
        {
          streamIndex = streams.IndexOf(streamId);
        }
        if (streamIndexByApplicationId.ContainsKey(appId))
        {
          return false;
        }
        streamIndexByApplicationId.Add(appId, streamIndex);
        return true;
      });
    }

    public string LookupStream(string applicationId)
    {
      if (ValidAppId(applicationId, out string appId) && streamIndexByApplicationId.ContainsKey(appId))
      {
        var streamIndex = streamIndexByApplicationId[appId];
        if (streamIndex < streams.Count())
        {
          return streams[streamIndex];
        }
      }
      return "";
    }

    //For testing
    List<GSACacheRecord> IGSACacheForTesting.Records => ExecuteWithLock(() => recordCollection.GetAllRecords());

    private bool ValidAppId(string appIdIn, out string appIdOut)
    {
      appIdOut = null;
      if (appIdIn == null)
      {
        return false;
      }
      var appIdTrimmed = appIdIn.Replace(" ", "");
      if (!string.IsNullOrEmpty(appIdTrimmed))
      {
        appIdOut = appIdTrimmed;
        return true;
      }
      return false;
    }

    private bool ValidAppIds(IEnumerable<string> appIdIns, out List<string> appIdOuts)
    {
      appIdOuts = new List<string>();
      if (appIdIns == null)
      {
        return false;
      }
      appIdOuts = new List<string>();
      foreach (var aid in appIdIns)
      {
        if (ValidAppId(aid, out string appId))
        {
          appIdOuts.Add(aid);
        }
      }
      return appIdOuts.Count() > 0;
    }
  }
}
