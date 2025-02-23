using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Runtime.Caching;

namespace CacheCommander
{
    public class CacheDbDataAdapter : DbDataAdapter
    {
        private readonly CacheDbCommand _cachedCommand;
        private static readonly MemoryCache _cache = MemoryCache.Default;

        private const string AppConfigSectionName = "CacheCommander.StoredProcedures";
        private const int DefaultCacheTimeInMinutes = 3;

        private CacheDbDataAdapter()
        {
            GC.SuppressFinalize(this);
        }

        public CacheDbDataAdapter(CacheDbCommand command) : this()
        {
            _cachedCommand = command ?? throw new ArgumentNullException(nameof(command));
        }



        public override int Fill(DataSet dataSet)
        {
            if (dataSet == null)
                throw new InvalidOperationException("dataSet cannot be null");

            var procedures = GetCacheProcedures();

            bool useCache = procedures?.Keys?.Contains(_cachedCommand.CommandText) == true;

            string cacheKey = GenerateCacheKey();

            if (useCache && _cache.Contains(cacheKey))
            {
                var cacheDataSet = (DataSet)_cache.Get(cacheKey);
                dataSet.Merge(cacheDataSet);
                return cacheDataSet.Tables.Cast<DataTable>().Sum(t => t.Rows.Count);
            }

            var newDataSet = new DataSet();
            using (var reader = _cachedCommand.ExecuteCacheDataReader())
            {
                DataTable param = null;
                newDataSet.Load(reader, LoadOption.OverwriteChanges, param);
            }

            if (useCache)
            {
                var clonedDataSet = newDataSet.Copy();
                TimeSpan cacheDuration = TimeSpan.FromMinutes(procedures[_cachedCommand.CommandText]);
                _cache.Set(cacheKey, clonedDataSet, DateTimeOffset.UtcNow.Add(cacheDuration));
            }

            dataSet.Merge(newDataSet);

            return newDataSet.Tables.Cast<DataTable>().Sum(t => t.Rows.Count);
        }

        public new int Fill(DataTable dataTable)
        {
            if (dataTable == null)
                throw new ArgumentNullException(nameof(dataTable));

            var procedures = GetCacheProcedures();

            bool useCache = procedures?.Keys?.Contains(_cachedCommand.CommandText) == true;

            string cacheKey = GenerateCacheKey();

            if (useCache && _cache.Contains(cacheKey))
            {
                var cacheDataTable = (DataTable)_cache.Get(cacheKey);
                dataTable.Merge(cacheDataTable);
                return cacheDataTable.Rows.Count;
            }

            using (var reader = _cachedCommand.ExecuteAdapterDataReader())
            {
                dataTable.Load(reader);
            }

            if (useCache)
            {
                var clonedTable = dataTable.Copy();
                TimeSpan cacheDuration = TimeSpan.FromMinutes(procedures[_cachedCommand.CommandText]);
                _cache.Set(cacheKey, clonedTable, DateTimeOffset.UtcNow.Add(cacheDuration));
            }

            return dataTable.Rows.Count;
        }


        private string GenerateCacheKey()
        {
            return _cachedCommand.CommandText + string.Join(",", _cachedCommand.Parameters.Cast<DbParameter>().Select(p => p.Value?.ToString() ?? "NULL"));
        }

        private Dictionary<string, int> GetCacheProcedures()
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
                        int cacheTimeInMinutes;

                        // value stored in app.config should be integer time in minutes to cache the result of the stored procedure (key)
                        if (!int.TryParse(collection[key], out cacheTimeInMinutes) || !(cacheTimeInMinutes > 0))
                        {
                            cacheTimeInMinutes = DefaultCacheTimeInMinutes;
                        }

                        response[key] = cacheTimeInMinutes;
                    }
                }
            }
            catch (Exception ex)
            {
                response = new Dictionary<string, int>();
                Console.WriteLine(ex.ToString());
            }

            return response;
        }

    }
}
