using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA
{
    public static partial class MessageLog
    {
        public static event EventHandler<MessageEventArgs> MessageAdded;
        public static event EventHandler<MessageEventArgs> ErrorAdded;

        private static bool IsInit;

        public static void Init(EventHandler<MessageEventArgs> messageHandler, EventHandler<MessageEventArgs> errorHandler)
        {
            if (IsInit)
                return;
            
            MessageAdded += messageHandler;
            ErrorAdded += errorHandler;

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

}
