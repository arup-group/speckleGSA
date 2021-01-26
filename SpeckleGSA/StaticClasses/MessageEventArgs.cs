using SpeckleGSAInterfaces;
using System;

namespace SpeckleGSA
{
  public class MessageEventArgs : EventArgs
  {
    public MessageEventArgs(MessageIntent intent, MessageLevel level, params string[] messagePortions)
    {
      this.MessagePortions = messagePortions;
      this.Intent = intent;
      this.Level = level;
    }

    public string[] MessagePortions { get; }
    public MessageIntent Intent { get; }
    public MessageLevel Level { get; }
  }
}
