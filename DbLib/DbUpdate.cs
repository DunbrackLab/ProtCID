using System;
using System.Data;
using System.Data.Odbc;

namespace DbLib
{
	/// <summary>
	/// Update database columns
	/// </summary>
	public class DbUpdate
	{
        private OdbcCommand updateCmd = null;

		public DbUpdate()
		{
		}


        public DbUpdate (DbConnect dbConnect)
        {
            if (!dbConnect.IsConnected())
            {
                dbConnect.ConnectToDatabase();
            }
            updateCmd = dbConnect.CreateCommand();
        }
        /// <summary>
        /// get results 
        /// </summary>
        /// <param name="queryString"></param>
        /// <returns></returns>
        public void Update(DbConnect dbConnect, string queryString)
        {
            try
            {
                if (! dbConnect.IsConnected())
                {
                    dbConnect.ConnectToDatabase();
                }
                OdbcCommand updateCmd = dbConnect.CreateCommand();
                updateCmd.CommandText = queryString;
                updateCmd.ExecuteNonQuery();
                updateCmd.Dispose();

       //         updateCmd.CommandText = "commit";
      //          updateCmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new Exception("Query Errors: " + ex.Message + " (The query: " + queryString + " )");
            }
        }

        /// <summary>
        /// get results 
        /// </summary>
        /// <param name="queryString"></param>
        /// <returns></returns>
        public void Update(string queryString)
        {
            try
            {              
                updateCmd.CommandText = queryString;
                updateCmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new Exception("Query Errors: " + ex.Message + " (The query: " + queryString + " )");
            }
        }

        /// <summary>
        /// get results 
        /// </summary>
        /// <param name="queryString"></param>
        /// <returns></returns>
        public void Update(OdbcCommand updateCmd, string queryString)
        {
            try
            {
                updateCmd.CommandText = queryString;
                updateCmd.ExecuteNonQuery();

                //         updateCmd.CommandText = "commit";
                //          updateCmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new Exception("Query Errors: " + ex.Message + " (The query: " + queryString + " )");
            }
        }

        /// <summary>
        /// get results 
        /// </summary>
        /// <param name="queryString"></param>
        /// <returns></returns>
        public void Delete(DbConnect dbConnect, string queryString)
        {
            try
            {
                if (!dbConnect.IsConnected())
                {
                    dbConnect.ConnectToDatabase();
                }
                OdbcCommand updateCmd = dbConnect.CreateCommand();
                updateCmd.CommandText = queryString;
                updateCmd.ExecuteNonQuery();
                updateCmd.Dispose();
            }
            catch (Exception ex)
            {
                throw new Exception("Query Errors: " + ex.Message + " (The query: " + queryString + " )");
            }
        }
        /// <summary>
        /// get results 
        /// </summary>
        /// <param name="queryString"></param>
        /// <returns></returns>
        public void Delete(string queryString)
        {
            try
            {
                updateCmd.CommandText = queryString;
                updateCmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new Exception("Query Errors: " + ex.Message + " (The query: " + queryString + " )");
            }
        }

        /// <summary>
        /// get results 
        /// </summary>
        /// <param name="queryString"></param>
        /// <returns></returns>
        public void Delete(OdbcCommand updateCmd, string queryString)
        {
            try
            {
                updateCmd.CommandText = queryString;
                updateCmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new Exception("Query Errors: " + ex.Message + " (The query: " + queryString + " )");
            }
        }
	}
}
