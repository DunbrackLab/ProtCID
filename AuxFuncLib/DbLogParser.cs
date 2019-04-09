using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using DbLib;

namespace AuxFuncLib
{
    public class DbLogParser
    {
        public void ParseDBInsertLogFile(string fileName, DbConnect dbConnect)
        {
            DbInsert dbInsert = new DbInsert();
            StreamReader dataReader = new StreamReader(fileName);
            string line = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.ToUpper().IndexOf("INSERT INTO") > -1)
                {
                    dbInsert.InsertDataIntoDb (dbConnect, line);
                }
            }
            dataReader.Close();
        }
    }
}
