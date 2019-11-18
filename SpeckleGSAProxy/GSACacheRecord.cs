﻿using SpeckleCore;
using SpeckleGSAInterfaces;
using System;

namespace SpeckleGSAProxy
{
  public class GSACacheRecord
  {
    public string Keyword { get; set; }
    public int Index { get; set; }    
    public string ApplicationId { get; set; }
    public SpeckleObject SpeckleObj { get; set; }
    public bool Latest { get; set; }
    public bool Previous { get; set; }
    public string Gwa { get; set; }
    public GwaSetCommandType GwaSetCommandType { get; set; }
    public string SpeckleType => SpeckleObj.Type.ChildType();

    public GSACacheRecord(string keyword, int index, string gwa, string applicationId = "", bool previous = false, bool latest = true, SpeckleObject so = null, 
      GwaSetCommandType gwaSetCommandType = GwaSetCommandType.Set)
    {
      Keyword = keyword;
      Index = index;
      Gwa = gwa;
      Latest = latest;
      Previous = previous;
      //Application ID cannot have spaces
      ApplicationId = applicationId.Replace(" ","");
      SpeckleObj = so;
      GwaSetCommandType = gwaSetCommandType;
    }
  }
}
