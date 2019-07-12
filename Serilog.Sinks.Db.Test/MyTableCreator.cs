using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace Serilog.Sinks.Db.Test
{
    public class MyTableCreator : DefaultTableCreator
    {
        public override string GetCreateTableSql(DbOptions opt, DataTable columnList)
        {
            if (opt.Type == ProviderType.SQLite)
            {
                return GetSqliteCreateTableString(opt, columnList);
            }
            return base.GetCreateTableSql(opt, columnList);
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
    }
}
