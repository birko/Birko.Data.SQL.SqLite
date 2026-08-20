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

## Timestamps — two kinds of `DateTime` column

```csharp
[UtcField]                                  // an INSTANT
public DateTime ObservedAt { get; set; }     // reads back DateTimeKind.Utc

public DateTime NoticeDate { get; set; }     // a WALL CLOCK
                                             // reads back DateTimeKind.Unspecified
```

A plain `DateTime` column stores the value's components exactly as supplied; `DateTimeKind` is not persisted.
A `[UtcField]` one stores an **instant** — normalised to UTC on write, read back as `Kind=Utc`. Neither
preserves a caller's original offset; if you need the offset itself, store it in its own column.

**On SQLite `[UtcField]` falls back** — there is no timezone-aware type. The column is *declared*
`INTEGER` while the driver actually stores ISO-8601 text carrying the offset; that mismatch is
pre-existing and shared with a plain `DateTime` column, and is left as-is deliberately. The instant is
exact either way.

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
