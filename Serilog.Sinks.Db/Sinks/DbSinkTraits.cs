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

using Serilog.Debugging;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;


namespace Serilog.Sinks.Db
{
    /// <summary>Contains common functionality and properties used by both MSSqlServerSinks.</summary>
    public class DbSinkTraits : IDisposable
    {
        /// <summary>
        /// 
        /// </summary>
        public string connectionString { get; }
        /// <summary>
        /// 
        /// </summary>
        public string tableName { get; }
        /// <summary>
        /// 
        /// </summary>
        public string schemaName { get; }
        /// <summary>
        /// 
        /// </summary>
        public ColumnOptions columnOptions { get; }
        /// <summary>
        /// 
        /// </summary>
        public IFormatProvider formatProvider { get; }
        /// <summary>
        /// 
        /// </summary>
        public JsonLogEventFormatter jsonLogEventFormatter { get; }
        /// <summary>
        /// 
        /// </summary>
        public ISet<string> additionalColumnNames { get; }
        /// <summary>
        /// 
        /// </summary>
        public DataTable eventTable { get; }
        /// <summary>
        /// 
        /// </summary>
        public ISet<string> standardColumnNames { get; }

        private ProviderType _type;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="tableName"></param>
        /// <param name="schemaName"></param>
        /// <param name="columnOptions"></param>
        /// <param name="formatProvider"></param>
        /// <param name="autoCreateSqlTable"></param>
        public DbSinkTraits(ProviderType type, Func<IDbConnection> func, string tableName, string schemaName, ColumnOptions columnOptions, IFormatProvider formatProvider, bool autoCreateSqlTable)
        {
            //if (string.IsNullOrWhiteSpace(connectionString))
            //    throw new ArgumentNullException(nameof(connectionString));

            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentNullException(nameof(tableName));

            _type = type;
            //this.connectionString = connectionString;
            this.tableName = tableName;
            this.schemaName = schemaName;
            this.columnOptions = columnOptions ?? new ColumnOptions();
            this.formatProvider = formatProvider;

            standardColumnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var stdCol in this.columnOptions.Store)
            {
                var col = this.columnOptions.GetStandardColumnOptions(stdCol);
                standardColumnNames.Add(col.ColumnName);
            }

            additionalColumnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (this.columnOptions.AdditionalColumns != null)
                foreach (var col in this.columnOptions.AdditionalColumns)
                    additionalColumnNames.Add(col.ColumnName);

            if (this.columnOptions.Store.Contains(StandardColumn.LogEvent))
                jsonLogEventFormatter = new JsonLogEventFormatter(null);

            eventTable = CreateDataTable();

            if (autoCreateSqlTable)
            {
                try
                {
                    SqlTableCreator tableCreator = new SqlTableCreator(type, func, this.schemaName, this.tableName, eventTable, this.columnOptions);
                    tableCreator.CreateTable(); // return code ignored, 0 = failure?
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception creating table {tableName}:\n{ex}");
                }
            }
        }

        /// <summary>Gets a list of the column names paired with their values to emit for the specified <paramref name="logEvent"/>.</summary>
        /// <param name="logEvent">The log event to emit.</param>
        /// <returns>
        /// A list of mappings between column names and values to emit to the database for the specified <paramref name="logEvent"/>.
        /// </returns>
        public virtual IEnumerable<KeyValuePair<string, object>> GetColumnsAndValues(LogEvent logEvent)
        {
            foreach (var column in columnOptions.Store)
            {
                // skip Id (auto-incrementing identity)
                if (column != StandardColumn.Id)
                    yield return GetStandardColumnNameAndValue(column, logEvent);
            }

            if (columnOptions.AdditionalColumns != null)
            {
                foreach (var columnValuePair in ConvertPropertiesToColumn(logEvent.Properties))
                    yield return columnValuePair;
            }
        }

        public virtual string GetInsertSqlString(LogEvent logEvent, out Dictionary<string, object> parameters)
        {
            int type = (int)_type;
            StringBuilder fieldList = new StringBuilder($"INSERT INTO {Utils.LeftTokens[type]}{tableName}{Utils.RightTokens[type]} (");
            StringBuilder parameterList = new StringBuilder(") VALUES (");

            parameters = new Dictionary<string, object>();

            int index = 0;
            foreach (var field in GetColumnsAndValues(logEvent))
            {
                if (index != 0)
                {
                    fieldList.Append(',');
                    parameterList.Append(',');
                }

                string pname = $"{Utils.ParamPrefixs[type]}p{index}";
                fieldList.Append(field.Key);
                parameterList.Append(pname);

                parameters.Add(pname, field.Value ?? DBNull.Value);
                //var parameter = command.CreateParameter();
                //parameter.ParameterName = pname;
                //parameter.Value = field.Value ?? DBNull.Value;

                //if (field.Value is DateTime) parameter.DbType = DbType.DateTime2;

                index++;
            }

            parameterList.Append(')');
            fieldList.Append(parameterList.ToString());

            return fieldList.ToString();
        }

