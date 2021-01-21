namespace SpeckleGSAInterfaces
{
  public interface IGSAAppResources
  {
    IGSASettings Settings { get; }
    IGSAProxy Proxy { get; }
    IGSACacheForKit Cache { get; }
    IGSAMessenger Messenger { get; }
  }
}