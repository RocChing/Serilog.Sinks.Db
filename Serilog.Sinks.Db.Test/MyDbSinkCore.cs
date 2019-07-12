using System;
using System.Collections.Generic;
using System.Text;
using Serilog.Events;
using Serilog.Sinks.Db;

namespace Serilog.Sinks.Db.Test
{
    public class MyDbSinkCore : DbSinkCore
    {
        public MyDbSinkCore(DbOptions opt, ITableCreator tableCreator) : base(opt, tableCreator)
        {

        }

        public override KeyValuePair<string, Dictionary<string, object>> GetInsertSqlString(LogEvent logEvent)
        {
            return base.GetInsertSqlString(logEvent);
        }

        public override IEnumerable<KeyValuePair<string, object>> GetColumnsAndValues(LogEvent logEvent)
        {
            var list = base.GetColumnsAndValues(logEvent);
            foreach (var item in list)
            {
                yield return item;
            }

            yield return new KeyValuePair<string, object>("Address", "北京");
            yield return new KeyValuePair<string, object>("Age2", 9.3);
        }
    }
}
