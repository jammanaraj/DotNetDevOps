using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json.Linq;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetDevOps.Web
{
    public class CachedItem : TableEntity
    {
        public CachedItem() { }
        public CachedItem(string partitionKey, string rowKey, byte[] data = null)
            : base(partitionKey, rowKey)
        {
            this.Data = data;
        }

        public byte[] Data { get; set; }
        public TimeSpan? SlidingExperiation { get; set; }
        public DateTimeOffset? AbsolutExperiation { get; set; }
        public DateTimeOffset? LastAccessTime { get; set; }
    }

    public class AzureTableStorageCacheHandler : IDistributedCache
    {
        private readonly string partitionKey;
        private readonly string accountKey;
        private readonly string accountName;
        private readonly string connectionString;
        private CloudTableClient client;
        private CloudTable azuretable;

        private AzureTableStorageCacheHandler(string tableName, string partitionKey)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentNullException("tableName cannot be null or empty");
            }
            if (string.IsNullOrWhiteSpace(partitionKey))
            {
                throw new ArgumentNullException("partitionKey cannot be null or empty");
            }
            this.tableName = tableName;
            this.partitionKey = partitionKey;
        }
        public AzureTableStorageCacheHandler(string tableName, string partitionKey, CloudStorageAccount account) : this(tableName, partitionKey)
        {
            client = account.CreateCloudTableClient();
            Connect();
        }
        public AzureTableStorageCacheHandler(string accountName, string accountKey, string tableName, string partitionKey)
            : this(tableName, partitionKey)
        {
            if (string.IsNullOrWhiteSpace(accountName))
            {
                throw new ArgumentNullException("accountName cannot be null or empty");
            }
            if (string.IsNullOrWhiteSpace(accountKey))
            {
                throw new ArgumentNullException("accountKey cannot be null or empty");
            }

            this.accountName = accountName;
            this.accountKey = accountKey;
            Connect();
        }

        private readonly string tableName;

        public AzureTableStorageCacheHandler(string connectionString, string tableName, string partitionKey)
            : this(tableName, partitionKey)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentNullException("Connection string cannot be null or empty");
            }

            this.connectionString = connectionString;
            Connect();
        }

        public void Connect()
        {
            ConnectAsync().Wait();
        }

        public async Task ConnectAsync()
        {
            if (client == null)
            {
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    var creds = new StorageCredentials(accountName, accountKey);
                    client = new CloudStorageAccount(creds, true).CreateCloudTableClient();
                }
                else
                {
                    client = CloudStorageAccount.Parse(connectionString).CreateCloudTableClient();
                }
            }
            if (azuretable == null)
            {
                this.azuretable = client.GetTableReference(this.tableName);
                await this.azuretable.CreateIfNotExistsAsync();
            }
        }

        public byte[] Get(string key)
        {
            return GetAsync(key).Result;
        }

        public async Task<byte[]> GetAsync(string key, CancellationToken token = default(CancellationToken))
        {
            var cachedItem = await RetrieveAsync(key);
            if (cachedItem != null && cachedItem.Data != null && ShouldDelete(cachedItem))
            {
                await RemoveAsync(key);
                return null;
            }
            return cachedItem?.Data;
        }

        public void Refresh(string key)
        {
            this.RefreshAsync(key).Wait();
        }

        public async Task RefreshAsync(string key, CancellationToken token = default(CancellationToken))
        {
            var data = await RetrieveAsync(key);
            if (data != null)
            {
                if (ShouldDelete(data))
                {
                    await RemoveAsync(key);
                    return;
                }
            }
        }

        private async Task<CachedItem> RetrieveAsync(string key, CancellationToken token = default(CancellationToken))
        {
            var op = TableOperation.Retrieve<CachedItem>(partitionKey, key);
            var result = await azuretable.ExecuteAsync(op);
            var data = result?.Result as CachedItem;
            return data;
        }

        private bool ShouldDelete(CachedItem data)
        {
            var currentTime = DateTimeOffset.UtcNow;
            if (data.AbsolutExperiation != null && data.AbsolutExperiation.Value <= currentTime)
            {
                return true;
            }
            if (data.SlidingExperiation.HasValue && data.LastAccessTime.HasValue && data.LastAccessTime.Value.Add(data.SlidingExperiation.Value) < currentTime)
            {
                return true;
            }

            return false;
        }

        public void Remove(string key)
        {
            this.RemoveAsync(key).Wait();
        }

        public Task RemoveAsync(string key, CancellationToken token = default(CancellationToken))
        {
            var op = TableOperation.Delete(new CachedItem(partitionKey, key) { ETag = "*" });
            return azuretable.ExecuteAsync(op);
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            this.SetAsync(key, value, options).Wait();
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default(CancellationToken))
        {
            DateTimeOffset? absoluteExpiration = null;
            var currentTime = DateTimeOffset.UtcNow;
            if (options.AbsoluteExpirationRelativeToNow.HasValue)
            {
                absoluteExpiration = currentTime.Add(options.AbsoluteExpirationRelativeToNow.Value);
            }
            else if (options.AbsoluteExpiration.HasValue)
            {
                if (options.AbsoluteExpiration.Value <= currentTime)
                {
                    throw new ArgumentOutOfRangeException(
                       nameof(options.AbsoluteExpiration),
                       options.AbsoluteExpiration.Value,
                       "The absolute expiration value must be in the future.");
                }
                absoluteExpiration = options.AbsoluteExpiration;
            }
            var item = new CachedItem(partitionKey, key, value) { LastAccessTime = currentTime };
            if (absoluteExpiration.HasValue)
            {
                item.AbsolutExperiation = absoluteExpiration;
            }
            if (options.SlidingExpiration.HasValue)
            {
                item.SlidingExperiation = options.SlidingExpiration;
            }
            var op = TableOperation.InsertOrReplace(item);
            return this.azuretable.ExecuteAsync(op);
        }


    }

    public static class AzureTableStorageCacheExtensions
    {
        /// <summary>
        /// Add azure table storage cache as an IDistributedCache to the service container
        /// </summary>
        /// <param name="services"></param>
        /// <param name="connectionString">The connection string of your account (can be found in the preview portal)</param>
        /// <param name="tableName">the name of the table you wish to use. If the table doesn't exist it will be created.</param>
        /// <param name="partitionKey">the partition key you would like to use</param>
        /// <returns></returns>
        public static IServiceCollection AddAzureTableStorageCache(this IServiceCollection services, string connectionString, string tableName, string partitionKey)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentNullException("connectionString must not be null");
            }

            //services.AddSingleton<Azure>
            services.Add(ServiceDescriptor.Singleton<IDistributedCache, AzureTableStorageCacheHandler>(a => new AzureTableStorageCacheHandler(connectionString, tableName, partitionKey)));
            return services;
        }
        public static IServiceCollection AddAzureTableStorageCache(this IServiceCollection services, string tableName, string partitionKey)
        {


            //services.AddSingleton<Azure>
            services.Add(ServiceDescriptor.Singleton<IDistributedCache, AzureTableStorageCacheHandler>(a => new AzureTableStorageCacheHandler(tableName, partitionKey, a.GetRequiredService<CloudStorageAccount>())));
            return services;
        }


        /// <summary>
        /// Add azure table storage cache as an IDistributedCache to the service container
        /// </summary>
        /// <param name="services"></param>
        /// <param name="accountName">the name of your storage account</param>
        /// <param name="accountKey">the key of your storage account</param>
        /// <param name="tableName">the name of the table you wish to use. If the table doesn't exist it will be created.</param>
        /// <param name="partitionKey">the partition key you would like to use</param>
        /// <returns></returns>
        public static IServiceCollection AddAzureTableStorageCache(this IServiceCollection services, string accountName, string accountKey, string tableName, string partitionKey)
        {
            if (string.IsNullOrWhiteSpace(accountName))
            {
                throw new ArgumentNullException("accountName must not be null");
            }
            if (string.IsNullOrWhiteSpace(accountKey))
            {
                throw new ArgumentNullException("accountKey must not be null");
            }

            //services.AddSingleton<Azure>
            services.Add(ServiceDescriptor.Singleton<IDistributedCache, AzureTableStorageCacheHandler>(a =>
            new AzureTableStorageCacheHandler(accountName, accountKey, tableName, partitionKey)));
            return services;
        }

        private static void checkTableData(string tableName, string partitionkey)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentNullException("tableName must not be null");
            }
            if (string.IsNullOrWhiteSpace(partitionkey))
            {
                throw new ArgumentNullException("partitionkey must not be null");
            }
        }
    }

    public class AdalDistributedTokenCache : TokenCache
    {
        private readonly IDistributedCache _cache;
        private readonly string _userId;

        public AdalDistributedTokenCache(IDistributedCache cache, string userId)
        {
            _cache = cache;
            _userId = userId;
            BeforeAccess = BeforeAccessNotification;
            AfterAccess = AfterAccessNotification;
        }

        private string GetCacheKey()
        {
            return $"{_userId}_TokenCache";
        }

        private void BeforeAccessNotification(TokenCacheNotificationArgs args)
        {
            Deserialize(_cache.Get(GetCacheKey()));
        }

        private void AfterAccessNotification(TokenCacheNotificationArgs args)
        {
            if (HasStateChanged)
            {
                _cache.Set(GetCacheKey(), Serialize(), new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1)
                });
                HasStateChanged = false;
            }
        }
    }

    public class Startup
    {
        private readonly IConfiguration configuration;

        public Startup(IConfiguration configuration)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDataProtection();
            services.AddScoped(sp => sp.GetDataProtector("generic"));
            services.AddAzureTableStorageCache("cache", "ga");


            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Latest); 
         //   services.AddHsts(o => { o.IncludeSubDomains = false; o.Preload = true; });
         //   services.AddHttpsRedirection(o => { });

            services.AddSingleton(CloudStorageAccount.Parse(configuration.GetValue<string>("storage:connectionString")));

            services.AddCors();

            //services.AddAuthentication()
            //     .AddOpenIdConnect(opts =>
            //     {

            //         opts.CallbackPath = "signin-oidc";
            //         opts.CorrelationCookie.Path = "signin-oidc";
            //         opts.NonceCookie.Path = "signin-oidc";


            //         opts.Events = new OpenIdConnectEvents
            //         {
            //             OnAuthorizationCodeReceived = async ctx =>
            //             {
            //                 var request = ctx.HttpContext.Request;
            //                 var currentUri = UriHelper.BuildAbsolute(request.Scheme, request.Host, request.PathBase, request.Path);
            //                 var credential = new ClientCredential(ctx.Options.ClientId, ctx.Options.ClientSecret);

            //                 var distributedCache = ctx.HttpContext.RequestServices.GetRequiredService<IDistributedCache>();
            //                 string userId = ctx.Principal.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier").Value;

            //                 var cache = new AdalDistributedTokenCache(distributedCache, userId);

            //                 var authContext = new AuthenticationContext(ctx.Options.Authority, cache);

            //                 var result = await authContext.AcquireTokenByAuthorizationCodeAsync(
            //                     ctx.ProtocolMessage.Code, new Uri(currentUri), credential);

            //                 ctx.HandleCodeRedemption(result.AccessToken, result.IdToken);
            //             }
            //         };
            //     });

        }



        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseDeveloperExceptionPage();

            if (env.IsDevelopment())
            {
               
            }

            app.Use((c, n) =>
            {
                c.Response.Headers["Access-Control-Allow-Origin"] = "*";
                return Task.CompletedTask;
            });

            app.Map("/oidc-signin", (appinner) =>
            {
                appinner.Use(async (ctx, next) =>
                {

                    var state = ctx.Request.Form["state"];
                    var code = ctx.Request.Form["code"];
                    var id_token = ctx.Request.Form["id_token"];

                    var user = new JwtSecurityTokenHandler().ReadToken(id_token) as JwtSecurityToken;

                    
                    var endpoints = ctx.RequestServices.GetRequiredService<IOptions<EndpointOptions>>();
                   // var state = await distributedCache.GetAsync("kv_item_" + id);
                    var obj = JToken.Parse(Base64.Decode(state));


                    var credential = new ClientCredential("348f8c76-1a9d-4fcb-bb7b-26f785495226", configuration.GetValue<string>("app:secret"));

                    string userId = user.Claims.FirstOrDefault(v=>v.Type == "upn").Value;

                    var cache = new AdalDistributedTokenCache(ctx.RequestServices.GetRequiredService<IDistributedCache>(), userId);

                    var authContext = new AuthenticationContext(obj.SelectToken("$.authority").ToString(), cache);
                   
                    var result = await authContext.AcquireTokenByAuthorizationCodeAsync(
                        code, new Uri(endpoints.Value.ResourceApiEndpoint + "/oidc-signin"), credential);
                    var rdirect = obj.SelectToken("$.redirectUri").ToString();
                    ctx.Response.Redirect($"{rdirect}{(rdirect.Contains("?")?"&":"?")}userid="+userId);


                });
            });

            app.UseCors(c=>c.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin().SetPreflightMaxAge(TimeSpan.FromHours(1)));

            app.UseStaticFiles();

            app.UseMvc();
        }
    }
}
