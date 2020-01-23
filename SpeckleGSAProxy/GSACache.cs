using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SpeckleCore;
using SpeckleGSAInterfaces;

namespace SpeckleGSAProxy
{
  public class GSACache : IGSACache, IGSACacheForKit, IGSACacheForTesting
  {
    //private readonly List<GSACacheRecord> records = new List<GSACacheRecord>();
    private ReadOnlyCollection<GSACacheRecord> records => recordsByKeywordIndex.SelectMany(k => k.Value).Select(r => r.Value).ToList().AsReadOnly();

    private readonly Dictionary<string, Dictionary<int, GSACacheRecord>> recordsByKeywordIndex = new Dictionary<string, Dictionary<int, GSACacheRecord>>();

    //Shortcut optimisation for the records above
    private readonly Dictionary<string, Dictionary<int, string>> applicationIdLookup = new Dictionary<string, Dictionary<int, string>>();

    // < keyword , { < index, app_id >, < index, app_id >, ... } >
    private readonly Dictionary<string, Dictionary<int, string>> provisionals = new Dictionary<string, Dictionary<int, string>>();

    public Dictionary<int, object> GetIndicesSpeckleObjects(string speckleTypeName)
    {
      speckleTypeName = speckleTypeName.ChildType();
      return records.Where(r => r.SpeckleObj != null && r.SpeckleType == speckleTypeName).ToDictionary(v => v.Index, v => (object)v.SpeckleObj);
    }

    public List<SpeckleObject> GetSpeckleObjects(string speckleTypeName, string applicationId, bool? latest = true, string streamId = null)
    {
      speckleTypeName = speckleTypeName.ChildType();
      var matchingRecords = records.Where(r => r.SpeckleObj != null
        && r.SpeckleType == speckleTypeName
        && r.ApplicationId.EqualsWithoutSpaces(applicationId)
        && (string.IsNullOrEmpty(streamId) || r.StreamId.EqualsWithoutSpaces(streamId))
        && ((latest.HasValue && latest.Value) || (!latest.HasValue)));

      return matchingRecords.Select(r => r.SpeckleObj).ToList();
    }

    public bool ApplicationIdExists(string keyword, string applicationId)
    {
      if (recordsByKeywordIndex.ContainsKey(keyword))
      {
        return recordsByKeywordIndex[keyword].Select(d => d.Value).Any(r => r.ApplicationId.EqualsWithoutSpaces(applicationId));
      }
      return false;
    }

    public bool ContainsType(string speckleTypeName)
    {
      speckleTypeName = speckleTypeName.ChildType();
      return records.Any(r => r.SpeckleObj != null && r.SpeckleType == speckleTypeName);
    }

    //Used by the ToSpeckle methods in the kit; either the previous needs to be serialised for merging purposes during reception, or newly-arrived GWA needs to be serialised for transmission
    public Dictionary<int, string> GetGwaToSerialise(string keyword) => (recordsByKeywordIndex.ContainsKey(keyword))
      ? recordsByKeywordIndex[keyword].Select(d => d.Value)
        .Where(r => ((r.Previous == false && r.Latest == true && r.SpeckleObj == null) || (r.Previous == true && r.SpeckleObj == null)))
        .ToDictionary(r => r.Index, r => r.Gwa)
      : new Dictionary<int, string>();

    //TO DO: review if this is needed
    public List<string> GetNewlyAddedGwa()
      => records.Where(r => r.Previous == false && r.Latest == true).Select(r => r.Gwa).ToList();

    public List<string> GetGwa(string keyword, int index) => (recordsByKeywordIndex.ContainsKey(keyword) && recordsByKeywordIndex[keyword].ContainsKey(index))
      ? new List<string> { recordsByKeywordIndex[keyword][index].Gwa }
      : new List<string>();

    public List<string> GetGwa(string keyword) => (recordsByKeywordIndex.ContainsKey(keyword))
      ? recordsByKeywordIndex[keyword].Select(d => d.Value).Select(r => r.Gwa).ToList()
      : new List<string>();

    public List<string> GetCurrentGwa() => records.Where(r => r.Latest).Select(r => r.Gwa).ToList();

    public void Clear()
    {
      recordsByKeywordIndex.Clear();
      applicationIdLookup.Clear();
    }

    //For testing
    public List<string> GetGwaSetCommands() => records.Select(r => (r.GwaSetCommandType == GwaSetCommandType.Set) ? "SET\t" + r.Gwa
      : string.Join("\t", new[] { "SET_AT", r.Index.ToString(), r.Gwa })).ToList();

