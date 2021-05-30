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

    public IGSACache LocalCache { get; set; }

    public IGSALocalMessenger LocalMessenger { get; set; } = new GsaMessenger();

    public ISpeckleObjectMerger Merger { get; set; } = new SpeckleObjectMerger();

    public IGSASettings Settings { get => LocalSettings; }

    public IGSAProxy Proxy { get => LocalProxy; }

    public IGSACacheForKit Cache { get; set; }

    public IGSAMessenger Messenger {  get=> LocalMessenger;}

    public TestAppResources() { }

    public TestAppResources(IGSALocalProxy testProxy, IGSALocalSettings testSettings)
    {
      LocalProxy = testProxy;
      LocalSettings = testSettings;
      var cache = new GSACache();
      LocalCache = cache;
      Cache = cache;
    }
  }
}
