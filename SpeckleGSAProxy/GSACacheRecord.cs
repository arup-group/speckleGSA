using SpeckleCore;
using SpeckleGSAInterfaces;
using System.Linq;

namespace SpeckleGSAProxy
{
  public class GSACacheRecord
  {
    public string Keyword { get; private set; }
    public int Index { get; private set; }
    public string ApplicationId { get; set; }
    public string StreamId { get; private set; }    
    public SpeckleObject SpeckleObj { get; set; }
    //Note: these booleans can't be merged into one state property because records could be both previous and latest, or only one of them
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
      ApplicationId = (applicationId == null) ? "" : applicationId.Replace(" ", "");
      SpeckleObj = so;
      GwaSetCommandType = gwaSetCommandType;
    }
  }
}
