using Newtonsoft.Json;
using Sdc.Ypakp.Business.Parameters;
using Sdc.Ypakp.WebServices.Rest;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Caching;
using System.Windows.Input;

namespace WebApiThrottle
{
    /// <summary>
    /// Stores throttle metrics in Redis
    /// </summary>
    public class RedisRepository : IThrottleRepository
    {
        static IConnectionMultiplexer _connectionMultiplexer;
        const int DbNum = 4;
        const int ServerNum = 0;

        static RedisRepository() 
        {
            _connectionMultiplexer = ConnectionMultiplexer
                .Connect(ConfigurationManager.ConnectionStrings["RedisConnection"].ConnectionString, o => o.AllowAdmin = true);
        }

        private IDatabase GetDatabase() => _connectionMultiplexer.GetDatabase(DbNum);

        public void Save(string id, ThrottleCounter throttleCounter, TimeSpan expirationTime)
        {
            GetDatabase().StringSet(id, JsonConvert.SerializeObject(throttleCounter), expirationTime);
        }

        public bool Any(string id) => GetDatabase().KeyExists(id);

        public ThrottleCounter? FirstOrDefault(string id)
        {
            var tc = GetDatabase().StringGet(id);

            if (tc.HasValue) return JsonConvert.DeserializeObject<ThrottleCounter>(tc);
            else return null;
        }

        public void Remove(string id)
        {
            GetDatabase().KeyDelete(id);
        }

        public void Clear()
        {
            _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints()[ServerNum]).FlushDatabase(DbNum);
        }
    }
}
