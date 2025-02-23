using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CacheCommander
{
    internal class CacheDbDataReader : DbDataReader
    {

        private readonly List<Dictionary<string, object>> _rows;
        private readonly string[] _columnNames;
        private int _currentIndex = -1;
        private readonly DataTable _schemaTable;

        public CacheDbDataReader(List<Dictionary<string, object>> rows, DataTable schemaTable)
        {
            _rows = rows ?? new List<Dictionary<string, object>>();
            _columnNames = _rows.Any() ? _rows.First().Keys.ToArray() : new string[0];
            _schemaTable = schemaTable;
        }

        public override bool Read()
        {
            if (_currentIndex + 1 < _rows.Count)
            {
                _currentIndex++;
                return true;
            }
            return false;
        }

        public override object this[string name] => _rows[_currentIndex].TryGetValue(name.ToLower(), out var value) ? value : throw new IndexOutOfRangeException($"Column '{name}' not found.");
        public override object this[int ordinal] => _rows[_currentIndex][_columnNames[ordinal]];

        public override object GetValue(int ordinal) => _rows[_currentIndex][_columnNames[ordinal]];
        public override bool IsDBNull(int ordinal) => GetValue(ordinal) == DBNull.Value;

        public override string GetName(int ordinal) => _columnNames[ordinal];
        public override int GetOrdinal(string name) => Array.IndexOf(_columnNames, name.ToLower());
        public override int FieldCount => _columnNames.Length;
        public override bool HasRows => _rows.Count > 0;

        public override int Depth => 0;
        public override bool IsClosed => false;
        public override int RecordsAffected => 0;

        public override void Close() { }

        public override string GetString(int ordinal) => GetValue(ordinal)?.ToString() ?? string.Empty;
        public override short GetInt16(int ordinal) => Convert.ToInt16(GetValue(ordinal));
        public override int GetInt32(int ordinal) => Convert.ToInt32(GetValue(ordinal));
        public override long GetInt64(int ordinal) => Convert.ToInt64(GetValue(ordinal));
        public override double GetDouble(int ordinal) => Convert.ToDouble(GetValue(ordinal));
        public override bool GetBoolean(int ordinal) => Convert.ToBoolean(GetValue(ordinal));

        public override System.Data.DataTable GetSchemaTable()
        {
            if (_schemaTable == null)
                throw new InvalidOperationException("Schema table is unavailable.");

            return _schemaTable.Copy();
        }

        public override System.Collections.IEnumerator GetEnumerator() => throw new NotImplementedException();
        public override bool NextResult() => false;
        public override int GetValues(object[] values)
        {
            if (_currentIndex < 0 || _currentIndex >= _rows.Count)
                throw new InvalidOperationException("No data available");

            var currentRow = _rows[_currentIndex];
            int fieldCount = FieldCount;

            for (int i = 0; i < fieldCount; i++)
            {
                values[i] = currentRow.Values.ElementAt(i) ?? DBNull.Value;
            }

            return fieldCount;
        }
        public override byte GetByte(int ordinal) => throw new NotImplementedException();
        public override char GetChar(int ordinal) => throw new NotImplementedException();
        public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length) => throw new NotImplementedException();
        public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length) => throw new NotImplementedException();
        public override Guid GetGuid(int ordinal) => throw new NotImplementedException();
        public override Type GetFieldType(int ordinal) => throw new NotImplementedException();
        public override decimal GetDecimal(int ordinal) => throw new NotImplementedException();
        public override float GetFloat(int ordinal) => throw new NotImplementedException();
        public override DateTime GetDateTime(int ordinal) => throw new NotImplementedException();

        public override string GetDataTypeName(int ordinal)
        {
            throw new NotImplementedException();
        }

    }
}
