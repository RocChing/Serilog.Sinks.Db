﻿using System.Configuration;

// Disable XML comment warnings for internal config classes which are required to have public members
#pragma warning disable 1591

namespace Serilog.Sinks.Db
{
    public class StandardColumnConfig : ConfigurationElement
    {
        public StandardColumnConfig()
        { }

        [ConfigurationProperty("Name", IsRequired = true, IsKey = true)]
        public string Name
        {
            get { return (string)this["Name"]; }
            set { this["Name"] = value; }
        }
    }
}

#pragma warning restore 1591
