using System;
using System.Collections.Generic;
using System.Text;
using System.Data;

namespace Serilog.Sinks.Db
{
    public class DbOptions
    {
        public ProviderType Type { get; set; }

        public Func<IDbConnection> Connection { get; set; }

        public string TableName { get; set; }

        public bool AutoCreateSqlTable { get; set; }

        public string SchemaName { get; set; }

        public ColumnOptions ColumnOptions { get; set; }

        public IFormatProvider FormatProvider { get; set; }

        public DbOptions()
        {
            Type = ProviderType.SQLServer;
            TableName = "Logs";
            AutoCreateSqlTable = true;
            SchemaName = "dbo";
            ColumnOptions = new ColumnOptions() { AdditionalColumns = new List<SqlColumn>() };
        }

        public bool Check()
        {
            if (string.IsNullOrWhiteSpace(TableName)) throw new ArgumentNullException(nameof(TableName));

            if (ColumnOptions == null) throw new ArgumentNullException(nameof(ColumnOptions));

            if (Connection == null) throw new ArgumentNullException(nameof(Connection));

            return true;
        }

        public void AddColumn(SqlColumn column)
        {
            ColumnOptions.AdditionalColumns.Add(column);
        }

        public void AddColumn(DataColumn column)
        {
            ColumnOptions.AdditionalColumns.Add(new SqlColumn(column));
        }

        public void AddColumn(string columnName, SqlDbType dataType, bool allowNull = true, int dataLength = -1)
        {
            ColumnOptions.AdditionalColumns.Add(new SqlColumn(columnName, dataType, allowNull, dataLength));
        }
    }
}
