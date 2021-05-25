using SpeckleGSAInterfaces;
using SpeckleGSAProxy;
using SpeckleUtil;

namespace SpeckleGSA
{
  public interface IGSALocalAppResources : IGSAAppResources
  {
    IGSACache LocalCache { get; set; }
    IGSALocalSettings LocalSettings { get; set; }
    IGSALocalProxy LocalProxy { get; }
    IGSALocalMessenger LocalMessenger { get; set; }

    ISpeckleObjectMerger Merger { get; set; }
  }
}
