using SpeckleGSAInterfaces;
using SpeckleGSAProxy;
using SpeckleUtil;

namespace SpeckleGSA
{
  public class GsaAppResources : IGSAAppResources, IGSALocalAppResources
  {
    //For the kit(s) use only
    public IGSACacheForKit Cache { get => gsaCache; }
    public IGSAProxy Proxy { get => LocalProxy; }
    public IGSASettings Settings { get => LocalSettings; }
    public IGSAMessenger Messenger { get => LocalMessenger; }

    //For local use only - which can be overridden with mocks for testing purposes
    public IGSACache LocalCache
    {
      get => gsaCache;
      set
      {
        if (value is GSACache)
        {
          gsaCache = (GSACache)value;
        }
      }
    }
    public IGSALocalSettings LocalSettings { get; set; } = new Settings();
    public IGSALocalMessenger LocalMessenger { get; set; } = new GsaMessenger();

    public ISpeckleObjectMerger Merger { get; set; } = new SpeckleObjectMerger();

    public IGSALocalProxy LocalProxy { get; set; } = new GSAProxy();

    private GSACache gsaCache = new GSACache();

    public GsaAppResources()
    {
    }

  }
}
