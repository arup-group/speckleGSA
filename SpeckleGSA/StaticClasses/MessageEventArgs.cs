using System;

namespace SpeckleGSA
{
  public class MessageEventArgs : EventArgs
  {
    private readonly string message;

    public MessageEventArgs(string message)
    {
      this.message = message;
    }

    public string Message
    {
      get { return message; }
    }
  }
}
