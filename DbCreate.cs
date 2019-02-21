﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbInfoCreate
{
    public class DbToCSharpInfo
    {
        public string type;
        public string defaultValue;

        public DbToCSharpInfo(string _type, string _defaultValue)
        {
            type = _type;
            defaultValue = _defaultValue;
        }
    }


    public class DbCreate
    {
        static Dictionary<string, DbToCSharpInfo> cSharpTypeDict = new Dictionary<string, DbToCSharpInfo>();
        SqlConnection con;

        static string temp = System.Environment.GetEnvironmentVariable("TEMP");
        static string programDirPath = temp + @"\programDB\";
        static string dataBaseOpDirPath = temp + @"\programDB\DataBaseOp\";

        string saveTableModelDirPath = dataBaseOpDirPath + @"\TableModel\";
        string saveScDBDirPath = dataBaseOpDirPath;

        bool isRebulidTableModel = false;
        public void DbCreateInfos(string[] dbNames, string[] conStrs, bool isRebulidTableModel = false)
        {
            this.isRebulidTableModel = isRebulidTableModel;

            if (!Directory.Exists(programDirPath))
                Directory.CreateDirectory(programDirPath);
            File.SetAttributes(programDirPath, FileAttributes.Hidden);

            if (!Directory.Exists(dataBaseOpDirPath))
                Directory.CreateDirectory(dataBaseOpDirPath);
            File.SetAttributes(dataBaseOpDirPath, FileAttributes.Hidden);

            CreateCSharpTypeDict();

            for (int i=0; i < conStrs.Count(); i++)
            {
                DbCreateInfo(dbNames[i], conStrs[i]);
            }

            string s = CreateDataBaseStringStream(dbNames, conStrs);
            WriteStreamToFile(s, saveScDBDirPath + "ScDB.cs");
        }

        public void DbCreateInfo(string dbName, string conStr)
        {
            con = new SqlConnection();
            con.ConnectionString = conStr;
            con.Open();

            List<string> tableNames = new List<string>();
            string[] utableNames = QueryAllTableName("U");
            string[] vtableNames = QueryAllTableName("V");
      
            tableNames.AddRange(utableNames);
            tableNames.AddRange(vtableNames);

            if (isRebulidTableModel)
            {
                foreach (string tableName in utableNames)
                    CreateDataTableOpFile(dbName, tableName);

                foreach (string tableName in vtableNames)
                    CreateDataTableOpFile(dbName, tableName);
            }

 
            string s = CreateDataBaseOpStringStream(dbName, tableNames.ToArray());
            WriteStreamToFile(s, saveScDBDirPath + "ScDB" + dbName + ".cs");

            con.Close();
            con.Dispose();
        }

        string[] QueryAllTableName(string tableType)
        {
            SqlCommand com = new SqlCommand();
            com.Connection = con;
            com.CommandType = CommandType.Text;
            com.CommandText = "SELECT Name FROM SysObjects Where XType='" + tableType + "' ORDER BY Name";
            SqlDataReader dr = com.ExecuteReader();

            List<string> tableNameList = new List<string>();

            while (dr.Read())
            {
                tableNameList.Add(dr[0].ToString().ToUpper());
            }

            com.Dispose();
            dr.Close();

            return tableNameList.ToArray();
        }

        void CreateDataTableOpFile(string dbName, string tableName)
        {
           
            SqlCommand com = new SqlCommand();
            com.Connection = con;
            com.CommandType = CommandType.Text;
            com.CommandText = "SELECT name AS column_name, TYPE_NAME(system_type_id)AS column_type,max_length, is_nullable FROM sys.columns WHERE object_id = OBJECT_ID(N'" + tableName + "')";
            SqlDataReader dr = com.ExecuteReader();
            string modelStreamStr = CreateModelDataStringStream(dbName, tableName, dr);

            string file = "DB_" +dbName + "_DT_" + tableName + ".cs";

            string tbFullPath = saveTableModelDirPath + @"\" + dbName + @"\";

            if (!Directory.Exists(tbFullPath))
                Directory.CreateDirectory(tbFullPath);

            WriteStreamToFile(modelStreamStr, tbFullPath + file);

            com.Dispose();
            dr.Close();
        }


        string CreateSqlFromParams(string sql, List<object> paramList)
        {
            int atIdx;
            int whiteSpaceIdx = 0;
            string atVarStr;
            char[] anyOf = new char[] { ',', ' ', ')', '(', '*' };

            for (int i = 0; i < paramList.Count; i++)
            {
                atIdx = sql.IndexOf('@');

                if (atIdx != -1)
                {
                    whiteSpaceIdx = sql.IndexOfAny(anyOf, atIdx);

                    if (whiteSpaceIdx == -1)
                        whiteSpaceIdx = sql.Length;

                    atVarStr = sql.Substring(atIdx, whiteSpaceIdx - atIdx);

                    if (paramList[i].GetType() == typeof(string))
                        sql = sql.Replace(atVarStr, "'" + paramList[i].ToString() + "'");
                    else
                        sql = sql.Replace(atVarStr, paramList[i].ToString());

                    continue;
                }
                break;
            }

            return sql;
        }

        string CreateModelDataStringStream(string dbName, string tableName, SqlDataReader modelFieldDr)
        {
            string className = "DB_" + dbName + "_DT_" + tableName;
            string usingHeaderStr = "using System; \r\n\r\n";

            string modelStreamStr = usingHeaderStr + "public class " + className + "\r\n" + "{" + "\r\n";

            string fieldName;
            string fieldType;
            string fieldDefaultValue;
            string fieldDataStr;
            DbToCSharpInfo info;

            while (modelFieldDr.Read())
            {
                fieldName = modelFieldDr[0].ToString();

                info = GetCSharpType(modelFieldDr[1].ToString());
                fieldType = info.type;
                fieldDefaultValue = info.defaultValue;
                fieldDataStr = "public " + fieldType + "  " + fieldName + " = " + fieldDefaultValue + ";";

                modelStreamStr += "    " + fieldDataStr + "\r\n";
            }

            modelStreamStr += "} \r\n";

            return modelStreamStr;
        }



        string CreateDataBaseOpStringStream(string dbName, string[] tableNames)
        {
            string usingStr = "using System.Data.SqlClient; \r\n\r\n";
            string nameSpace = "namespace " + "DataBaseOp" + "\r\n";
            string className = "ScDB" + dbName;

            string classStr =
                usingStr +
                nameSpace +
                "{ \r\n" +
                "    public class " + className + " : ScDataBaseTableOpBase" + "\r\n" +
                "    { \r\n" ;


            string dbTableName;
            string dbTableOp;

            foreach (string tbName in tableNames)
            {
                dbTableName = "DB_" + dbName + "_DT_" + tbName;
                dbTableOp = "DTOP_" + tbName;

                classStr += "        public DataBaseAccess<" + dbTableName + "> " + dbTableOp + "; \r\n";
            }

            classStr +=
                "\r\n        public " + className + "(string dbName, string conStr) \r\n" +
                 "        :base(dbName, conStr) \r\n" +
                 "        {\r\n" +
                 "        }\r\n\r\n";


            classStr +=
               "\r\n        public void CreateDataTableOp() \r\n" +
                "        {\r\n";

            foreach (string tbName in tableNames)
            {
                dbTableName = "DB_" + dbName + "_DT_" + tbName;
                dbTableOp = "DTOP_" + tbName;

                classStr +=
                   "            " + dbTableOp + " = new DataBaseAccess<" + dbTableName + ">(); \r\n" +
                   "            " + dbTableOp + ".ResetTableInfo(\"" + tbName + "\", typeof(" + dbTableName + ")); \r\n" +
                   "            " + dbTableOp + ".con = con; \r\n\r\n";
            }

            classStr += "        }\r\n\r\n";


            classStr += "     }\r\n}\r\n";

            return classStr;
        }



        string CreateDataBaseStringStream(string[] dbNames, string[] conStrs)
        {
            string usingStr = "using System.Collections.Generic; \r\n using System.Data.SqlClient; \r\n\r\n";
            string nameSpace = "namespace " + "DataBaseOp" + "\r\n";
            string className = "ScDB";

            string classStr =
                usingStr +
                nameSpace +
                "{ \r\n" +
                "    public class " + className + "\r\n" +
                "    { \r\n" +
                "         static public Dictionary<string, ScDataBaseTableOpBase> TableOpDict = new Dictionary<string, ScDataBaseTableOpBase>();\r\n\r\n";


            string dbNewName;
            string dbClassName;

            foreach (string dbName in dbNames)
            {
                dbNewName = "DB_" + dbName;
                dbClassName = "ScDB" + dbName;
                classStr += "        static public " + dbClassName + " " + dbNewName + "; \r\n";
            }
            classStr += "\r\n";
            classStr +=
                 "        static public void Init(string[] dbNamesMatch, string[] conStrsMatch) \r\n" +
                 "        {\r\n";

            for(int i=0; i<dbNames.Count(); i++)
            {
                dbNewName = "DB_" + dbNames[i];
                dbClassName = "ScDB" + dbNames[i];

                classStr +=
                    "            " + dbNewName + " = new " + dbClassName + "(\"" + dbNames[i] + "\"" + "," + "\"" + conStrs[i] + "\"); \r\n" +
                    "            TableOpDict.Add(" + dbNewName + ".dbName ," + dbNewName + "); \r\n" +
                    "            " + dbNewName + ".ResetMatchConnection(dbNamesMatch, conStrsMatch); \r\n" +
                    "            " + dbNewName + ".CreateDataTableOp(); \r\n\r\n";

            }
            classStr += "        }\r\n\r\n";


            classStr +=
               "        static public void Dispose() \r\n" +
               "        {\r\n";

            for (int i = 0; i < dbNames.Count(); i++)
            {
                dbNewName = "DB_" + dbNames[i];
                classStr += "            " + dbNewName + ".Dispose(); \r\n";
            }
            classStr += "        }\r\n\r\n";


            classStr += "    }\r\n }\r\n";

            return classStr;
        }

      
        static public void CreateCSharpTypeDict()
        {
            DbToCSharpInfo info = new DbToCSharpInfo("int", "int.MinValue");
            cSharpTypeDict.Add("int", info);

            info = new DbToCSharpInfo("int", "int.MinValue");
            cSharpTypeDict.Add("smallint", info);

            info = new DbToCSharpInfo("int", "int.MinValue");
            cSharpTypeDict.Add("tinyint", info);

            info = new DbToCSharpInfo("string", "string.Empty");
            cSharpTypeDict.Add("varchar", info);

            info = new DbToCSharpInfo("string", "string.Empty");
            cSharpTypeDict.Add("nvarchar", info);

            info = new DbToCSharpInfo("decimal", "decimal.MinValue");
            cSharpTypeDict.Add("decimal", info);

            info = new DbToCSharpInfo("DateTime", "DateTime.MinValue");
            cSharpTypeDict.Add("datetime", info);

            info = new DbToCSharpInfo("string", "string.Empty");
            cSharpTypeDict.Add("nchar", info);

            info = new DbToCSharpInfo("string", "string.Empty");
            cSharpTypeDict.Add("char", info);
 
            info = new DbToCSharpInfo("string", "string.Empty");
            cSharpTypeDict.Add("varbinary", info);

            info = new DbToCSharpInfo("string", "string.Empty");
            cSharpTypeDict.Add("sysname", info);

        }


        public DbToCSharpInfo GetCSharpType(string dbTypeStr)
        {
            if (cSharpTypeDict.ContainsKey(dbTypeStr))
                return cSharpTypeDict[dbTypeStr];

            return null;
        }



        public void WriteStreamToFile(string stream, string filePath)
        {
            FileStream fs = new FileStream(filePath, FileMode.Create);
            StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.UTF8);
            sw.WriteLine(stream);
            sw.Close();
        }

        string GenerateCode(string codefile)
        {
            StreamReader sr = new StreamReader(codefile, Encoding.Default);
            string line;
            string code = "";
            while ((line = sr.ReadLine()) != null)
            {
                code += line.ToString() + "\r\n";
            }

            return code;
        }

    }
}
