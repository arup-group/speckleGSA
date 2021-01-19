using SpeckleGSAInterfaces;
using System;
using System.Collections.Generic;

namespace SpeckleGSA
{
  public class Messager : IGSAMessager
  {
    public event EventHandler<MessageEventArgs> MessageAdded;

    private List<MessageEventArgs> MessageCache = new List<MessageEventArgs>();

    //For use by the kits, which will store the messages to be triggered later
    public bool Message(MessageIntent intent, MessageLevel level, params string[] messagePortions)
    {
      MessageCache.Add(new MessageEventArgs(intent, level, messagePortions));
      return true;
    }


    //For immediate use by the app
    public bool AddMessage(params string[] messagePortions)
    {
      MessageAdded?.Invoke(null, new MessageEventArgs(MessageIntent.Display, MessageLevel.Information, messagePortions));
      return true;
    }

    public bool AddError(params string[] messagePortions)
    {
      MessageAdded?.Invoke(null, new MessageEventArgs(MessageIntent.Display, MessageLevel.Error, messagePortions));
      return true;
    }

    public void Trigger()
    {
      foreach (var m in MessageCache)
      {
        MessageAdded?.Invoke(null, m);
      }
    }
  }
}
