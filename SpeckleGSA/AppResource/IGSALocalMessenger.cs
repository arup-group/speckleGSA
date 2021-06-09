using SpeckleGSAInterfaces;
using System;
using System.Collections.Generic;

namespace SpeckleGSA
{
  public interface IGSALocalMessenger : IGSAMessenger
  {
    bool Append(IEnumerable<string> messagePortionsToMatch, IEnumerable<string> additional);
    void Trigger();
    event EventHandler<MessageEventArgs> MessageAdded;
  }
}
