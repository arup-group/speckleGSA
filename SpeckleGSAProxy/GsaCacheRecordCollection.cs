using SpeckleCore;
using SpeckleGSAInterfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SpeckleGSAProxy
{
  internal class GsaCacheRecordCollection : IGSACacheRecordCollection
  {
    private readonly List<GSACacheRecord> records = new List<GSACacheRecord>();
    private readonly Dictionary<string, HashSet<int>> collectionIndicesByKw = new Dictionary<string, HashSet<int>>();
    private readonly Dictionary<string, HashSet<int>> collectionIndicesByApplicationId = new Dictionary<string, HashSet<int>>();
    private readonly Dictionary<string, HashSet<int>> collectionIndicesByStreamId = new Dictionary<string, HashSet<int>>();
    private readonly Dictionary<string, HashSet<int>> collectionIndicesBySpeckleTypeName = new Dictionary<string, HashSet<int>>();

    public bool AssignApplicationId(string kw, int gsaIndex, string applicationId)
    {
      if (string.IsNullOrEmpty(kw) || !collectionIndicesByKw.ContainsKey(kw) || string.IsNullOrEmpty(applicationId))
      {
        return false;
      }
      var colIndices = collectionIndicesByKw[kw].Where(i => records[i].Index == gsaIndex).OrderBy(i => records[i].Index).ToList();
      if (colIndices.Count() == 0)
      {
        return false;
      }
      var colIndex = colIndices.First();
      records[colIndex].ApplicationId = applicationId;
      if (!collectionIndicesByApplicationId.ContainsKey(applicationId))
      {
        collectionIndicesByApplicationId.Add(applicationId, new HashSet<int>());
      }
      collectionIndicesByApplicationId[applicationId].Add(colIndex);
      return true;
      /*
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
        */
    }

    public bool AssignSpeckleObject(string kw, string applicationId, SpeckleObject so, string streamId)
    {
      if (string.IsNullOrEmpty(kw) || !collectionIndicesByKw.ContainsKey(kw) || string.IsNullOrEmpty(applicationId) 
        || !collectionIndicesByApplicationId.ContainsKey(applicationId))
      {
        return false;
      }
      var colIndices = collectionIndicesByKw[kw].Intersect(collectionIndicesByApplicationId[applicationId]);
      if (!string.IsNullOrEmpty(streamId))
      {
        colIndices = colIndices.Intersect(collectionIndicesByStreamId[streamId]);
      }
      if (colIndices.Count() == 0)
      {
        return false;
      }
      var matching = colIndices.Where(i => records[i].SpeckleObj == null).OrderBy(i => records[i].Index).ToList();
      var colIndex = colIndices.First();
      records[colIndex].SpeckleObj = so;
      var speckleType = SpeckleTypeName(so);
      if (!collectionIndicesBySpeckleTypeName.ContainsKey(speckleType))
      {
        collectionIndicesBySpeckleTypeName.Add(speckleType, new HashSet<int>());
      }
      collectionIndicesBySpeckleTypeName[speckleType].Add(colIndex);
      return true;
      /*
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
      */
    }

    public void Clear()
    {
      //recordsByKeyword.Clear();
      //applicationIdLookup.Clear();
      records.Clear();
      collectionIndicesByApplicationId.Clear();
      collectionIndicesByKw.Clear();
      collectionIndicesBySpeckleTypeName.Clear();
      collectionIndicesByStreamId.Clear();
    }

    public bool ContainsKeyword(string keyword, string applicationId)
    {
      if (string.IsNullOrEmpty(keyword) || !collectionIndicesByKw.ContainsKey(keyword) 
        || string.IsNullOrEmpty(applicationId) || !collectionIndicesByApplicationId.ContainsKey(applicationId))
      {
        return false;
      }
      var colIndices = collectionIndicesByApplicationId[applicationId].Intersect(collectionIndicesByKw[keyword]).ToList();
      return colIndices.Count() > 0;
    }

    public bool ContainsSpeckleType(string speckleTypeName)
    {
      return records.Any(r => r.SpeckleObj != null && r.SpeckleType == speckleTypeName);
    }

    public List<GSACacheRecord> GetAllRecords(string kw, int gsaIndex)
    {
      //if (!recordsByKeyword.ContainsKey(kw))
      //{
      //  recordsByKeyword.Add(kw, new List<GSACacheRecord>());
      //}
      //matchingRecords = recordsByKeyword[kw].Where(r => r.Index == index).ToList();
      if (string.IsNullOrEmpty(kw) || !collectionIndicesByKw.ContainsKey(kw))
      {
        return new List<GSACacheRecord>();
      }
      return collectionIndicesByKw[kw].Where(i => records[i].Index == gsaIndex).OrderBy(i => records[i].Index).Select(i => records[i]).ToList();
    }

    public List<GSACacheRecord> GetAllRecords()
    {
      return records;
    }

    public List<string> GetAllRecordsByKeyword(string kw)
    {
      if (string.IsNullOrEmpty(kw) || !collectionIndicesByKw.ContainsKey(kw))
      {
        return new List<string>();
      }
      //return (recordsByKeyword.ContainsKey(kw)) ? recordsByKeyword[kw].Select(r => r.Gwa).ToList() : new List<string>();
      return collectionIndicesByKw[kw].OrderBy(i => records[i].Index).Select(i => records[i].Gwa).ToList();
    }

    public string GetApplicationId(string kw, int gsaIndex)
    {
      if (string.IsNullOrEmpty(kw) || !collectionIndicesByKw.ContainsKey(kw))
      {
        return "";
      }
      return collectionIndicesByKw[kw].Where(i => records[i].Index == gsaIndex).OrderBy(i => i).Select(i => records[i].ApplicationId).FirstOrDefault();
      /*
        if (applicationIdLookup.ContainsKey(kw))
        {
          if (applicationIdLookup[kw].ContainsKey(index))
          {
            return applicationIdLookup[kw][index];
          }
        }
        return "";
        */
    }

    public List<Tuple<string, int, string, GwaSetCommandType>> GetDeletableData()
    {
      var matchingRecords = records.Where(r => IsAlterable(r.Keyword, r.ApplicationId) && r.Latest == true).ToList();
      var returnData = new List<Tuple<string, int, string, GwaSetCommandType>>();

      for (int i = 0; i < matchingRecords.Count(); i++)
      {
        returnData.Add(new Tuple<string, int, string, GwaSetCommandType>
          (matchingRecords[i].Keyword, matchingRecords[i].Index, matchingRecords[i].Gwa, matchingRecords[i].GwaSetCommandType));
      }

      return returnData;
    }

    public List<Tuple<string, int, string, GwaSetCommandType>> GetExpiredData()
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
    }

    public List<string> GetGwaCommandsWithSet()
    {
      //public List<string> GetGwaSetCommands() => ExecuteWithLock(() => records.Select(r => (r.GwaSetCommandType == GwaSetCommandType.Set) ? "SET\t" + r.Gwa
      //  : string.Join(GSAProxy.GwaDelimiter.ToString(), new[] { "SET_AT", r.Index.ToString(), r.Gwa })).ToList());
      return records.OrderBy(r => r.Keyword).ThenBy(r => r.Index).Select(r => (r.GwaSetCommandType == GwaSetCommandType.Set) 
      ? "SET\t" + r.Gwa 
      : string.Join(GSAProxy.GwaDelimiter.ToString(), new[] { "SET_AT", r.Index.ToString(), r.Gwa })).ToList();
    }

    public Dictionary<int, string> GetIndexedGwa(string kw)
    {
      if (string.IsNullOrEmpty(kw) || !collectionIndicesByKw.ContainsKey(kw))
      {
        return new Dictionary<int, string>();
      }
      /*
        return (recordsByKeyword.ContainsKey(kw))
          ? recordsByKeyword[kw]
            .Where(r => ((r.Previous == false && r.Latest == true && r.SpeckleObj == null) || (r.Previous == true && r.SpeckleObj == null)))
            .ToDictionary(r => r.Index, r => r.Gwa)
          : new Dictionary<int, string>();
        */
      return collectionIndicesByKw[kw].Where(i => (!records[i].Previous && records[i].Latest && records[i].SpeckleObj == null)
        || (records[i].Previous && records[i].SpeckleObj == null)).OrderBy(i => records[i].Index).ToDictionary(i => records[i].Index, i => records[i].Gwa);
    }

    public List<string> GetLatestGwa()
    {
      return records.Where(r => r.Latest).Select(r => r.Gwa).ToList();
    }

    public HashSet<int> GetRecordIndexHashSet(string kw)
    {
      if (string.IsNullOrEmpty(kw) || !collectionIndicesByKw.ContainsKey(kw))
      {
        return new HashSet<int>();
      }
      //should return GSA indices, be ordered!

      var gsaIndexHash = new HashSet<int>();
      foreach (var i in collectionIndicesByKw[kw])
      {
        if (!gsaIndexHash.Contains(records[i].Index))
        {
          gsaIndexHash.Add(records[i].Index);
        }
      }

      return gsaIndexHash;
      /*
      var cachedAnalIndices = (recordsByKeyword.ContainsKey(analKey)
        ? recordsByKeyword[analKey].Select(r => r.Index).ToList()
        : new List<int>();
      */
    }

    public SortedSet<int> GetRecordIndices(string kw)
    {
      if (string.IsNullOrEmpty(kw) || !collectionIndicesByKw.ContainsKey(kw))
      {
        return new SortedSet<int>();
      }
      //should return GSA indices, and be ordered!

      var gsaIndexHash = GetRecordIndexHashSet(kw);
      var retSet = new SortedSet<int>();
      foreach (var i in gsaIndexHash)
      {
        retSet.Add(i);
      }
      return retSet;
      /*
      var cachedAnalIndices = (recordsByKeyword.ContainsKey(analKey)
        ? recordsByKeyword[analKey].Select(r => r.Index).ToList()
        : new List<int>();
      */
    }

    public Dictionary<string, List<GSACacheRecord>> GetLatestRecordsByKeyword()
    {
      //The list should be sorted by GSA index
      var retDict = new Dictionary<string, List<GSACacheRecord>>();
      foreach (var kw in collectionIndicesByKw.Keys)
      {
        if (!retDict.ContainsKey(kw))
        {
          retDict.Add(kw, new List<GSACacheRecord>());
        }
        var latestRecords = collectionIndicesByKw[kw].Where(i => !records[i].Previous && records[i].Latest).OrderBy(i => records[i].Index).Select(i => records[i]);
        retDict[kw].AddRange(latestRecords);
      }
      return retDict;
      //var latestRecords = records.Where(r => r.Previous == false && r.Latest == true);        
      //var latestRecordsByKeyword = latestRecords.GroupBy(r => r.Keyword).ToDictionary(g => g.Key, g => g.ToList());
    }

    public List<string> GetRecord(string keyword, int gsaIndex)
    {
      if (string.IsNullOrEmpty(keyword) || !collectionIndicesByKw.ContainsKey(keyword))
      {
        return new List<string>();
      }
      var colIndices = collectionIndicesByKw[keyword].Where(i => records[i].Index == gsaIndex);
      return (colIndices.Count() == 0) ? new List<string>() : colIndices.Select(i => records[i].Gwa).ToList();
      //return (recordsByKeyword.ContainsKey(kw)) ? recordsByKeyword[kw].Where(r => r.Index == index).Select(r => r.Gwa).ToList() : new List<string>();
    }

    public bool GetRecordSummaries(string kw, out List<string> gwa, out List<int> gsaIndices, out List<string> applicationIds)
    {
      if (string.IsNullOrEmpty(kw) || !collectionIndicesByKw.ContainsKey(kw))
      {
        gsaIndices = new List<int>();
        gwa = new List<string>();
        applicationIds = new List<string>();
        return false;
      }
      gsaIndices = collectionIndicesByKw[kw].Select(i => records[i].Index).OrderBy(i => i).ToList();
      gwa = collectionIndicesByKw[kw].Select(i => records[i].Gwa).ToList();
      applicationIds = collectionIndicesByKw[kw].Select(i => records[i].ApplicationId).ToList();
      return true;
      /*
      gwa = new List<string>();
      indices = new List<int>();
      applicationIds = new List<string>();
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
      */
    }

    public List<SpeckleObject> GetSpeckleObjects(string speckleTypeName, string applicationId, bool? latest, string streamId)
    {
      speckleTypeName = FormatSpeckleTypeName(speckleTypeName);
      if (string.IsNullOrEmpty(speckleTypeName) || !collectionIndicesBySpeckleTypeName.ContainsKey(speckleTypeName) 
        || string.IsNullOrEmpty(applicationId) || !collectionIndicesByApplicationId.ContainsKey(applicationId))
      {
        return new List<SpeckleObject>();
      }
      var colIndices = collectionIndicesByApplicationId[applicationId].Intersect(collectionIndicesBySpeckleTypeName[speckleTypeName]).OrderBy(i => i);
      return colIndices.Where(i => records[i].SpeckleObj != null).Select(i => records[i].SpeckleObj).ToList();
      /*
        var matchingRecords = records.Where(r => r.SpeckleObj != null
          && r.SpeckleType == speckleTypeName
          && r.ApplicationId.EqualsWithoutSpaces(applicationId)
          && (string.IsNullOrEmpty(streamId) || r.StreamId.EqualsWithoutSpaces(streamId))
          && ((latest.HasValue && latest.Value) || (!latest.HasValue)));
        return matchingRecords.Select(r => r.SpeckleObj).ToList();
        */
    }

    public Dictionary<int, object> GetSpeckleObjectsByTypeName(string speckleTypeName)
    {
      if (string.IsNullOrEmpty(speckleTypeName) || !collectionIndicesBySpeckleTypeName.ContainsKey(speckleTypeName))
      {
        return new Dictionary<int, object>();
      }
      return collectionIndicesBySpeckleTypeName[speckleTypeName].OrderBy(i => records[i].Index).ToDictionary(i => records[i].Index, i => (object)records[i].SpeckleObj);
      // records.Where(r => r.SpeckleObj != null && r.SpeckleType.EndsWith(speckleTypeName)).ToDictionary(v => v.Index, v => (object)v.SpeckleObj)
    }

    public void MarkPrevious(string kw, string applicationId)
    {
      if (string.IsNullOrEmpty(kw) || !collectionIndicesByKw.ContainsKey(kw)
        || string.IsNullOrEmpty(applicationId) || !collectionIndicesByApplicationId.ContainsKey(applicationId))
      {
        return;
      }
      var colIndices = collectionIndicesByApplicationId[applicationId].Intersect(collectionIndicesByKw[kw]).ToList();
      if (colIndices.Count() == 0)
      {
        return;
      }
      foreach (var i in colIndices)
      {
        records[i].Previous = true;
        records[i].Latest = false;
      }
      /*
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
         */
    }

    public void Snapshot(string streamId)
    {
      var indicesToRemove = new HashSet<int>();
      foreach (var keyword in collectionIndicesByKw.Keys)
      {
        var indices = collectionIndicesByKw[keyword].Union(collectionIndicesByStreamId[streamId]).ToList();

        foreach (var i in indices)
        {
          var record = records[i];
          if (record.StreamId == null || record.StreamId != streamId || !IsAlterable(keyword, record.ApplicationId))
          {
            continue;
          }

          // The use and function of IsAlterable needs to be reviewed.  Nodes are a special case as they are generated outside of Speckle feeds 
          // and these ones need to be preserved
          if (record.Latest == false)
          {
            if (!indicesToRemove.Contains(i))
            {
              indicesToRemove.Add(i);
            }
          }
          else
          {
            record.Previous = true;
            record.Latest = false;
          }
        }
      }

      //Remove the indices from the hash sets before actually removing the records
      var indicesToRemoveSorted = indicesToRemove.OrderByDescending(i => i).ToList();
      foreach (var i in indicesToRemoveSorted)
      {
        var record = records[i];
        collectionIndicesByKw[record.Keyword].Remove(i);
        if (!string.IsNullOrEmpty(record.ApplicationId) && collectionIndicesByApplicationId.ContainsKey(record.ApplicationId))
        {
          collectionIndicesByApplicationId[record.ApplicationId].Remove(i);
        }
        if (!string.IsNullOrEmpty(record.StreamId) && collectionIndicesByStreamId.ContainsKey(record.StreamId))
        {
          collectionIndicesByStreamId[record.StreamId].Remove(i);
        }
        if (record.SpeckleObj != null && !string.IsNullOrEmpty(record.SpeckleObj.Type))
        {
          var type = SpeckleTypeName(record.SpeckleObj);
          if (collectionIndicesBySpeckleTypeName.ContainsKey(type))
          {
            collectionIndicesBySpeckleTypeName[type].Remove(i);
          }
        }
      }
      foreach (var i in indicesToRemove)
      {
        records.RemoveAt(i);
      }
    }

    //private readonly Dictionary<string, HashSet<int>> recordIndicesBySpeckleTypeName = new Dictionary<string, HashSet<int>>();

    public void Upsert(string kw, int gsaIndex, string gwa, string streamId, string applicationId, bool latest, SpeckleObject so, GwaSetCommandType gwaSetCommandType)
    {
      var newColIndex = records.Count();
      var newRecord = new GSACacheRecord(kw, gsaIndex, gwa, streamId: streamId, applicationId: applicationId, latest: true, so: so, gwaSetCommandType: gwaSetCommandType);
      records.Add(newRecord);
      if (!collectionIndicesByKw.ContainsKey(kw))
      {
        collectionIndicesByKw.Add(kw, new HashSet<int>());
      }
      collectionIndicesByKw[kw].Add(newColIndex);
      if (!string.IsNullOrEmpty(applicationId))
      {
        if (!collectionIndicesByApplicationId.ContainsKey(applicationId))
        {
          collectionIndicesByApplicationId.Add(applicationId, new HashSet<int>());
        }
        collectionIndicesByApplicationId[applicationId].Add(newColIndex);
      }
      if (!string.IsNullOrEmpty(streamId))
      {
        if (!collectionIndicesByStreamId.ContainsKey(streamId))
        {
          collectionIndicesByStreamId.Add(streamId, new HashSet<int>());
        }
        collectionIndicesByStreamId[streamId].Add(newColIndex);
      }
      if (so != null && !string.IsNullOrEmpty(so.Type))
      {
        var speckleTypeName = SpeckleTypeName(so);
        if (!collectionIndicesBySpeckleTypeName.ContainsKey(speckleTypeName))
        {
          collectionIndicesBySpeckleTypeName.Add(speckleTypeName, new HashSet<int>());
        }
        collectionIndicesBySpeckleTypeName[speckleTypeName].Add(newColIndex);
      }
      /*
          if (!recordsByKeyword.ContainsKey(kw))
          {
            recordsByKeyword.Add(kw, new List<GSACacheRecord>());
          }
          recordsByKeyword[kw].Add(new GSACacheRecord(kw, index, gwa, streamId: streamId, applicationId: applicationId, latest: true, so: so, gwaSetCommandType: gwaSetCommandType));

          UpsertApplicationIdLookup(kw, index, applicationId);
          */
    }

    private string SpeckleTypeName(SpeckleObject so)
    {
      return (so == null || string.IsNullOrEmpty(so.Type)) ? "" : FormatSpeckleTypeName(so.Type);
    }

    private string FormatSpeckleTypeName(string fullTypeName)
    {
      return (fullTypeName == null || string.IsNullOrEmpty(fullTypeName)) ? "" : fullTypeName.Split('/').Last();
    }

    public List<int?> GetRecordIndices(string kw, IEnumerable<string> applicationIds)
    {
      var appIds = applicationIds.Where(aid => !string.IsNullOrEmpty(aid) && collectionIndicesByApplicationId.ContainsKey(aid)).ToList();
      if (!collectionIndicesByKw.ContainsKey(kw) || appIds.Count() == 0)
      {
        return new List<int?>();
      }
      var colIndicesHash = new HashSet<int>();
      foreach (var colIndex in collectionIndicesByKw[kw])
      {
        //The appIds have already been checked and they are all present as keys in the recordIndicesByApplicationId dictionary
        foreach (var appId in appIds)
        {
          if (collectionIndicesByApplicationId[appId].Contains(colIndex) && !colIndicesHash.Contains(colIndex))
          {
            colIndicesHash.Add(colIndex);
          }
        }
      }
      var indicesToReturn = colIndicesHash.Select(i => records[i].Index).Distinct().OrderBy(i => i).Select(i => (int?)i).ToList();
      return indicesToReturn;
      /*
        if (recordsByKeyword.ContainsKey(kw) && applicationIds != null)
        {
          matchingRecords.AddRange(recordsByKeyword[kw].Where(r => r.Index > 0 && applicationIds.Any(id => r.ApplicationId.EqualsWithoutSpaces(id))
            && r.Latest));
        }

        if (matchingRecords.Count() == 0)
        {
          return new List<int?>();
        }
        return matchingRecords.Select(r => (int?)r.Index).Distinct().ToList();
        */
    }

    public int? GetRecordIndex(string kw, string applicationId)
    {
      if (string.IsNullOrEmpty(applicationId) || string.IsNullOrEmpty(kw) || !collectionIndicesByApplicationId.ContainsKey(applicationId) || !collectionIndicesByKw.ContainsKey(kw))
      {
        return null;
      }
      var colIndices = collectionIndicesByApplicationId[applicationId].Intersect(collectionIndicesByKw[kw]);
      return (colIndices.Count() == 0) ? null : (int?)colIndices.Select(i => records[i].Index).OrderBy(i => i).First();
      /*
        if (recordsByKeyword.ContainsKey(kw))
        {
          matchingRecords.AddRange(recordsByKeyword[kw].Where(r => r.Index > 0 && r.ApplicationId.EqualsWithoutSpaces(applicationId)));
        }
        if (matchingRecords.Count() == 0)
        {
          return null;
        }
        return (int?) matchingRecords.Select(r => r.Index).First();
        */
    }

    public List<GSACacheRecord> GetAllRecords(string kw, string applicationId)
    {
      if (string.IsNullOrEmpty(applicationId) || string.IsNullOrEmpty(kw) || !collectionIndicesByApplicationId.ContainsKey(applicationId) || !collectionIndicesByKw.ContainsKey(kw))
      {
        return new List<GSACacheRecord>();
      }
      var colIndices = collectionIndicesByApplicationId[applicationId].Intersect(collectionIndicesByKw[kw]).OrderBy(i => i);
      return colIndices.Select(i => records[i]).ToList();
      /*
      var matchingRecords = new List<GSACacheRecord>();

      if (recordsByKeyword.ContainsKey(kw))
      {
        matchingRecords.AddRange(recordsByKeyword[kw].Where(r => r.ApplicationId.EqualsWithoutSpaces(applicationId)).ToList());
      }
      */
    }

    /*
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
    */

    private bool IsAlterable(string keyword, string applicationId)
    {
      if (keyword.Contains("NODE") && (applicationId == "" || (applicationId != null && applicationId.StartsWith("gsa"))))
      {
        return false;
      }
      return true;
    }
  }
}
