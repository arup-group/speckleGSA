using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSAInterfaces
{
  public interface IGSAMessager
  {
    bool Message(MessageIntent intent, MessageLevel level, params string[] messagePortions);
  }
}
