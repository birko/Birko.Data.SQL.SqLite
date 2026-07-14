using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Birko.Data.SQL.Conditions;
using Birko.Data.SQL.Connectors;
using Birko.Data.SQL.Fields;
using SqLiteSettings = Birko.Data.SQL.SqLite.Stores.SqLiteSettings;
using PasswordSettings = Birko.Configuration.PasswordSettings;

namespace Birko.Data.SQL.Connectors
{
    public partial class SqLiteConnector : AbstractAsyncConnector
    {
        public SqLiteConnector(Birko.Configuration.PasswordSettings settings) : base(settings)
        {
            OnException += SqLiteConnector_OnException;
        }

        /// <summary>
        /// Detects SQLite transient errors: database locked (5), database busy (6).
        /// </summary>
        public override bool IsTransientException(Exception ex)
        {
            if (base.IsTransientException(ex)) return true;
            if (ex is SqliteException sqliteEx)
            {
                switch (sqliteEx.SqliteErrorCode)
                {
                    case 5:   // SQLITE_BUSY — database is locked
                    case 6:   // SQLITE_LOCKED — table in the database is locked
                        return true;
                }
            }
            return false;
        }

        private void SqLiteConnector_OnException(Exception ex, string? commandText)
        {
            if (ex is SqliteException && !IsInitializing && ex.Message.Contains("SQLite Error") && ex.Message.Contains("no such table"))
            {
                DoInit();
            }
            else
            {
                throw new Exception(commandText, ex);
            }
        }

        public string? Path
        {
            get
            {
                return (!string.IsNullOrEmpty(_settings?.Location) && !string.IsNullOrEmpty(_settings?.Name))
                    ? System.IO.Path.Combine(_settings.Location, _settings.Name)
                    : null;
            }
        }

        public override DbConnection CreateConnection(PasswordSettings settings)
        {
            if (settings == null || string.IsNullOrEmpty(Path))
            {
                throw new Exception("No path provided");
            }

            bool init = !System.IO.File.Exists(Path);

            if (settings is SqLiteSettings sqliteSettings)
            {
                var connection = new SqliteConnection(sqliteSettings.GetConnectionString());
                if (init)
                {
                    DoInit();
                }
                return connection;
            }

            var connectionString = $"Data Source={Path}";
            if (!string.IsNullOrEmpty(settings.Password))
                connectionString += $";Password={settings.Password}";
            var fallbackConnection = new SqliteConnection(connectionString);
            if (init)
            {
                DoInit();
            }
            return fallbackConnection;
        }

        public override string ConvertType(DbType type, AbstractField field)
        {
            switch (type)
            {
                case DbType.Decimal:
                case DbType.VarNumeric:
                case DbType.Double:
                case DbType.Currency:
                    {
                        if (field is DecimalField decimalField && decimalField.Precision != null && decimalField.Scale != null)
                        {
                            return string.Format("NUMERIC({0},{1})", decimalField.Precision, decimalField.Scale);
                        }
                        else
                        {
                            return "REAL";
                        }
                    }
                case DbType.Boolean:
                case DbType.Date:
                case DbType.DateTime:
                case DbType.Time:
                case DbType.DateTime2:
                case DbType.DateTimeOffset:
                case DbType.Int16:
                case DbType.Int32:
                case DbType.Int64:
                case DbType.UInt16:
                case DbType.UInt32:
                case DbType.UInt64:
                case DbType.Single:
                case DbType.SByte:
                case DbType.Byte:
                    return "INTEGER";
                case DbType.Xml:
                case DbType.Object:
                case DbType.Binary:
                    return "BLOB";
                case DbType.Guid:
                case DbType.String:
                case DbType.StringFixedLength:
                case DbType.AnsiString:
                case DbType.AnsiStringFixedLength:
                default:
                    return "TEXT";
            }
        }

