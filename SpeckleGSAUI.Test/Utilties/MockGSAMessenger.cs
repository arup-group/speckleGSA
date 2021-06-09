using SpeckleGSA;
using SpeckleGSAInterfaces;
using System;
using System.Collections.Generic;

namespace SpeckleGSAUI.Test
{
  public class MockGSAMessenger : IGSALocalMessenger
  {
    public List<Tuple<MessageIntent, MessageLevel, string[]>> Messages = new List<Tuple<MessageIntent, MessageLevel, string[]>>();

    public event EventHandler<MessageEventArgs> MessageAdded;

    public bool Append(IEnumerable<string> messagePortionsToMatch, IEnumerable<string> additional)
    {
      return true;
    }

    public bool CacheMessage(MessageIntent intent, MessageLevel level, params string[] messagePortions)
    {
      return Message(intent, level, messagePortions);
    }

    public bool CacheMessage(MessageIntent intent, MessageLevel level, Exception ex, params string[] messagePortions)
    {
      return Message(intent, level, messagePortions);
    }

    public void ClearCache(MessageIntent intent, MessageLevel level)
    {
      Messages.Clear();
    }

    public List<object> GetCachedMessages(MessageIntent intent, MessageLevel level)
    {
      return new List<object>();
    }

    public bool Message(MessageIntent intent, MessageLevel level, params string[] messagePortions)
    {
      Messages.Add(new Tuple<MessageIntent, MessageLevel, string[]>(intent, level, messagePortions));
      return true;
    }

    public bool Message(MessageIntent intent, MessageLevel level, Exception ex, params string[] messagePortions)
    {
      Messages.Add(new Tuple<MessageIntent, MessageLevel, string[]>(intent, level, messagePortions));
      return true;
    }

    public void Trigger()
    {
      return;
    }
  }
}
