using SpeckleGSA;
using SpeckleInterface;
using System;

namespace SpeckleGSAUI.Test
{
  internal class MockProgressMessenger : ISpeckleAppMessenger
  {
    private IProgress<MessageEventArgs> progress;

    public MockProgressMessenger(IProgress<MessageEventArgs> progress)
    {
      this.progress = progress;
    }

    public bool Message(MessageIntent intent, MessageLevel level, params string[] messagePortions)
    {
      return true;
    }

    public bool Message(MessageIntent intent, MessageLevel level, Exception ex, params string[] messagePortions)
    {
      return true;
    }
  }
}
