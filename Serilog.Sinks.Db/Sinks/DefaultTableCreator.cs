using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace Serilog.Sinks.Db
{
    public class DefaultTableCreator : ITableCreator
    {
        public DefaultTableCreator()
        {

        }

        public virtual void CreateTable(DbOptions opt, DataTable columnList)
        {
            string sql = GetCreateTableSql(opt, columnList);

            if (string.IsNullOrEmpty(sql))
            {
                throw new Exception($"数据库类型为{opt.Type}的创建表语句暂时未实现");
            }

            try
            {
                using (var conn = opt.Connection())
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        conn.Open();
                        cmd.CommandText = sql;
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"创建表{opt.TableName}失败,错误信息: {e.ToString()}");
                throw e;
            }
        }

        public virtual string GetCreateTableSql(DbOptions opt, DataTable columnList)
        {
            string sql = string.Empty;
            switch (opt.Type)
            {
                case ProviderType.SQLServer:
                    sql = GetMsSqlFromDataTable(opt, columnList);
                    break;
                case ProviderType.MySql:
                    sql = GetMysqlCreateTableString(opt, columnList);
                    break;
                case ProviderType.Oracle:
                    break;
                case ProviderType.SQLite:
                    sql = GetSqliteCreateTableString(opt, columnList);
                    break;
                case ProviderType.Oledb:
                    break;
            }
            return sql;
        }

        private string GetSqliteCreateTableString(DbOptions opt, DataTable columnList)
        {
            var sql = new StringBuilder();
            var i = 0;

            sql.AppendLine($"CREATE TABLE IF NOT EXISTS [{opt.TableName}] ( ");
            sql.AppendLine("[Id] INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,");

            foreach (DataColumn column in columnList.Columns)
            {
                i++;
                var common = (SqlColumn)column.ExtendedProperties["SqlColumn"];
                if (common != null && common == opt.ColumnOptions.PrimaryKey)
                    continue;

                sql.Append($"[{column.ColumnName}] ");

                sql.Append(GetDbFieldType(common.DataType));

                sql.Append(common.AllowNull ? " NULL" : " NOT NULL");

                if (columnList.Columns.Count > i)
                {
                    sql.Append(",");
                }
                sql.AppendLine();
            }

            sql.AppendLine(");");

            Console.WriteLine(sql.ToString());

            return sql.ToString();
        }

        private string GetDbFieldType(SqlDbType type)
        {
            if (type == SqlDbType.Int) return "INTEGER";
            if (type == SqlDbType.BigInt) return "DOUBLE";
            return "TEXT";
        }

        private string GetMysqlCreateTableString(DbOptions opt, DataTable columnList)
        {
            var sql = new StringBuilder();
            var i = 0;

            sql.AppendLine($"CREATE TABLE IF NOT EXISTS `{opt.TableName}` ( ");
            sql.AppendLine("`Id` BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,");

            foreach (DataColumn column in columnList.Columns)
            {
                i++;
                var common = (SqlColumn)column.ExtendedProperties["SqlColumn"];
                if (common != null && common == opt.ColumnOptions.PrimaryKey)
                    continue;
                sql.Append($"`{column.ColumnName}` ");

                sql.Append(column.DataType.ToString().ToUpperInvariant());

                sql.Append(common.AllowNull ? " NULL" : " NOT NULL");

                if (columnList.Columns.Count > i)
                {
                    sql.Append(",");
                }
                sql.AppendLine();
            }

            sql.AppendLine(");");

            return sql.ToString();
        }

        private string GetMsSqlFromDataTable(DbOptions opt, DataTable columnList)
        {
            var sql = new StringBuilder();
            var ix = new StringBuilder();
            int indexCount = 1;

            string schemaName = opt.SchemaName;
            string tableName = opt.TableName;
            var columnOptions = opt.ColumnOptions;

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
            foreach (DataColumn column in columnList.Columns)
            {
                var common = (SqlColumn)column.ExtendedProperties["SqlColumn"];

                sql.Append(GetColumnDDL(common));
                if (columnList.Columns.Count > i++) sql.Append(",");
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
