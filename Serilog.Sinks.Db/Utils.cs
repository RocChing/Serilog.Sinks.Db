using System;
using System.Collections.Generic;
using System.Text;

namespace Serilog.Sinks.Db
{
    public static class Utils
    {
        public static string[] LeftTokens = new string[] { "[", "`", "\"", "[" };
        public static string[] RightTokens = new string[] { "]", "`", "\"", "]" };
        public static string[] ParamPrefixs = new string[] { "@", "?", ":", "@" };
    }
}
