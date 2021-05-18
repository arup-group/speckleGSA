using SpeckleInterface;
using System;
using System.Collections.Generic;

namespace SpeckleGSA.UI.Utilities
{
  //This is mainly for use in the SpeckleInterface library, not the kit
  public class ProgressMessenger : ISpeckleAppMessenger
  {
    private readonly IProgress<MessageEventArgs> loggingProgress;

    //These are meant to be in corresponding order so that an index of one used in looking up the other correlates correct pairs
    private static readonly List<SpeckleGSAInterfaces.MessageIntent> AppIntent = new List<SpeckleGSAInterfaces.MessageIntent>() 
      { SpeckleGSAInterfaces.MessageIntent.Display, SpeckleGSAInterfaces.MessageIntent.TechnicalLog, SpeckleGSAInterfaces.MessageIntent.Telemetry };
    private static readonly List<MessageIntent> InterfaceIntent = new List<MessageIntent>()  { MessageIntent.Display, MessageIntent.TechnicalLog, MessageIntent.Telemetry };

    private static readonly List<SpeckleGSAInterfaces.MessageLevel> AppLevel = new List<SpeckleGSAInterfaces.MessageLevel>()
      { SpeckleGSAInterfaces.MessageLevel.Information, SpeckleGSAInterfaces.MessageLevel.Debug, SpeckleGSAInterfaces.MessageLevel.Error, SpeckleGSAInterfaces.MessageLevel.Fatal };
    private static readonly List<MessageLevel> InterfaceLevel = new List<MessageLevel>()  { MessageLevel.Information, MessageLevel.Debug, MessageLevel.Error, MessageLevel.Fatal };

    public ProgressMessenger(IProgress<MessageEventArgs> loggingProgress)
    {
      this.loggingProgress = loggingProgress;
    }

    public bool Message(MessageIntent intent, MessageLevel level, params string[] messagePortions)
    {
      loggingProgress.Report(new MessageEventArgs(AppIntent[InterfaceIntent.IndexOf(intent)], AppLevel[InterfaceLevel.IndexOf(level)], messagePortions));
      return true;
    }

    public bool Message(MessageIntent intent, MessageLevel level, Exception ex, params string[] messagePortions)
    {
      loggingProgress.Report(new MessageEventArgs(AppIntent[InterfaceIntent.IndexOf(intent)], AppLevel[InterfaceLevel.IndexOf(level)], ex, messagePortions));
      return true;
    }
  }
}
