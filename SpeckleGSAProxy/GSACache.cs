using System;
using System.Collections.Generic;
using System.Linq;
using SpeckleCore;
using SpeckleGSAInterfaces;

namespace SpeckleGSAProxy
{
  public class GSACache : IGSACache, IGSACacheForKit
  {
    private readonly List<GSACacheRecord> records = new List<GSACacheRecord>();

    public List<SpeckleObject> GetSpeckleObjects(Type t, string applicationId) => records.Where(r => r.Type == t && r.ApplicationId.Equals(applicationId) && r.SpeckleObject != null).Select(r => r.SpeckleObject).ToList();

    public bool Exists(string applicationId) => records.Any(r => r.ApplicationId == applicationId);

    public bool ContainsType(Type t) => records.Any(r => r.Type == t);    

    public List<string> GetNewlyAddedGwa() => records.Where(r => r.Previous != false && r.Latest == true).Select(r => r.Gwa).ToList();

    public List<string> GetCurrentSessionGwa() => records.Where(r => !r.CurrentSession).Select(r => r.Gwa).ToList();

    public List<string> GetToBeDeletedGwa() => records.Where(r => r.Previous != true && r.Latest == false).Select(r => r.Gwa).ToList();

    public void Clear() => records.Clear();

    public string GetApplicationId(string keyword, int index)
    {
      var matchingRecords = records.Where(r => r.Keyword.Equals(keyword, StringComparison.InvariantCultureIgnoreCase) && r.Index == index);
      return (matchingRecords == null || matchingRecords.Count() < 1) ? "" : matchingRecords.First().ApplicationId;
    }

    //The gwa could be a command that
    public bool Upsert(string keyword, int index, string gwa, string applicationId = "", SpeckleObject so = null, bool currentSession = true, GwaSetCommandType gwaSetCommandType = GwaSetCommandType.Set)
    {
      var sameKeywordRecords = records.Where(r => r.Keyword.Equals(keyword, StringComparison.InvariantCultureIgnoreCase)).ToList();
      var matchingRecords = sameKeywordRecords.Where(r => r.Index == index || r.Gwa.Equals(gwa, StringComparison.InvariantCultureIgnoreCase)).ToList();
      if (matchingRecords.Count() > 0)
      {
        //These will be return at the netx call to GetToBeDeletedGwa() and removed at the next call to Snapshot()
        for (int i = 0; i < matchingRecords.Count(); i++)
        {
          matchingRecords[i].Latest = false;
        }
      }

      records.Add(new GSACacheRecord(keyword, index, gwa, applicationId, latest: true, so: so, currentSession: currentSession, gwaSetCommandType: gwaSetCommandType));
      return true;
    }

    public bool AssignSpeckleObject(Type t, string applicationId, SpeckleObject so)
    {
      var matchingRecords = records.Where(r => r.Type == t && r.ApplicationId.Equals(applicationId));
      if (matchingRecords == null)
      {
        return false;
      }

      matchingRecords.First().SpeckleObject = so;
      return true;
    }

    public void Snapshot()
    {
      var indicesToRemove = new List<int>();
      for (int i = 0; i < records.Count(); i++)
      {
        if (records[i].Latest == false)
        {
          indicesToRemove.Add(i);
        }
      }
      for (int i = indicesToRemove.Count(); i > 0; i--)
      {
        records.RemoveAt(indicesToRemove[i - 1]);
      }

      for (int i = 0; i < records.Count(); i++)
      {
        records[i].Previous = true;
        records[i].Latest = null;
      }
    }

    private List<int> GetIndices(string keyword)
    {
      return records.Where(r => r.Keyword.Equals(keyword)).Select(r => r.Index).OrderBy(i => i).ToList();
    }

    public int ResolveIndex(string keyword, string type, string applicationId = "")
    {
      var existingIndex = LookupIndex(keyword, type, applicationId);
      if (existingIndex == null)
      {
        var indices = GetIndices(keyword);
        var highestIndex = indices.Last();
        for (int i = 1; i <= highestIndex; i++)
        {
          if (!indices.Contains(i))
          {
            return i;
          }
        }
        return highestIndex + 1;
      }
      return existingIndex.Value;
    }
    public int? LookupIndex(string keyword, string type, string applicationId)
    {
      var matchingRecords = records.Where(r => r.Keyword.Equals(keyword, StringComparison.InvariantCultureIgnoreCase) 
        && r.SpeckleObject != null && nameof(r.SpeckleObject.Type) == type
        && r.ApplicationId.Equals(applicationId));
      if (matchingRecords.Count() == 0)
      {
        return null;
      }
      return matchingRecords.Select(r => r.Index).First();
    }
    public List<int?> LookupIndices(string keyword, string type, IEnumerable<string> applicationIds)
    {
      var matchingRecords = records.Where(r => r.Keyword.Equals(keyword, StringComparison.InvariantCultureIgnoreCase)
        && r.SpeckleObject != null && nameof(r.SpeckleObject.Type) == type
        && applicationIds.Any(ai => r.ApplicationId.Equals(ai)));
      if (matchingRecords.Count() == 0)
      {
        return null;
      }
      return matchingRecords.Select(r => (int?) r.Index).ToList();
    }
  }
}
