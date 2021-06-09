using SpeckleInterface;
using System;

namespace SpeckleGSAProxy.Test
{
  internal class MockProgressMessenger : ISpeckleAppMessenger
  {
    private Progress<string> progress;

    public MockProgressMessenger(Progress<string> progress)
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