    public void MarkAsPrevious(string keyword, string applicationId)
    {
      if (recordsByKeywordIndex.ContainsKey(keyword))
      {
        var matchingRecords = recordsByKeywordIndex[keyword].Select(d => d.Value).Where(r => r.ApplicationId.EqualsWithoutSpaces(applicationId) && r.Latest).ToList();
        if (matchingRecords != null && matchingRecords.Count() > 0)
        {
          for (int i = 0; i < matchingRecords.Count(); i++)
          {
            matchingRecords[i].Previous = true;
            matchingRecords[i].Latest = false;
          }
        }
      }
    }

    public bool Upsert(string keyword, int index, string gwaWithoutSet, string streamId, string applicationId, GwaSetCommandType gwaSetCommandType)
    {
      return Upsert(keyword, index, gwaWithoutSet, applicationId, null, gwaSetCommandType, streamId: streamId);
    }

    //Not every record as stream IDs (like generated nodes)
    public bool Upsert(string keyword, int index, string gwa, string applicationId = "", SpeckleObject so = null, GwaSetCommandType gwaSetCommandType = GwaSetCommandType.Set, bool? latest = true, string streamId = null)
    {
      if (!recordsByKeywordIndex.ContainsKey(keyword))
      {
        recordsByKeywordIndex.Add(keyword, new Dictionary<int, GSACacheRecord>());
      }
      var sameKeywordRecords = recordsByKeywordIndex[keyword].Select(d => d.Value).ToList();
      var matchingRecords = sameKeywordRecords.Where(r => r.Index == index || r.Gwa.Equals(gwa, StringComparison.InvariantCultureIgnoreCase)).ToList();
      if (matchingRecords.Count() > 0)
      {
        var matchingGwaRecords = matchingRecords.Where(r => r.Gwa.Equals(gwa, StringComparison.InvariantCultureIgnoreCase)).ToList();
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
            matchingGwaRecords.First().Latest = latest.Value;
          }

          return true;
        }
        else
        {
          //These will be return at the next call to GetToBeDeletedGwa() and removed at the next call to Snapshot()
          for (int i = 0; i < matchingRecords.Count(); i++)
          {
            matchingRecords[i].Latest = false;
          }
        }
      }
      if (!recordsByKeywordIndex.ContainsKey(keyword))
      {
        recordsByKeywordIndex.Add(keyword, new Dictionary<int, GSACacheRecord>());
      }
      recordsByKeywordIndex[keyword][index] = new GSACacheRecord(keyword, index, gwa, streamId: streamId, applicationId: applicationId, latest: true, so: so, gwaSetCommandType: gwaSetCommandType);
      UpsertApplicationIdLookup(keyword, index, applicationId);
      RemoveFromProvisional(keyword, index);
      return true;
    }

    public bool AssignSpeckleObject(string keyword, string applicationId, SpeckleObject so, string streamId = null)
    {
      if (recordsByKeywordIndex.ContainsKey(keyword))
      {
        var matchingRecords = recordsByKeywordIndex[keyword].Select(d => d.Value)
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
    }
    public void Snapshot(string streamId)
    {
      //First remove all the non-latest records
      var keysToRemove = new List<string>();
      foreach (var keyword in recordsByKeywordIndex.Keys)
      {
        var indicesToRemove = new List<int>();
        foreach (var index in recordsByKeywordIndex[keyword].Keys)
        {
          if (recordsByKeywordIndex[keyword][index].Latest == false)
          {
            indicesToRemove.Add(index);
          }
        }
        foreach (var i in indicesToRemove)
        {
          recordsByKeywordIndex[keyword].Remove(i);
        }
        if (recordsByKeywordIndex[keyword].Keys.Count() == 0)
        {
          keysToRemove.Add(keyword);
        }
      }
      foreach (var k in keysToRemove)
      {
        recordsByKeywordIndex.Remove(k);
      }

      //Set all the current records to be previous and not latest
      foreach (var keyword in recordsByKeywordIndex.Keys)
      {
        foreach (var index in recordsByKeywordIndex[keyword].Keys)
        {
          recordsByKeywordIndex[keyword][index].Previous = true;
          recordsByKeywordIndex[keyword][index].Latest = true;
        }
      }
    }

    public int ResolveIndex(string keyword, string applicationId = "")
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

        if (recordsByKeywordIndex.ContainsKey(keyword))
        {
          matchingRecords.AddRange(recordsByKeywordIndex[keyword].Select(d => d.Value).Where(r => r.ApplicationId.EqualsWithoutSpaces(applicationId)).ToList());
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
    }

    public int? LookupIndex(string keyword, string applicationId)
    {
      var matchingRecords = new List<GSACacheRecord>();
      if (recordsByKeywordIndex.ContainsKey(keyword))
      {
        matchingRecords.AddRange(recordsByKeywordIndex[keyword].Select(d => d.Value).Where(r => r.Index > 0 && r.ApplicationId.EqualsWithoutSpaces(applicationId)));
      }
      if (matchingRecords.Count() == 0)
      {
        return null;
      }
      return matchingRecords.Select(r => r.Index).First();
    }

    public List<int?> LookupIndices(string keyword, IEnumerable<string> applicationIds)
    {
      var matchingRecords = new List<GSACacheRecord>();
      if (recordsByKeywordIndex.ContainsKey(keyword))
      {
        matchingRecords.AddRange(recordsByKeywordIndex[keyword].Select(d => d.Value).Where(r => r.Index > 0 && applicationIds.Any(id => r.ApplicationId.EqualsWithoutSpaces(id))));
      }
      if (matchingRecords.Count() == 0)
      {
        return new List<int?>();
      }
      return matchingRecords.Select(r => (int?)r.Index).ToList();
    }

    public List<int?> LookupIndices(string keyword) => (recordsByKeywordIndex.ContainsKey(keyword)) 
      ? recordsByKeywordIndex[keyword].Keys.Where(k => k > 0).Select(k => (int?)k).ToList()
      : new List<int?>();

    public List<Tuple<string, int, string, GwaSetCommandType>> GetExpiredData()
    {
      var matchingRecords = records.Where(r => IsAlterable(r.Keyword, r.ApplicationId) && r.Previous == true && r.Latest == false).ToList();
      var returnData = new List<Tuple<string, int, string, GwaSetCommandType>>();

      for (int i = 0; i < matchingRecords.Count(); i++)
      {
        returnData.Add(new Tuple<string, int, string, GwaSetCommandType>(matchingRecords[i].Keyword, matchingRecords[i].Index, matchingRecords[i].Gwa, matchingRecords[i].GwaSetCommandType));
      }

      return returnData;
    }

    public List<Tuple<string, int, string, GwaSetCommandType>> GetDeletableData()
    {
      var matchingRecords = records.Where(r => IsAlterable(r.Keyword, r.ApplicationId) && r.Latest == true).ToList();
      var returnData = new List<Tuple<string, int, string, GwaSetCommandType>>();

      for (int i = 0; i < matchingRecords.Count(); i++)
      {
        returnData.Add(new Tuple<string, int, string, GwaSetCommandType>(matchingRecords[i].Keyword, matchingRecords[i].Index, matchingRecords[i].Gwa, matchingRecords[i].GwaSetCommandType));
      }

      return returnData;
    }

    private List<int> GetIndices(string keyword) => (recordsByKeywordIndex.ContainsKey(keyword))
      ? recordsByKeywordIndex[keyword].Keys.Where(k => k > 0).OrderBy(i => i).ToList()
      : new List<int>();

    private bool IsAlterable(string keyword, string applicationId)
    {
      return (!(keyword.Contains("NODE") && applicationId != null && (applicationId.StartsWith("gsa") || applicationId == "")));
    }

    private void UpsertProvisional(string keyword, int index, string applicationId = "")
    {
      if (!provisionals.ContainsKey(keyword))
      {
        provisionals.Add(keyword, new Dictionary<int, string>());
      }
      provisionals[keyword].Add(index, applicationId);
    }

    private bool ProvisionalContains(string keyword, int index)
    {
      if (!provisionals.ContainsKey(keyword) || provisionals[keyword] == null || provisionals[keyword].Count() == 0)
      {
        return false;
      }
      return provisionals[keyword].ContainsKey(index);
    }

    private int? HighestProvisional(string keyword)
    {
      if (!provisionals.ContainsKey(keyword) || provisionals[keyword] == null || provisionals[keyword].Count() == 0)
      {
        return null;
      }

      return provisionals[keyword].Keys.Max();
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

    private bool FindProvisionalIndex(string keyword, string applicationId, out int? provisionalIndex)
    {
      provisionalIndex = null;
      if (provisionals.ContainsKey(keyword))
      {
        var matching = provisionals[keyword].Where(kvp => kvp.Value.Equals(applicationId));
        if (matching.Count() > 0)
        {
          provisionalIndex = matching.First().Key;
          return true;
        }
      }
      return false;
    }

    #region applicationIdLookup
    public string GetApplicationId(string keyword, int index)
    {
      if (applicationIdLookup.ContainsKey(keyword))
      {
        if (applicationIdLookup[keyword].ContainsKey(index))
        {
          return applicationIdLookup[keyword][index];
        }
      }
      return "";
    }

    private void UpsertApplicationIdLookup(string keyword, int index, string applicationId)
    {
      if (!applicationIdLookup.ContainsKey(keyword))
      {
        applicationIdLookup.Add(keyword, new Dictionary<int, string>());
      }
      applicationIdLookup[keyword][index] = applicationId.Replace(" ", ""); ;
    }

    private void RemoveFromApplicationIdLookup(string keyword, int index)
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
    }

    #endregion

    //For testing
    ReadOnlyCollection<GSACacheRecord> IGSACacheForTesting.Records => records;
  }
}
