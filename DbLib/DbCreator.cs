using System;
using System.Data;
using System.IO;
using System.Data.Odbc;
using System.Collections.Generic;

namespace DbLib
{
	/// <summary>
	/// create database tables
	/// </summary>
	public class DbCreator
	{
		public DbCreator()
		{
			
		}
	
		#region create tables from schema file

		/// <summary>
		/// read table structure from the file
		/// </summary>
		/// <param name="dbSchemaFile"></param>
		private void ReadAndCreateTables(string dbSchemaFile, DbConnect dbConnect)
		{
			try
			{
				using (StreamReader fileReader = new StreamReader (dbSchemaFile))
				{
					string line = "";
					string sqlCreateStr = "";
					string tableName = "";
					bool isTable = true;
					
					// Read lines from the file until the end of 
					// the file is reached.
					while ((line = fileReader.ReadLine()) != null)
					{
						// read the comment lines
						if (line.IndexOf ("#") > -1 )
						{
							continue;
						}
						if (line == "")
						{
							continue;
						}
						if (line.Length > 5 && line.Substring(0, 6).ToUpper ()  == "CREATE")
						{	
							string [] splitStr = line.Split ();	
							if (splitStr[1].ToUpper ().Trim () == "INDEX")
							{
								isTable = false;
								tableName = splitStr[4];
							}
							else
							{
								isTable = true;
								tableName = splitStr[2];
							}								
						}
						sqlCreateStr += line;
						// end of a create table sql string
						if (line.IndexOf (");") > -1)
						{
							// create the table		
							if (isTable)
							{
								CreateTable(dbConnect, sqlCreateStr, tableName);
							}
							else
							{
                                CreateIndex(dbConnect, sqlCreateStr, tableName);
							}
							sqlCreateStr = "";
						}
					}
				}
			}
			catch(Exception ex)
			{
				throw new Exception (ex.Message);
			}
		}
		#endregion

