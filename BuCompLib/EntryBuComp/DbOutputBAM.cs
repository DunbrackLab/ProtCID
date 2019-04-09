using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.IO;
using System.Data;
using DbLib;
using AuxFuncLib;
using ProtCidSettingsLib;
using BuQueryLib;

namespace BuCompLib.EntryBuComp
{
    public class DbOutputBAM
    {
        #region member variables
        private DbQuery dbQuery = new DbQuery();
        private DbInsert dbInsert = new DbInsert();
        private DbUpdate dbUpdate = new DbUpdate();
        private BiolUnitQuery buQuery = new BiolUnitQuery();
        private string buTableName = "ProtBudBiolAssemblies";
        private string parsedEntryFile = "parsedEntries.txt";
        private bool isUpdate = false;
        #endregion

        public void PrintBAMFiles(string[] updateEntries)
        {
            if (updateEntries == null)
            {
                CreatProtBudBuTableInDb();
                PrintBiolAssembliesToFile (null);
                PrintEntryChainMatchInfo(null);
            }
            else
            {
                PrintBiolAssembliesToFile (updateEntries);
                PrintEntryChainMatchInfo(updateEntries);

                PrintBiolAssembliesToFile(null);
                PrintEntryChainMatchInfo(null);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entries"></param>
        public void PrintBiolAssembliesToFile(string[] entries)
        {
            ProtCidSettings.logWriter.WriteLine("Retrieving biol units info");
          
            string buFile = "";
            bool headerLineExist = false;
           
            if (entries == null)
            {
                string entryFile = "ProtEntries.txt";
                entries = GetAsuEntries (parsedEntryFile, entryFile, true);
        //        entries = GetMissingEntries();
                buFile = Path.Combine(ProtCidSettings.dirSettings.pfamPath, "biolAssemblies.txt");
            }
            else
            {
                buFile = Path.Combine(ProtCidSettings.dirSettings.pfamPath, "newbiolAssemblies.txt");
            }

            if (File.Exists(buFile))
            {
                headerLineExist = true;
            }

            StreamWriter fileWriter = new StreamWriter(buFile, true);
            if (!headerLineExist)
            {
                string headerLine = GetHeaderLine();
                fileWriter.WriteLine(headerLine);
            }

            StreamWriter parsedEntryWriter = new StreamWriter(parsedEntryFile, true);
            
            DataTable buTable = null;
            string line = "";
            foreach (string pdbId in entries)
            {
                if (isUpdate)
                {
                    DeleteObsoleteBuData(pdbId);
                }
                buTable = new DataTable(buTableName);
                buQuery.GetBiolUnitForPdbEntry(pdbId, "", "", ref buTable);
                dbInsert.InsertDataIntoDBtables(ProtCidSettings.pdbfamDbConnection, buTable);

                foreach (DataRow buRow in buTable.Rows)
                {
                    line = "";
                    
                    foreach (object item in buRow.ItemArray)
                    {
                        line += item.ToString();
                        line += "\t";
                    }
                    fileWriter.WriteLine(line.TrimEnd('\t'));
                    
                }
                fileWriter.Flush();
                parsedEntryWriter.WriteLine(pdbId);
                parsedEntryWriter.Flush();
            }
            fileWriter.Close();
            parsedEntryWriter.Close();

            ParseHelper.ZipPdbFile(buFile);

            ProtCidSettings.logWriter.WriteLine("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        public void PrintProtBuDBiolAssembliesToFile()
        {
            string queryString = "Select * From " + buTableName + ";";
            DataTable buTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);

            string buFile = Path.Combine(ProtCidSettings.dirSettings.pfamPath, "biolAssemblies.txt");
            string headerLine = GetHeaderLine();
            StreamWriter fileWriter = new StreamWriter(buFile);
            fileWriter.WriteLine(headerLine);
            string line = "";
            foreach (DataRow buRow in buTable.Rows)
            {
                line = "";
                foreach (object item in buRow.ItemArray)
                {
                    line += item.ToString();
                    line += "\t";
                }
                fileWriter.WriteLine(line.TrimEnd('\t'));

            }
            fileWriter.Close();
            ParseHelper.ZipPdbFile(buFile);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="parseEntryFile"></param>
        /// <param name="entryFile"></param>
        /// <param name="protOnly"></param>
        /// <returns></returns>
        private string[] GetAsuEntries(string parseEntryFile, string entryFile, bool protOnly)
        {
            string[] entries = null;

            if (File.Exists(entryFile))
            {
                ArrayList parsedEntryList = new ArrayList();
                if (File.Exists(parseEntryFile))
                {
                    StreamReader parsedEntryReader = new StreamReader(parseEntryFile);
                    string parsedEntryLine = "";
                    while ((parsedEntryLine = parsedEntryReader.ReadLine()) != null)
                    {
                        parsedEntryList.Add(parsedEntryLine);
                    }
                    parsedEntryReader.Close();
                }

                ArrayList entryList = new ArrayList();
                StreamReader dataReader = new StreamReader(entryFile);
                string line = "";
                while ((line = dataReader.ReadLine()) != null)
                {
                    if (parsedEntryList.Contains(line))
                    {
                        continue;
                    }
                    entryList.Add(line);
                }
                dataReader.Close();
                entries = new string[entryList.Count];
                entryList.CopyTo(entries);
            }
            else
            {
                StreamWriter entryWriter = new StreamWriter(entryFile);

                string queryString = "";
                if (protOnly)
                {
                    queryString = "Select Distinct PdbID From AsymUnit Where PolymerType = 'polypeptide' Order by PdbID;";
                }
                else
                {
                    queryString = "Select Distinct PdbID From AsymUnit;";
                }
                DataTable entryTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
                entries = new string[entryTable.Rows.Count];
                int count = 0;
                string pdbId = "";
                foreach (DataRow entryRow in entryTable.Rows)
                {
                    pdbId = entryRow["PdbID"].ToString();
                    entries[count] = pdbId;
                    entryWriter.WriteLine(pdbId);
                    count++;
                }
                entryWriter.Close();
            }
            return entries;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        public void PrintEntryChainMatchInfo(string[] updateEntries)
        {
             ProtCidSettings.logWriter.WriteLine ("Get Chain map data for molide");

            string chainFile = "";
            string queryString = "";
            string parsedEntryFile = "parsedChainInfoEntries.txt";
            bool headerExist = false;
            if (updateEntries == null)
            {
                chainFile = Path.Combine(ProtCidSettings.dirSettings.pfamPath, "chaininfo.txt");
                string entryFile = "AsuEntries.txt";
                updateEntries = GetAsuEntries(parsedEntryFile, entryFile, false);
            }
            else
            {
                chainFile = Path.Combine(ProtCidSettings.dirSettings.pfamPath, "newchaininfo.txt");
            }

            if (File.Exists(chainFile))
            {
                headerExist = true;
            }
            StreamWriter dataWriter = new StreamWriter(chainFile, true);
            if (!headerExist)
            {
                dataWriter.WriteLine("PdbID\tEntityID\tAsymID\tAuthorID");
            }
            StreamWriter parseEntryWriter = new StreamWriter(parsedEntryFile, true);
            string line = "";
            foreach (string entry in updateEntries)
            {
                queryString = string.Format("Select * From AsymUnit Where PdbID = '{0}';", entry);
                DataTable asuTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
                foreach (DataRow asuRow in asuTable.Rows)
                {
                    if (asuRow["PolymerStatus"].ToString().TrimEnd().ToLower() == "water")
                    {
                        continue;
                    }
                    line = asuRow["PdbID"].ToString() + "\t" + 
                        asuRow["EntityID"].ToString() + "\t" +
                        asuRow["AsymID"].ToString().TrimEnd() + "\t" + 
                        asuRow["AuthorChain"].ToString().TrimEnd();
                    dataWriter.WriteLine(line);
                }
                dataWriter.Flush();
                parseEntryWriter.WriteLine(entry);
                parseEntryWriter.Flush();
            }
            dataWriter.Close();
            parseEntryWriter.Close();

            ParseHelper.ZipPdbFile(chainFile);
             ProtCidSettings.logWriter.WriteLine ("Done!");
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetUpdateEntries()
        {
            ArrayList updateEntryList = new ArrayList();
            StreamReader dataReader = new StreamReader(Path.Combine(ProtCidSettings.dirSettings.pfamPath, "newls-pdb.txt"));
            string line = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                updateEntryList.Add(line.Substring (0, 4));
            }
            dataReader.Close();
            string[] updateEntries = new string[updateEntryList.Count];
            updateEntryList.CopyTo(updateEntries);
            return updateEntries;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parsedEntryFile"></param>
        /// <returns></returns>
        private ArrayList ReadParsedEntryList(string parsedEntryFile)
        {
            ArrayList parsedEntryList = new ArrayList();
            string line = "";
            StreamReader dataReader = new StreamReader(parsedEntryFile);
            while ((line = dataReader.ReadLine()) != null)
            {
                parsedEntryList.Add(line);
            }
            dataReader.Close();
            return parsedEntryList;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string GetHeaderLine()
        {
            string[] buTableColumns = buQuery.BuTableColumns;
            string headerLine = "";
            foreach (string col in buTableColumns)
            {
                headerLine += (col + "\t");
            }
            return headerLine.TrimEnd ('\t');
        }

        /// <summary>
        /// 
        /// </summary>
        private void CreatProtBudBuTableInDb()
        {
            string[] buColumns = buQuery.BuTableColumns;
            string createTableString = "Create Table " + buTableName + "( ";
            string lowerCaseBuCol = "";
            foreach (string buCol in buColumns)
            {
                lowerCaseBuCol = buCol.ToLower();
                if (lowerCaseBuCol == "pdbid")
                {
                    createTableString += "PdbID CHAR(4) NOT NULL, ";
                }
                else if (lowerCaseBuCol.IndexOf("buid") > -1)
                {
                    createTableString += (buCol + " VARCHAR(10), ");
                }
                else if (lowerCaseBuCol.IndexOf("_entity") > -1 ||
                    lowerCaseBuCol.IndexOf("_asymid") > -1 ||
                    lowerCaseBuCol.IndexOf("_auth") > -1 ||
                    lowerCaseBuCol.IndexOf("_abc") > -1)
                {
                    createTableString += (buCol + " BLOB SUB_TYPE TEXT NOT NULL, ");
                }
                else if (lowerCaseBuCol == "samebus")
                {
                    createTableString += (buCol + " CHAR(9), ");
                }
                else if (lowerCaseBuCol == "dna" || lowerCaseBuCol == "rna" || lowerCaseBuCol == "ligands")
                {
                    createTableString += (buCol + " CHAR(3), ");
                }
                else if (lowerCaseBuCol == "resolution")
                {
                    createTableString += (buCol + " VARCHAR(32), ");
                }
                else if (lowerCaseBuCol == "names")
                {
                    createTableString += (buCol + " BLOB SUB_TYPE TEXT NOT NULL, ");
                }
            }
            createTableString = createTableString.TrimEnd(", ".ToCharArray ());
            createTableString += ");";
            DbCreator dbCreat = new DbCreator();
            dbCreat.CreateTableFromString(ProtCidSettings.pdbfamDbConnection, createTableString, buTableName);

            string createIndexString = "Create Index " + buTableName + "_idx1 ON " + buTableName +
                " ( PdbID, PdbBuID, PisaBuID);";
            dbCreat.CreateIndex(ProtCidSettings.pdbfamDbConnection, createIndexString, buTableName);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        private void DeleteObsoleteBuData(string pdbId)
        {
            string deleteString = string.Format("Delete From {0} Where PdbID = '{1}';", buTableName, pdbId);
            dbUpdate.Delete(ProtCidSettings.pdbfamDbConnection, deleteString);
        }
    }
}
