namespace SpeckleGSA.UI.Models
{
  public class SpeckleAccountForUI
  {
    public SpeckleAccountForUI(string clientName, string serverUrl, string emailAddress, string token)
    {
      this.ClientName = clientName;
      this.ServerUrl = serverUrl;
      this.EmailAddress = emailAddress;
      this.Token = token;
    }

    public string ClientName { get; set; }
    public string ServerUrl { get; set; }
    public string EmailAddress { get; set; }
    public string Token { get; set; }

    public string Summary { get => string.Join(" ", ClientName, EmailAddress); }

    public bool IsValid { get => !string.IsNullOrEmpty(ServerUrl) && !string.IsNullOrEmpty(EmailAddress) && !string.IsNullOrEmpty(Token); }
  }
}