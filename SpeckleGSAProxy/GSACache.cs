using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using SpeckleCore;
using SpeckleGSAInterfaces;

namespace SpeckleGSAProxy
{
  public class GSACache : IGSACache, IGSACacheForKit, IGSACacheForTesting
  {
    //For future use
    //private object recordsLock = new object();
    //private object provisionalsLock = new object();

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

    private ReadOnlyCollection<GSACacheRecord> records => recordsByKeyword.SelectMany(k => k.Value).ToList().AsReadOnly();

    //There could be multiple entries at the same index - namely, a previous and a latest
    private readonly Dictionary<string, List<GSACacheRecord>> recordsByKeyword = new Dictionary<string, List<GSACacheRecord>>();

    //Shortcut optimisation for the records above
    private readonly Dictionary<string, Dictionary<int, string>> applicationIdLookup = new Dictionary<string, Dictionary<int, string>>();

    // < keyword , { < index, app_id >, < index, app_id >, ... } >
    private readonly Dictionary<string, Dictionary<int, string>> provisionals = new Dictionary<string, Dictionary<int, string>>();

    private readonly List<string> streams = new List<string>();
    private readonly Dictionary<string, int> streamIndexByApplicationId = new Dictionary<string, int>();

    //Hardcoded for now to use current 10.1 keywords - to be reviewed
    private readonly string analKey = "ANAL";
    private readonly string comboKey = "COMBINATION";

    public Dictionary<int, object> GetIndicesSpeckleObjects(string speckleTypeName)
    {
      speckleTypeName = speckleTypeName.ChildType();
      return ExecuteWithLock(() => records.Where(r => r.SpeckleObj != null && r.SpeckleType.EndsWith(speckleTypeName)).ToDictionary(v => v.Index, v => (object)v.SpeckleObj));
    }

    public List<SpeckleObject> GetSpeckleObjects(string speckleTypeName, string applicationId, bool? latest = true, string streamId = null)
    {
      return ExecuteWithLock(() =>
      {
        speckleTypeName = speckleTypeName.ChildType();
        var matchingRecords = records.Where(r => r.SpeckleObj != null
          && r.SpeckleType == speckleTypeName
          && r.ApplicationId.EqualsWithoutSpaces(applicationId)
          && (string.IsNullOrEmpty(streamId) || r.StreamId.EqualsWithoutSpaces(streamId))
          && ((latest.HasValue && latest.Value) || (!latest.HasValue)));
        return matchingRecords.Select(r => r.SpeckleObj).ToList();
      });
    }

    public bool ApplicationIdExists(string keyword, string applicationId) => ExecuteWithLock(() =>
      {
        var kw = keyword.Split('.').First();
        if (recordsByKeyword.ContainsKey(kw))
        {
          return recordsByKeyword[kw].Any(r => r.ApplicationId.EqualsWithoutSpaces(applicationId));
        }
        return false;
      });

    public bool ContainsType(string speckleTypeName)
    {
      speckleTypeName = speckleTypeName.ChildType();
      return ExecuteWithLock(() => records.Any(r => r.SpeckleObj != null && r.SpeckleType == speckleTypeName));
    }

    //Used by the ToSpeckle methods in the kit; either the previous needs to be serialised for merging purposes during reception, or newly-arrived GWA needs to be serialised for transmission
    public Dictionary<int, string> GetGwaToSerialise(string keyword) => ExecuteWithLock(() =>
      {
        var kw = keyword.Split('.').First();
        return (recordsByKeyword.ContainsKey(kw))
          ? recordsByKeyword[kw]
            .Where(r => ((r.Previous == false && r.Latest == true && r.SpeckleObj == null) || (r.Previous == true && r.SpeckleObj == null)))
            .ToDictionary(r => r.Index, r => r.Gwa)
          : new Dictionary<int, string>();
      });

    public List<string> GetNewGwaSetCommands() => ExecuteWithLock(() =>
      {
        var retList = new List<string>();

        //Ensure records within each keyword group is ordered by index
        var latestRecords = records.Where(r => r.Previous == false && r.Latest == true);
        var latestRecordsByKeyword = latestRecords.GroupBy(r => r.Keyword).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var kw in latestRecordsByKeyword.Keys)
        {
          foreach (var r in latestRecordsByKeyword[kw].OrderBy(r => r.Index))
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
        return (recordsByKeyword.ContainsKey(kw)) ? recordsByKeyword[kw].Where(r => r.Index == index).Select(r => r.Gwa).ToList() : new List<string>();
      });

