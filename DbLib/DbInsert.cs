using System;
using System.Data;
using System.Data.Odbc;
#if DEBUG
using System.IO;
#endif

namespace DbLib
{
	/// <summary>
	/// Summary description for DbInsert.
	/// </summary>
	public class DbInsert
	{
#if DEBUG
        public StreamWriter logWriter = null;
#endif
        // for firebird
        private const int queryLength = 65536;
        private const int numOfRowsAllowed = 255;
        OdbcCommand insertCommand = null;


		public DbInsert()
		{

        }

        public DbInsert (DbConnect dbConnect)
        {
            if (!dbConnect.IsConnected())
            {
                dbConnect.ConnectToDatabase();
            }
            insertCommand = dbConnect.CreateCommand();
        }

        public void Dispose ()
        {
            if (insertCommand != null)
            {
                insertCommand.Dispose();
            }
        }
        #region insert data into database by the input db connection
        /// <summary>
        /// insert data into corresponding database tables
        /// </summary>
        public void InsertDataIntoDBtables(DbConnect dbConnect, DataTable[] dataTables)
        {
#if DEBUG
            logWriter = new StreamWriter("dbInsertErrorLog.txt", true);
#endif
            try
            {
                if (!dbConnect.IsConnected())
                {
                    dbConnect.ConnectToDatabase();
                }
                System.Data.Odbc.OdbcCommand insertCommand = dbConnect.CreateCommand();

                foreach (DataTable dataTable in dataTables)
                {
                    InsertionSqlString insertSqlStr = new InsertionSqlString(dataTable.TableName);

                    foreach (DataRow dRow in dataTable.Rows)
                    {
                        try
                        {
                            for (int colI = 0; colI < dataTable.Columns.Count; colI++)
                            {
                                string colName = dataTable.Columns[colI].ColumnName;
                                insertSqlStr.AddKeyValuePair(colName, dRow[colName]);
                            }

                            insertCommand.CommandText = insertSqlStr.ToString();
                            insertCommand.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            //	throw new Exception (string.Format ("{0} Insertion Errors: {1}", dataTable.TableName, ex.Message));
#if DEBUG
                            logWriter.WriteLine(string.Format("{0} Insertion Errors: {1}", dataTable.TableName, ex.Message));
                            logWriter.WriteLine(insertCommand.CommandText);
#endif
                        }
                        finally
                        {
                            insertSqlStr.ClearSqlInsertString();
                        }
                    }
                    // commit the insertion
                    insertCommand.CommandText = "Commit";
                    insertCommand.ExecuteNonQuery();
                }
                insertCommand.Dispose();

            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
#if DEBUG
                logWriter.Close();
#endif
            }
        }


        /// <summary>
        /// insert data into corresponding database tables
        /// </summary>
        public void InsertDataIntoDBtables(DbConnect dbConnect, DataTable dataTable)
        {
            if (dataTable == null || dataTable.Rows.Count == 0)
            {
                return;
            }
#if DEBUG
            logWriter = new StreamWriter("dbInsertErrorLog.txt", true);
#endif
            try
            {
                if (! dbConnect.IsConnected())
                {
                     dbConnect.ConnectToDatabase();
                }
                System.Data.Odbc.OdbcCommand insertCommand =  dbConnect.CreateCommand();

                InsertionSqlString insertSqlStr = new InsertionSqlString(dataTable.TableName);

                foreach (DataRow dRow in dataTable.Rows)
                {
                    try
                    {
                        for (int colI = 0; colI < dataTable.Columns.Count; colI++)
                        {
                            string colName = dataTable.Columns[colI].ColumnName;
                            insertSqlStr.AddKeyValuePair(colName, dRow[colName]);
                        }

                        insertCommand.CommandText = insertSqlStr.ToString();
                        insertCommand.ExecuteNonQuery();

                    }
                    catch (Exception ex)
                    {
                        //	throw new Exception (string.Format ("{0} Insertion Errors: {1}", dataTable.TableName, ex.Message));
#if DEBUG
                        logWriter.WriteLine(string.Format("{0} Insertion Errors: {1}", dataTable.TableName, ex.Message));
                        logWriter.WriteLine(insertCommand.CommandText);
#endif
                    }
                    finally
                    {
                        insertSqlStr.ClearSqlInsertString();
                    }
                }
                // commit the insertion
                insertCommand.CommandText = "Commit";
                insertCommand.ExecuteNonQuery();

                insertCommand.Dispose();
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
#if DEBUG
                logWriter.Close();
#endif
                
            }
        }
        

