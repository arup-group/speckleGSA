using System.Collections.Generic;

namespace SpeckleGSAProxy
{
  public interface IGSACacheForTesting
  {
    List<string> GetGwaSetCommands();
  }
}
