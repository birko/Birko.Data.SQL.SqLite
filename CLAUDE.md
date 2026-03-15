# Birko.Data.SQL.SqLite

## Overview
SQLite implementation of Birko.Data.SQL stores and repositories.

## Project Location
`C:\Source\Birko.Data.SQL.SqLite\`

## Purpose
- Provides SQLite-specific data store implementations
- Embedded database support (no server required)
- Lightweight, file-based database

## Components

### Stores
- `SqLiteStore<T>` - Synchronous SQLite store
- `SqLiteBulkStore<T>` - Bulk operations store
- `AsyncSqLiteStore<T>` - Asynchronous SQLite store
- `AsyncSqLiteBulkStore<T>` - Async bulk operations store

### Repositories
- `SqLiteRepository<T>` - SQLite repository
- `SqLiteBulkRepository<T>` - Bulk repository
- `AsyncSqLiteRepository<T>` - Async repository
- `AsyncSqLiteBulkRepository<T>` - Async bulk repository

### Connector
- `SqLiteConnector` - SQLite connection management

## Database Connection

Connection string format:
```
Data Source=path/to/database.db;
```

Or in-memory:
```
Data Source=:memory:;
```

## Implementation

```csharp
using Birko.Data.SQL.SqLite.Stores;
using Microsoft.Data.Sqlite;

public class CustomerStore : SqLiteStore<Customer>
{
    public override Guid Create(Customer item)
    {
        var cmd = Connector.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO customers (id, name, email)
            VALUES (@Id, @Name, @Email)";

        cmd.Parameters.AddWithValue("@Id", item.Id);
        cmd.Parameters.AddWithValue("@Name", item.Name);
        cmd.Parameters.AddWithValue("@Email", item.Email);

        cmd.ExecuteNonQuery();
        return item.Id;
    }
}
```

## Bulk Operations

SQLite bulk operations use transactions:

```csharp
public override IEnumerable<KeyValuePair<Customer, Guid>> CreateAll(IEnumerable<Customer> items)
{
    using (var transaction = Connector.BeginTransaction())
    {
        try
        {
            foreach (var item in items)
            {
                // Insert each item
            }
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
```

## Data Types

Common SQLite to .NET type mappings:
- `TEXT` → `string`, `Guid` (stored as string)
- `INTEGER` → `int`, `long`
- `REAL` → `double`, `decimal`
- `BLOB` → `byte[]`
- `NUMERIC` → `decimal`

### Guid Storage
SQLite stores Guid as TEXT (default):

```sql
CREATE TABLE customers (
    id TEXT PRIMARY KEY,
    name TEXT,
    email TEXT
);
```

## SQLite Specific Features

### AUTOINCREMENT
For tables with auto-increment:
```sql
CREATE TABLE customers (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT
);
```

### WITHOUT ROWID
For better performance on certain tables:
```sql
CREATE TABLE customers (
    id TEXT PRIMARY KEY,
    name TEXT
) WITHOUT ROWID;
```

### Upsert (UPSERT)
SQLite 3.24+ supports upsert:
```sql
INSERT INTO customers (id, name, email)
VALUES (@Id, @Name, @Email)
ON CONFLICT(id) DO UPDATE SET name = excluded.name, email = excluded.email;
```

## Dependencies
- Birko.Data.Core
- Birko.Data.Stores
- Birko.Data.SQL
- Microsoft.Data.Sqlite

## Limitations
- Single writer at a time (concurrent writes may fail)
- Limited data types compared to other databases
- No built-in user permissions (file-based)
- Not suitable for high-concurrency scenarios

## Best Practices

### Connection Management
SQLite works best with a single long-lived connection:
```csharp
// Keep connection open for app lifetime
// Use connection pooling if multiple connections needed
```

### PRAGMA Settings
Configure SQLite for your use case:
```sql
PRAGMA journal_mode = WAL; -- Better concurrency
PRAGMA synchronous = NORMAL; -- Balance safety/performance
PRAGMA cache_size = -64000; -- 64MB cache
PRAGMA temp_store = MEMORY; -- Store temp tables in memory
```

### Transactions
Always use transactions for multiple operations:
```csharp
using (var transaction = Connector.BeginTransaction())
{
    // Multiple operations
    transaction.Commit();
}
```

## Use Cases

- Desktop applications
- Mobile applications (via Xamarin/MAUI)
- Small web applications
- Testing and development
- Embedded scenarios

## Maintenance

### README Updates
When making changes that affect the public API, features, or usage patterns of this project, update the README.md accordingly. This includes:
- New classes, interfaces, or methods
- Changed dependencies
- New or modified usage examples
- Breaking changes

### CLAUDE.md Updates
When making major changes to this project, update this CLAUDE.md to reflect:
- New or renamed files and components
- Changed architecture or patterns
- New dependencies or removed dependencies
- Updated interfaces or abstract class signatures
- New conventions or important notes

### Test Requirements
Every new public functionality must have corresponding unit tests. When adding new features:
- Create test classes in the corresponding test project
- Follow existing test patterns (xUnit + FluentAssertions)
- Test both success and failure cases
- Include edge cases and boundary conditions
