﻿using SpeckleGSA;
using SpeckleGSAInterfaces;
using System.Collections.Generic;
using System.Linq;

namespace SpeckleGSAUI.Models
{
  public abstract class TabBase
  {
    public GSATargetLayer TargetLayer { get; set; }
    public StreamMethod StreamMethod { get; set; } = StreamMethod.Single;
    public StreamList StreamList { get; set; } = new StreamList();

    protected List<SidSpeckleRecord> sidSpeckleRecords = new List<SidSpeckleRecord>();

    public double PollingRateMilliseconds { get; set; } = 2000;
    public TabBase(GSATargetLayer defaultLayer)
    {
      TargetLayer = defaultLayer;
    }

    public bool ChangeSidRecordStreamName(string streamId, string streamName)
    {
      var matching = sidSpeckleRecords.FirstOrDefault(r => r.StreamId.Equals(streamId, System.StringComparison.InvariantCultureIgnoreCase));
      if (matching == null)
      {
        return false;
      }
      matching.SetName(streamName);
      return true;
    }

    public bool SidRecordsToStreamList()
    {
      StreamList.SeletedStreamListItem = null;
      StreamList.StreamListItems.Clear();
      foreach (var sidr in sidSpeckleRecords)
      {
        StreamList.StreamListItems.Add(new StreamListItem(sidr.StreamId, sidr.Name));
      }
      return true;
    }

    public void StreamListToSidRecords()
    {
      sidSpeckleRecords = StreamList.StreamListItems.Select(sli => new SidSpeckleRecord(sli.StreamId, sli.StreamName)).ToList();
    }

    public bool RemoveSidSpeckleRecord(SidSpeckleRecord r)
    {
      var matching = sidSpeckleRecords.Where(ssr => ssr.StreamId.Equals(r.StreamId, System.StringComparison.InvariantCultureIgnoreCase)).ToList();
      if (matching.Count > 0)
      {
        var indices = matching.Select(m => sidSpeckleRecords.IndexOf(m)).OrderByDescending(i => i).ToList();
        foreach (var i in indices)
        {
          sidSpeckleRecords.RemoveAt(i);
        }
      }
      return true;
    }
  }
}
