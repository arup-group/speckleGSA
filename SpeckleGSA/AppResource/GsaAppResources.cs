using SpeckleGSAInterfaces;
using SpeckleGSAProxy;
using SpeckleUtil;

namespace SpeckleGSA
{
  public class GsaAppResources : IGSAAppResources
  {
    public IGSASettings Settings { get => gsaSettings; }
    public IGSAProxy Proxy { get => gsaProxy; }
    public IGSACacheForKit Cache { get => gsaCache; }
    public ISpeckleGSAAppUI AppUI { get => appUi; }
    public IGSAMessager Messager { get => gsaMessager; }

    /// <summary>
    /// Static class which interfaces with GSA
    /// </summary>

    public Settings gsaSettings = new Settings();
    public ISpeckleObjectMerger Merger = new SpeckleObjectMerger();
    public GSAProxy gsaProxy = new GSAProxy();
    public GSACache gsaCache = new GSACache();
    public SpeckleAppUI appUi = new SpeckleAppUI();
    public Messager gsaMessager = new Messager();

    public GsaAppResources()
    {
    }

    

    
  }
}
