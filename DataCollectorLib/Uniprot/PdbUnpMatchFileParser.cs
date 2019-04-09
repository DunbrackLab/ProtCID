using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using DbLib;
using AuxFuncLib;

namespace DataCollectorLib.Uniprot
{
    public class PdbUnpMatchFileParser
    {
        #region member variables
        private DataTable dbRefTable = null;
        private DataTable dbRefSeqTable = null;
        private DbInsert dbInsert = new DbInsert();
        private DbQuery dbQuery = new DbQuery();
        private string dataDir = @"E:\DbProjectData\UniProt";
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

        #region parse PDB SWS file
        public void ParsePdbSwsFiles()
        {
            CreateDbRefTableStructures("sws");
            CreateDbRefTablesInDb("sws");

            string chainFile = Path.Combine(dataDir, "pdbsws_chain.txt");
            Hashtable entryEntityChainHash = new Hashtable();
            string[] addedEntryChains = ParsePdbSwsChainFile(chainFile, ref entryEntityChainHash);
            dbInsert.InsertDataIntoDBtables (dbRefTable);

            string residueFile = Path.Combine(dataDir, "pdbsws_res.txt");
            ParsePdbSwsResidueFile(residueFile, addedEntryChains, entryEntityChainHash);
            dbInsert.InsertDataIntoDBtables(dbRefSeqTable);
        }

        /// <summary>
        /// parse PDBSws chain file
        /// for those chains with same entityID, only pick up the first in the alphabet order
        /// </summary>
        /// <param name="chainFile"></param>
        public string[] ParsePdbSwsChainFile(string chainFile, ref Hashtable entryEntityChainHash)
        {
            string line = "";
            StreamReader dataReader = new StreamReader(chainFile);
            entryEntityChainHash.Clear (); // store the entity and chains in each entry
            Hashtable entryRefHash = new Hashtable(); // store the current ref id
            int refId = 0;
            int entityId = 0;
            ArrayList addedEntryChainList = new ArrayList();
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split(' ');
                if (! entryEntityChainHash.ContainsKey(fields[0]))
                {
                    Hashtable entityChainHash = GetEntryEntityChainsHash(fields[0]);
                    entryEntityChainHash.Add(fields[0], entityChainHash);
                }
                Hashtable entityChainHash = (Hashtable)entryEntityChainHash[fields[0]];
                if (IsChainRedundant(fields[0], fields[1], entityChainHash))
                {
                    continue;
                }

                entityId = GetEntityId(fields[1], entityChainHash);

                addedEntryChainList.Add(fields[0] + fields[1]);

                if (entryRefHash.ContainsKey(fields[0]))
                {
                    refId = (int)entryRefHash[fields[0]];
                    refId++;
                    entryRefHash[fields[0]] = refId;
                }
                else
                {
                    refId = 1;
                    entryRefHash.Add(fields[0], 1);
                }
                DataRow dataRow = dbRefTable.NewRow();
                dataRow["PdbID"] = fields[0];
                dataRow["RefID"] = refId;
                dataRow["EntityID"] = entityId;
                dataRow["DbAccession"] = fields[2];
                dbRefTable.Rows.Add(dataRow);
            }
            dataReader.Close();
            string[] addedEntryChains = new  string[addedEntryChainList.Count];
            addedEntryChainList.CopyTo(addedEntryChains);
            return addedEntryChains;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="residueFile"></param>
        /// <param name="addedEntryChains"></param>
        public void ParsePdbSwsResidueFile(string residueFile, Hashtable entryEntityChainHash)
        {
            StreamReader dataReader = new StreamReader(residueFile);
            string line = "";
            string preChainUnpString = "";
            string chainUnpString = "";

            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = ParseHelper.SplitPlus(line, ' ');
                if (Array.BinarySearch(addedEntryChains, fields[0] + fields[1]) > -1)
                {
                    chainUnpString = fields[0] + fields[1] + fields[5];
                    if (preChainUnpString == "")
                    {
                        preChainUnpString = chainUnpString;
                    }
                    while (preChainUnpString == chainUnpString)
                    {
                        
                    }

                }
            }
            dataReader.Close();
        }
        #endregion

        #region parse SSMAP file

        public void ParseSSMapFile()
        {

        }
        #endregion

