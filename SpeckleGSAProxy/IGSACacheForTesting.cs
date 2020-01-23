using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SpeckleGSAProxy
{
  public interface IGSACacheForTesting
  {
    List<string> GetGwaSetCommands();

    ReadOnlyCollection<GSACacheRecord> Records { get; }
  }
}
