using SpeckleCore;
using SpeckleGSA;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SpeckleGSAProxy.Test
{
  internal class TestSpeckleGSAReceiver : ISpeckleGSAReceiver
  {
    public string Units => "mm";

    public List<SpeckleObject> Objects { get; set; }

    public event EventHandler<EventArgs> UpdateGlobalTrigger;

    public void Dispose()
    {
      Objects.Clear();
    }

    public List<SpeckleObject> GetObjects()
    {
      return Objects; 
    }

    public async Task InitializeReceiver(string streamID, string clientID = "")
    {
    }
  }
}