        #region accessory functions
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="authorChain"></param>
        /// <returns></returns>
        private int GetEntityIDFromAuthorChain(string pdbId, string authorChain)
        {
            string queryString = string.Format("Select EntityID From AsymUnit Where PdbID = '{0}' AND EntityID = {1};", pdbId, authorChain);
            DataTable entityIdTable = dbQuery.Query(queryString);
            if (entityIdTable.Rows.Count == 0)
            {
                if (authorChain == "A")
                {
                    authorChain = "_";
                }
                queryString = string.Format("Select EntityID From AsymUnit Where PdbID = '{0}' AND EntityID = {1};", pdbId, authorChain);
                entityIdTable = dbQuery.Query(queryString);
            }
            if (entityIdTable.Rows.Count > 0)
            {
                return Convert.ToInt32(entityIdTable.Rows[0]["EntityID"].ToString ());
            }
            return -1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="authChain"></param>
        /// <param name="entityChainHash"></param>
        /// <returns></returns>
        private int GetEntityId(string authChain, Hashtable entityChainHash)
        {
            foreach (int entityId in entityChainHash.Keys)
            {
                ArrayList chainList = (ArrayList)entityChainHash[entityId];
                if (chainList.Contains(authChain))
                {
                    return entityId;
                }
            }
            return -1;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private Hashtable GetEntryEntityChainsHash(string pdbId)
        {
            string queryString = string.Format("Select EntityID, AuthorChain From AsymUnit " + 
                " Where PdbID = '{0}' AND PolymerType = 'polypeptide' ORDER BY EntityID, AuthorChain;", pdbId);
            DataTable entityChainTable = dbQuery.Query(queryString);
            int entityId = 0;
            string authChain = "";
            Hashtable entityChainHash = new Hashtable ();
            foreach (DataRow chainRow in entityChainTable.Rows)
            {
                entityId = Convert.ToInt32(chainRow["EntityID"].ToString ());
                authChain = chainRow["AuthorChain"].ToString().TrimEnd();
                if (authChain == "_")
                {
                    authChain = "A";
                }
                if (entityChainHash.ContainsKey(entityId))
                {
                    ArrayList authChainList = (ArrayList)entityChainHash[entityId];
                    authChainList.Add(authChain);
                }
                else
                {
                    ArrayList authChainList = new ArrayList();
                    authChainList.Add(authChain);
                    entityChainHash.Add(entityId, authChainList);
                }
            }
            return entityChainHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="authChain"></param>
        /// <param name="entityChainHash"></param>
        /// <returns></returns>
        private bool IsChainRedundant(string pdbId, string authChain, Hashtable entityChainHash)
        {
            foreach (int entityId in entityChainHash.Keys)
            {
                ArrayList chainList = (ArrayList)entityChainHash[entityId];
                if (chainList[0].ToString() != authChain)
                {
                    return true;
                }
            }
            return false;
        }

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
        #endregion
        #endregion

        #region create data tables
        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataType"></param>
        private void CreateDbRefTableStructures(string dataType)
        {
            string dbRefTableName = "";
            string dbRefSeqTableName = "";
            if (dataType == "sws")
            {
                dbRefTableName = "PdbSwsDbRef";
                dbRefSeqTableName = "PdbSwsDbRefSeq";

                dbRefTable = new DataTable(dbRefTableName);
                string[] dbRefColumns = { "RefID", "PdbID", "EntityID", "DbAccession" };
                foreach (string dbRefCol in dbRefColumns)
                {
                    dbRefTable.Columns.Add(new DataColumn(dbRefCol));
                }
            }
            else
            {
                dbRefTableName = "PdbSsmapDbRef";
                dbRefSeqTableName = "PdbSsmapDbRefSeq";

                dbRefTable = new DataTable(dbRefTableName);
                string[] dbRefColumns = { "RefID", "PdbID", "EntityID", "DbAccession", "Isoform"};
                foreach (string dbRefCol in dbRefColumns)
                {
                    dbRefTable.Columns.Add(new DataColumn(dbRefCol));
                }
            }

            dbRefSeqTable = new DataTable(dbRefSeqTableName);
            string[] dbRefSeqColumns = {"AlignID", "PdbID", "RefID", "DbAlignBeg", "DbAlignEnd", "AuthorChain", 
                                           "AuthorAlignBeg", "AuthorALignEnd", "SeqAlignBeg", "SeqAlignEnd"};
            foreach (string dbRefSeqCol in dbRefSeqColumns)
            {
                dbRefSeqTable.Columns.Add(new DataColumn(dbRefSeqCol));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataType"></param>
        private void CreateDbRefTablesInDb(string dataType)
        {
            string createTableString = "";
            string createIndexString = "";
            DbCreator dbCreator = new DbCreator();
            if (dataType == "sws")
            {
                createTableString = "CREATE TABLE " + dbRefTable.TableName + " ( " +
                    " PdbID CHAR(4) NOT NULL, " +
                    " RefID INTEGER NOT NULL, " +
                    " EntityID INTEGER NOT NULL, " +
               //     " AuthorChain CHAR(3) NOT NULL, " +
                    " DbAccession VARCHAR(15) NOT NULL, " + 
                    " PRIMARY KEY (PdbID, RefID));";
            }
            else
            {
                createTableString = "CREATE TABLE " + dbRefTable.TableName + " ( " +
                   " PdbID CHAR(4) NOT NULL, " +
                   " RefID INTEGER NOT NULL, " +
                   " EntityID INTEGER NOT NULL, " +
              //     " AuthorChain CHAR(3) NOT NULL, " +
                   " DbAccession VARCHAR(15) NOT NULL, " + 
                   " Isoform INTEGER NOT NULL, " + 
                   " PRIMARY KEY (PdbID, RefID));";
            }
            dbCreator.CreateTableFromString(createTableString, dbRefTable.TableName);
            createIndexString = "CREATE INDEX " + dbRefTable.TableName + "_idx1 ON " + dbRefTable.TableName + "(PdbID, EntityID);";
            dbCreator.CreateIndex(createIndexString, dbRefTable.TableName);

            createTableString = "CREATE TABLE" + dbRefSeqTable.TableName + " ( " +
                " AlignID INTEGER NOT NULL, " +
                " PdbID CHAR(4) NOT NULL, " +
                " RefID INTEGER NOT NULL, " +
                " AuthorChain CHAR(3) NOT NULL, " +
                " DbAlignBeg INTEGER NOT NULL, " +
                " DbAlignEnd INTEGER NOT NULL, " +
                " AuthorAlignBeg INTEGER NOT NULL, " +
                " AuthorAlignEnd INTEGER NOT NULL, " +
                " SeqAlignBeg INTEGER NOT NULL, " +
                " SeqAlignEnd INTEGER NOT NULL, " +  
                " PRIMARY KEY (PdbID, AlignID));";
            dbCreator.CreateTableFromString(createTableString, dbRefSeqTable.TableName);
            createIndexString = "CREATE INDEX " + dbRefSeqTable.TableName + "_idx1 ON " + dbRefSeqTable.TableName + "(PdbID, RefID);";
            dbCreator.CreateIndex(createIndexString, dbRefSeqTable.TableName);
        }
        #endregion
    }
}