    public List<string> GetGwa(string keyword) => ExecuteWithLock(() =>
      {
        var kw = keyword.Split('.').First();
        return (recordsByKeyword.ContainsKey(kw)) ? recordsByKeyword[kw].Select(r => r.Gwa).ToList() : new List<string>();
      });

    public List<string> GetCurrentGwa() => ExecuteWithLock(() => records.Where(r => r.Latest).Select(r => r.Gwa).ToList());

    public void Clear()
    {
      ExecuteWithLock(() =>
      {
        recordsByKeyword.Clear();
        applicationIdLookup.Clear();
        provisionals.Clear();
        streamIndexByApplicationId.Clear();
        streams.Clear();
      });
    }

    //For results
    public bool GetKeywordRecordsSummary(string keyword, out List<string> gwa, out List<int> indices, out List<string> applicationIds)
    {
      gwa = new List<string>();
      indices = new List<int>();
      applicationIds = new List<string>();
      var kw = keyword.Split('.').First();
      if (!recordsByKeyword.ContainsKey(kw))
      {
        return false;
      }
      foreach (var record in recordsByKeyword[kw])
      {
        gwa.Add(record.Gwa);
        indices.Add(record.Index);
        applicationIds.Add(record.ApplicationId);
      }
      return true;
    }

    //For testing
    public List<string> GetGwaSetCommands() => ExecuteWithLock(() => records.Select(r => (r.GwaSetCommandType == GwaSetCommandType.Set) ? "SET\t" + r.Gwa
      : string.Join(GSAProxy.GwaDelimiter.ToString(), new[] { "SET_AT", r.Index.ToString(), r.Gwa })).ToList());

    public void MarkAsPrevious(string keyword, string applicationId)
    {
      ExecuteWithLock(() =>
      {
        var kw = keyword.Split('.').First();
        if (recordsByKeyword.ContainsKey(kw))
        {
          var matchingRecords = recordsByKeyword[kw].Where(r => r.ApplicationId.EqualsWithoutSpaces(applicationId) && r.Latest).ToList();
          if (matchingRecords != null && matchingRecords.Count() > 0)
          {
            for (int i = 0; i < matchingRecords.Count(); i++)
            {
              matchingRecords[i].Previous = true;
              matchingRecords[i].Latest = false;
            }
          }
        }
      });
    }

    public bool Upsert(string keyword, int index, string gwaWithoutSet, string streamId, string applicationId, GwaSetCommandType gwaSetCommandType)
    {
      return Upsert(keyword, index, gwaWithoutSet, applicationId, null, gwaSetCommandType, streamId: streamId);
    }

    #region methods_used_within_lock_by_Upsert
    private void UpsertApplicationIdLookup(string keyword, int index, string applicationId)
    {
      if (!string.IsNullOrEmpty(applicationId))
      {
        if (!applicationIdLookup.ContainsKey(keyword))
        {
          applicationIdLookup.Add(keyword, new Dictionary<int, string>());
        }
        applicationIdLookup[keyword][index] = applicationId.Replace(" ", "");
      }
    }

