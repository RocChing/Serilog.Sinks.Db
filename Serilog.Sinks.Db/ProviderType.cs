using System;
using System.Collections.Generic;
using System.Text;

namespace Serilog.Sinks.Db
{
    public enum ProviderType
    {
        SQLServer = 0,
        MySql = 1,
        Oracle = 2,
        SQLite = 3,
        Oledb = 4
    }
}
