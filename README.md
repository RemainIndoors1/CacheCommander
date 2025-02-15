# CacheCommander Basic 

a "Quick and Dirty" simple cached implementation of SqlDataReader written in .Net Framework 4.6.2

I realize there are better ways to accomplish what this library is doing, but I had a need for a quick caching implementation for SQL calls in .NET Framework, and this implementation saves me from having to add caching manually to multiple locations in code.

## Example app.config settings

In the example app.config below, you'll see there's a section added to the configSections node for CacheCommander.StoredProcedures, which allows for a custom `CacheCommander.StoredProcedures` section below that.
For each stored procedure you want to cache, you'll need to add a key (stored procedure name) and value (time in minutes to cache) for each stored procedure you want to cache.

```
<configuration>
  <configSections>
    <section name="CacheCommander.StoredProcedures" type="System.Configuration.NameValueSectionHandler" />
  </configSections>
    <startup> 
        <supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8" />
    </startup>
  <CacheCommander.StoredProcedures>
    <add key="my.storedprocedure.name" value="30" />
    <add key="my.other.storedprocedure.name" value="10" />
  </CacheCommander.StoredProcedures>
</configuration>
```

## Example usage in C Sharp

In the example below, you'll see we change the SqlCommand to a new CacheDbCommand object, which passes in the new SqlCommand. 
After that, we set the CommandType to StoredProcedure and add Parameters as needed.

Next, instead of calling ExecuteDataReader, we'll call ExecuteCacheDataReader, and the rest should be pretty standard usage.

```
SqlConnection conn = new SqlConnection("SomeSqlConnectionStringValue");
conn.Open();
using (CacheDbCommand cmd = new CacheDbCommand(new SqlCommand("my.storedprocedure.name", conn)))
{
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
conn.Close();
conn.Dispose();
```

## How the above code works

This is basically a wrapper for the SqlDataReader, which uses app.config settings to decide whether to cache the result of your SQL call.  

If caching isn't configured in the app.config for a given stored procedure, it will execute the sql call as normal and return a new DataReader that can be accessed as seen above.

If caching _is_ configured in the app.config for a given stored procedure, it will execute the sql call the first time, then add a List of key value pairs to an in-memory cache for the number of minutes configured in the app.config.
On any subsequent calls until the timeout is reached, it will load the cached value from memory and return that as a new DataReader that can be accessed the same as any other SQL call.

Important note:  If you configure a stored procedure in the app.config, but you don't specify a timeout value that is greater than 0, it will default to a 3 minute cache timeout.