    private void RemoveFromProvisional(string keyword, int index)
    {
      if (provisionals.ContainsKey(keyword))
      {
        if (provisionals[keyword].ContainsKey(index))
        {
          provisionals[keyword].Remove(index);
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
      if (applicationId == null)
      {
        applicationId = "";
      }

      var kw = keyword.Split('.').First();

      try
      {
        var matchingRecords = new List<GSACacheRecord>();
        ExecuteWithLock(() =>
        {
          if (!recordsByKeyword.ContainsKey(kw))
          {
            recordsByKeyword.Add(kw, new List<GSACacheRecord>());
          }
          matchingRecords = recordsByKeyword[kw].Where(r => r.Index == index).ToList();
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
          if (!recordsByKeyword.ContainsKey(kw))
          {
            recordsByKeyword.Add(kw, new List<GSACacheRecord>());
          }
          recordsByKeyword[kw].Add(new GSACacheRecord(kw, index, gwa, streamId: streamId, applicationId: applicationId, latest: true, so: so, gwaSetCommandType: gwaSetCommandType));

          UpsertApplicationIdLookup(kw, index, applicationId);
          RemoveFromProvisional(kw, index);
        });
        return true;
      }
      catch
      {
        return false;
      }
    }

    public bool AssignSpeckleObject(string keyword, string applicationId, SpeckleObject so, string streamId = null)
    {
      return ExecuteWithLock(() =>
      {
        var kw = keyword.Split('.').First();
        if (recordsByKeyword.ContainsKey(kw))
        {
          var matchingRecords = recordsByKeyword[kw]
            .Where(r => !string.IsNullOrEmpty(r.ApplicationId)
              && r.ApplicationId.EqualsWithoutSpaces(applicationId)
              && (string.IsNullOrEmpty(streamId) || (!string.IsNullOrEmpty(r.StreamId) && r.StreamId.Equals(streamId ?? "")))
              && r.SpeckleObj == null);

          if (matchingRecords == null || matchingRecords.Count() == 0)
          {
            return false;
          }

          matchingRecords.First().SpeckleObj = so;
          return true;
        }
        return false;
      });
    }

    public void Snapshot(string streamId)
    {
      ExecuteWithLock(() =>
      {
        foreach (var keyword in recordsByKeyword.Keys)
        {
          var indicesToRemove = new List<int>();

          for (int i = 0; i < recordsByKeyword[keyword].Count(); i++)
          {
            if (recordsByKeyword[keyword][i].StreamId == null || recordsByKeyword[keyword][i].StreamId != streamId || !IsAlterable(keyword, recordsByKeyword[keyword][i].ApplicationId))
            {
              continue;
            }

            // The use and function of IsAlterable needs to be reviewed.  Nodes are a special case as they are generated outside of Speckle feeds 
            // and these ones need to be preserved
            if (recordsByKeyword[keyword][i].Latest == false)
            {
              indicesToRemove.Add(i);
            }
            else
            {
              recordsByKeyword[keyword][i].Previous = true;
              recordsByKeyword[keyword][i].Latest = false;
            }
          }

          for (int i = (indicesToRemove.Count() - 1); i >= 0; i--)
          {
            recordsByKeyword[keyword].RemoveAt(indicesToRemove[i]);
          }
        }
      });
    }

    #region methods_used_within_lock_by_UpsertProvisional
    private List<int> GetIndices(string keyword) => recordsByKeyword.ContainsKey(keyword)
        ? recordsByKeyword[keyword].Select(r => r.Index).Where(k => k > 0).OrderBy(i => i).ToList()
        : new List<int>();

    private int? HighestProvisional(string keyword)
    {
      if (!provisionals.ContainsKey(keyword) || provisionals[keyword] == null || provisionals[keyword].Count() == 0)
      {
        return null;
      }

      return provisionals[keyword].Keys.Max();
    }

    private void UpsertProvisional(string keyword, int index, string applicationId = "")
    {
      if (!provisionals.ContainsKey(keyword))
      {
        provisionals.Add(keyword, new Dictionary<int, string>());
      }
      provisionals[keyword].Add(index, applicationId);
    }

    private bool FindProvisionalIndex(string keyword, string applicationId, out int? provisionalIndex)
    {
      if (applicationId != null && provisionals.ContainsKey(keyword))
      {
        var matching = provisionals[keyword].Where(kvp => kvp.Value != null && kvp.Value.Equals(applicationId));
        if (matching.Count() > 0)
        {
          provisionalIndex = matching.First().Key;
          return true;
        }
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
      return provisionals[keyword].ContainsKey(index);
    }
    #endregion

    public bool ReserveIndex(string keyword, string applicationId)
    {
      return (ResolveIndex(keyword, applicationId) > 0);
    }

    public int ResolveIndex(string keyword, string applicationId = "")
    {
      return ExecuteWithLock(() =>
      {
        var kw = keyword.Split('.').First();
        if (applicationId == "")
        {
          var indices = GetIndices(kw);
          var highestProvisional = HighestProvisional(kw);
          var highestIndex = Math.Max((indices.Count() == 0) ? 0 : indices.Last(), highestProvisional ?? 0);
          for (int i = 1; i <= highestIndex; i++)
          {
            if (!indices.Contains(i) && !ProvisionalContains(kw, i))
            {
              UpsertProvisional(kw, i, applicationId);
              return i;
            }
          }

          UpsertProvisional(kw, highestIndex + 1, applicationId);
          return highestIndex + 1;
        }
        else
        {
          var matchingRecords = new List<GSACacheRecord>();

          if (recordsByKeyword.ContainsKey(kw))
          {
            matchingRecords.AddRange(recordsByKeyword[kw].Where(r => r.ApplicationId.EqualsWithoutSpaces(applicationId)).ToList());
          }

          if (matchingRecords.Count() == 0)
          {
            if (FindProvisionalIndex(kw, applicationId, out int? provisionalIndex))
            {
              return provisionalIndex.Value;
            }
            //No matches in either previous or latest
            var indices = GetIndices(kw);
            var highestProvisional = HighestProvisional(kw);
            var highestIndex = Math.Max((indices.Count() == 0) ? 0 : indices.Last(), highestProvisional ?? 0);
            for (int i = 1; i <= highestIndex; i++)
            {
              if (!indices.Contains(i) && !ProvisionalContains(kw, i))
              {
                UpsertProvisional(kw, i, applicationId);
                return i;
              }
            }
            UpsertProvisional(kw, highestIndex + 1, applicationId);
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
      });
    }

    public int? LookupIndex(string keyword, string applicationId)
    {
      var matchingRecords = new List<GSACacheRecord>();
      return ExecuteWithLock(() =>
      {
        var kw = keyword.Split('.').First();
        if (recordsByKeyword.ContainsKey(kw))
        {
          matchingRecords.AddRange(recordsByKeyword[kw].Where(r => r.Index > 0 && r.ApplicationId.EqualsWithoutSpaces(applicationId)));
        }
        if (matchingRecords.Count() == 0)
        {
          return null;
        }
        return (int?) matchingRecords.Select(r => r.Index).First();
      });
    }

    public List<int?> LookupIndices(string keyword, IEnumerable<string> applicationIds)
    {
      var matchingRecords = new List<GSACacheRecord>();

      return ExecuteWithLock(() =>
      {
        var kw = keyword.Split('.').First();
        if (recordsByKeyword.ContainsKey(kw) && applicationIds != null)
        {
          matchingRecords.AddRange(recordsByKeyword[kw].Where(r => r.Index > 0 && applicationIds.Any(id => r.ApplicationId.EqualsWithoutSpaces(id))));
        }

        if (matchingRecords.Count() == 0)
        {
          return new List<int?>();
        }
        return matchingRecords.Select(r => (int?)r.Index).ToList();
      });
    }

    public List<int?> LookupIndices(string keyword)
    {
      return ExecuteWithLock(() =>
      {
        var kw = keyword.Split('.').First();
        return (recordsByKeyword.ContainsKey(kw))
         ? recordsByKeyword[kw].Select(r => r.Index).Where(k => k > 0).Select(k => (int?)k).ToList()
         : new List<int?>();
      });
    }

    public List<Tuple<string, int, string, GwaSetCommandType>> GetExpiredData()
    {
      return ExecuteWithLock(() =>
      {
        var matchingRecords = records.Where(r => IsAlterable(r.Keyword, r.ApplicationId) && r.Previous == true && r.Latest == false).ToList();
        //Order by index as for some keywords (like LOAD_2D_FACE.2) the records do actually move indices when one is deleted
        matchingRecords = matchingRecords.OrderByDescending(r => r.Index).ToList();
        var returnData = new List<Tuple<string, int, string, GwaSetCommandType>>();

        for (int i = 0; i < matchingRecords.Count(); i++)
        {
          returnData.Add(new Tuple<string, int, string, GwaSetCommandType>(matchingRecords[i].Keyword, matchingRecords[i].Index, matchingRecords[i].Gwa, matchingRecords[i].GwaSetCommandType));
        }

        return returnData;
      });
    }

    public List<Tuple<string, int, string, GwaSetCommandType>> GetDeletableData()
    {
      return ExecuteWithLock(() =>
      {
        var matchingRecords = records.Where(r => IsAlterable(r.Keyword, r.ApplicationId) && r.Latest == true).ToList();
        var returnData = new List<Tuple<string, int, string, GwaSetCommandType>>();

        for (int i = 0; i < matchingRecords.Count(); i++)
        {
          returnData.Add(new Tuple<string, int, string, GwaSetCommandType>(matchingRecords[i].Keyword, matchingRecords[i].Index, matchingRecords[i].Gwa, matchingRecords[i].GwaSetCommandType));
        }

        return returnData;
      });
    }

    public List<string> ExpandLoadCasesAndCombinations(string loadCaseString)
    {
      var retList = new List<string>();

      if (string.IsNullOrEmpty(loadCaseString) || recordsByKeyword.Keys.Count == 0
        || !ProcessLoadCaseCombinationSpec(loadCaseString, out List<string> aParts, out List<string> cParts))
      {
        return retList;
      }

      var cachedAnalIndices = (recordsByKeyword.ContainsKey(analKey))
        ? recordsByKeyword[analKey].Select(r => r.Index).ToList()
        : new List<int>();

      var cachedComboIndices = (recordsByKeyword.ContainsKey(comboKey))
        ? recordsByKeyword[comboKey].Select(r => r.Index).ToList()
        : new List<int>();

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

      Task.WaitAll(tasks.ToArray());

      return retList;
    }

    private List<string> ExpandLoadCasesAndCombinationSubset(List<string> listParts, string marker, List<int> cachedIndices)
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
          : ExpandSubsetViaProxy(cachedIndices, listParts, marker);
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
    {
      return ExecuteWithLock(() =>
      {
        var kw = keyword.Split('.').First();
        if (applicationIdLookup.ContainsKey(kw))
        {
          if (applicationIdLookup[kw].ContainsKey(index))
          {
            return applicationIdLookup[kw][index];
          }
        }
        return "";
      });
    }

    public bool SetApplicationId(string keyword, int index, string applicationId)
    {
      return ExecuteWithLock(() =>
      {
        var kw = keyword.Split('.').First();
        if (!recordsByKeyword.ContainsKey(kw) || index >= recordsByKeyword[kw].Count())
        {
          return false;
        }
        var recordsWithSameIndex = recordsByKeyword[kw].Where(r => r.Index == index);
        if (recordsWithSameIndex.Count() == 0)
        {
          return false;
        }
        var record = recordsWithSameIndex.First();
        record.ApplicationId = applicationId;
        UpsertApplicationIdLookup(kw, index, applicationId);
        return true;
      }
      );
    }

    private void RemoveFromApplicationIdLookup(string keyword, int index)
    {
      ExecuteWithLock(() =>
      {
        var kw = keyword.Split('.').First();
        if (applicationIdLookup.ContainsKey(kw))
        {
          if (applicationIdLookup[kw].ContainsKey(index))
          {
            applicationIdLookup[kw].Remove(index);
          }
          if (applicationIdLookup[kw].Keys.Count() == 0)
          {
            applicationIdLookup.Remove(kw);
          }
        }
      });
    }

    #endregion

    private bool IsAlterable(string keyword, string applicationId)
    {
      if (keyword.Contains("NODE") && (applicationId == "" || (applicationId != null && applicationId.StartsWith("gsa"))))
      {
        return false;
      }
      return true;
    }

    public bool SetStream(string applicationId, string streamId)
    {
      if (string.IsNullOrEmpty(applicationId) || string.IsNullOrEmpty(streamId))
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
        if (streamIndexByApplicationId.ContainsKey(applicationId))
        {
          return false;
        }
        streamIndexByApplicationId.Add(applicationId, streamIndex);
        return true;
      });
    }

    public string LookupStream(string applicationId)
    {
      if (!string.IsNullOrEmpty(applicationId) && streamIndexByApplicationId.ContainsKey(applicationId))
      {
        var streamIndex = streamIndexByApplicationId[applicationId];
        if (streamIndex < streams.Count())
        {
          return streams[streamIndex];
        }
      }
      return "";
    }

    //For testing
    ReadOnlyCollection<GSACacheRecord> IGSACacheForTesting.Records => records;
  }
}
