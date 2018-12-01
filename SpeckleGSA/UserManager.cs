using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SpeckleCore;

namespace SpeckleGSA
{
    public class UserManager
    {
        public SpeckleApiClient userClient;
        
        public string Email { get; set; }
        public string Password { get; set; }
        public string ApiToken { get; set; }
        public string ServerAddress { get; set; }
        public string ServerName { get; set; }
        public string RestApi { get; set; }

        public UserManager(string email, string password, string serverAdress)
        {
            Email = email;
            Password = password;
            ServerAddress = serverAdress;
        }

        public int Login()
        {
            // Create user
            User myUser = new User()
            {
                Email = this.Email,
                Password = this.Password
            };

            // Create Uri for server
            Uri ServerAddress;
            Uri.TryCreate(this.ServerAddress, UriKind.Absolute, out ServerAddress);

            userClient = new SpeckleApiClient() { BaseUrl = ServerAddress.ToString() };
            
            // Get server name
            string rawPingReply = "";
            dynamic parsedReply = null;
            using (var client = new WebClient())
            {
                try
                {
                    rawPingReply = client.DownloadString(ServerAddress.ToString());
                    parsedReply = JsonConvert.DeserializeObject(rawPingReply);
                }
                catch
                {
                    Console.Write("Failed to contact: " + ServerAddress.ToString());
                    return 1;
                }
            }
            ServerName = (string)parsedReply.serverName;

            // Login and get API token
            try
            {
                var response = userClient.UserLoginAsync(myUser).Result;
                ApiToken = response.Resource.Apitoken;
                RestApi = ServerAddress.ToString();
                return 0;
            }
            catch
            {
                Console.WriteLine("Failed to login.");
                return 1;
            }
        }
    }
}
