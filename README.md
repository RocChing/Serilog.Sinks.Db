# Serilog.Sinks.Db
Serilog.Sinks.Db


Serilog.Sinks.Db 是 Serilog 写入数据库的支持组件

目前支持 sql server , mysql ,sqlite 

也可以自定义其他适配，比如

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