        public override string FieldDefinition(AbstractField field)
        {
            var result = new StringBuilder();
            if (field != null)
            {
                // TASK-058: SQLite accepts AUTOINCREMENT only as part of an `INTEGER PRIMARY KEY
                // AUTOINCREMENT` column constraint — the keyword is inseparable from an INTEGER primary
                // key, must sit adjacent to PRIMARY KEY, and the declared type must be exactly INTEGER.
                // The previous code appended a bare `AUTOINCREMENT` after any UNIQUE/NOT NULL for ANY
                // autoincrement field, producing invalid DDL: detached from PRIMARY KEY even for a PK
                // column, and — worse — emitted for a NON-primary-key increment field (e.g. a dual-key
                // model with a [PrimaryField] Guid + a separate [IncrementField] Id), which SQLite
                // rejects outright, so CreateTable threw.
                if (field.IsPrimary && field.IsAutoincrement)
                {
                    result.Append(field.Name);
                    result.Append(" INTEGER PRIMARY KEY AUTOINCREMENT");
                    return result.ToString();
                }

                result.Append(field.Name);
                result.AppendFormat(" {0}", ConvertType(field.Type, field));
                if (field.IsPrimary)
                {
                    result.AppendFormat(" PRIMARY KEY");
                }
                if (field.IsUnique)
                {
                    result.AppendFormat(" UNIQUE");
                }
                if (field.IsNotNull)
                {
                    result.AppendFormat(" NOT NULL");
                }

                // A non-primary-key [IncrementField] cannot be AUTOINCREMENT in SQLite (no per-column
                // auto-increment outside INTEGER PRIMARY KEY), so it is emitted as a plain column and
                // the caller assigns the value. Providers with real non-PK identity (MSSql IDENTITY,
                // PostgreSQL SERIAL) differ here — documented on the connector.
            }
            return result.ToString();
        }

        public override DbCommand AddParameter(DbCommand command, string name, object? value)
        {
            if(value is Guid)
            {
                value = value?.ToString();
            }
            if (command.Parameters.Contains(name))
            {
                ((SqliteParameter)command.Parameters[name]).Value = value ?? DBNull.Value;
            }
            else
            {
                ((SqliteCommand)command).Parameters.AddWithValue(name, value ?? DBNull.Value);
            }
            return command;
        }

        private object? ConvertFieldValue(AbstractField field, object model)
        {
            var value = field.Write(model);
            if (value is Guid guid)
                return guid.ToString();
            return value;
        }

        private object? ConvertPrimaryKeyValue(AbstractField field, object model)
        {
            var value = field.Property.GetValue(model);
            if (value is Guid guid)
                return guid.ToString();
            return value;
        }

        #region Native Bulk Operations

