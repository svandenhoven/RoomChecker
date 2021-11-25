using Microsoft.Identity.Client;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RoomChecker.Helpers
{
    public class TokenEntity : TableEntity
    {
        public string UserTokenCacheId { get; set; }
        public byte[] CacheBits { get; set; }
        public DateTime LastWrite { get; set; }

        public TokenEntity(string webUniqueUserId, byte[] token, DateTime lastWriteTime)
        {
            UserTokenCacheId = webUniqueUserId;
            CacheBits = token;
            LastWrite = lastWriteTime;

            RowKey = webUniqueUserId;
            PartitionKey = "usertoken";
        }

        public TokenEntity() { }
    }

    public class TokenEntityRepository
    {
        private readonly CloudTable _cloudTable;
        public TokenEntityRepository(string connectionString, string tableName)
        {
            var cloudAccount = CloudStorageAccount.Parse(connectionString);
            _cloudTable = cloudAccount.CreateCloudTableClient().GetTableReference(tableName);
            _cloudTable.CreateIfNotExistsAsync().Wait();
        }

        public IEnumerable<TokenEntity> GetAllTokensForUser(string userId)
        {
            TableQuery<TokenEntity> query = new TableQuery<TokenEntity>()
                .Where(TableQuery.CombineFilters(
                    TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, "usertoken"),
                    TableOperators.And,
                    TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, userId)));

            return _cloudTable.ExecuteQuerySegmentedAsync(query,null).Result.ToList();
        }

        public void Delete(TokenEntity tokenEntity)
        {
            var tableOp = TableOperation.Delete(tokenEntity);
            _cloudTable.ExecuteAsync(tableOp).Wait();
        }

        public void InsertOrReplace(TokenEntity tokenEntity)
        {
            var tableOp = TableOperation.InsertOrReplace(tokenEntity);
            _cloudTable.ExecuteAsync(tableOp).Wait();
        }
    }

    public class AzureTableTokenCache
    {
        private readonly string userId;
        private readonly TokenEntityRepository _repository;
        private TokenEntity Cache;
        private TokenCache _tokenCache = new TokenCache();

        public AzureTableTokenCache(string signedInUserIdId, string connectionString, string tableName)
        {
            this.userId = signedInUserIdId;
            _tokenCache.SetAfterAccess(AfterAccessNotification);
            _tokenCache.SetBeforeAccess(BeforeAccessNotification);

            this._repository = new TokenEntityRepository(connectionString, tableName);

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
            var latestToken = _repository.GetAllTokensForUser(userId)
                .OrderByDescending(a => a.LastWrite)
                .FirstOrDefault();

            if (Cache == null || (latestToken != null && Cache.LastWrite < latestToken.LastWrite))
                Cache = latestToken;

            _tokenCache.Deserialize((Cache == null) ? null : Cache.CacheBits);
        }

        public void Clear()
        {
            foreach (var item in _repository.GetAllTokensForUser(userId))
            {
                _repository.Delete(item);
            }
        }

        public void Persist()
        {
            var cacheBits = _tokenCache.Serialize();

            Cache = new TokenEntity(userId, cacheBits, DateTime.UtcNow);
            _repository.InsertOrReplace(Cache);

            _tokenCache.HasStateChanged = false;
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