        /// <summary>
        /// insert data into corresponding database tables
        /// </summary>
        public void BatchInsertDataIntoDBtables(DbConnect dbConnect, DataTable[] dataTables)
        {
            try
            {
                if (!dbConnect.IsConnected())
                {
                    dbConnect.ConnectToDatabase();
                }
                foreach (DataTable dataTable in dataTables)
                {
                    BatchInsertDataIntoDBtables(dbConnect, dataTable);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }

        }


        /// <summary>
        /// insert data into corresponding database tables
        /// </summary>
        public void BatchInsertDataIntoDBtables(DbConnect dbConnect, DataTable dataTable)
        {
            if (dataTable == null || dataTable.Rows.Count == 0)
            {
                return;
            }

#if DEBUG
            logWriter = new StreamWriter("dbInsertErrorLog.txt", true);
#endif
            string executeBlock = "";
            int rowCount = 1;
            try
            {
                if (!dbConnect.IsConnected())
                {
                    dbConnect.ConnectToDatabase();
                }
                System.Data.Odbc.OdbcCommand insertCommand = dbConnect.CreateCommand();

                InsertionSqlString insertSqlStr = new InsertionSqlString(dataTable.TableName);

                executeBlock += "EXECUTE BLOCK AS BEGIN ";

                foreach (DataRow dRow in dataTable.Rows)
                {
                    try
                    {
                        for (int colI = 0; colI < dataTable.Columns.Count; colI++)
                        {
                            string colName = dataTable.Columns[colI].ColumnName;
                            insertSqlStr.AddKeyValuePair(colName, dRow[colName]);
                        }

                        if (rowCount > numOfRowsAllowed ||
                            executeBlock.Length + insertSqlStr.ToString().Length > queryLength)
                        {
                            executeBlock += "END";

                            insertCommand.CommandText = executeBlock;
                            insertCommand.ExecuteNonQuery();

                            // commit the insertion
               //             insertCommand.CommandText = "Commit";
               //             insertCommand.ExecuteNonQuery();

                            executeBlock = "EXECUTE BLOCK AS BEGIN ";
                            rowCount = 1;
                        }
                        executeBlock += (insertSqlStr.ToString() + " ");

                        rowCount++;
                    }
                    catch (Exception ex)
                    {
                        //	throw new Exception (string.Format ("{0} Insertion Errors: {1}", dataTable.TableName, ex.Message));
#if DEBUG
                        logWriter.WriteLine(string.Format("{0} Insertion Errors: {1}", dataTable.TableName, ex.Message));
                        logWriter.WriteLine(insertCommand.CommandText);
#endif
                        executeBlock = "EXECUTE BLOCK AS BEGIN ";
                        rowCount = 1;
                    }
                    finally
                    {
                        insertSqlStr.ClearSqlInsertString();                        
              //          executeBlock += "END";
                    }
                }
                executeBlock += "END";

                insertCommand.CommandText = executeBlock;
                insertCommand.ExecuteNonQuery();

                // commit the insertion
                insertCommand.CommandText = "Commit";
                insertCommand.ExecuteNonQuery();

                insertCommand.Dispose();
            }
            catch (Exception ex)
            {
                Exception thisException = new Exception(executeBlock + " eror: " + ex.Message);
                throw thisException;
            }
            finally
            {
#if DEBUG
                logWriter.Close ();
#endif
            }
        }

        /// <summary>
        /// insert data into corresponding database tables
        /// </summary>
        public void BatchInsertDataIntoDBtables( DataTable dataTable)
        {
            if (dataTable == null || dataTable.Rows.Count == 0)
            {
                return;
            }

#if DEBUG
            logWriter = new StreamWriter("dbInsertErrorLog.txt", true);
#endif
            string executeBlock = "";
            int rowCount = 1;
            try
            {                             
                InsertionSqlString insertSqlStr = new InsertionSqlString(dataTable.TableName);

                executeBlock += "EXECUTE BLOCK AS BEGIN ";

                foreach (DataRow dRow in dataTable.Rows)
                {
                    try
                    {
                        for (int colI = 0; colI < dataTable.Columns.Count; colI++)
                        {
                            string colName = dataTable.Columns[colI].ColumnName;
                            insertSqlStr.AddKeyValuePair(colName, dRow[colName]);
                        }

                        if (rowCount > numOfRowsAllowed ||
                            executeBlock.Length + insertSqlStr.ToString().Length > queryLength)
                        {
                            executeBlock += "END";

                            insertCommand.CommandText = executeBlock;
                            insertCommand.ExecuteNonQuery();

                            // commit the insertion
                            //             insertCommand.CommandText = "Commit";
                            //             insertCommand.ExecuteNonQuery();

                            executeBlock = "EXECUTE BLOCK AS BEGIN ";
                            rowCount = 1;
                        }
                        executeBlock += (insertSqlStr.ToString() + " ");

                        rowCount++;
                    }
                    catch (Exception ex)
                    {
                        //	throw new Exception (string.Format ("{0} Insertion Errors: {1}", dataTable.TableName, ex.Message));
#if DEBUG
                        logWriter.WriteLine(string.Format("{0} Insertion Errors: {1}", dataTable.TableName, ex.Message));
                        logWriter.WriteLine(insertCommand.CommandText);
#endif
                        executeBlock = "EXECUTE BLOCK AS BEGIN ";
                        rowCount = 1;
                    }
                    finally
                    {
                        insertSqlStr.ClearSqlInsertString();
                        //          executeBlock += "END";
                    }
                }
                executeBlock += "END";

                insertCommand.CommandText = executeBlock;
                insertCommand.ExecuteNonQuery();

                // commit the insertion
                insertCommand.CommandText = "Commit";
                insertCommand.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Exception thisException = new Exception(executeBlock + " eror: " + ex.Message);
                throw thisException;
            }
            finally
            {
#if DEBUG
                logWriter.Close();
#endif
            }
        }
        /// <summary>
        /// Insert data into database table
        /// </summary>
        /// <param name="thisEntrySameSeqEntriesTable"></param>
        public void InsertDataIntoDb(DbConnect dbConnect, DataTable dataTable, string tableName, string createTableString)
        {
            if (tableName == "" && dataTable.TableName == "")
            {
                throw new Exception("Database insertion error: No table name provided. ");
            }
            if (tableName == "")
            {
                tableName = dataTable.TableName;
            }

            try
            {
                bool prevConnected = dbConnect.IsConnected();
                if (!prevConnected)
                {
                    dbConnect.ConnectToDatabase();
                }

                // new table needs to be created
                if (createTableString != "")
                {
                    DbCreator tableCreator = new DbCreator();
                    tableCreator.CreateTableFromString(dbConnect, createTableString, tableName);
                }

                if (dataTable.TableName == "")
                {
                    dataTable.TableName = tableName;
                }
                InsertDataIntoDBtables(dbConnect, dataTable);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Insert one datarow into database table
        /// </summary>
        /// <param name="thisEntrySameSeqEntriesTable"></param>
        public void InsertDataIntoDb(DbConnect dbConnect, DataRow dRow)
        {
             if (!dbConnect.IsConnected())
            {
                dbConnect.ConnectToDatabase();
            }
            InsertionSqlString insertSqlStr = new InsertionSqlString(dRow.Table.TableName);
            System.Data.Odbc.OdbcCommand insertCommand = dbConnect.CreateCommand();

            try
            {
                for (int colI = 0; colI < dRow.Table.Columns.Count; colI++)
                {
                    string colName = dRow.Table.Columns[colI].ColumnName;
                    insertSqlStr.AddKeyValuePair(colName, dRow[colName]);
                }

                insertCommand.CommandText = insertSqlStr.ToString();
                insertCommand.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("{0} Insertion Errors: {1}", dRow.Table.TableName, ex.Message));
            }
            finally
            {
                insertSqlStr.ClearSqlInsertString();
                insertCommand.Dispose();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dbConnect"></param>
        /// <param name="insertString"></param>
        public void InsertDataIntoDb(DbConnect dbConnect, string insertString)
        {
            if (!dbConnect.IsConnected())
            {
                dbConnect.ConnectToDatabase();
            }
            System.Data.Odbc.OdbcCommand insertCommand = dbConnect.CreateCommand();
            insertCommand.CommandText = insertString;
            insertCommand.ExecuteNonQuery();
            insertCommand.Dispose();
        }
        // end of function
        #endregion


        #region insert data into database by the input db connection
        /// <summary>
        /// insert data into corresponding database tables
        /// </summary>
        public void InsertDataIntoDBtables(DataTable[] dataTables)
        {
#if DEBUG
            logWriter = new StreamWriter("dbInsertErrorLog.txt", true);
#endif
            try
            {              
                foreach (DataTable dataTable in dataTables)
                {
                    InsertionSqlString insertSqlStr = new InsertionSqlString(dataTable.TableName);

                    foreach (DataRow dRow in dataTable.Rows)
                    {
                        try
                        {
                            for (int colI = 0; colI < dataTable.Columns.Count; colI++)
                            {
                                string colName = dataTable.Columns[colI].ColumnName;
                                insertSqlStr.AddKeyValuePair(colName, dRow[colName]);
                            }

                            insertCommand.CommandText = insertSqlStr.ToString();
                            insertCommand.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            //	throw new Exception (string.Format ("{0} Insertion Errors: {1}", dataTable.TableName, ex.Message));
#if DEBUG
                            logWriter.WriteLine(string.Format("{0} Insertion Errors: {1}", dataTable.TableName, ex.Message));
                            logWriter.WriteLine(insertCommand.CommandText);
#endif
                        }
                        finally
                        {
                            insertSqlStr.ClearSqlInsertString();
                        }
                    }
                    // commit the insertion
                    insertCommand.CommandText = "Commit";
                    insertCommand.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
#if DEBUG
                logWriter.Close();
#endif
            }
        }


        /// <summary>
        /// insert data into corresponding database tables
        /// </summary>
        public void InsertDataIntoDBtables(DataTable dataTable)
        {
            if (dataTable == null || dataTable.Rows.Count == 0)
            {
                return;
            }
#if DEBUG
            logWriter = new StreamWriter("dbInsertErrorLog.txt", true);
#endif
            try
            {
                InsertionSqlString insertSqlStr = new InsertionSqlString(dataTable.TableName);

                foreach (DataRow dRow in dataTable.Rows)
                {
                    try
                    {
                        for (int colI = 0; colI < dataTable.Columns.Count; colI++)
                        {
                            string colName = dataTable.Columns[colI].ColumnName;
                            insertSqlStr.AddKeyValuePair(colName, dRow[colName]);
                        }

                        insertCommand.CommandText = insertSqlStr.ToString();
                        insertCommand.ExecuteNonQuery();

                    }
                    catch (Exception ex)
                    {
                        //	throw new Exception (string.Format ("{0} Insertion Errors: {1}", dataTable.TableName, ex.Message));
#if DEBUG
                        logWriter.WriteLine(string.Format("{0} Insertion Errors: {1}", dataTable.TableName, ex.Message));
                        logWriter.WriteLine(insertCommand.CommandText);
#endif
                    }
                    finally
                    {
                        insertSqlStr.ClearSqlInsertString();
                    }
                }
                // commit the insertion
                insertCommand.CommandText = "Commit";
                insertCommand.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
#if DEBUG
                logWriter.Close();
#endif

            }
        }
      
        /// <summary>
        /// Insert one datarow into database table
        /// </summary>
        /// <param name="thisEntrySameSeqEntriesTable"></param>
        public void InsertDataIntoDb(DataRow dRow)
        {           
            InsertionSqlString insertSqlStr = new InsertionSqlString(dRow.Table.TableName);

            try
            {
                for (int colI = 0; colI < dRow.Table.Columns.Count; colI++)
                {
                    string colName = dRow.Table.Columns[colI].ColumnName;
                    insertSqlStr.AddKeyValuePair(colName, dRow[colName]);
                }

                insertCommand.CommandText = insertSqlStr.ToString();
                insertCommand.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("{0} Insertion Errors: {1}", dRow.Table.TableName, ex.Message));
            }
            finally
            {
                insertSqlStr.ClearSqlInsertString();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dbConnect"></param>
        /// <param name="insertString"></param>
        public void InsertDataIntoDb(string insertString)
        {
            insertCommand.CommandText = insertString;
            insertCommand.ExecuteNonQuery();
        }
        // end of function
        #endregion
    }
}