        public void BulkInsert(Type type, IEnumerable<object> models)
        {
            if (models == null || !models.Any())
                return;

            var table = DataBase.LoadTable(type);
            if (table == null)
                return;

            var fields = table.Fields.Select(f => f.Value).Where(f => !f.IsAutoincrement).ToList();
            if (!fields.Any())
                return;

            // CR-M144: wrap in ExecuteWithRetry so SQLITE_BUSY/SQLITE_LOCKED (flagged transient by the
            // overridden IsTransientException) are retried per the configured RetryPolicy, matching the
            // base RunCommandTransaction. Each attempt opens a fresh connection/transaction.
            ExecuteWithRetry(() =>
            {
                using var connection = (SqliteConnection)CreateConnection(_settings);
                connection.Open();
                using var transaction = connection.BeginTransaction();
                string? commandText = null;
                try
                {
                    using var command = connection.CreateCommand();
                    command.Transaction = transaction;

                    var columnNames = string.Join(", ", fields.Select(f => f.Name));
                    var paramNames = string.Join(", ", fields.Select(f => "@INS_" + f.Name.Replace(".", "")));
                    command.CommandText = "INSERT INTO " + QuoteIdentifier(table.Name)
                        + " (" + columnNames + ") VALUES (" + paramNames + ")";
                    commandText = command.CommandText;

                    foreach (var field in fields)
                    {
                        command.Parameters.AddWithValue("@INS_" + field.Name.Replace(".", ""), DBNull.Value);
                    }

                    foreach (var model in models)
                    {
                        foreach (var field in fields)
                        {
                            command.Parameters["@INS_" + field.Name.Replace(".", "")].Value = ConvertFieldValue(field, model) ?? DBNull.Value;
                        }
                        command.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    InitException(ex, commandText ?? "BulkInsert into " + table.Name);
                }
            }, "BulkInsert into " + table.Name);
        }

        public async Task BulkInsertAsync(Type type, IEnumerable<object> models, CancellationToken ct = default)
        {
            if (models == null || !models.Any())
                return;

            var table = DataBase.LoadTable(type);
            if (table == null)
                return;

            var fields = table.Fields.Select(f => f.Value).Where(f => !f.IsAutoincrement).ToList();
            if (!fields.Any())
                return;

            // CR-M144: retry SQLITE_BUSY/SQLITE_LOCKED via ExecuteWithRetryAsync (cancellation still
            // propagates — OperationCanceledException is not transient).
            await ExecuteWithRetryAsync(async () =>
            {
                using var connection = (SqliteConnection)CreateConnection(_settings);
                await connection.OpenAsync(ct).ConfigureAwait(false);
                using var transaction = connection.BeginTransaction();
                string? commandText = null;
                try
                {
                    using var command = connection.CreateCommand();
                    command.Transaction = transaction;

                    var columnNames = string.Join(", ", fields.Select(f => f.Name));
                    var paramNames = string.Join(", ", fields.Select(f => "@INS_" + f.Name.Replace(".", "")));
                    command.CommandText = "INSERT INTO " + QuoteIdentifier(table.Name)
                        + " (" + columnNames + ") VALUES (" + paramNames + ")";
                    commandText = command.CommandText;

                    foreach (var field in fields)
                    {
                        command.Parameters.AddWithValue("@INS_" + field.Name.Replace(".", ""), DBNull.Value);
                    }

                    foreach (var model in models)
                    {
                        ct.ThrowIfCancellationRequested();
                        foreach (var field in fields)
                        {
                            command.Parameters["@INS_" + field.Name.Replace(".", "")].Value = ConvertFieldValue(field, model) ?? DBNull.Value;
                        }
                        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                    }

                    transaction.Commit();
                }
                catch (OperationCanceledException)
                {
                    transaction.Rollback();
                    throw;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    InitException(ex, commandText ?? "BulkInsertAsync into " + table.Name);
                }
            }, ct, "BulkInsertAsync into " + table.Name);
        }

        public void BulkUpdate(Type type, IEnumerable<object> models)
        {
            if (models == null || !models.Any())
                return;

            var table = DataBase.LoadTable(type);
            if (table == null)
                return;

            var primaryFields = (table.GetPrimaryFields() ?? Enumerable.Empty<AbstractField>()).ToList();
            if (!primaryFields.Any())
                return;

            var allFields = table.Fields.Select(f => f.Value).ToList();
            var updateFields = allFields.Where(f => !f.IsPrimary && !f.IsAutoincrement).ToList();
            if (!updateFields.Any())
                return;

            // CR-M144: retry SQLITE_BUSY/SQLITE_LOCKED via ExecuteWithRetry.
            ExecuteWithRetry(() =>
            {
                using var connection = (SqliteConnection)CreateConnection(_settings);
                connection.Open();
                using var transaction = connection.BeginTransaction();
                string? commandText = null;
                try
                {
                    using var command = connection.CreateCommand();
                    command.Transaction = transaction;

                    var setClauses = updateFields.Select(f => f.Name + " = @SET_" + f.Name.Replace(".", ""));
                    var whereClauses = primaryFields.Select(f => f.Name + " = @PK_" + f.Name.Replace(".", ""));
                    command.CommandText = "UPDATE " + QuoteIdentifier(table.Name)
                        + " SET " + string.Join(", ", setClauses)
                        + " WHERE " + string.Join(" AND ", whereClauses);
                    commandText = command.CommandText;

                    foreach (var field in updateFields)
                    {
                        command.Parameters.AddWithValue("@SET_" + field.Name.Replace(".", ""), DBNull.Value);
                    }
                    foreach (var field in primaryFields)
                    {
                        command.Parameters.AddWithValue("@PK_" + field.Name.Replace(".", ""), DBNull.Value);
                    }

                    foreach (var model in models)
                    {
                        foreach (var field in updateFields)
                        {
                            command.Parameters["@SET_" + field.Name.Replace(".", "")].Value = ConvertFieldValue(field, model) ?? DBNull.Value;
                        }
                        foreach (var field in primaryFields)
                        {
                            command.Parameters["@PK_" + field.Name.Replace(".", "")].Value = ConvertPrimaryKeyValue(field, model) ?? DBNull.Value;
                        }
                        command.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    InitException(ex, commandText ?? "BulkUpdate " + table.Name);
                }
            }, "BulkUpdate " + table.Name);
        }

        public async Task BulkUpdateAsync(Type type, IEnumerable<object> models, CancellationToken ct = default)
        {
            if (models == null || !models.Any())
                return;

            var table = DataBase.LoadTable(type);
            if (table == null)
                return;

            var primaryFields = (table.GetPrimaryFields() ?? Enumerable.Empty<AbstractField>()).ToList();
            if (!primaryFields.Any())
                return;

            var allFields = table.Fields.Select(f => f.Value).ToList();
            var updateFields = allFields.Where(f => !f.IsPrimary && !f.IsAutoincrement).ToList();
            if (!updateFields.Any())
                return;

            // CR-M144: retry SQLITE_BUSY/SQLITE_LOCKED via ExecuteWithRetryAsync.
            await ExecuteWithRetryAsync(async () =>
            {
                using var connection = (SqliteConnection)CreateConnection(_settings);
                await connection.OpenAsync(ct).ConfigureAwait(false);
                using var transaction = connection.BeginTransaction();
                string? commandText = null;
                try
                {
                    using var command = connection.CreateCommand();
                    command.Transaction = transaction;

                    var setClauses = updateFields.Select(f => f.Name + " = @SET_" + f.Name.Replace(".", ""));
                    var whereClauses = primaryFields.Select(f => f.Name + " = @PK_" + f.Name.Replace(".", ""));
                    command.CommandText = "UPDATE " + QuoteIdentifier(table.Name)
                        + " SET " + string.Join(", ", setClauses)
                        + " WHERE " + string.Join(" AND ", whereClauses);
                    commandText = command.CommandText;

                    foreach (var field in updateFields)
                    {
                        command.Parameters.AddWithValue("@SET_" + field.Name.Replace(".", ""), DBNull.Value);
                    }
                    foreach (var field in primaryFields)
                    {
                        command.Parameters.AddWithValue("@PK_" + field.Name.Replace(".", ""), DBNull.Value);
                    }

                    foreach (var model in models)
                    {
                        ct.ThrowIfCancellationRequested();
                        foreach (var field in updateFields)
                        {
                            command.Parameters["@SET_" + field.Name.Replace(".", "")].Value = ConvertFieldValue(field, model) ?? DBNull.Value;
                        }
                        foreach (var field in primaryFields)
                        {
                            command.Parameters["@PK_" + field.Name.Replace(".", "")].Value = ConvertPrimaryKeyValue(field, model) ?? DBNull.Value;
                        }
                        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                    }

                    transaction.Commit();
                }
                catch (OperationCanceledException)
                {
                    transaction.Rollback();
                    throw;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    InitException(ex, commandText ?? "BulkUpdateAsync " + table.Name);
                }
            }, ct, "BulkUpdateAsync " + table.Name);
        }

        public void BulkDelete(Type type, IEnumerable<object> models)
        {
            if (models == null || !models.Any())
                return;

            var table = DataBase.LoadTable(type);
            if (table == null)
                return;

            var primaryFields = (table.GetPrimaryFields() ?? Enumerable.Empty<AbstractField>()).ToList();
            if (!primaryFields.Any())
                return;

            // CR-M144: retry SQLITE_BUSY/SQLITE_LOCKED via ExecuteWithRetry.
            ExecuteWithRetry(() =>
            {
                using var connection = (SqliteConnection)CreateConnection(_settings);
                connection.Open();
                using var transaction = connection.BeginTransaction();
                string? commandText = null;
                try
                {
                    using var command = connection.CreateCommand();
                    command.Transaction = transaction;

                    var whereClauses = primaryFields.Select(f => f.Name + " = @PK_" + f.Name.Replace(".", ""));
                    command.CommandText = "DELETE FROM " + QuoteIdentifier(table.Name)
                        + " WHERE " + string.Join(" AND ", whereClauses);
                    commandText = command.CommandText;

                    foreach (var field in primaryFields)
                    {
                        command.Parameters.AddWithValue("@PK_" + field.Name.Replace(".", ""), DBNull.Value);
                    }

                    foreach (var model in models)
                    {
                        foreach (var field in primaryFields)
                        {
                            command.Parameters["@PK_" + field.Name.Replace(".", "")].Value = ConvertPrimaryKeyValue(field, model) ?? DBNull.Value;
                        }
                        command.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    InitException(ex, commandText ?? "BulkDelete " + table.Name);
                }
            }, "BulkDelete " + table.Name);
        }

        public async Task BulkDeleteAsync(Type type, IEnumerable<object> models, CancellationToken ct = default)
        {
            if (models == null || !models.Any())
                return;

            var table = DataBase.LoadTable(type);
            if (table == null)
                return;

            var primaryFields = (table.GetPrimaryFields() ?? Enumerable.Empty<AbstractField>()).ToList();
            if (!primaryFields.Any())
                return;

            // CR-M144: retry SQLITE_BUSY/SQLITE_LOCKED via ExecuteWithRetryAsync.
            await ExecuteWithRetryAsync(async () =>
            {
                using var connection = (SqliteConnection)CreateConnection(_settings);
                await connection.OpenAsync(ct).ConfigureAwait(false);
                using var transaction = connection.BeginTransaction();
                string? commandText = null;
                try
                {
                    using var command = connection.CreateCommand();
                    command.Transaction = transaction;

                    var whereClauses = primaryFields.Select(f => f.Name + " = @PK_" + f.Name.Replace(".", ""));
                    command.CommandText = "DELETE FROM " + QuoteIdentifier(table.Name)
                        + " WHERE " + string.Join(" AND ", whereClauses);
                    commandText = command.CommandText;

                    foreach (var field in primaryFields)
                    {
                        command.Parameters.AddWithValue("@PK_" + field.Name.Replace(".", ""), DBNull.Value);
                    }

                    foreach (var model in models)
                    {
                        ct.ThrowIfCancellationRequested();
                        foreach (var field in primaryFields)
                        {
                            command.Parameters["@PK_" + field.Name.Replace(".", "")].Value = ConvertPrimaryKeyValue(field, model) ?? DBNull.Value;
                        }
                        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                    }

                    transaction.Commit();
                }
                catch (OperationCanceledException)
                {
                    transaction.Rollback();
                    throw;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    InitException(ex, commandText ?? "BulkDeleteAsync " + table.Name);
                }
            }, ct, "BulkDeleteAsync " + table.Name);
        }

        #endregion
    }
}
