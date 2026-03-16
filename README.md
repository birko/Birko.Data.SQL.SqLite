# Birko.Data.SQL.SqLite

SQLite implementation of Birko.Data.SQL stores and repositories.

## Features

- SQLite stores (sync/async, single/bulk)
- Embedded database (no server required)
- File-based and in-memory database support
- Transaction-based bulk operations

## Installation

```bash
dotnet add package Birko.Data.SQL.SqLite
```

## Dependencies

- Birko.Data.Core (AbstractModel)
- Birko.Data.Stores (store interfaces, Settings)
- Birko.Data.SQL
- Microsoft.Data.Sqlite (migrated from System.Data.SQLite)

## Migration from System.Data.SQLite

This project now uses `Microsoft.Data.Sqlite` instead of `System.Data.SQLite`. If upgrading:
- Replace `using System.Data.SQLite;` with `using Microsoft.Data.Sqlite;`
- Rename types: `SQLiteConnection` -> `SqliteConnection`, `SQLiteCommand` -> `SqliteCommand`, `SQLiteParameter` -> `SqliteParameter`, `SQLiteException` -> `SqliteException`
- Remove `Version=3` from connection strings

## Usage

```csharp
using Birko.Data.SQL.SqLite.Stores;

public class CustomerStore : SqLiteStore<Customer>
{
    public override Guid Create(Customer item)
    {
        var cmd = Connector.CreateCommand();
        cmd.CommandText = "INSERT INTO customers (id, name, email) VALUES (@Id, @Name, @Email)";
        cmd.Parameters.AddWithValue("@Id", item.Id);
        cmd.Parameters.AddWithValue("@Name", item.Name);
        cmd.Parameters.AddWithValue("@Email", item.Email);
        cmd.ExecuteNonQuery();
        return item.Id;
    }
}
```

### Connection Strings

```
Data Source=path/to/database.db;    -- File-based
Data Source=:memory:;               -- In-memory
```

## API Reference

### Stores

- **SqLiteStore\<T\>** - Sync store
- **SqLiteBulkStore\<T\>** - Bulk operations (transaction-based)
- **AsyncSqLiteStore\<T\>** - Async store
- **AsyncSqLiteBulkStore\<T\>** - Async bulk store

### Repositories

- **SqLiteRepository\<T\>** / **SqLiteBulkRepository\<T\>**
- **AsyncSqLiteRepository\<T\>** / **AsyncSqLiteBulkRepository\<T\>**

### Connector

- **SqLiteConnector** - SQLite connection management

## Related Projects

- [Birko.Data.SQL](../Birko.Data.SQL/) - SQL base classes

## License

Part of the Birko Framework.
