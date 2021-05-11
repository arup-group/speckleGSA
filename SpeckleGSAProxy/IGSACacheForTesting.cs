using SpeckleGSAInterfaces;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SpeckleGSAProxy
{
  public interface IGSACacheForTesting
  {
    List<string> GetGwaSetCommands();

    List<GSACacheRecord> Records { get; }
    void Clear();

    bool Upsert(string keyword, int index, string gwaWithoutSet, string streamId, string applicationId, GwaSetCommandType gwaSetCommandType);
  }
}
