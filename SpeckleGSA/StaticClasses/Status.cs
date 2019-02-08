using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA
{
    public static partial class Status
    {
        public static event EventHandler<MessageEventArgs> MessageAdded;
        public static event EventHandler<MessageEventArgs> ErrorAdded;
        public static event EventHandler<StatusEventArgs> StatusChanged;

        private static bool IsInit;

        public static void Init(EventHandler<MessageEventArgs> messageHandler, EventHandler<MessageEventArgs> errorHandler, EventHandler<StatusEventArgs> statusHandler)
        {
            if (IsInit)
                return;
            
            MessageAdded += messageHandler;
            ErrorAdded += errorHandler;
            StatusChanged += statusHandler;

            IsInit = true;
        }


        public static void AddMessage(string message)
        {
            if (MessageAdded != null)
            {
                MessageAdded(null, new MessageEventArgs(message));
            }
        }

        public static void AddError(string error)
        {
            if (MessageAdded != null)
            {
                ErrorAdded(null, new MessageEventArgs(error));
            }
        }

        public static void ChangeStatus(string name, double percent = -1)
        {
            if (StatusChanged != null)
            {
                StatusChanged(null, new StatusEventArgs(name.ToUpper(), percent));
            }
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
