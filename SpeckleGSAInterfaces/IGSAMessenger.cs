namespace SpeckleGSAInterfaces
{
  public interface IGSAMessenger
  {
    bool CacheMessage(MessageIntent intent, MessageLevel level, params string[] messagePortions);
    bool Message(MessageIntent intent, MessageLevel level, params string[] messagePortions);
  }
}
