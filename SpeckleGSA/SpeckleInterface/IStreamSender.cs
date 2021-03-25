using System.Collections.Generic;
using System.Threading.Tasks;

namespace SpeckleGSA
{
  public interface IStreamSender
  {
    string StreamID { get; }
    string ClientID { get; }

    Task InitializeSender(string streamID = "", string clientID = "", string streamName = "");
    void UpdateName(string streamName);
    int SendGSAObjects(Dictionary<string, List<object>> value);
    void Dispose();
  }
}