        /// <summary>
        /// 
        /// </summary>
        public void Dispose()
        {
            eventTable.Dispose();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="column"></param>
        /// <param name="logEvent"></param>
        /// <returns></returns>
        public KeyValuePair<string, object> GetStandardColumnNameAndValue(StandardColumn column, LogEvent logEvent)
        {
            switch (column)
            {
                case StandardColumn.Message:
                    return new KeyValuePair<string, object>(columnOptions.Message.ColumnName, logEvent.RenderMessage(formatProvider));
                case StandardColumn.MessageTemplate:
                    return new KeyValuePair<string, object>(columnOptions.MessageTemplate.ColumnName, logEvent.MessageTemplate.Text);
                case StandardColumn.Level:
                    return new KeyValuePair<string, object>(columnOptions.Level.ColumnName, columnOptions.Level.StoreAsEnum ? (object)logEvent.Level : logEvent.Level.ToString());
                case StandardColumn.TimeStamp:
                    return new KeyValuePair<string, object>(columnOptions.TimeStamp.ColumnName, columnOptions.TimeStamp.ConvertToUtc ? logEvent.Timestamp.ToUniversalTime().DateTime : logEvent.Timestamp.DateTime);
                case StandardColumn.Exception:
                    return new KeyValuePair<string, object>(columnOptions.Exception.ColumnName, logEvent.Exception != null ? logEvent.Exception.ToString() : null);
                case StandardColumn.Properties:
                    return new KeyValuePair<string, object>(columnOptions.Properties.ColumnName, ConvertPropertiesToXmlStructure(logEvent.Properties));
                case StandardColumn.LogEvent:
                    return new KeyValuePair<string, object>(columnOptions.LogEvent.ColumnName, LogEventToJson(logEvent));
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private string LogEventToJson(LogEvent logEvent)
        {
            if (columnOptions.LogEvent.ExcludeAdditionalProperties)
            {
                var filteredProperties = logEvent.Properties.Where(p => !additionalColumnNames.Contains(p.Key));
                logEvent = new LogEvent(logEvent.Timestamp, logEvent.Level, logEvent.Exception, logEvent.MessageTemplate, filteredProperties.Select(x => new LogEventProperty(x.Key, x.Value)));
            }

            var sb = new StringBuilder();
            using (var writer = new System.IO.StringWriter(sb))
                jsonLogEventFormatter.Format(logEvent, writer);
            return sb.ToString();
        }

        private string ConvertPropertiesToXmlStructure(IEnumerable<KeyValuePair<string, LogEventPropertyValue>> properties)
        {
            var options = columnOptions.Properties;

            if (options.ExcludeAdditionalProperties)
                properties = properties.Where(p => !additionalColumnNames.Contains(p.Key));

            if (options.PropertiesFilter != null)
            {
                try
                {
                    properties = properties.Where(p => options.PropertiesFilter(p.Key));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Unable to filter properties to store in {0} due to following error: {1}", this, ex);
                }
            }

            var sb = new StringBuilder();

            sb.AppendFormat("<{0}>", options.RootElementName);

            foreach (var property in properties)
            {
                var value = XmlPropertyFormatter.Simplify(property.Value, options);
                if (options.OmitElementIfEmpty && string.IsNullOrEmpty(value))
                {
                    continue;
                }

                if (options.UsePropertyKeyAsElementName)
                {
                    sb.AppendFormat("<{0}>{1}</{0}>", XmlPropertyFormatter.GetValidElementName(property.Key), value);
                }
                else
                {
                    sb.AppendFormat("<{0} key='{1}'>{2}</{0}>", options.PropertyElementName, property.Key, value);
                }
            }

            sb.AppendFormat("</{0}>", options.RootElementName);

            return sb.ToString();
        }

        /// <summary>
        ///     Mapping values from properties which have a corresponding data row.
        ///     Matching is done based on Column name and property key
        ///     Standard columns are not mapped
        /// </summary>        
        /// <param name="properties"></param>
        public virtual IEnumerable<KeyValuePair<string, object>> ConvertPropertiesToColumn(IReadOnlyDictionary<string, LogEventPropertyValue> properties)
        {
            foreach (var property in properties)
            {
                if (!eventTable.Columns.Contains(property.Key) || standardColumnNames.Contains(property.Key))
                    continue;

                var columnName = property.Key;
                var columnType = eventTable.Columns[columnName].DataType;

                if (!(property.Value is ScalarValue scalarValue))
                {
                    yield return new KeyValuePair<string, object>(columnName, property.Value.ToString());
                    continue;
                }

                if (scalarValue.Value == null && eventTable.Columns[columnName].AllowDBNull)
                {
                    yield return new KeyValuePair<string, object>(columnName, DBNull.Value);
                    continue;
                }

                if (TryChangeType(scalarValue.Value, columnType, out var conversion))
                {
                    yield return new KeyValuePair<string, object>(columnName, conversion);
                }
                else
                {
                    yield return new KeyValuePair<string, object>(columnName, property.Value.ToString());
                }
            }
        }

        /// <summary>
        ///     Try to convert the object to the given type
        /// </summary>
        /// <param name="obj">object</param>
        /// <param name="type">type to convert to</param>
        /// <param name="conversion">result of the converted value</param>        
        private static bool TryChangeType(object obj, Type type, out object conversion)
        {
            conversion = null;
            try
            {
                conversion = Convert.ChangeType(obj, type);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private DataTable CreateDataTable()
        {
            var eventsTable = new DataTable(tableName);

            foreach (var standardColumn in columnOptions.Store)
            {
                var standardOpts = columnOptions.GetStandardColumnOptions(standardColumn);
                var dataColumn = standardOpts.AsDataColumn();
                eventsTable.Columns.Add(dataColumn);
                if (standardOpts == columnOptions.PrimaryKey)
                    eventsTable.PrimaryKey = new DataColumn[] { dataColumn };
            }

            if (columnOptions.AdditionalColumns != null)
            {
                foreach (var addCol in columnOptions.AdditionalColumns)
                {
                    var dataColumn = addCol.AsDataColumn();
                    eventsTable.Columns.Add(dataColumn);
                    if (addCol == columnOptions.PrimaryKey)
                        eventsTable.PrimaryKey = new DataColumn[] { dataColumn };
                }
            }

            return eventsTable;
        }

    }
}
