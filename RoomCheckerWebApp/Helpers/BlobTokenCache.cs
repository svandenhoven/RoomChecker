using Microsoft.Identity.Client;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RoomChecker.Helpers
{

    public class TokenBlobRepository
    {
        private readonly CloudBlobContainer _container;
        public TokenBlobRepository(string connectionString, string container)
        {
            if (CloudStorageAccount.TryParse(connectionString, out CloudStorageAccount storageAccount))
            {
                CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();

                _container = cloudBlobClient.GetContainerReference(container);
            }
        }

        public async Task<byte[]> GetTokensForUser(string userId)
        {
            if (_container != null)
            {
                var userTokenBlob = _container.GetBlockBlobReference(userId);
                if (await userTokenBlob.ExistsAsync())
                {
                    var tokenStream = new MemoryStream();
                    await userTokenBlob.DownloadToStreamAsync(tokenStream);
                    var token = tokenStream.ToArray();
                    return token;
                }
            }

            return null;
        }

        public async Task InsertOrReplaceToken(byte[] token, string userId)
        {
            if (_container != null)
            {
                var userTokenBlob = _container.GetBlockBlobReference(userId);
                await userTokenBlob.UploadFromByteArrayAsync(token, 0, token.Length);
            }
        }

        public async Task RemoveToken(string userId)
        {
            var userTokenBlob = _container.GetBlockBlobReference(userId);
            await userTokenBlob.DeleteIfExistsAsync();
        }
    }

    public class BlobStorageTokenCache
    {
        private static readonly object FileLock = new object();
        private readonly string userId;
        private readonly TokenBlobRepository _repository;
        private TokenCache _tokenCache = new TokenCache();

        public BlobStorageTokenCache(string signedInUserIdId, string connectionString, string containerName)
        {
            this.userId = signedInUserIdId;
            _tokenCache.SetAfterAccess(AfterAccessNotification);
            _tokenCache.SetBeforeAccess(BeforeAccessNotification);

            this._repository = new TokenBlobRepository(connectionString, containerName);

        }

        public TokenCache GetCacheInstance()
        {
            _tokenCache.SetBeforeAccess(BeforeAccessNotification);
            _tokenCache.SetAfterAccess(AfterAccessNotification);
            Load();

            return _tokenCache;
        }

        public void Load()
        {
            lock (FileLock)
            {
                var latestToken = _repository.GetTokensForUser(userId).Result;
                _tokenCache.Deserialize(latestToken);
            }
        }

        public void Clear()
        {
            lock (FileLock)
            {
                _repository.RemoveToken(userId).Wait();
            }
        }

        public void Persist()
        {
            lock (FileLock)
            {
                var token = _tokenCache.Serialize();

                _repository.InsertOrReplaceToken(token, userId).Wait();

                _tokenCache.HasStateChanged = false;
            }
        }

        void BeforeAccessNotification(TokenCacheNotificationArgs args)
        {
            Load();
        }

        void AfterAccessNotification(TokenCacheNotificationArgs args)
        {
            if (_tokenCache.HasStateChanged)
            {
                Persist();
            }
        }
    }
}
