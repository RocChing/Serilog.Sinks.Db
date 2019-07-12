using System;
using System.Data;
using System.Text;

namespace Serilog.Sinks.Db
{
    internal class SqlTableCreator
    {
        private ProviderType _type;
        private Func<IDbConnection> _dbFunc;
        private string tableName;
        private string schemaName;
        private DataTable dataTable;
        private ColumnOptions columnOptions;

        public SqlTableCreator(ProviderType type, Func<IDbConnection> func, string schemaName, string tableName, DataTable dataTable, ColumnOptions columnOptions)
        {
            this._type = type;
            this._dbFunc = func;
            this.schemaName = schemaName;
            this.tableName = tableName;
            this.dataTable = dataTable;
            this.columnOptions = columnOptions;
        }

        public int CreateTable()
        {
            string sql = string.Empty;
            switch (_type)
            {
                case ProviderType.SQLServer:
                    sql = GetMsSqlFromDataTable();
                    break;
                case ProviderType.MySql:
                    sql = GetMySqlFromDataTable();
                    break;
                case ProviderType.Oracle:
                    break;
                case ProviderType.SQLite:
                    break;
                case ProviderType.Oledb:
                    break;
            }

            if (string.IsNullOrEmpty(sql))
            {
                throw new Exception($"数据库类型为{_type}的创建表语句暂时未实现");
            }

            using (var conn = _dbFunc())
            {
                using (var cmd = conn.CreateCommand())
                {
                    conn.Open();
                    cmd.CommandText = sql;

                    return cmd.ExecuteNonQuery();
                }
            }
        }

        private string GetMySqlFromDataTable()
        {
            var sql = new StringBuilder();
            var i = 0;

            sql.AppendLine($"CREATE TABLE IF NOT EXISTS `{tableName}` ( ");
            sql.AppendLine("`Id` BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,");

            foreach (DataColumn column in dataTable.Columns)
            {
                i++;
                var common = (SqlColumn)column.ExtendedProperties["SqlColumn"];
                if (common != null && common == columnOptions.PrimaryKey)
                    continue;
                sql.Append($"`{column.ColumnName}` TEXT NULL");

                if (dataTable.Columns.Count > i)
                {
                    sql.Append(",");
                }
                sql.AppendLine();
            }

            sql.AppendLine(");");

            return sql.ToString();
        }

        private string GetMsSqlFromDataTable()
        {
            var sql = new StringBuilder();
            var ix = new StringBuilder();
            int indexCount = 1;

            //start schema check and DDL(wrap in EXEC to make a separate batch)
            sql.AppendLine($"IF(NOT EXISTS(SELECT * FROM sys.schemas WHERE name = '{schemaName}'))");
            sql.AppendLine("BEGIN");
            sql.AppendLine($"EXEC('CREATE SCHEMA [{schemaName}] AUTHORIZATION [dbo]')");
            sql.AppendLine("END");

            // start table-creatin batch and DDL
            sql.AppendLine($"IF NOT EXISTS (SELECT s.name, t.name FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = '{schemaName}' AND t.name = '{tableName}')");
            sql.AppendLine("BEGIN");
            sql.AppendLine($"CREATE TABLE [{schemaName}].[{tableName}] ( ");

            // build column list
            int i = 1;
            foreach (DataColumn column in dataTable.Columns)
            {
                var common = (SqlColumn)column.ExtendedProperties["SqlColumn"];

                sql.Append(GetColumnDDL(common));
                if (dataTable.Columns.Count > i++) sql.Append(",");
                sql.AppendLine();

                // collect non-PK indexes for separate output after the table DDL
                if (common != null && common.NonClusteredIndex && common != columnOptions.PrimaryKey)
                    ix.AppendLine($"CREATE NONCLUSTERED INDEX [IX{indexCount++}_{tableName}] ON [{schemaName}].[{tableName}] ([{common.ColumnName}]);");
            }

            // primary key constraint at the end of the table DDL
            if (columnOptions.PrimaryKey != null)
            {
                var clustering = (columnOptions.PrimaryKey.NonClusteredIndex ? "NON" : string.Empty);
                sql.AppendLine($" CONSTRAINT [PK_{tableName}] PRIMARY KEY {clustering}CLUSTERED ([{columnOptions.PrimaryKey.ColumnName}])");
            }

            // end of CREATE TABLE
            sql.AppendLine(");");

            // CCI is output separately after table DDL
            if (columnOptions.ClusteredColumnstoreIndex)
                sql.AppendLine($"CREATE CLUSTERED COLUMNSTORE INDEX [CCI_{tableName}] ON [{schemaName}].[{tableName}]");

            // output any extra non-clustered indexes
            sql.Append(ix);

            // end of batch
            sql.AppendLine("END");

            return sql.ToString();
        }

        // Examples of possible output:
        // [Id] BIGINT IDENTITY(1,1) NOT NULL
        // [Message] VARCHAR(1024) NULL
        private string GetColumnDDL(SqlColumn column)
        {
            var sb = new StringBuilder();

            sb.Append($"[{column.ColumnName}] ");

            sb.Append(column.DataType.ToString().ToUpperInvariant());

            if (SqlDataTypes.DataLengthRequired.Contains(column.DataType))
                sb.Append("(").Append(column.DataLength == -1 ? "MAX" : column.DataLength.ToString()).Append(")");

            if (column.StandardColumnIdentifier == StandardColumn.Id)
                sb.Append(" IDENTITY(1,1)");

            sb.Append(column.AllowNull ? " NULL" : " NOT NULL");

            return sb.ToString();
        }
    }
}
