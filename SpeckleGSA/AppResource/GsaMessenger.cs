using SpeckleGSAInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpeckleGSA
{
  public class GsaMessenger : IGSAMessenger
  {
    public event EventHandler<MessageEventArgs> MessageAdded;

    private readonly object syncLock = new object();
    private List<MessageEventArgs> MessageCache = new List<MessageEventArgs>();

    //For use by the kits, which will store the messages to be triggered later
    public bool CacheMessage(MessageIntent intent, MessageLevel level, Exception ex, params string[] messagePortions)
    {
      lock (syncLock)
      {
        MessageCache.Add(new MessageEventArgs(intent, level, ex, messagePortions));
      }
      return true;
    }

    public bool CacheMessage(MessageIntent intent, MessageLevel level, params string[] messagePortions)
    {
      lock (syncLock)
      {
        MessageCache.Add(new MessageEventArgs(intent, level, messagePortions));
      }
      return true;
    }

    public bool Message(MessageIntent intent, MessageLevel level, params string[] messagePortions)
    {
      MessageAdded?.Invoke(null, new MessageEventArgs(intent, level, messagePortions));
      return true;
    }

    public bool Message(MessageIntent intent, MessageLevel level, Exception ex, params string[] messagePortions)
    {
      MessageAdded?.Invoke(null, new MessageEventArgs(intent, level, ex, messagePortions));
      return true;
    }

    public void Trigger()
    {
      ConsolidateCache();
      lock (syncLock)
      {
        foreach (var m in MessageCache)
        {
          MessageAdded?.Invoke(null, m);
        }
        MessageCache.Clear();
      }
    }

    public void ConsolidateCache()
    {
      var newCache = new List<MessageEventArgs>();
      //Currently just recognises the first two levels of message portions
      lock (syncLock)
      {
        //Any message with exceptions shouldn't be consolidated
        newCache.AddRange(MessageCache.Where(m => m.Exception != null));

        var msgGroups = MessageCache.Where(m => m.Exception == null).GroupBy(m => new { m.Intent, m.Level }).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var gk in msgGroups.Keys)
        {
          var msgDict = new Dictionary<string, List<string>>();
          foreach (var m in msgGroups[gk].Where(m => m.MessagePortions.Count() > 0))
          {
            var validPortions = m.MessagePortions.Where(mp => !string.IsNullOrEmpty(mp)).ToList();
            if (validPortions.Count() == 0)
            {
              continue;
            }
            var l1msg = validPortions.First();
            if (!msgDict.ContainsKey(l1msg))
            {
              msgDict.Add(l1msg, new List<string>());
            }
            if (validPortions.Count() > 1)
            {
              msgDict[l1msg].Add(validPortions[1]);
            }
          }
          if (msgDict.Keys.Count() > 0)
          {
            foreach (var k in msgDict.Keys)
            {
              if (msgDict[k].Count() == 0)
              {
                newCache.Add(new MessageEventArgs(gk.Intent, gk.Level, k));
              }
              else
              {
                newCache.Add(new MessageEventArgs(gk.Intent, gk.Level, k, string.Join(", ", msgDict[k])));
              }
            }
          }
        }
        MessageCache = newCache;
      }
    }
  }
}
