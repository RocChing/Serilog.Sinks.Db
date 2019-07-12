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

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Sinks.PeriodicBatching;

namespace Serilog.Sinks.Db
{
    /// <summary>
    ///     Writes log events as rows in a table of MSSqlServer database.
    /// </summary>
    public class DbSink : PeriodicBatchingSink
    {
        /// <summary>
        ///     A reasonable default for the number of events posted in
        ///     each batch.
        /// </summary>
        public const int DefaultBatchPostingLimit = 50;

        private IDbSinkCore _db;
        /// <summary>
        ///     A reasonable default time to wait between checking for event batches.
        /// </summary>
        public static readonly TimeSpan DefaultPeriod = TimeSpan.FromSeconds(5);

        public DbSink(int batchPostingLimit, TimeSpan period, IDbSinkCore dbSinkCore)
            : base(batchPostingLimit, period)
        {
            _db = dbSinkCore;

            _db.DbOpt.Check();

            _db.ColumnOptions.FinalizeConfigurationForSinkConstructor();
        }

        public DbSink(int batchPostingLimit, TimeSpan period, DbOptions opt, ITableCreator tableCreator)
            : base(batchPostingLimit, period)
        {
            _db = new DbSinkCore(opt, tableCreator);

            _db.DbOpt.Check();

            _db.ColumnOptions.FinalizeConfigurationForSinkConstructor();
        }

        /// <summary>
        ///     Emit a batch of log events, running asynchronously.
        /// </summary>
        /// <param name="events">The events to emit.</param>
        /// <remarks>
        ///     Override either <see cref="PeriodicBatchingSink.EmitBatch" /> or <see cref="PeriodicBatchingSink.EmitBatchAsync" />
        ///     ,
        ///     not both.
        /// </remarks>
        protected override async Task EmitBatchAsync(IEnumerable<LogEvent> events)
        {
            // Copy the events to the data table
            //FillDataTable(events);

            try
            {
                //using (var cn = new SqlConnection(_traits.connectionString))
                //{
                //    await cn.OpenAsync().ConfigureAwait(false);
                //    using (var copy = _traits.columnOptions.DisableTriggers
                //            ? new SqlBulkCopy(cn)
                //            : new SqlBulkCopy(cn, SqlBulkCopyOptions.CheckConstraints | SqlBulkCopyOptions.FireTriggers, null)
                //    )
                //    {
                //        copy.DestinationTableName = string.Format("[{0}].[{1}]", _traits.schemaName, _traits.tableName);
                //        foreach (var column in _traits.eventTable.Columns)
                //        {
                //            var columnName = ((DataColumn)column).ColumnName;
                //            var mapping = new SqlBulkCopyColumnMapping(columnName, columnName);
                //            copy.ColumnMappings.Add(mapping);
                //        }

                //        await copy.WriteToServerAsync(_traits.eventTable).ConfigureAwait(false);
                //    }
                //}

                using (var cn = _db.DbOpt.Connection())
                {
                    cn.Open();
                    using (var cmd = cn.CreateCommand())
                    {
                        foreach (var logEvent in events)
                        {
                            cmd.CommandType = CommandType.Text;
                            var res = _db.GetInsertSqlString(logEvent);

                            foreach (var item in res.Value)
                            {
                                var parameter = cmd.CreateParameter();
                                parameter.ParameterName = item.Key;
                                parameter.Value = item.Value;

                                if (item.Value is DateTime) parameter.DbType = DbType.DateTime2;
                                cmd.Parameters.Add(parameter);
                            }

                            cmd.CommandText = res.Key;
                            cmd.ExecuteNonQuery();
                            cmd.Parameters.Clear();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to write {0} log events to the database due to following error: {1}", events.Count(), ex.Message);
            }
            finally
            {
                // Processed the items, clear for the next run
                //_traits.eventTable.Clear();
            }
        }

        //void FillDataTable(IEnumerable<LogEvent> events)
        //{
        //    // Add the new rows to the collection. 
        //    foreach (var logEvent in events)
        //    {
        //        var row = _traits.eventTable.NewRow();

        //        foreach (var field in _db.GetColumnsAndValues(logEvent))
        //        {
        //            row[field.Key] = field.Value;
        //        }

        //        _traits.eventTable.Rows.Add(row);
        //    }

        //    _traits.eventTable.AcceptChanges();
        //}

        /// <summary>
        ///     Disposes the connection
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                _db.Dispose();
            }
        }
    }
}
