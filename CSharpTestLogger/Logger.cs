using System;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;

namespace CSharpTestLogger
{
    public class Logger
    {
        private static string dllFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        private static string configFile = dllFolder + Path.DirectorySeparatorChar + "loggerConfig.xml";

        public readonly bool fileStorage_Enabled;
        private string fileStorage_Path;
        public readonly bool dbStorage_Enabled;
        private string dbStorage_Server;
        private string dbStorage_Database;

        public class ELoggerException : Exception
        {
            public ELoggerException(string message, Exception innerException) : base(message, innerException) { }
        };

        public static string GetCurrentDirectory()
        {
            return Environment.CurrentDirectory;
        }

        public Logger()
        {
            XmlDocument config = new XmlDocument();
            try
            {
                config.Load(configFile);

                XmlNode fileNode = config.SelectSingleNode("Config").SelectSingleNode("File");
                fileStorage_Enabled = fileNode.Attributes.GetNamedItem("Enabled").Value.Equals("1");
                fileStorage_Path = fileNode.Attributes.GetNamedItem("Path").Value;

                XmlNode dbNode = config.SelectSingleNode("Config").SelectSingleNode("Database");
                dbStorage_Enabled = dbNode.Attributes.GetNamedItem("Enabled").Value.Equals("1");
                dbStorage_Server = dbNode.Attributes.GetNamedItem("Server").Value;
                dbStorage_Database = dbNode.Attributes.GetNamedItem("Database").Value;
            }
            catch (IOException source)
            {
                throw new ELoggerException("Cannot load logger config", source);
            }

            string fileStorage_Folder = Path.GetDirectoryName(fileStorage_Path);
            switch (fileStorage_Folder)
            {
                case "":
                    fileStorage_Folder = dllFolder;
                    break;
                case "*MyDocuments":
                    fileStorage_Folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    break;
            }
            fileStorage_Path = fileStorage_Folder + Path.DirectorySeparatorChar + Path.GetFileName(fileStorage_Path);
        }

        public void WriteLine(string text)
        {
            try
            {
                if (fileStorage_Enabled)
                    WriteLogLineToFile(text);

                if (dbStorage_Enabled)
                    WriteLogLineToDB(text);
            }
            catch (Exception source)
            {
                throw new ELoggerException("Cannot write to the file storage " + fileStorage_Path, source);
            }
        }

        private void WriteLogLineToFile(string logLine)
        {
            File.AppendAllLines(fileStorage_Path, new string[] { logLine });
        }

        private void WriteLogLineToDB(string logLine)
        {
            try
            {
                DoSql((cmd) =>
                {
                    cmd.CommandText = SqlScripts.writeLine;
                    cmd.Parameters.AddWithValue("text", logLine);
                    cmd.ExecuteNonQuery();
                });
            }
            catch (Exception source)
            {
                throw new ELoggerException(
                    "Cannot connect to database.\nServer=" + dbStorage_Server + "\nDatabase=" + dbStorage_Database,
                    source
                );
            }
        }

        public string GetLastLogLineFromFile()
        {
            try
            {
                return File.ReadLines(fileStorage_Path).Last();
            }
            catch
            {
                return null;
            }

        }

        public string GetLastLogLineFromDB()
        {
            object result = "";
            DoSql((cmd) =>
            {
                cmd.CommandText = SqlScripts.readLastLine;
                result = cmd.ExecuteScalar();
            });
            if (result == null)
                return null;
            else
                return result.ToString();
        }

        private delegate void DoSqlDelegate(SqlCommand cmd);
        private void DoSql(DoSqlDelegate code, string connectionString = null)
        {
            if (connectionString == null)
                connectionString = string.Format("Server={0};Database={1};", dbStorage_Server, dbStorage_Database);

            SqlConnection conn = new SqlConnection(connectionString);
            SqlCommand cmd = conn.CreateCommand();
            conn.Open();
            try
            {
                code(cmd);
            }
            finally
            {
                conn.Close();
            }
        }

        public static void RecreateConfigAndDB()
        {
            RecreateConfig();
            new Logger().RecreateDB();
        }

        private static void RecreateConfig()
        {
            XmlDocument config = new XmlDocument();
            Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("CSharpTestLogger.DefaultConfig.xml");
            config.Load(s);
            config.Save(configFile);
        }

        private void RecreateDB()
        {
            string connectionString = string.Format("Server={0}", dbStorage_Server);
            DoSql((cmd) =>
            {
                cmd.CommandText = string.Format(SqlScripts.recreateDB, dbStorage_Database);
                cmd.ExecuteNonQuery();
            },
            connectionString);

            DoSql((cmd) =>
            {
                cmd.CommandText = SqlScripts.createLogsTable;
                cmd.ExecuteNonQuery();
            });
        }

        private class SqlScripts
        {
            public static string recreateDB =
@"IF DB_ID(N'{0}') IS NOT NULL
    DROP DATABASE {0};

CREATE DATABASE {0};";

            public static string createLogsTable =
@"CREATE TABLE logs (
    id INT IDENTITY,
    text NVARCHAR(1024)
);";

            public static string writeLine =
@"INSERT INTO logs (text)
VALUES (@text);";

            public static string readLastLine =
@"SELECT TOP 1 text
FROM logs
ORDER BY id DESC;";

        }
    }
}