        #region create tables from schema file by the input db connection
        /// <summary>
        /// create db tables from a text file
        /// </summary>
        /// <param name="dbSchemaFile"></param>
        /// <returns></returns>
        public void CreateTablesFromFile(DbConnect dbConnect, string dbSchemaFile)
        {
            // set up a connection to the database

            try
            {
                if (!dbConnect.IsConnected())
                {
                    dbConnect.ConnectToDatabase();
                }
                ReadAndCreateTables(dbConnect, dbSchemaFile);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }


        /// <summary>
        /// read table structure from the file
        /// </summary>
        /// <param name="dbSchemaFile"></param>
        private void ReadAndCreateTables(DbConnect dbConnect, string dbSchemaFile)
        {
            try
            {
                using (StreamReader fileReader = new StreamReader(dbSchemaFile))
                {
                    string line = "";
                    string sqlCreateStr = "";
                    string tableName = "";
                    bool isTable = true;

                    // Read lines from the file until the end of 
                    // the file is reached.
                    while ((line = fileReader.ReadLine()) != null)
                    {
                        // read the comment lines
                        if (line.IndexOf("#") > -1)
                        {
                            continue;
                        }
                        if (line == "")
                        {
                            continue;
                        }
                        if (line.Length > 5 && line.Substring(0, 6).ToUpper() == "CREATE")
                        {
                            string[] splitStr = line.Split();
                            if (splitStr[1].ToUpper().Trim() == "INDEX")
                            {
                                isTable = false;
                                tableName = splitStr[4];
                            }
                            else
                            {
                                isTable = true;
                                tableName = splitStr[2];
                            }
                        }
                        sqlCreateStr += line;
                        // end of a create table sql string
                        if (line.IndexOf(");") > -1)
                        {
                            // create the table		
                            if (isTable)
                            {
                                CreateTable(dbConnect, sqlCreateStr, tableName);
                            }
                            else
                            {
                                CreateIndex(dbConnect, sqlCreateStr, tableName);
                            }
                            sqlCreateStr = "";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
        #endregion

        #region helper functions
        /// <summary>
        /// after read in create string, 
        /// create the tabble in the database if the table is not exist
        /// </summary>
        /// <param name="tableCreateSqlString"></param>
        public void CreateTable(DbConnect dbConnect, string tableCreateSqlString, string tableName)
        {
            try
            {
                if (!dbConnect.IsConnected())
                {
                    dbConnect.ConnectToDatabase();
                }
                if (IsTableExist(dbConnect, tableName))
                {
                    DropTable(dbConnect, tableName);
                }
                OdbcCommand createCommand = new OdbcCommand(tableCreateSqlString, dbConnect.dbConnection);
                createCommand.ExecuteNonQuery();
                createCommand.Dispose();
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Create Table {0} Error: {1}.", tableName, ex.Message));
            }
        }

        /// <summary>
        /// after read in create string, 
        /// create the tabble in the database if the table is not exist
        /// </summary>
        /// <param name="tableCreateSqlString"></param>
        public void CreateIndex(DbConnect dbConnect, string indexCreateSqlString, string tableName)
        {
            try
            {
                if (!dbConnect.IsConnected())
                {
                    dbConnect.ConnectToDatabase();
                }
                if (IsTableExist(dbConnect, tableName))
                {
                    OdbcCommand createCommand = new OdbcCommand(indexCreateSqlString, dbConnect.dbConnection);
                    createCommand.ExecuteNonQuery();
                    createCommand.Dispose();
                }
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Create Index On {0} Error: {1}.", tableName, ex.Message));
            }
        }

        /// <summary>
        /// before create a table, check if it exists or not
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public bool IsTableExist(DbConnect dbConnect, string tableName)
        {
            if (!dbConnect.IsConnected())
            {
                dbConnect.ConnectToDatabase();
            }
            OdbcCommand showTablesCommand = dbConnect.CreateCommand();
            showTablesCommand.CommandText = @"SELECT RDB$RELATION_NAME FROM RDB$RELATIONS WHERE RDB$SYSTEM_FLAG = 0;";
            DataTable tableNamesTable = new DataTable("Table Names");
            System.Data.Odbc.OdbcDataAdapter adapter = new System.Data.Odbc.OdbcDataAdapter();
            try
            {
                adapter.SelectCommand = showTablesCommand;
                adapter.Fill(tableNamesTable);
            }
            catch 
            {
                showTablesCommand.CommandText = "Show tables";
                adapter.SelectCommand = showTablesCommand;
                adapter.Fill(tableNamesTable);
            }
            foreach (DataRow dRow in tableNamesTable.Rows)
            {
                if (dRow[0].ToString().ToLower().Trim() == tableName.ToLower())
                {
                    return true;
                }
            }
            showTablesCommand.Dispose();
            return false;
        }

        /// <summary>
        /// drop the table if it is already in the database
        /// </summary>
        /// <param name="tableName"></param>
        public void DropTable(DbConnect dbConnect, string tableName)
        {
            try
            {
                if (!dbConnect.IsConnected())
                {
                    dbConnect.ConnectToDatabase();
                }
                string tableDropString = string.Format("Drop Table {0};", tableName);
                OdbcCommand createTableCommand = dbConnect.CreateCommand();
                createTableCommand.CommandText = tableDropString;
                createTableCommand.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Drop Table {0} Errors: {1}.", tableName, ex.Message));
            }
        }
		#endregion

        #region Create tables from string by the input database connection
        /// <summary>
        /// create a table from sql string
        /// </summary>
        /// <param name="createTableString"></param>
        /// <param name="tableName"></param>
        public void CreateTableFromString(DbConnect dbConnect, string createTableString, string tableName)
        {
            if (!dbConnect.IsConnected())
            {
                dbConnect.ConnectToDatabase();
            }
            OdbcCommand createCommand = dbConnect.CreateCommand();
            bool isTableExist = IsTableExist(dbConnect, tableName);
            if (createTableString != "")
            {
                if (isTableExist)
                {
                    DropTable(dbConnect, tableName);
                }
                createCommand.CommandText = createTableString;
                createCommand.ExecuteNonQuery();
            }
            else if (!isTableExist && createTableString == "")
            {
                throw new Exception("Data cannot be inserted. Table not exist");
            }
        }

        /// <summary>
        /// create a table from sql string
        /// </summary>
        /// <param name="createTableString"></param>
        /// <param name="tableName"></param>
        public void CreateTableFromString(DbConnect dbConnect, string createTableString, string tableName, bool newTable)
        {
            if (!dbConnect.IsConnected())
            {
                dbConnect.IsConnected();
            }
            OdbcCommand createCommand = dbConnect.CreateCommand();
            bool isTableExist = IsTableExist(dbConnect, tableName);
            if (createTableString != "")
            {
                if (isTableExist)
                {
                    if (newTable)
                    {
                        DropTable(dbConnect, tableName);
                        createCommand.CommandText = createTableString;
                        createCommand.ExecuteNonQuery();
                    }
                }
                else
                {
                    createCommand.CommandText = createTableString;
                    createCommand.ExecuteNonQuery();
                }
            }
            else if (!isTableExist && createTableString == "")
            {
                throw new Exception("Data cannot be inserted. Table not exist");
            }
        }
        #endregion
    }
}
