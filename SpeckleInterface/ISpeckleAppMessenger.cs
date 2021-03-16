using System;
namespace SpeckleInterface
{
  public interface ISpeckleAppMessenger
  {
    bool Message(MessageIntent intent, MessageLevel level, params string[] messagePortions);
    bool Message(MessageIntent intent, MessageLevel level, Exception ex, params string[] messagePortions);
  }
}
