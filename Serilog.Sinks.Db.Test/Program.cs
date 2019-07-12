using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SqlClient;
using System.Data.SQLite;

namespace Serilog.Sinks.Db.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            string mysqlConnectionString = "";

            string connectionString = "server=(local);database=demo;uid=sa;pwd=sa1234!;";

            string sqliteConnectionString = "Data Source=db/demo.db3;Version=3;";

            //ColumnOptions opt = new ColumnOptions()
            //{
            //    AdditionalColumns = new Collection<SqlColumn>
            //    {
            //         new SqlColumn { DataType = SqlDbType.NVarChar, ColumnName = "Name" },
            //         new SqlColumn { DataType = SqlDbType.Int, ColumnName = "Age" }
            //    }
            //};

            //mysql
            //var logger = new LoggerConfiguration()
            //    .MinimumLevel.Verbose()
            //    .WriteTo.Db(ProviderType.MySql, () =>
            //    {
            //        return new MySql.Data.MySqlClient.MySqlConnection(mysqlConnectionString);
            //    }, "MyLogs", autoCreateSqlTable: true, columnOptions: opt)
            //    .CreateLogger();

            //var logger = new LoggerConfiguration()
            //    .MinimumLevel.Verbose()
            //    .WriteTo.Db(ProviderType.SQLServer, () =>
            //    {
            //        return new SqlConnection(connectionString);
            //    }, "MyLogs", autoCreateSqlTable: true, columnOptions: opt)
            //    .CreateLogger();

            //sql server
            //var logger = new LoggerConfiguration()
            //    .MinimumLevel.Verbose()
            //    .WriteTo.Db((opt) =>
            //    {
            //        opt.TableName = "MyLogs";
            //        opt.AddColumn("Name", SqlDbType.NVarChar);
            //        opt.AddColumn("Age", SqlDbType.Int);
            //        opt.AddColumn("Address", SqlDbType.NVarChar, dataLength: 200);
            //        opt.Connection = () => new SqlConnection(connectionString);
            //    })
            //    .CreateLogger();

            //sqlite
            var opt = new DbOptions();
            opt.Type = ProviderType.SQLite;
            opt.TableName = "MyLogs";
            opt.AddColumn("Name", SqlDbType.NVarChar, allowNull: false);
            opt.AddColumn("Age", SqlDbType.Int);
            opt.AddColumn("Address", SqlDbType.NVarChar, dataLength: 200);
            opt.AddColumn("Age2", SqlDbType.BigInt);

            opt.Connection = () => new SQLiteConnection(sqliteConnectionString);
            //opt.Connection = () => new SqlConnection(connectionString);

            var logger = new LoggerConfiguration()
               .MinimumLevel.Verbose()
               .WriteTo.Db(new MyDbSinkCore(opt, new MyTableCreator()))
               .CreateLogger();

            logger.Information("Test log for {@User}, {@Name}, {@Age}", new User(1, 20, "Roc"), "chengpeng", 20);

            Console.Read();
        }
    }

    public class User
    {
        public string Name { get; set; }

        public int Age { get; set; }

        public int Id { get; set; }

        public User() { }

        public User(int id, int age, string name)
        {
            Id = id;
            Age = age;
            Name = name;
        }
    }
}
