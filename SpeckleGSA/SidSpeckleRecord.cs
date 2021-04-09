namespace SpeckleGSA
{
  public class SidSpeckleRecord
  {
    public string StreamId { get; set; }
    public string ClientId { get; set; }
    public string StreamName { get; set; }
    public SidSpeckleRecord(string streamId, string streamName, string clientId)
    {
      this.StreamId = streamId;
      this.StreamName = streamName;
      this.ClientId = clientId;
    }
  }
}
