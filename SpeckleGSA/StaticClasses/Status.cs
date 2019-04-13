using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA
{
    /// <summary>
    /// Handles messages and status change events.
    /// </summary>
    public static partial class Status
    {
        public static event EventHandler<MessageEventArgs> MessageAdded;
        public static event EventHandler<MessageEventArgs> ErrorAdded;
        public static event EventHandler<StatusEventArgs> StatusChanged;

        private static bool IsInit;

        /// <summary>
        /// Initializes the handler.
        /// </summary>
        /// <param name="messageHandler">Event handler for handling messages</param>
        /// <param name="errorHandler">Event handler for handling errors</param>
        /// <param name="statusHandler">Event handler for status changes</param>
        public static void Init(EventHandler<MessageEventArgs> messageHandler, EventHandler<MessageEventArgs> errorHandler, EventHandler<StatusEventArgs> statusHandler)
        {
            if (IsInit)
                return;
            
            MessageAdded += messageHandler;
            ErrorAdded += errorHandler;
            StatusChanged += statusHandler;

            IsInit = true;
        }

        /// <summary>
        /// Create new message.
        /// </summary>
        /// <param name="message">Message</param>
        public static void AddMessage(string message)
        {
            if (MessageAdded != null)
                MessageAdded(null, new MessageEventArgs(message));
        }

        /// <summary>
        /// Create new error.
        /// </summary>
        /// <param name="error">Message</param>
        public static void AddError(string message)
        {
            if (MessageAdded != null)
                ErrorAdded(null, new MessageEventArgs(message));
        }
        
        /// <summary>
        /// Change the status of SpeckleGSA.
        /// </summary>
        /// <param name="name">Current state name</param>
        /// <param name="percent">Status bar progress</param>
        public static void ChangeStatus(string name, double percent = -1)
        {
            if (StatusChanged != null)
                StatusChanged(null, new StatusEventArgs(name.ToUpper(), percent));
        }
    }

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

    public class StatusEventArgs : EventArgs
    {
        private readonly double percent;
        private readonly string name;

        public StatusEventArgs(string name, double percent)
        {
            this.name = name;
            this.percent = percent;
        }

        public double Percent
        {
            get { return percent; }
        }

        public string Name
        {
            get { return name; }
        }
    }
}
