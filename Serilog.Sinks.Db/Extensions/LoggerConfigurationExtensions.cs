using Microsoft.Extensions.Configuration;
using Serilog.Configuration;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Text;
using System.Data;

namespace Serilog.Sinks.Db
{
    public static class LoggerConfigurationExtensions
    {
        public static LoggerConfiguration Db(this LoggerSinkConfiguration loggerConfiguration,
            Action<DbOptions> dbAction = null, ITableCreator tableCreator = null,
       LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
       int batchPostingLimit = DbSink.DefaultBatchPostingLimit,
       TimeSpan? period = null,
       IConfigurationSection columnOptionsSection = null
       )
        {
            if (loggerConfiguration == null) throw new ArgumentNullException("loggerConfiguration");

            var defaultedPeriod = period ?? DbSink.DefaultPeriod;

            DbOptions opt = new DbOptions();
            dbAction?.Invoke(opt);
            opt.ColumnOptions = ApplyMicrosoftExtensionsConfiguration.ConfigureColumnOptions(opt.ColumnOptions, columnOptionsSection);
            return loggerConfiguration.Sink(new DbSink(batchPostingLimit, defaultedPeriod, opt, tableCreator ?? new DefaultTableCreator()), restrictedToMinimumLevel);
        }

        public static LoggerConfiguration Db(this LoggerSinkConfiguration loggerConfiguration, IDbSinkCore dbSinkCore, LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum, int batchPostingLimit = DbSink.DefaultBatchPostingLimit, TimeSpan? period = null)
        {
            if (loggerConfiguration == null) throw new ArgumentNullException("loggerConfiguration");

            var defaultedPeriod = period ?? DbSink.DefaultPeriod;

            return loggerConfiguration.Sink(new DbSink(batchPostingLimit, defaultedPeriod, dbSinkCore), restrictedToMinimumLevel);
        }

        public static LoggerConfiguration Db(this LoggerAuditSinkConfiguration loggerAuditSinkConfiguration, Action<DbOptions> dbAction = null, ITableCreator tableCreator = null, LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum, IConfigurationSection columnOptionsSection = null)
        {
            if (loggerAuditSinkConfiguration == null) throw new ArgumentNullException("loggerAuditSinkConfiguration");

            DbOptions opt = new DbOptions();
            dbAction?.Invoke(opt);
            opt.ColumnOptions = ApplyMicrosoftExtensionsConfiguration.ConfigureColumnOptions(opt.ColumnOptions, columnOptionsSection);
            return loggerAuditSinkConfiguration.Sink(new DbAuditSink(opt, tableCreator ?? new DefaultTableCreator()), restrictedToMinimumLevel);
        }

        public static LoggerConfiguration Db(this LoggerAuditSinkConfiguration loggerAuditSinkConfiguration, IDbSinkCore dbSinkCore, LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum)
        {
            if (loggerAuditSinkConfiguration == null) throw new ArgumentNullException("loggerAuditSinkConfiguration");

            return loggerAuditSinkConfiguration.Sink(new DbAuditSink(dbSinkCore), restrictedToMinimumLevel);
        }
    }
}
