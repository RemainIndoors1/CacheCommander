using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;

namespace CacheCommander
{
    internal class Configuration
    {
        private const string AppConfigSectionName = "CacheCommander.StoredProcedures";
        private const int DefaultCacheTimeInMinutes = 3;

        internal static Dictionary<string, int> GetCacheProcedures()
        {
            Dictionary<string, int> response = new Dictionary<string, int>();

            try
            {
                var config = ConfigurationManager.GetSection(AppConfigSectionName);

                if (config != null)
                {
                    var collection = (NameValueCollection)config;

                    foreach (string key in collection.Keys)
                    {
                        string configValue = collection[key];

                        if (configValue != "0")
                        {
                            int cacheTimeInMinutes;

                            // value stored in app.config should be integer time in minutes to cache the result of the stored procedure
                            if (!int.TryParse(configValue, out cacheTimeInMinutes))
                            {
                                cacheTimeInMinutes = DefaultCacheTimeInMinutes;
                            }

                            response[key] = cacheTimeInMinutes;
                        }

                    }
                }
            }
            catch
            {
                response = new Dictionary<string, int>();
            }

            return response;
        }
    }
}
