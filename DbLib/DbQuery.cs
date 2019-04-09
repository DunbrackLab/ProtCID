using System;
using System.Data;
using System.Data.Odbc;
using System.IO;

namespace DbLib
{
	/// <summary>
	/// Summary description for DbQuery.
	/// </summary>
	public class DbQuery
	{
        private OdbcDataAdapter adapter = new OdbcDataAdapter();
        private OdbcCommand selectCmd = null;
 
		public DbQuery()
		{
        }

        public DbQuery(DbConnect dbConnect)
        {
            SetSelectCmd(dbConnect);
        }

        public void Dispose ()
        {
            adapter.Dispose();
            if (selectCmd != null)
            {
                selectCmd.Dispose();
            }
        }

        #region query database by the input db connection
        /// <summary>
        /// get results 
        /// </summary>
        /// <param name="queryString"></param>
        /// <returns></returns>
        public DataTable Query(DbConnect dbConnect, string queryString)
        {
            DataTable resultTable = new DataTable();
            try
            {
                if (!dbConnect.IsConnected())
                {
                    dbConnect.ConnectToDatabase();
                }
                OdbcCommand selectCmd = dbConnect.CreateCommand();
                selectCmd.CommandText = queryString;
                adapter.SelectCommand = selectCmd;
                adapter.Fill(resultTable);
        //        selectCmd.Dispose();
            }
            catch (Exception ex)
            {
                throw new Exception("Query Errors: " + ex.Message + " (The query: " + queryString + " )");
            }
            return resultTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dbConnect"></param>
        public void SetSelectCmd (DbConnect dbConnect)
        {
            if (!dbConnect.IsConnected())
            {
                dbConnect.ConnectToDatabase();
            }
            selectCmd = dbConnect.CreateCommand();
        }

        /// <summary>
        /// get results 
        /// </summary>
        /// <param name="queryString"></param>
        /// <returns></returns>
        public DataTable Query(string queryString)
        {
            DataTable resultTable = new DataTable();
            try
            {
                selectCmd.CommandText = queryString;
                adapter.SelectCommand = selectCmd;
                adapter.Fill(resultTable);
            }
            catch (Exception ex)
            {
                throw new Exception("Query Errors: " + ex.Message + " (The query: " + queryString + " )");
            }
            return resultTable;
        }
        /// <summary>
        /// get results 
        /// </summary>
        /// <param name="queryString"></param>
        /// <returns></returns>
        public DataTable Query(OdbcCommand selectCmd, string queryString)
        {
            DataTable resultTable = new DataTable();
            try
            {              
                selectCmd.CommandText = queryString;               
                adapter.SelectCommand = selectCmd;
                adapter.Fill(resultTable);
            }
            catch (Exception ex)
            {
                throw new Exception("Query Errors: " + ex.Message + " (The query: " + queryString + " )");
            }
            return resultTable;
        }
        #endregion
    }
}
