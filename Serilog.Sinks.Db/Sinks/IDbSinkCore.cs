using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Text;

namespace Serilog.Sinks.Db
{
    public interface IDbSinkCore : IDisposable
    {
        DbOptions DbOpt { get; set; }

        ColumnOptions ColumnOptions { get; }

        KeyValuePair<string, Dictionary<string, object>> GetInsertSqlString(LogEvent logEvent);

        IEnumerable<KeyValuePair<string, object>> GetColumnsAndValues(LogEvent logEvent);

        KeyValuePair<string, object> GetStandardColumnNameAndValue(StandardColumn column, LogEvent logEvent);
    }
}
