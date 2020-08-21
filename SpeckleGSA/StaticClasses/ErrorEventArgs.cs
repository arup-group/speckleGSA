using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA
{
  public class ErrorEventArgs : EventArgs
  {
    private readonly string message;
    private readonly Exception ex;

    public ErrorEventArgs(string message, Exception ex = null)
    {
      this.message = message;
      this.ex = ex;
    }

    public string Message
    {
      get { return message; }
    }

    public Exception Exception
    {
      get { return ex; }
    }
  }
}
