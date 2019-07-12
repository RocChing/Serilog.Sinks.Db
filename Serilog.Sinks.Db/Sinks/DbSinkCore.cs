using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using Serilog.Events;

namespace Serilog.Sinks.Db
{
    public class DbSinkCore : IDbSinkCore, IDisposable
    {
        private DbOptions _opt;
        private ISet<string> _additionalColumnNames;
        private DataTable _eventTable;
        private ISet<string> _standardColumnNames;
        private JsonLogEventFormatter _jsonLogEventFormatter;
        private ITableCreator _tableCreator;

        public DbSinkCore(DbOptions opt, ITableCreator tableCreator)
        {
            _opt = opt;
            _tableCreator = tableCreator;

            Init();
        }

        public DbSinkCore(DbOptions opt) : this(opt, null)
        {

        }

        #region private method
        private void Init()
        {
            _standardColumnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var stdCol in _opt.ColumnOptions.Store)
            {
                var col = _opt.ColumnOptions.GetStandardColumnOptions(stdCol);
                _standardColumnNames.Add(col.ColumnName);
            }

            _additionalColumnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (_opt.ColumnOptions.AdditionalColumns != null)
                foreach (var col in _opt.ColumnOptions.AdditionalColumns)
                    _additionalColumnNames.Add(col.ColumnName);

            if (_opt.ColumnOptions.Store.Contains(StandardColumn.LogEvent))
                _jsonLogEventFormatter = new JsonLogEventFormatter(this);

            _eventTable = CreateDataTable();

            if (_tableCreator != null && _opt.AutoCreateSqlTable)
            {
                _tableCreator.CreateTable(_opt, _eventTable);
            }
        }

        private DataTable CreateDataTable()
        {
            var eventsTable = new DataTable(_opt.TableName);
            var columnOptions = _opt.ColumnOptions;

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

        private bool TryChangeType(object obj, Type type, out object conversion)
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

        private string LogEventToJson(LogEvent logEvent)
        {
            if (_opt.ColumnOptions.LogEvent.ExcludeAdditionalProperties)
            {
                var filteredProperties = logEvent.Properties.Where(p => !_additionalColumnNames.Contains(p.Key));
                logEvent = new LogEvent(logEvent.Timestamp, logEvent.Level, logEvent.Exception, logEvent.MessageTemplate, filteredProperties.Select(x => new LogEventProperty(x.Key, x.Value)));
            }

            var sb = new StringBuilder();
            using (var writer = new System.IO.StringWriter(sb))
                _jsonLogEventFormatter.Format(logEvent, writer);
            return sb.ToString();
        }

        private string ConvertPropertiesToXmlStructure(IEnumerable<KeyValuePair<string, LogEventPropertyValue>> properties)
        {
            var options = _opt.ColumnOptions.Properties;

            if (options.ExcludeAdditionalProperties)
                properties = properties.Where(p => !_additionalColumnNames.Contains(p.Key));

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
        #endregion

        public virtual DbOptions DbOpt { get { return _opt; } set { _opt = value; } }

        public virtual ColumnOptions ColumnOptions { get { return _opt.ColumnOptions; } }

        public virtual KeyValuePair<string, Dictionary<string, object>> GetInsertSqlString(LogEvent logEvent)
        {
            int type = (int)_opt.Type;
            StringBuilder sql = new StringBuilder($"INSERT INTO {Utils.LeftTokens[type]}{_opt.TableName}{Utils.RightTokens[type]} (");
            StringBuilder parameterList = new StringBuilder(") VALUES (");

            var parameters = new Dictionary<string, object>();

            int index = 0;
            foreach (var field in GetColumnsAndValues(logEvent))
            {
                if (index != 0)
                {
                    sql.Append(',');
                    parameterList.Append(',');
                }

                string pname = $"{Utils.ParamPrefixs[type]}p{index}";
                sql.Append(field.Key);
                parameterList.Append(pname);

                parameters.Add(pname, field.Value ?? DBNull.Value);

                index++;
            }

            parameterList.Append(')');
            sql.Append(parameterList.ToString());

            return new KeyValuePair<string, Dictionary<string, object>>(sql.ToString(), parameters);
        }

        public virtual KeyValuePair<string, object> GetStandardColumnNameAndValue(StandardColumn column, LogEvent logEvent)
        {
            var columnOptions = _opt.ColumnOptions;
            switch (column)
            {
                case StandardColumn.Message:
                    return new KeyValuePair<string, object>(columnOptions.Message.ColumnName, logEvent.RenderMessage(_opt.FormatProvider));
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

        public virtual IEnumerable<KeyValuePair<string, object>> GetColumnsAndValues(LogEvent logEvent)
        {
            var columnOptions = _opt.ColumnOptions;
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

        public virtual IEnumerable<KeyValuePair<string, object>> ConvertPropertiesToColumn(IReadOnlyDictionary<string, LogEventPropertyValue> properties)
        {
            foreach (var property in properties)
            {
                if (!_eventTable.Columns.Contains(property.Key) || _standardColumnNames.Contains(property.Key))
                    continue;

                var columnName = property.Key;
                var columnType = _eventTable.Columns[columnName].DataType;

                if (!(property.Value is ScalarValue scalarValue))
                {
                    yield return new KeyValuePair<string, object>(columnName, property.Value.ToString());
                    continue;
                }

                if (scalarValue.Value == null && _eventTable.Columns[columnName].AllowDBNull)
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

        public void Dispose()
        {
            _standardColumnNames.Clear();
            _additionalColumnNames.Clear();
            _eventTable.Dispose();
        }
    }
}
