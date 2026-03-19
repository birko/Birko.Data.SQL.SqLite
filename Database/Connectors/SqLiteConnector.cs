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
using PasswordSettings = Birko.Configuration.PasswordSettings;

namespace Birko.Data.SQL.Connectors
{
    public partial class SqLiteConnector : AbstractConnector
    {
        public SqLiteConnector(Birko.Configuration.PasswordSettings settings) : base(settings)
        {
            OnException += SqLiteConnector_OnException;
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
            if (settings != null && !string.IsNullOrEmpty(Path))
            {

                bool init = !System.IO.File.Exists(Path);
                var connectionString = $"Data Source={Path}";
                if (!string.IsNullOrEmpty(settings.Password))
                    connectionString += $";Password={settings.Password}";
                var connection = new SqliteConnection(connectionString);
                if (init)
                {
                    DoInit();
                }
                return connection;
            }
            else
            {
                throw new Exception("No path provided");
            }
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

                if (field.IsAutoincrement)
                {
                    result.AppendFormat(" AUTOINCREMENT");
                }
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
        }

        #endregion
    }
}
