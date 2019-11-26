using System.Collections.Generic;

namespace SpeckleGSAProxy
{
  public interface IGSACacheForTesting
  {
    List<string> GetGwaSetCommands();

    List<GSACacheRecord> Records { get; }
  }
}
