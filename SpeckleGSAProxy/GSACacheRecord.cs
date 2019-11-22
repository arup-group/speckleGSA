using SpeckleCore;
using SpeckleGSAInterfaces;
using System.Linq;

namespace SpeckleGSAProxy
{
  public class GSACacheRecord
  {
    public string Keyword { get; private set; }
    public int Index { get; private set; }
    public string ApplicationId { get; private set; }
    public string StreamId { get; private set; }
    public string Sid => StreamId + "|" + ApplicationId;
    public SpeckleObject SpeckleObj { get; set; }
    public bool Latest { get; set; }
    public bool Previous { get; set; }
    public string Gwa { get; private set; }
    public GwaSetCommandType GwaSetCommandType { get; private set; }
    public string SpeckleType => SpeckleObj.Type.ChildType();

    public GSACacheRecord(string keyword, int index, string gwa, string streamId = "", string applicationId = "", bool previous = false, bool latest = true, SpeckleObject so = null,
      GwaSetCommandType gwaSetCommandType = GwaSetCommandType.Set)
    {
      Keyword = keyword;
      Index = index;
      Gwa = gwa;
      Latest = latest;
      Previous = previous;
      StreamId = streamId;
      //values cannot have spaces
      ApplicationId = applicationId.Replace(" ", "");
      SpeckleObj = so;
      GwaSetCommandType = gwaSetCommandType;
    }
  }
}
