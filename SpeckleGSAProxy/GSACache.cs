using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
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

    private object syncLock = new object();

    private T ExecuteWithLock<T>(Func<T> f)
    {
      var stackTrace = new StackTrace();
      var callingMethodName = ((stackTrace.GetFrames().Count() >= 2) ? stackTrace.GetFrames()[1].GetMethod().Name : stackTrace.GetFrames().Last().GetMethod().Name);
      //if (!callingMethodName.Equals("AssignSpeckleObject"))
      //{
      //  Debug.WriteLine("Lock asked for on thread: " + Thread.CurrentThread.ManagedThreadId + " method " + callingMethodName);
      //}
      lock (syncLock)
      {
        var ret = f();
        return ret;
      }
    }

    private void ExecuteWithLock(Action a)
    {
      var stackTrace = new StackTrace();
      var callingMethodName = ((stackTrace.GetFrames().Count() >= 2) ? stackTrace.GetFrames()[1].GetMethod().Name : stackTrace.GetFrames().Last().GetMethod().Name);
      //if (!callingMethodName.Equals("AssignSpeckleObject"))
      //{
      //  Debug.WriteLine("Lock asked for on thread: " + Thread.CurrentThread.ManagedThreadId + " method " + callingMethodName);
      //}
      lock (syncLock)
      {
        a();
      }
    }

    //private readonly List<GSACacheRecord> records = new List<GSACacheRecord>();
    private ReadOnlyCollection<GSACacheRecord> records => recordsByKeyword.SelectMany(k => k.Value).ToList().AsReadOnly();

    //There could be multiple entries at the same index - namely, a previous and a latest
    private readonly Dictionary<string, List<GSACacheRecord>> recordsByKeyword = new Dictionary<string, List<GSACacheRecord>>();

    //Shortcut optimisation for the records above
    private readonly Dictionary<string, Dictionary<int, string>> applicationIdLookup = new Dictionary<string, Dictionary<int, string>>();

    // < keyword , { < index, app_id >, < index, app_id >, ... } >
    private readonly Dictionary<string, Dictionary<int, string>> provisionals = new Dictionary<string, Dictionary<int, string>>();

    //Hardcoded for now to use current 10.1 keywords - to be reviewed
    private readonly string analKey = "ANAL.1";
    private readonly string comboKey = "COMBINATION.1";

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
        if (recordsByKeyword.ContainsKey(keyword))
        {
          return recordsByKeyword[keyword].Any(r => r.ApplicationId.EqualsWithoutSpaces(applicationId));
        }
        return false;
      });

    public bool ContainsType(string speckleTypeName)
    {
      speckleTypeName = speckleTypeName.ChildType();
      return ExecuteWithLock(() => records.Any(r => r.SpeckleObj != null && r.SpeckleType == speckleTypeName));
    }

    //Used by the ToSpeckle methods in the kit; either the previous needs to be serialised for merging purposes during reception, or newly-arrived GWA needs to be serialised for transmission
    public Dictionary<int, string> GetGwaToSerialise(string keyword) => ExecuteWithLock(() => (recordsByKeyword.ContainsKey(keyword))
      ? recordsByKeyword[keyword]
        .Where(r => ((r.Previous == false && r.Latest == true && r.SpeckleObj == null) || (r.Previous == true && r.SpeckleObj == null)))
        .ToDictionary(r => r.Index, r => r.Gwa)
      : new Dictionary<int, string>());

    //TO DO: review if this is needed
    public List<string> GetNewlyAddedGwa() => ExecuteWithLock(()
      => records.Where(r => r.Previous == false && r.Latest == true).Select(r => r.Gwa).ToList());

    //To review: does this need to be latest records only?
    public List<string> GetGwa(string keyword, int index) => ExecuteWithLock(() => (recordsByKeyword.ContainsKey(keyword))
      ? recordsByKeyword[keyword].Where(r => r.Index == index).Select(r => r.Gwa).ToList()
      : new List<string>());

    public List<string> GetGwa(string keyword) => ExecuteWithLock(() => (recordsByKeyword.ContainsKey(keyword))
      ? recordsByKeyword[keyword].Select(r => r.Gwa).ToList()
      : new List<string>());

    public List<string> GetCurrentGwa() => ExecuteWithLock(() => records.Where(r => r.Latest).Select(r => r.Gwa).ToList());

    public void Clear()
    {
      ExecuteWithLock(() =>
      {
        recordsByKeyword.Clear();
        applicationIdLookup.Clear();
        provisionals.Clear();
      });
    }

    //For testing
    public List<string> GetGwaSetCommands() => ExecuteWithLock(() => records.Select(r => (r.GwaSetCommandType == GwaSetCommandType.Set) ? "SET\t" + r.Gwa
      : string.Join("\t", new[] { "SET_AT", r.Index.ToString(), r.Gwa })).ToList());

    public void MarkAsPrevious(string keyword, string applicationId)
    {
      ExecuteWithLock(() =>
      {
        if (recordsByKeyword.ContainsKey(keyword))
        {
          var matchingRecords = recordsByKeyword[keyword].Where(r => r.ApplicationId.EqualsWithoutSpaces(applicationId) && r.Latest).ToList();
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
      if (!applicationIdLookup.ContainsKey(keyword))
      {
        applicationIdLookup.Add(keyword, new Dictionary<int, string>());
      }
      applicationIdLookup[keyword][index] = applicationId.Replace(" ", "");
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
      try
      {
        var matchingRecords = new List<GSACacheRecord>();
        ExecuteWithLock(() =>
        {
          if (!recordsByKeyword.ContainsKey(keyword))
          {
            recordsByKeyword.Add(keyword, new List<GSACacheRecord>());
          }
          matchingRecords = recordsByKeyword[keyword].Where(r => r.Index == index).ToList();
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
          if (!recordsByKeyword.ContainsKey(keyword))
          {
            recordsByKeyword.Add(keyword, new List<GSACacheRecord>());
          }
          recordsByKeyword[keyword].Add(new GSACacheRecord(keyword, index, gwa, streamId: streamId, applicationId: applicationId, latest: true, so: so, gwaSetCommandType: gwaSetCommandType));

          UpsertApplicationIdLookup(keyword, index, applicationId);
          RemoveFromProvisional(keyword, index);
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
        if (recordsByKeyword.ContainsKey(keyword))
        {
          var matchingRecords = recordsByKeyword[keyword]
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

    public int ResolveIndex(string keyword, string applicationId = "")
    {
      return ExecuteWithLock(() =>
      {
        if (applicationId == "")
        {
          var indices = GetIndices(keyword);
          var highestProvisional = HighestProvisional(keyword);
          var highestIndex = Math.Max((indices.Count() == 0) ? 0 : indices.Last(), highestProvisional ?? 0);
          for (int i = 1; i <= highestIndex; i++)
          {
            if (!indices.Contains(i) && !ProvisionalContains(keyword, i))
            {
              UpsertProvisional(keyword, i, applicationId);
              return i;
            }
          }

          UpsertProvisional(keyword, highestIndex + 1, applicationId);
          return highestIndex + 1;
        }
        else
        {
          var matchingRecords = new List<GSACacheRecord>();

          if (recordsByKeyword.ContainsKey(keyword))
          {
            matchingRecords.AddRange(recordsByKeyword[keyword].Where(r => r.ApplicationId.EqualsWithoutSpaces(applicationId)).ToList());
          }

          if (matchingRecords.Count() == 0)
          {
            if (FindProvisionalIndex(keyword, applicationId, out int? provisionalIndex))
            {
              return provisionalIndex.Value;
            }
            //No matches in either previous or latest
            var indices = GetIndices(keyword);
            var highestProvisional = HighestProvisional(keyword);
            var highestIndex = Math.Max((indices.Count() == 0) ? 0 : indices.Last(), highestProvisional ?? 0);
            for (int i = 1; i <= highestIndex; i++)
            {
              if (!indices.Contains(i) && !ProvisionalContains(keyword, i))
              {
                UpsertProvisional(keyword, i, applicationId);
                return i;
              }
            }
            UpsertProvisional(keyword, highestIndex + 1, applicationId);
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
        if (recordsByKeyword.ContainsKey(keyword))
        {
          matchingRecords.AddRange(recordsByKeyword[keyword].Where(r => r.Index > 0 && r.ApplicationId.EqualsWithoutSpaces(applicationId)));
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
        if (recordsByKeyword.ContainsKey(keyword) && applicationIds != null)
        {
          matchingRecords.AddRange(recordsByKeyword[keyword].Where(r => r.Index > 0 && applicationIds.Any(id => r.ApplicationId.EqualsWithoutSpaces(id))));
        }

        if (matchingRecords.Count() == 0)
        {
          return new List<int?>();
        }
        return matchingRecords.Select(r => (int?)r.Index).ToList();
      });
    }

    public List<int?> LookupIndices(string keyword) => ExecuteWithLock(() 
      => (recordsByKeyword.ContainsKey(keyword)) 
        ? recordsByKeyword[keyword].Select(r => r.Index).Where(k => k > 0).Select(k => (int?)k).ToList()
        : new List<int?>());

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
          gsaProxy.SetGwa(string.Join("\t", new[] { "SET", "NODE.3", indexStr, indexStr, "NO_RGB", "0", "0", "0" }));
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
        if (applicationIdLookup.ContainsKey(keyword))
        {
          if (applicationIdLookup[keyword].ContainsKey(index))
          {
            return applicationIdLookup[keyword][index];
          }
        }
        return "";
      });
    }

    private void RemoveFromApplicationIdLookup(string keyword, int index)
    {
      ExecuteWithLock(() =>
      {
        if (applicationIdLookup.ContainsKey(keyword))
        {
          if (applicationIdLookup[keyword].ContainsKey(index))
          {
            applicationIdLookup[keyword].Remove(index);
          }
          if (applicationIdLookup[keyword].Keys.Count() == 0)
          {
            applicationIdLookup.Remove(keyword);
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

    //For testing
    ReadOnlyCollection<GSACacheRecord> IGSACacheForTesting.Records => records;
  }
}
