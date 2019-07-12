using System.Data;

namespace Serilog.Sinks.Db
{
    public interface ITableCreator
    {
        void CreateTable(DbOptions opt, DataTable columnList);
    }
}
