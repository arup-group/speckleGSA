using System;

namespace SpeckleGSAInterfaces
{
  public interface IGSAMessenger
  {
    bool Message(MessageIntent intent, MessageLevel level, params string[] messagePortions);
    bool Message(MessageIntent intent, MessageLevel level, Exception ex, params string[] messagePortions);
  }
}
