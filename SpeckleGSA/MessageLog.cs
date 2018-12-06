using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA
{
    public class MessageLog
    {
        public event EventHandler<MessageEventArgs> MessageAdded;
        public event EventHandler<MessageEventArgs> ErrorAdded;

        public void AddMessage(string message)
        {
            if (MessageAdded != null)
            {
                MessageAdded(this, new MessageEventArgs(message));
            }
        }

        public void AddError(string error)
        {
            if (MessageAdded != null)
            {
                ErrorAdded(this, new MessageEventArgs(error));
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
