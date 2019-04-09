using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Data;
using DbLib;
using AuxFuncLib;
using BuQueryLib;
using ProtCidSettingsLib;

namespace BuCompLib.BamDb
{
    public class ProtBudBiolAssemblies
    {
        #region member variables
        private DbQuery dbQuery = new DbQuery();
        private DbInsert dbInsert = new DbInsert();
        private DbUpdate dbUpdate = new DbUpdate();
        private BiolUnitQuery buQuery = new BiolUnitQuery();
        private string buTableName = "ProtBudBiolAssemblies";
        private string parsedEntryFile = "parsedEntries.txt";
        public bool isUpdate = false;
        #endregion

        /// <summary>
        /// create protbud biol assemblies table 
        /// </summary>
        public void RetrieveProtBudBiolAssemblies()
        {
            isUpdate = false;
            CreatProtBudBuTableInDb();
            UpdateProtBudBiolAssemblies(null);
        }
        
        /// <summary>
        /// 
        /// </summary>
        public void UpdateProtBudBiolAssemblies ()
        {
            isUpdate = true;
            string[] updateEntries = GetUpdateEntries();
            UpdateProtBudBiolAssemblies(updateEntries);
        }

        /// <summary>
        /// update protbudbiolassemblies table 
        /// </summary>
        /// <param name="entries"></param>
        public void UpdateProtBudBiolAssemblies (string[] entries)
        {
            Console.WriteLine("Retrieving biol units info");

            if (entries == null)
            {
                string entryFile = "ProtEntries.txt";
                entries = GetAsuEntries (parsedEntryFile, entryFile, true);       
            }           

            StreamWriter parsedEntryWriter = new StreamWriter(parsedEntryFile, true);
            
            DataTable buTable = null;
            foreach (string pdbId in entries)
            {
                if (isUpdate)
                {
                    DeleteObsoleteBuData(pdbId);
                }
                buTable = new DataTable(buTableName);
                buQuery.GetBiolUnitForPdbEntry(pdbId, "", "", ref buTable);
                UpdateBuTableNmrEMResolution(buTable);
                dbInsert.InsertDataIntoDBtables(ProtCidSettings.pdbfamDbConnection, buTable);
              
                parsedEntryWriter.WriteLine(pdbId);
                parsedEntryWriter.Flush();
            }
            parsedEntryWriter.Close();

            Console.WriteLine("Done!");
        }

        /// <summary>
        /// update butable by converting non-digit resolution to -1
        /// </summary>
        /// <param name="buTable"></param>
        private void UpdateBuTableNmrEMResolution (DataTable buTable)
        {
            double resolution = 0;
            for (int i = 0; i < buTable.Rows.Count; i ++ )
            {
                if (!Double.TryParse(buTable.Rows[i]["Resolution"].ToString(), out resolution))
                {
                    buTable.Rows[i]["Resolution"] = "-1";
                }
            }
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
                List<string> parsedEntryList = new List<string> ();
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

                List<string> entryList = new List<string> ();
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
        /// <returns></returns>
        private string[] GetUpdateEntries()
        {
            List<string> updateEntryList = new List<string> ();
            StreamReader dataReader = new StreamReader(Path.Combine(ProtCidSettings.dirSettings.xmlPath, "newls-pdb.txt"));
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
        private List<string> ReadParsedEntryList(string parsedEntryFile)
        {
            List<string> parsedEntryList = new List<string> ();
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
                    createTableString += (buCol + " VARCHAR(8), ");
                }
                else if (lowerCaseBuCol.IndexOf("_entity") > -1 ||
                    lowerCaseBuCol.IndexOf("_asymid") > -1 ||
                    lowerCaseBuCol.IndexOf("_auth") > -1 ||
                    lowerCaseBuCol.IndexOf("_abc") > -1)
                {
                    createTableString += (buCol + " VARCHAR(255), ");
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
                    createTableString += (buCol + " DOUBLE PRECISION, ");
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
