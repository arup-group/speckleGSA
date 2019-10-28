using SpeckleCore;
using SpeckleGSAInterfaces;
using System;

namespace SpeckleGSAProxy
{
  public class GSACacheRecord
  {
    public string Keyword { get; set; }
    public int Index { get; set; }    
    public string ApplicationId { get; set; }
    public SpeckleObject SpeckleObject { get; set; }
    public bool? Latest { get; set; }
    public bool? Previous { get; set; }
    public string Gwa { get; set; }
    public Type Type => typeof(SpeckleObject);
    public GwaSetCommandType GwaSetCommandType { get; set; }
    public bool CurrentSession { get; set; }

    public GSACacheRecord(string keyword, int index, string gwa, string applicationId = "", bool? previous = null, bool? latest = null, SpeckleObject so = null, bool currentSession = true, 
      GwaSetCommandType gwaSetCommandType = GwaSetCommandType.Set)
    {
      Keyword = keyword;
      Index = index;
      Gwa = gwa;
      ApplicationId = applicationId;
      SpeckleObject = so;
      CurrentSession = currentSession;
      GwaSetCommandType = gwaSetCommandType;
    }
  }
}
