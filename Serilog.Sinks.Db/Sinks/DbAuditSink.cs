// Copyright 2018 Serilog Contributors 
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using System;
using System.Data;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace Serilog.Sinks.Db
{
    /// <summary>
    ///  Writes log events as rows in a table of MSSqlServer database using Audit logic, meaning that each row is synchronously committed
    ///  and any errors that occur are propagated to the caller.
    /// </summary>
    public class DbAuditSink : ILogEventSink, IDisposable
    {
        private IDbSinkCore _db;

        public DbAuditSink(DbOptions opt, ITableCreator tableCreator)
        {
            opt.Check();
            var columnOptions = opt.ColumnOptions;
            if (columnOptions != null)
            {
                if (columnOptions.DisableTriggers) throw new NotSupportedException($"The {nameof(ColumnOptions.DisableTriggers)} option is not supported for auditing.");

                columnOptions.FinalizeConfigurationForSinkConstructor();
            }
            _db = new DbSinkCore(opt, tableCreator);
        }

        public DbAuditSink(IDbSinkCore dbSinkCore)
        {
            if (dbSinkCore == null)
            {
                throw new ArgumentNullException(nameof(dbSinkCore));
            }
            var opt = dbSinkCore.DbOpt;
            opt.Check();
            var columnOptions = opt.ColumnOptions;
            if (columnOptions != null)
            {
                if (columnOptions.DisableTriggers) throw new NotSupportedException($"The {nameof(ColumnOptions.DisableTriggers)} option is not supported for auditing.");

                columnOptions.FinalizeConfigurationForSinkConstructor();
            }
            //_db = dbSinkCore ?? new DbSinkCore(opt, tableCreator);
        }

        /// <summary>Emit the provided log event to the sink.</summary>
        /// <param name="logEvent">The log event to write.</param>
        public virtual void Emit(LogEvent logEvent)
        {
            try
            {
                using (IDbConnection connection = _db.DbOpt.Connection())
                {
                    connection.Open();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandType = CommandType.Text;
                        var res = _db.GetInsertSqlString(logEvent);

                        foreach (var item in res.Value)
                        {
                            var parameter = command.CreateParameter();
                            parameter.ParameterName = item.Key;
                            parameter.Value = item.Value;

                            if (item.Value is DateTime) parameter.DbType = DbType.DateTime2;
                            command.Parameters.Add(parameter);
                        }

                        command.CommandText = res.Key;
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to write log event to the database due to following error: {1}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Releases the unmanaged resources used by the Serilog.Sinks.Db.MSSqlServerAuditSink and optionally
        /// releases the managed resources.
        /// </summary>
        /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged
        ///                         resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _db.Dispose();
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
