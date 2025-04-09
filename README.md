# CacheCommander Basic 

a simple cached implementation of SqlDataReader written in .Net Framework 4.6.2

I realize there are better ways to accomplish what this library is doing in later versions of .NET, but I had a need for a quick caching implementation for SQL calls in .NET Framework, and this implementation saves me from having to add caching manually to multiple locations in code.

## Example app.config settings

In the example app.config below, you'll see there's a section added to the configSections node for CacheCommander.StoredProcedures, which allows for a custom `CacheCommander.StoredProcedures` section below that.
For each stored procedure you want to cache, you'll need to add a key (stored procedure name) and value (time in minutes to cache) for each stored procedure you want to cache.

If you assign a given stored procedure value="0" it will disable caching for that stored procedure. This should allow for disabling caching without needing to remove a row entirely. Leaving the value blank, or any non-integer value will use the Default cache time of 3 minutes.

```
<configuration>
  <configSections>
    <section name="CacheCommander.StoredProcedures" type="System.Configuration.NameValueSectionHandler" />
  </configSections>
  <CacheCommander.StoredProcedures>
    <add key="my.storedprocedure.name" value="30" />
    <add key="my.other.storedprocedure.name" value="10" />
  </CacheCommander.StoredProcedures>
</configuration>
```

## Example usage in C Sharp

In the examples below, you'll see we change the SqlCommand to a new CacheDbCommand object, which passes in the new SqlCommand. 
After that, we set the CommandType to StoredProcedure and add Parameters as needed.

ExecuteReader has been replaced with ExecuteCacheDataReader because it's not an override-able method, but ExecuteScalar and ExecuteNonQuery are overridden.

```
SqlConnection conn = new SqlConnection("SomeSqlConnectionStringValue");
conn.Open();

// example cmd.ExecuteReader() implementation
using (CacheDbCommand cmd = new CacheDbCommand(new SqlCommand()))
{
    cmd.Connection = conn;
    cmd.CommandText = "my.storedprocedure.name";
    cmd.CommandType = CommandType.StoredProcedure;
    cmd.Parameters.Add(new SqlParameter("@ParameterName", "value"));

    using (DbDataReader reader = cmd.ExecuteCacheDataReader())
    {
        while (reader.Read())
        {
            Console.WriteLine(reader["ColumnName"]);
        }
    }
}

// example cmd.ExecuteScalar() implementation
// Note:  ExecuteScalar only returns a single value so it doesn't return a DataReader object
using (CacheDbCommand cmd = new CacheDbCommand(new SqlCommand()))
{
    cmd.Connection = conn;
    cmd.CommandText = "my.storedprocedure.name";
    cmd.CommandType = CommandType.StoredProcedure;
    cmd.Parameters.Add(new SqlParameter("@ParameterName", "value"));

    var outValue = cmd.ExecuteScalar();
    
    Console.WriteLine(outValue);
}

// example cmd.ExecuteNonQuery() implementation
// Note:  caching is only used for ExecuteNonQuery if there are Output parameters specified
using (CacheDbCommand cmd = new CacheDbCommand(new SqlCommand()))
{
    cmd.Connection = conn;
    cmd.CommandText = "my.storedprocedure.name";
    cmd.CommandType = CommandType.StoredProcedure;
    cmd.Parameters.Add(new SqlParameter("@ParameterName", "value"));
    cmd.Parameters.Add(new SqlParameter("@MyOutputParam", SqlDbType.Int) { Direction = ParameterDirection.Output });

    cmd.ExecuteNonQuery();

    Console.WriteLine(cmd.Parameters["@MyOutputParam"].Value);
}

conn.Close();
conn.Dispose();
```

## SqlDataAdapter implementation

SqlDataAdapter is different from a normal SqlDataReader in that it creates a DataTable to populate with the results of the SQL call. I would recommend using this sparingly as it will take up more space in memory to cache than a normal DataReader object.

This implementation was only added to simplify implementation in an existing codebase without the need for drastic refactoring.

```
// CacheDbDataAdapter usage

DataTable myTable = new DataTable();

SqlConnection conn = new SqlConnection("SomeSqlConnectionStringValue");
conn.Open();
using (CacheDbCommand cmd = new CacheDbCommand(new SqlCommand()))
{
    cmd.Connection = conn;
    cmd.CommandText = "my.storedprocedure.name";
    cmd.CommandType = CommandType.StoredProcedure;
    cmd.Parameters.Add(new SqlParameter("@ParameterName", "value"));

    var adapter = new CacheDbDataAdapter(cmd);
    adapter.Fill(myTable);

    foreach (DataRow row in myTable.Rows)
    {
        Console.WriteLine(row["ColumnName"]);
    }
}
conn.Close();
conn.Dispose();
```

## How the above code works

This library is basically a wrapper for common Sql commands calling stored procedures, which uses app.config settings to decide whether to cache the result of your SQL call.  

If caching isn't configured in the app.config for a given stored procedure, it will execute the sql call as normal and return a new DataReader that can be accessed as seen above.

If caching _is_ configured in the app.config for a given stored procedure, it will execute the sql call the first time, then add a List of key value pairs to an in-memory cache for the number of minutes configured in the app.config.
On any subsequent calls until the timeout is reached, it will load the cached value from memory and return that as a new DataReader that can be accessed the same as any other SQL call.

Important note:  If you configure a stored procedure in the app.config, but you don't specify any timeout value, it will default to a 3 minute cache timeout.