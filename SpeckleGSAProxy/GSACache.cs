using System;
using System.Collections.Generic;
using System.Linq;
using SpeckleCore;
using SpeckleGSAInterfaces;

namespace SpeckleGSAProxy
{
  public class GSACache : IGSACache, IGSACacheForKit, IGSACacheForTesting
  {
    private readonly List<GSACacheRecord> records = new List<GSACacheRecord>();

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
        && r.ApplicationId.SidValueCompare(applicationId)
        && (string.IsNullOrEmpty(streamId) || r.StreamId.SidValueCompare(streamId))
        && ((latest.HasValue && latest.Value) || (!latest.HasValue)));
      
      return matchingRecords.Select(r => r.SpeckleObj).ToList();
    }

    public bool ApplicationIdExists(string keyword, string applicationId) 
      => records.Any(r => r.Keyword.Equals(keyword, StringComparison.InvariantCultureIgnoreCase) && r.ApplicationId.SidValueCompare(applicationId) && r.Latest == true);

    public bool ContainsType(string speckleTypeName)
    {
      speckleTypeName = speckleTypeName.ChildType();
      return records.Any(r => r.SpeckleObj != null && r.SpeckleType == speckleTypeName);
    }

    //Used by the ToSpeckle methods in the kit; either the previous needs to be serialised for merging purposes during reception, or newly-arrived GWA needs to be serialised for transmission
    public Dictionary<int, string> GetGwaToSerialise(string keyword) 
      => records.Where(r => r.Keyword.Equals(keyword, StringComparison.InvariantCultureIgnoreCase)
        && ((r.Previous == false && r.Latest == true && r.SpeckleObj == null) || (r.Previous == true && r.SpeckleObj == null))).ToDictionary(r => r.Index, r => r.Gwa);

    //TO DO: review if this is needed
    public List<string> GetNewlyAddedGwa() 
      => records.Where(r => r.Previous == false && r.Latest == true).Select(r => r.Gwa).ToList();

    public List<string> GetGwa(string keyword, int index) 
      => records.Where(r => r.Keyword.Equals(keyword, StringComparison.InvariantCultureIgnoreCase) && r.Index == index).Select(r => r.Gwa).ToList();

    public List<string> GetGwa(string keyword) 
      => records.Where(r => r.Keyword.Equals(keyword, StringComparison.InvariantCultureIgnoreCase)).Select(r => r.Gwa).ToList();

    public List<string> GetCurrentGwa() 
      => records.Where(r => r.Latest).Select(r => r.Gwa).ToList();

    public void Clear() => records.Clear();

    //For testing
    public List<string> GetGwaSetCommands() => records.Select(r => (r.GwaSetCommandType == GwaSetCommandType.Set) ? "SET\t" + r.Gwa
      : string.Join("\t", new[] { "SET_AT", r.Index.ToString(), r.Gwa })).ToList();

    public void MarkAsPrevious(string keyword, string sid)
    {
      var matchingRecords = records.Where(r => r.Keyword.Equals(keyword, StringComparison.InvariantCultureIgnoreCase) && r.Sid.SidValueCompare(sid) && r.Latest).ToList();
      if (matchingRecords != null && matchingRecords.Count() > 0)
      {
        for (int i = 0; i < matchingRecords.Count(); i++)
        {
          matchingRecords[i].Previous = true;
          matchingRecords[i].Latest = false;
        }
      }
    }

    public string GetSid(string keyword, int index)
    {
      var matchingRecords = records.Where(r => r.Keyword.Equals(keyword, StringComparison.InvariantCultureIgnoreCase) && r.Index == index);
      return (matchingRecords == null || matchingRecords.Count() < 1) ? "" : matchingRecords.First().Sid;
    }

    public bool Upsert(string keyword, int index, string gwaWithoutSet, string applicationId, GwaSetCommandType gwaSetCommandType)
    {
      return Upsert(keyword, index, gwaWithoutSet, applicationId, null, gwaSetCommandType);
    }

    //Not every record as stream IDs (like generated nodes)
    public bool Upsert(string keyword, int index, string gwa, string applicationId = "", SpeckleObject so = null, GwaSetCommandType gwaSetCommandType = GwaSetCommandType.Set, bool? latest = true, string streamId = null)
    {
      var sameKeywordRecords = records.Where(r => r.Keyword.Equals(keyword, StringComparison.InvariantCultureIgnoreCase)
        && (!latest.HasValue || (latest.HasValue && r.Latest == latest))).ToList();
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
          matchingGwaRecords.First().Latest = true;

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

      records.Add(new GSACacheRecord(keyword, index, gwa, streamId: streamId, applicationId: applicationId, latest: true, so: so, gwaSetCommandType: gwaSetCommandType));
      return true;
    }

    public bool AssignSpeckleObject(string keyword, string applicationId, SpeckleObject so, string streamId = null)
    {
      var matchingRecords = records.Where(r => r.Keyword.Equals(keyword, StringComparison.InvariantCultureIgnoreCase)
        && !string.IsNullOrEmpty(r.ApplicationId) && r.ApplicationId.SidValueCompare(applicationId)
        && (string.IsNullOrEmpty(streamId) || (!string.IsNullOrEmpty(r.StreamId) && r.StreamId.Equals(streamId ?? "")))
        && r.SpeckleObj == null);

      if (matchingRecords == null || matchingRecords.Count() == 0)
      {
        return false;
      }

      matchingRecords.First().SpeckleObj = so;
      return true;
    }

    public void Snapshot(string streamId)
    {
      var indicesToRemove = new List<int>();
      //This needs to be reviewed.  Nodes are a special case as they are generated outside of Speckle feeds and these ones need to be preserved
      var relevantRecords = records.Where(r => r.StreamId != null && r.StreamId == streamId && IsAlterable(r.Keyword, r.Sid)).ToList();
      for (int i = 0; i < relevantRecords.Count(); i++)
      {
        if (relevantRecords[i].Latest == false)
        {
          indicesToRemove.Add(i);
        }
      }
      for (int i = indicesToRemove.Count(); i > 0; i--)
      {
        records.RemoveAt(indicesToRemove[i - 1]);
      }

      for (int i = 0; i < relevantRecords.Count(); i++)
      {
        relevantRecords[i].Previous = true;
        relevantRecords[i].Latest = false;
      }
    }

    public int ResolveIndex(string keyword, string applicationId = "")
    {
      var matchingRecords = records.Where(r => r.Keyword.Equals(keyword, StringComparison.InvariantCultureIgnoreCase) 
        && r.ApplicationId.SidValueCompare(applicationId));

      if (matchingRecords.Count() == 0)
      {
        //No matches in either previous or latest
        var indices = GetIndices(keyword);
        var highestIndex = (indices.Count() == 0) ? 0 : indices.Last();
        for (int i = 1; i <= highestIndex; i++)
        {
          if (!indices.Contains(i))
          {
            return i;
          }
        }
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

    public int? LookupIndex(string keyword, string applicationId)
    {
      var matchingRecords = records.Where(r => r.Keyword.Equals(keyword, StringComparison.InvariantCultureIgnoreCase) && r.Index > 0
        && r.ApplicationId.SidValueCompare(applicationId));
      if (matchingRecords.Count() == 0)
      {
        return null;
      }
      return matchingRecords.Select(r => r.Index).First();
    }
    public List<int?> LookupIndices(string keyword, IEnumerable<string> applicationIds)
    {
      var matchingRecords = records.Where(r => r.Keyword.Equals(keyword, StringComparison.InvariantCultureIgnoreCase) && r.Index > 0
        && applicationIds.Any(id => r.ApplicationId.SidValueCompare(id)));
      if (matchingRecords.Count() == 0)
      {
        return new List<int?>();
      }
      return matchingRecords.Select(r => (int?)r.Index).ToList();
    }

    public List<int?> LookupIndices(string keyword)
    {
      var matchingRecords = records.Where(r => r.Keyword.Equals(keyword, StringComparison.InvariantCultureIgnoreCase) && r.Index > 0);
      if (matchingRecords.Count() == 0)
      {
        return new List<int?>();
      }
      return matchingRecords.Select(r => (int?)r.Index).ToList();
    }

    public List<Tuple<string, int, string, GwaSetCommandType>> GetExpiredData()
    {
      var matchingRecords = records.Where(r => IsAlterable(r.Keyword, r.Sid) && r.Previous == true && r.Latest == false).ToList();
      var returnData = new List<Tuple<string, int, string, GwaSetCommandType>>();

      for (int i = 0; i < matchingRecords.Count(); i++)
      {
        returnData.Add(new Tuple<string, int, string, GwaSetCommandType>(matchingRecords[i].Keyword, matchingRecords[i].Index, matchingRecords[i].Gwa, matchingRecords[i].GwaSetCommandType));
      }

      return returnData;
    }

    public List<Tuple<string, int, string, GwaSetCommandType>> GetDeletableData()
    {
      var matchingRecords = records.Where(r => IsAlterable(r.Keyword, r.Sid) && r.Latest == true).ToList();
      var returnData = new List<Tuple<string, int, string, GwaSetCommandType>>();

      for (int i = 0; i < matchingRecords.Count(); i++)
      {
        returnData.Add(new Tuple<string, int, string, GwaSetCommandType>(matchingRecords[i].Keyword, matchingRecords[i].Index, matchingRecords[i].Gwa, matchingRecords[i].GwaSetCommandType));
      }

      return returnData;
    }

    private List<int> GetIndices(string keyword)
    {
      return records.Where(r => r.Keyword.Equals(keyword)).Select(r => r.Index).OrderBy(i => i).ToList();
    }

    private bool IsAlterable(string keyword, string applicationId)
    {
      return (!(keyword.Contains("NODE") && applicationId != null && (applicationId.StartsWith("gsa") || applicationId == "")));
    }

    //For testing
    List<GSACacheRecord> IGSACacheForTesting.Records => records;
  }
}
