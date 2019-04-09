using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.IO;
using System.Data;
using DbLib;
using AuxFuncLib;
using XtalLib.Settings;

namespace DataCollectorLib.Uniprot
{
    public class PdbUnpMatchDbFileParser
    {
        #region member variables
        private DataTable pdbUnpDbTable = null;
        private DbQuery dbQuery = new DbQuery();
        private DbInsert dbInsert = new DbInsert();

        const string three2OneTable = "ALA -A CYS -C ASP -D GLU -E PHE -F GLY -G " +
           "HIS -H ILE -I LYS -K LEU -L MET -M ASN -N " +
           "PRO -P GLN -Q ARG -R SER -S THR -T VAL -V " +
           "TRP -W TYR -Y ASX -N GLX -Q UNK -X INI -K " +
           "AAR -R ACE -X ACY -G AEI -T AGM -R ASQ -D " +
           "AYA -A BHD -D CAS -C CAY -C CEA -C CGU -E " +
           "CME -C CMT -C CSB -C CSD -C CSE -C CSO -C " +
           "CSP -C CSS -C CSW -C CSX -C CXM -M CYG -C " +
           "CYM -C DOH -D EHP -F FME -M FTR -W GL3 -G " +
           "H2P -H HIC -H HIP -H HTR -W HYP -P KCX -K " +
           "LLP -K LLY -K LYZ -K M3L -K MEN -N MGN -Q " +
           "MHO -M MHS -H MIS -S MLY -K MLZ -K MSE -M " +
           "NEP -H NPH -C OCS -C OCY -C OMT -M OPR -R " +
           "PAQ -Y PCA -Q PHD -D PRS -P PTH -Y PYX -C " +
           "SEP -S SMC -C SME -M SNC -C SNN -D SVA -S " +
           "TPO -T TPQ -Y TRF -W TRN -W TRO -W TYI -Y " +
           "TYN -Y TYQ -Y TYS -Y TYY -Y YOF -Y FOR -X";
        #endregion

