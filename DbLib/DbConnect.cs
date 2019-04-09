using System;
using System.Data.Odbc;
using System.Data;

namespace DbLib
{
	/// <summary>
	/// Summary description for DbInterface.
	/// </summary>
	public class DbConnect
	{
		/// <summary>
		/// database connection variables
		/// </summary> 
		private string connectString = "";		
		public System.Data.Odbc.OdbcConnection dbConnection = null;	

		public DbConnect()
		{
            dbConnection = new OdbcConnection();
		}

        public DbConnect(string connectString)
        {
            dbConnection = new OdbcConnection(connectString);
            dbConnection.ConnectionString = connectString;
            this.connectString = connectString;
        }

		/// <summary>
		/// connection string property
		/// </summary>
		public string ConnectString
		{
			get
			{
				return connectString;
			}
			set
			{
				connectString = value;
                dbConnection.ConnectionString = connectString;
			}
		}
		
		/// <summary>
		/// connect to the database 
		/// </summary>
		public void ConnectToDatabase()
		{
			if (connectString == "")
			{
				throw new Exception ("Connection string cannot be empty.");
			}
			else
			{
				try
				{
					if (dbConnection == null)
					{
						dbConnection = new OdbcConnection (connectString);
						dbConnection.Open ();
						return;
					}
					if (dbConnection.State.ToString () == "Closed")
					{
						dbConnection.Open ();
					}
				}
				catch(Exception ex)
				{
					throw new Exception ("Database Connect Errors: " + ex.Message);
				}
			}
		}

		/// <summary>
		/// disconnect from database
		/// </summary>
		public void DisconnectFromDatabase()
		{
			try
			{
				if (dbConnection != null && dbConnection.State.ToString () == "Open")
				{
					dbConnection.Close ();
				}

			}
			catch (Exception ex)
			{
				throw new Exception ("Database Disconnect Errors: " + ex.Message);
			}
		}
         
		/// <summary>
		/// create command
		/// </summary>
		public OdbcCommand CreateCommand()
		{
			OdbcCommand sqlCommand = new OdbcCommand ();
			try
			{
				sqlCommand = dbConnection.CreateCommand ();
			}
			catch(Exception ex)
			{
				throw ex;
			}
			return sqlCommand;
		}
		/// <summary>
		/// is it connected to the database
		/// </summary>
		/// <returns></returns>
		public bool IsConnected ()
		{
			if (dbConnection == null)
			{
				return false;
			}
			if (dbConnection.State.ToString () == "Open")
			{
				return true;
			}
			return false;
		}
	}
}
