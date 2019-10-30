/* 
*  Copyright (c) Microsoft. All rights reserved. Licensed under the MIT license. 
*  See LICENSE in the source repository root for complete license information. 
*/

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Identity.Client;
using Microsoft.Graph;
using System.Linq;
using RoomChecker.Extensions;
using RoomChecker.Helpers;

namespace RoomChecker.Helpers
{
    public class GraphAuthProvider : IGraphAuthProvider
    {
        private readonly IMemoryCache _memoryCache;
        private TokenCache _userTokenCache;
        private AzureAdOptions _azureOptions;
        // Properties used to get and manage an access token.
        private readonly string _appId;
        private readonly ClientCredential _credential;
        private readonly string[] _scopes;
        private readonly string _redirectUri;

        public GraphAuthProvider(IMemoryCache memoryCache, IConfiguration configuration)
        {
            _azureOptions = new AzureAdOptions();
            configuration.Bind("AzureAd", _azureOptions);

            _appId = _azureOptions.ClientId;
            _credential = new ClientCredential(_azureOptions.ClientSecret);
            _scopes = _azureOptions.GraphScopes.Split(new[] { ' ' });
            _redirectUri = _azureOptions.BaseUrl + _azureOptions.CallbackPath;

            _memoryCache = memoryCache;
        }

        // Gets an access token. First tries to get the access token from the token cache.
        // Using password (secret) to authenticate. Production apps should use a certificate.
        public async Task<string> GetUserAccessTokenAsync(string userId, string[] scopes)
        {
            //scopes = _scopes;
            _userTokenCache = new SessionTokenCache(userId, _memoryCache).GetCacheInstance();
            //_userTokenCache = new AzureTableTokenCache(userId, _azureOptions.TokenCacheConnectionString, _azureOptions.TokenCacheTableName).GetCacheInstance();

            var cca = new ConfidentialClientApplication(
                _appId,
                _redirectUri,
                _credential,
                _userTokenCache,
                null);

            var accounts = (await cca.GetAccountsAsync()).ToList();
            if (!accounts.Any()) throw new ServiceException(new Error
            {
                Code = "TokenNotFound",
                Message = "User not found in token cache. Maybe the server was restarted."
            });

            try
            {
                var result = await cca.AcquireTokenSilentAsync(scopes, accounts.First());
                return result.AccessToken;
            }

            // Unable to retrieve the access token silently.
            catch (Exception)
            {
                throw new ServiceException(new Error
                {
                    Code = GraphErrorCode.AuthenticationFailure.ToString(),
                    Message = "Caller needs to authenticate. Unable to retrieve the access token silently."
                });
            }
        }
    }

    public interface IGraphAuthProvider
    {
        Task<string> GetUserAccessTokenAsync(string userId, string[] scopes);
    }
}
