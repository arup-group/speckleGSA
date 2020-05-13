using System.Collections.Generic;
using System.Threading.Tasks;

namespace SpeckleGSA
{
  public interface ISpeckleGSASender
  {
    string StreamID { get; }
    string ClientID { get; }

    Task InitializeSender(string streamID = "", string clientID = "", string streamName = "");
    void UpdateName(string streamName);
    void SendGSAObjects(Dictionary<string, List<object>> value);
    void Dispose();
  }
}
