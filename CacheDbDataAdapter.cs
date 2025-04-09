using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Runtime.Caching;

namespace CacheCommander
{
    public class CacheDbDataAdapter : DbDataAdapter
    {
        private readonly CacheDbCommand _cachedCommand;
        private static readonly MemoryCache _cache = MemoryCache.Default;

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

            string cacheKey = GenerateCacheKey();

            var procedures = Configuration.GetCacheProcedures();

            bool useCache = procedures?.Keys?.Contains(_cachedCommand.CommandText) == true && cacheKey?.Length > 0;

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

            string cacheKey = GenerateCacheKey();

            var procedures = Configuration.GetCacheProcedures();

            bool useCache = procedures?.Keys?.Contains(_cachedCommand.CommandText) == true && cacheKey?.Length > 0;

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
            try
            {
                return _cachedCommand.CommandText + string.Join(",", _cachedCommand.Parameters.Cast<DbParameter>().Select(p => p.Value?.ToString() ?? "NULL"));
            }
            catch
            {
                return string.Empty;
            }
        }

    }
}
