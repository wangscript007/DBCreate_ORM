﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 这个文件属于dbormCompiler项目的资源文件，
/// 在扩展修改后，需要重新把此文件拖放到dbormCompiler -> Properties -> Resources.resx 的资源文件中
/// 替换旧的同名文件，同时重新编译dbormCompiler项目生成新的dbormCompiler.dll
/// </summary>
/// 
namespace DataBaseOp
{
    public class SqlHelper
    {
        public SqlHelper()
        {
        }

        /// <summary>
        /// 执行完后会销毁和数据库的连接
        /// </summary>
        /// <param name="connectionStr"></param>
        /// <param name="sql"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public DataTable ExecuteDataTable(string connectionStr, string sql, object[] parameters)
        {
            SqlConnection con = new SqlConnection();
            con.ConnectionString = connectionStr;
            ConnOpen(con);

            SqlCommand com = new SqlCommand();
            com.Connection = con;
            com.CommandType = CommandType.Text;

            sql = CreateSqlFromParams(sql, parameters);
            com.CommandText = sql;
            SqlDataReader dr = com.ExecuteReader();
            DataTable dt = new DataTable();
            dt.Load(dr);

            com.Dispose();

            dr.Close();
            con.Dispose();
            return dt;
        }

        /// <summary>
        /// 执行完后会销毁和数据库的连接
        /// </summary>
        /// <param name="connectionStr"></param>
        /// <param name="sql"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public int ExecuteNoneQuery(string connectionStr, string sql, object[] parameters)
        {
            SqlConnection con = new SqlConnection();
            con.ConnectionString = connectionStr;
            ConnOpen(con);

            int ret = 0;
            SqlCommand com = new SqlCommand();
            com.Connection = con;
            com.CommandType = CommandType.Text;
            com.CommandText = CreateSqlFromParams(sql, parameters);
            ret = com.ExecuteNonQuery();
            com.Dispose();      
            con.Dispose();
            return ret;
        }

        /// <summary>
        ///  执行完后会把数据库的连接放入连接池
        /// </summary>
        /// <param name="con"></param>
        /// <param name="trans"></param>
        /// <param name="sql"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public DataTable ExecuteDataTable(SqlConnection con, SqlTransaction trans, string sql, object[] parameters)
        {
            if (con.State != ConnectionState.Open)
                ConnOpen(con);

            SqlCommand com = new SqlCommand();
            com.Connection = con;
            com.Transaction = trans;
            com.CommandType = CommandType.Text;
            com.CommandText = CreateSqlFromParams(sql, parameters);
            SqlDataReader dr = com.ExecuteReader();
            DataTable dt = new DataTable();
            dt.Load(dr);

            dr.Close();
            com.Dispose();

            if (trans == null)
                con.Close();

            return dt;
        }

        /// <summary>
        ///  执行完后会把数据库的连接放入连接池
        /// </summary>
        /// <param name="con"></param>
        /// <param name="trans"></param>
        /// <param name="sql"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public int ExecuteNoneQuery(SqlConnection con, SqlTransaction trans, string sql, object[] parameters)
        {
            if (con.State != ConnectionState.Open)
                ConnOpen(con);

            int ret = 0;
            SqlCommand com = new SqlCommand();
            com.Connection = con;
            com.Transaction = trans;
            com.CommandType = CommandType.Text;
            com.CommandText = CreateSqlFromParams(sql, parameters);
            ret = com.ExecuteNonQuery();
            com.Dispose();

            if(trans == null)
                con.Close();

            return ret;
        }

        string CreateSqlFromParams(string sql, object[] parameters)
        {
            StringBuilder newSql = new StringBuilder();
            char[] sqlStream = sql.ToArray();

            int paramStrStartIdx, paramStrEndIdx;
            int paramIdx = 0;

            for (int i = 0; i < sqlStream.Count();)
            {
                if (sqlStream[i] == '@')
                {
                    paramStrStartIdx = i;
                    paramStrEndIdx = GetParamStrEndIndex(sqlStream, paramStrStartIdx);

                    if (parameters[paramIdx].GetType() == typeof(string) ||
                        parameters[paramIdx].GetType() == typeof(DateTime))
                    {
                        newSql.Append("'" + parameters[paramIdx].ToString() + "'");
                    }
                    else
                    {
                        newSql.Append(parameters[paramIdx].ToString());
                    }

                    i = paramStrEndIdx + 1;
                    paramIdx++;

                    continue;
                }
                else
                {
                    newSql.Append(sqlStream[i]);
                    i++;
                }
            }

            return newSql.ToString();
        }


        int GetParamStrEndIndex(char[] sqlStream, int paramStrStartIdx)
        {
            char[] separators = new char[] { ',', ' ', ')', '(', '*', '@' };

            for (int i = paramStrStartIdx + 1; i < sqlStream.Count(); i++)
            {
                if (separators.Contains(sqlStream[i]))
                {
                    return i - 1;
                }
            }

            return sqlStream.Count() - 1;
        }

        bool ConnOpen(SqlConnection con)
        {
            int flag = 0;
            while (flag < 2)
            {
                try
                {
                    con.Open();
                    return true;
                }
                catch
                {
                    Thread.Sleep(2000);
                    flag++;
                }
            }
            return false;
        }
    }
}