        /// <summary>
        /// 
        /// </summary>
        public void ParsePdbUnpSrcFiles()
        {
            if (AppSettings.dirSettings == null)
            {
                AppSettings.LoadDirSettings();
                DbBuilder.dbConnect.ConnectString =
                    "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=masterkey;DATABASE=" + AppSettings.dirSettings.dbPath;

            }
            DbBuilder.dbConnect.ConnectToDatabase();

            string dataDir = @"E:\DbProjectData\UniProt";
            string pdbSwsFile = Path.Combine(dataDir, "pdbsws_res.txt");
            ParsePdbSwsFile(pdbSwsFile);

            string pdbSsmapFile = Path.Combine(dataDir, "ssmap_test.dat");
            ParsePdbSsmapFile(pdbSsmapFile);

            DbBuilder.dbConnect.DisconnectFromDatabase();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataFile"></param>
        public void ParsePdbSwsFile(string dataFile)
        {
            CreateDataTableStructures("sws");
            CreateDbTable("sws");

            string line = "";
            string preUnpPdbChainString = "";
            string unpPdbChainString = "";
            string prePdbChain = "";
            string pdbChain = "";
            StreamReader dataReader = new StreamReader(dataFile);
            string pdbId = "";
            string authorChain = "";
            string dbAccession = "";
            ArrayList seqNumList = new ArrayList ();
            ArrayList pdbSeqList = new ArrayList ();
            ArrayList authSeqNumList = new ArrayList ();
            ArrayList unpSeqList = new ArrayList ();
            ArrayList unpSeqNumList = new ArrayList ();
            int totalNumOfRowsInTable = 10000;
            int numOfRowsInTable = 0;
     //       int numOfRowsInDb = 0;
            Hashtable entryEntityHash = new Hashtable();
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = ParseHelper.SplitPlus(line, ' ');
                pdbChain = fields[0] + fields[1];
                if (fields.Length == 8)
                {
                    unpPdbChainString = fields[0] + fields[1] + fields[5];
                }
                
                if (preUnpPdbChainString == "")
                {
                    preUnpPdbChainString = unpPdbChainString;
                }
                if (prePdbChain == "")
                {
                    prePdbChain = pdbChain;
                }
                if (prePdbChain != pdbChain)
                {
                    unpPdbChainString = "";
                    preUnpPdbChainString = "";
                }
                if (preUnpPdbChainString != unpPdbChainString || prePdbChain != pdbChain)
                {
             /*       numOfRowsInDb++;
                    if (numOfRowsInDb <= 60000)
                    {
                        continue;
                    }*/
                    DataRow dataRow = pdbUnpDbTable.NewRow();
                    dataRow["PdbID"] = pdbId;
                    dataRow["AuthorChain"] = authorChain;
                    dataRow["EntityID"] = GetEntityIDForAuthorChain (pdbId, authorChain, ref entryEntityHash);
                    dataRow["DbAccession"] = dbAccession;
                    dataRow["PdbSequence"] = pdbSeqList.ToString ();
                    dataRow["AuthorSeqNumbers"] = FormatArrayListToString (authSeqNumList);
                    dataRow["SeqNumbers"] = FormatArrayListToString (seqNumList);
                    dataRow["UnpSequence"] = unpSeqList.ToString ();
                    dataRow["unpSeqNumbers"] = FormatArrayListToString(unpSeqNumList);
               //     dbInsert.InsertDataIntoDb(dataRow);
                    pdbUnpDbTable.Rows.Add(dataRow);

                    numOfRowsInTable++;

                    if (numOfRowsInTable == totalNumOfRowsInTable)
                    {
                        dbInsert.InsertDataIntoDBtables(pdbUnpDbTable);
                        pdbUnpDbTable.Clear();
                        numOfRowsInTable = 0;
                    }

                    pdbSeqList.Clear();
                    authSeqNumList.Clear();
                    seqNumList.Clear();
                    unpSeqList.Clear();
                    unpSeqNumList.Clear();
                }
                pdbId = fields[0];
                authorChain = fields[1];
                preUnpPdbChainString = unpPdbChainString;
                prePdbChain = pdbChain;
                seqNumList.Add(fields[2]);
                pdbSeqList.Add(threeToOne(fields[3]));
                authSeqNumList.Add(fields[4]);
                if (fields.Length < 8)
                {
                    unpSeqList.Add("-");
                    unpSeqNumList.Add("-");
                }
                else
                {
                    dbAccession = fields[5];
                    unpSeqList.Add(fields[6]);
                    unpSeqNumList.Add(fields[7]);
                }
            }
            dataReader.Close();
            if (pdbUnpDbTable.Rows.Count > 0)
            {
                dbInsert.InsertDataIntoDBtables(pdbUnpDbTable);
                pdbUnpDbTable.Clear();
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataFile"></param>
        public void ParsePdbSsmapFile(string dataFile)
        {
            CreateDataTableStructures("ssmap");
            CreateDbTable("ssmap");

            string line = "";
            string preUnpPdbChainString = "";
            string unpPdbChainString = "";
            StreamReader dataReader = new StreamReader(dataFile);
            string pdbId = "";
            string authorChain = "";
            string isoform = "";
            string dbAccession = "";
            ArrayList seqNumList = new ArrayList();
            ArrayList pdbSeqList = new ArrayList();
            ArrayList authSeqNumList = new ArrayList();
            ArrayList unpSeqList = new ArrayList();
            ArrayList unpSeqNumList = new ArrayList();
            int totalNumOfRowsInTable = 10000;
            int numOfRowsInTable = 0;
            Hashtable entryEntityHash = new Hashtable();
            line = dataReader.ReadLine();
            StreamWriter logWriter = new StreamWriter("SSmapParseingLog.txt");
            while ((line = dataReader.ReadLine()) != null)
            {
                line = line.TrimEnd();
                if (line == "" || line == "//")
                {
                    continue;
                }

                string[] fields = line.Split('\t');
                if (fields.Length != 10)
                {
                    logWriter.WriteLine(line);
                    logWriter.Flush();
                    continue;
                }

                unpPdbChainString = fields[0] + fields[1] + fields[2] + fields[3];

                if (preUnpPdbChainString == "")
                {
                    preUnpPdbChainString = unpPdbChainString;
                }
                if (preUnpPdbChainString != unpPdbChainString)
                {
                    DataRow dataRow = pdbUnpDbTable.NewRow();
                    dataRow["PdbID"] = pdbId;
                    dataRow["AuthorChain"] = authorChain;
                    dataRow["Isoform"] = isoform;
                    dataRow["EntityID"] = GetEntityIDForAuthorChain(pdbId, authorChain, ref entryEntityHash);
                    dataRow["DbAccession"] = dbAccession;
                    dataRow["PdbSequence"] = pdbSeqList.ToString ();
                    dataRow["AuthorSeqNumbers"] = FormatArrayListToString(authSeqNumList);
                    dataRow["SeqNumbers"] = FormatArrayListToString(seqNumList);
                    dataRow["UnpSequence"] = unpSeqList.ToString ();
                    dataRow["unpSeqNumbers"] = FormatArrayListToString(unpSeqNumList);
                    pdbUnpDbTable.Rows.Add(dataRow);
                    numOfRowsInTable++;

                    if (numOfRowsInTable == totalNumOfRowsInTable)
                    {
                        dbInsert.InsertDataIntoDBtables(pdbUnpDbTable);
                        pdbUnpDbTable.Clear();
                        numOfRowsInTable = 0;
                    }
              //      dbInsert.InsertDataIntoDb(dataRow);

                    pdbSeqList.Clear();
                    authSeqNumList.Clear();
                    seqNumList.Clear();
                    unpSeqList.Clear();
                    unpSeqNumList.Clear();
                }
                dbAccession = fields[0];
                isoform = fields[1];
                pdbId = fields[2].ToLower ();
                authorChain = fields[3];
                preUnpPdbChainString = unpPdbChainString;
                if (fields[5] == "")
                {
                    pdbSeqList.Add("-");
                }
                else
                {
                    pdbSeqList.Add(fields[5]);
                }
                if (fields[6] == "")
                {
                    authSeqNumList.Add("-");
                }
                else
                {
                    authSeqNumList.Add(fields[6]);
                }
                if (fields[7] == "")
                {
                    seqNumList.Add("-");
                }
                else
                {
                    seqNumList.Add(fields[7]);
                }
                if (fields[8] == "")
                {
                    unpSeqList.Add("-");
                }
                else
                {
                    unpSeqList.Add(fields[8]);
                }
                // due to a bug in ssmap file, subtract by 1
                if (fields[9] == "-" || fields[9] == "")
                {
                    unpSeqNumList.Add("-");
                }
                else
                {
                    unpSeqNumList.Add(Convert.ToInt32(fields[9]) - 1);
                }
            }
            dataReader.Close();
            logWriter.Close();
            if (pdbUnpDbTable.Rows.Count > 0)
            {
                dbInsert.InsertDataIntoDBtables(pdbUnpDbTable);
                pdbUnpDbTable.Clear();
            }
        }

        #region create data table
        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataType"></param>
        private void CreateDataTableStructures(string dataType)
        {
            pdbUnpDbTable = new DataTable("PdbUnp" + dataType + "ResidueMap");
            string[] residueColumns = {"PdbID", "AuthorChain", "EntityID", "DbAccession", "PdbSequence", "UnpSequence", 
                                          "AuthorSeqNumbers", "UnpSeqNumbers", "SeqNumbers"};
            foreach (string residueCol in residueColumns)
            {
                pdbUnpDbTable.Columns.Add(new DataColumn (residueCol));
            }
            if (dataType == "ssmap")
            {
                pdbUnpDbTable.Columns.Add(new DataColumn ("Isoform"));
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataType"></param>
        private void CreateDbTable(string dataType)
        {
            DbCreator dbCreate = new DbCreator();
            string createTableString = "";
            createTableString = "CREATE TABLE " + pdbUnpDbTable.TableName + " ( " +
                " PdbID CHAR(4) NOT NULL, ";
            if (dataType == "ssmap")
            {
                createTableString += "Isoform INTEGER NOT NULL, ";
            }
            createTableString += (" AuthorChain CHAR(3) NOT NULL, " +
                " EntityID INTEGER NOT NULL, " +
                " DbAccession CHAR(6) NOT NULL, " +
                " PdbSequence BLOB Sub_Type Text NOT NULL, " +
                " UnpSequence BLOB Sub_Type Text NOT NULL, " +
                " AuthorSeqNumbers BLOB Sub_Type Text NOT NULL, " +
                " UnpSeqNumbers BLOB Sub_Type Text NOT NULL, " +
                " SeqNumbers BLOB Sub_Type Text NOT NULL );");
            dbCreate.CreateTableFromString(createTableString, pdbUnpDbTable.TableName);
            string createIndexString = "CREATE INDEX " + pdbUnpDbTable.TableName + "_idx1 ON " + pdbUnpDbTable.TableName + "(PdbID, AuthorChain);";
            dbCreate.CreateIndex(createIndexString, pdbUnpDbTable.TableName);
        }
        #endregion

        #region others
        /// <summary>
        /// convert three letter aa code to one letter code
        /// </summary>
        /// <param name="threeLetters"></param>
        /// <returns></returns>
        private string threeToOne(string threeLetters)
        {
            int threeIndex = three2OneTable.IndexOf(threeLetters);
            if (threeIndex == -1) // not found
            {
                return "X";
            }
            else
            {
                return three2OneTable.Substring(threeIndex + 5, 1);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="authorChain"></param>
        /// <returns></returns>
        private int GetEntityIDForAuthorChain(string pdbId, string authorChain, ref Hashtable entryEntityHash)
        {
            int entityId = -1;
            if (!entryEntityHash.ContainsKey(pdbId))
            {
                string queryString = string.Format("Select AuthorChain, EntityID From AsymUnit " +
                    " Where PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
                DataTable entityTable = dbQuery.Query(queryString);
                string authChain = "";
                Hashtable chainEntityHash = new Hashtable();
                foreach (DataRow chainEntityRow in entityTable.Rows)
                {
                    authChain = chainEntityRow["AuthorChain"].ToString().TrimEnd();
                    if (authChain == "_")
                    {
                        authChain = "A";
                    }
                    chainEntityHash.Add(authChain, Convert.ToInt32(chainEntityRow["EntityID"].ToString()));
                }
                entryEntityHash.Add(pdbId, chainEntityHash);
            }
            Hashtable authChainEntityHash = (Hashtable)entryEntityHash[pdbId];
            if (authChainEntityHash.ContainsKey(authorChain))
            {
                entityId = (int)authChainEntityHash[authorChain];
            }

            return entityId;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="itemList"></param>
        /// <returns></returns>
        private string FormatArrayListToString(ArrayList itemList)
        {
            string listString = "";
            foreach (object item in itemList)
            {
                listString += (item.ToString() + ",");
            }
            return listString.TrimEnd(',');
        }
        #endregion
    }
}
