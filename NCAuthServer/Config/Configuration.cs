using NCAuthServer.Model.Structs;
using NCommons.Utilities;
using Nini.Config;
using System;

namespace NCAuthServer.Config
{
    public class Configuration
    {
        private static Configuration Instance;

        private static IConfig _DBCfg = new IniConfigSource("Config/database.ini").Configs["database"];
        private static IConfig _NWCfg = new IniConfigSource("Config/network.ini").Configs["network"];

        public static DatabaseStruct Database;
        public static NetworkStruct Network;

        public Configuration()
        {
            Console.WriteLine("Load All Configuration...");
            Console.WriteLine("-------------------------------------------");

            Database = new DatabaseStruct(
                _DBCfg.GetString("db.mongo.url"),
                _DBCfg.GetString("db.mongo.name")
            );
            Log.Info("Loaded Database Configuration");

            Network = new NetworkStruct(
                _NWCfg.GetString("public.ip"),
                (ushort)_NWCfg.GetInt("public.port")
            );
            Log.Info("Loaded Network Configuration");
        }

        public static Configuration GetInstance()
        {
            return (Instance != null) ? Instance : Instance = Instance = new Configuration();
        }
    }
}
