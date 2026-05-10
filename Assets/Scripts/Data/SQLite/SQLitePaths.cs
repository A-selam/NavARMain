using System.IO;
using UnityEngine;

namespace NavAR.Data.SQLite
{
    public static class SQLitePaths
    {
        private const string DbFileName = "navar.db";

        public static string GetDatabasePath()
        {
            return Path.Combine(Application.persistentDataPath, DbFileName);
        }
    }
}
