using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Runtime.Caching;

namespace CacheCommander
{
    public class CacheDbCommand : DbCommand
    {

        private readonly DbCommand _innerCommand;
        private static readonly MemoryCache _cache = MemoryCache.Default;
        private bool _cacheSchema = false;

        public CacheDbCommand(DbCommand innerCommand)
        {
            _innerCommand = innerCommand ?? throw new ArgumentNullException(nameof(innerCommand));
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {

            string cacheKey = GenerateCacheKey();

            var procedures = Configuration.GetCacheProcedures();

            bool useCache = procedures?.Keys?.Contains(_innerCommand.CommandText) == true && cacheKey?.Length > 0;
            DataTable schemaTable;

            if (useCache)
            {

                if (_cache.Contains(cacheKey))
                {
                    var cachedData = (List<Dictionary<string, object>>)_cache.Get(cacheKey);
                    schemaTable = _cacheSchema ? (DataTable)_cache.Get(cacheKey + "_Schema") : null;
                    return new CacheDbDataReader(cachedData, schemaTable);
                }

            }

            using (var reader = _innerCommand.ExecuteReader(behavior))
            {
                schemaTable = _cacheSchema ? reader.GetSchemaTable()?.Copy() : null;
                var resultData = new List<Dictionary<string, object>>();
                var columnNames = Enumerable.Range(0, reader.FieldCount)
                                        .Select(reader.GetName)
                                        .ToArray();
                while (reader.Read())
                {
                    var row = new Dictionary<string, object>();
                    foreach (var colName in columnNames)
                    {
                        row[colName.ToLower()] = reader[colName];
                    }
                    resultData.Add(row);
                }

                if (useCache)
                {
                    TimeSpan cacheDuration = TimeSpan.FromMinutes(procedures[_innerCommand.CommandText]);
                    _cache.Set(cacheKey, resultData, DateTimeOffset.UtcNow.Add(cacheDuration));
                    if (_cacheSchema)
                        _cache.Set(cacheKey + "_Schema", schemaTable, DateTimeOffset.UtcNow.Add(cacheDuration));
                }
                
                return new CacheDbDataReader(resultData, schemaTable);
            }

        }

        public DbDataReader ExecuteCacheDataReader(CommandBehavior behavior = CommandBehavior.Default)
        {
            return ExecuteDbDataReader(behavior);
        }

        public DbDataReader ExecuteAdapterDataReader(CommandBehavior behavior = CommandBehavior.Default)
        {
            _cacheSchema = true;
            return ExecuteDbDataReader(behavior);
        }

        public override object ExecuteScalar()
        {
            string cacheKey = GenerateCacheKey();

            var procedures = Configuration.GetCacheProcedures();

            bool useCache = procedures?.Keys?.Contains(_innerCommand.CommandText) == true && cacheKey?.Length > 0;

            if (useCache)
            {

                if (_cache.Contains(cacheKey))
                {
                    return _cache.Get(cacheKey);
                }

                object result = _innerCommand.ExecuteScalar();

                if (result != null)
                {
                    TimeSpan cacheDuration = TimeSpan.FromMinutes(procedures[_innerCommand.CommandText]);
                    _cache.Set(cacheKey, result, DateTimeOffset.UtcNow.Add(cacheDuration));
                }
                
                return result;
            }

            return _innerCommand.ExecuteScalar();
        }

        public override int ExecuteNonQuery()
        {
            if (!HasOutputParameters())
                return _innerCommand.ExecuteNonQuery();

            string cacheKey = GenerateCacheKey();

            var procedures = Configuration.GetCacheProcedures();

            bool useCache = procedures?.Keys?.Contains(_innerCommand.CommandText) == true && cacheKey?.Length > 0;

            if (!useCache)
                return _innerCommand.ExecuteNonQuery();

            if (_cache.Contains(cacheKey))
            {
                var cachedResult = (Tuple<int, Dictionary<string, object>>)_cache.Get(cacheKey);

                foreach (DbParameter param in _innerCommand.Parameters)
                {
                    if (param.Direction == ParameterDirection.Output || param.Direction == ParameterDirection.InputOutput)
                    {
                        param.Value = cachedResult.Item2[param.ParameterName];
                    }
                    return cachedResult.Item1;
                }
            }

            // Execute and capture results
            int result = _innerCommand.ExecuteNonQuery();
            var outputValues = new Dictionary<string, object>();

            foreach (DbParameter param in _innerCommand.Parameters)
            {
                if (param.Direction == ParameterDirection.Output || param.Direction == ParameterDirection.InputOutput)
                {
                    outputValues[param.ParameterName] = param.Value;
                }
            }

            TimeSpan cacheDuration = TimeSpan.FromMinutes(procedures[_innerCommand.CommandText]);
            _cache.Set(cacheKey, Tuple.Create(result, outputValues), DateTimeOffset.UtcNow.Add(cacheDuration));
            return result;

        }

        private bool HasOutputParameters()
        {
            try
            {
                return _innerCommand.Parameters.Cast<DbParameter>()
                .Any(p => p.Direction == ParameterDirection.Output || p.Direction == ParameterDirection.InputOutput);
            }
            catch
            {
                return false;
            }
        }

        private string GenerateCacheKey()
        {
            try
            {
                return _innerCommand.CommandText + "_" + string.Join("_", _innerCommand.Parameters.Cast<DbParameter>().Select(p => p.Value?.ToString() ?? "NULL"));
            }
            catch
            {
                return string.Empty;
            }
        }

        public override string CommandText { get => _innerCommand.CommandText; set => _innerCommand.CommandText = value; }
        public override int CommandTimeout { get => _innerCommand.CommandTimeout; set => _innerCommand.CommandTimeout = value; }
        public override CommandType CommandType { get => _innerCommand.CommandType; set => _innerCommand.CommandType = value; }
        public override UpdateRowSource UpdatedRowSource { get => _innerCommand.UpdatedRowSource; set => _innerCommand.UpdatedRowSource = value; }
        protected override DbParameterCollection DbParameterCollection => _innerCommand.Parameters;
        protected override DbTransaction DbTransaction { get => _innerCommand.Transaction; set => _innerCommand.Transaction = value; }
        protected override bool CanRaiseEvents => true;
        protected override DbConnection DbConnection { get => _innerCommand.Connection; set => _innerCommand.Connection = value; }
        public override bool DesignTimeVisible { get => _innerCommand.DesignTimeVisible; set => _innerCommand.DesignTimeVisible = value; }

        public override void Cancel() => _innerCommand.Cancel();
        public override void Prepare() => _innerCommand.Prepare();
        protected override DbParameter CreateDbParameter() => _innerCommand.CreateParameter();

    }
}
