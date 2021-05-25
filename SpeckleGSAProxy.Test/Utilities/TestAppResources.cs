using SpeckleGSA;
using SpeckleGSAInterfaces;
using SpeckleUtil;
using System;

namespace SpeckleGSAProxy.Test
{
  internal class TestAppResources : IGSALocalAppResources
  {
    public IGSALocalProxy LocalProxy { get; set; }

    public IGSALocalSettings LocalSettings { get; set; } = new Settings();

    public IGSACache LocalCache { get; set; } = new GSACache();

    public IGSALocalMessenger LocalMessenger { get; set; }

    public ISpeckleObjectMerger Merger { get; set; }

    public IGSASettings Settings { get => LocalSettings; }

    public IGSAProxy Proxy { get => LocalProxy; }

    public IGSACacheForKit Cache { get; set; }

    public IGSAMessenger Messenger => throw new NotImplementedException();

    public TestAppResources() { }

    public TestAppResources(IGSALocalProxy testProxy, IGSALocalSettings testSettings)
    {
      LocalProxy = testProxy;
      LocalSettings = testSettings;
    }
  }
}
