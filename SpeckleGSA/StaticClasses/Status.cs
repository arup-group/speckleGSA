using System;
using System.Collections.Generic;

namespace SpeckleGSA
{
  /// <summary>
  /// Handles messages and status change events.
  /// </summary>
  public static partial class Status
  {
    //public static event EventHandler<MessageEventArgs> MessageAdded;
    //public static event EventHandler<ErrorEventArgs> ErrorAdded;
    public static event EventHandler<StatusEventArgs> StatusChanged;

    private static bool IsInit;

    /// <summary>
    /// Initializes the handler.
    /// </summary>
    /// <param name="statusHandler">Event handler for status changes</param>
    public static void Init(EventHandler<StatusEventArgs> statusHandler)
    {
      if (IsInit)
        return;

      //MessageAdded += messageHandler;
      //ErrorAdded += errorHandler;
      StatusChanged += statusHandler;

      IsInit = true;
    }

    /*
    /// <summary>
    /// Create new message.
    /// </summary>
    /// <param name="message">Message</param>
    public static void AddMessage(string message)
    {
      MessageAdded?.Invoke(null, new MessageEventArgs(message));
    }

    /// <summary>
    /// Create new error.
    /// </summary>
    /// <param name="error">Message</param>
    public static void AddError(string message, Exception ex = null)
    {
      if (MessageAdded != null)
      {
        ErrorAdded(null, new ErrorEventArgs(message, ex));
      }
    }
    */

    /// <summary>
    /// Change the status of SpeckleGSA.
    /// </summary>
    /// <param name="name">Current state name</param>
    /// <param name="percent">Status bar progress</param>
    public static void ChangeStatus(string name, double percent = -1)
    {
      StatusChanged?.Invoke(null, new StatusEventArgs(name, percent));
    }
  }
}
