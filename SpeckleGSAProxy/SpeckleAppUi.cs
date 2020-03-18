using SpeckleGSAInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSAProxy
{
  public class SpeckleAppUI : ISpeckleGSAAppUI
  {
    private Dictionary<string, List<string>> messages = new Dictionary<string, List<string>>();
    public bool Message(string headingMessage, string exampleDetail)
    {
      if (!messages.ContainsKey(headingMessage))
      {
        messages.Add(headingMessage, new List<string>());
      }
      messages[headingMessage].Add(exampleDetail);
      return true;
    }

    public List<string> GroupMessages()
    {
      return messages.Select(kvp => kvp.Key + " " + string.Join(",", kvp.Value)).ToList();
    }
  }
}
