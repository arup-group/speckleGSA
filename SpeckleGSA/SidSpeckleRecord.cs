namespace SpeckleGSA
{
  public class SidSpeckleRecord
  {
    public string StreamId { get; }
    public string ClientId { get; private set; }
    public string Bucket { get; }
    public string Name { get; private set; }
    public SidSpeckleRecord(string streamId, string bucket, string clientId = null, string streamName = null)
    {
      this.StreamId = streamId;
      this.Bucket = bucket;
      this.ClientId = clientId;
      this.Name = streamName;
    }

    public void SetName(string name)
    {
      this.Name = name;
    }

    public void SetClientId(string clientId)
    {
      this.ClientId = clientId;
    }
  }
}
