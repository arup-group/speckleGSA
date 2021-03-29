namespace SpeckleGSA.UI.Models
{
  public class SpeckleAccount
  {
    public SpeckleAccount(string clientName, string serverUrl, string emailAddress)
    {
      this.ClientName = clientName;
      this.ServerUrl = serverUrl;
      this.EmailAddress = emailAddress;
    }

    public string ClientName { get; set; }
    public string ServerUrl { get; set; }
    public string EmailAddress { get; set; }

    public string Summary { get => string.Join(" ", ClientName, EmailAddress); }
  }
}