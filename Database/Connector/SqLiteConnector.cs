using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using Birko.Data.SQL.Condition;
using Birko.Data.SQL.Connector;
using Birko.Data.SQL.Field;

namespace Birko.Data.SQL.Connector
{
    public class SqLiteConnector : AbstractConnector
    {
        public SqLiteConnector(Store.PasswordSettings settings) : base(settings)
        {
            OnException += SqLiteConnector_OnException;
        }

        private void SqLiteConnector_OnException(Exception ex, string commandText)
        {
            if (ex is SQLiteException && !IsInit && ex.Message.Contains("SQL logic error") && ex.Message.Contains("no such table:"))
            {
                DoInit();
            }
            else
            {
                throw new Exception(commandText, ex);
            }
        }

        public string Path
        {
            get
            {
                return (!string.IsNullOrEmpty(_settings?.Location) && !string.IsNullOrEmpty(_settings?.Name))
                    ? System.IO.Path.Combine(_settings.Location, _settings.Name)
                    : null;
            }
        }

        public override DbConnection CreateConnection(Store.PasswordSettings settings)
        {
            if (settings != null && !string.IsNullOrEmpty(Path))
            {

                bool init = !System.IO.File.Exists(Path);
                var connection = new SQLiteConnection(string.Format("Data Source={0}{1};Version=3", new[] {
                    Path,
                    !string.IsNullOrEmpty(settings.Password) ? string.Format(";Password={0};", settings.Password): null
                }));
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
                case DbType.Guid:
                    return "BLOB";
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

        public override DbCommand AddParameter(DbCommand command, string name, object value)
        {
            if (command.Parameters.Contains(name))
            {
                (command.Parameters[name] as SQLiteParameter).Value = value ?? DBNull.Value;
            }
            else
            {
                (command as SQLiteCommand).Parameters.AddWithValue(name, value ?? DBNull.Value);
            }
            return command;
        }
    }
}
