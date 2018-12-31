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
        public string RestApi { get; set; }

        public UserManager(string email, string password, string serverAdress)
        {
            Email = email;
            Password = password;
            ServerAddress = serverAdress;
        }

        public int Login()
        {
            // Create Uri for server
            Uri serverAddress;
            Uri.TryCreate(this.ServerAddress, UriKind.Absolute, out serverAddress);
            RestApi = serverAddress.ToString();

            // Attempt to use local cache
            Account storedAccount = LocalContext.GetAccountByEmailAndRestApi(Email, RestApi);

            if (storedAccount != null)
            {
                ApiToken = storedAccount.Token;
                return 0;
            }
            else
            { 
                // Login and get API token
                userClient = new SpeckleApiClient() { BaseUrl = RestApi };

                User myUser = new User()
                {
                    Email = this.Email,
                    Password = this.Password
                };

                try
                {
                    var response = userClient.UserLoginAsync(myUser).Result;
                    ApiToken = response.Resource.Apitoken;

                    LocalContext.AddAccount(new Account()
                    { Email = this.Email,
                        Token = this.ApiToken,
                        RestApi = this.ServerAddress });
                    return 0;
                }
                catch
                {
                    return 1;
                }
            }
        }
    }
}
