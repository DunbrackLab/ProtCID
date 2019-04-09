using System;
using System.Collections;
using System.Text;
using System.IO;
using System.Data;
using DataCollectorLib.FatcatAlignment;
using AuxFuncLib;
using XtalLib.Settings;
using DbLib;

namespace PfamLib.DomainFiles
{
    public class PfamDomainPairs
    {
        #region member variables
        private DbQuery dbQuery = new DbQuery();
        #endregion

        public PfamDomainPairs()
        {  
            if (AppSettings.alignmentDbConnection == null)
            {
                AppSettings.alignmentDbConnection = new DbConnect();

                AppSettings.alignmentDbConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;" +
                "UID=SYSDBA;PWD=masterkey;DATABASE=" + AppSettings.dirSettings.alignmentDbPath;
            }
        }
        #region chain pairs to be aligned in PFAM
        /// <summary>
        /// 
        /// </summary>
        public void ReadDomainPairsInPfamToBeAligned()
        {
            string queryString = "Select Distinct Pfam_ID From PdbPfam;";
            DataTable pfamFamilyTable = dbQuery.Query(queryString);

            string pfamDomainPairsFile = Path.Combine (AppSettings.dirSettings.pfamPath, "DomainAlign\\pfamDomainPairs.txt");
            StreamWriter dataWriter = new StreamWriter(pfamDomainPairsFile);
            AppSettings.progressInfo.Reset();
            AppSettings.progressInfo.currentOperationLabel = "PFAM domain pair list";
            AppSettings.progressInfo.totalStepNum = pfamFamilyTable.Rows.Count;
            AppSettings.progressInfo.totalOperationNum = pfamFamilyTable.Rows.Count;
            AppSettings.progressInfo.progStrQueue.Enqueue("Read PFAM domain pair list");

            string pfamId = "";
            foreach (DataRow pfamFamilyRow in pfamFamilyTable.Rows)
            {
                pfamId = pfamFamilyRow["Pfam_ID"].ToString().TrimEnd();

                AppSettings.progressInfo.currentStepNum++;
                AppSettings.progressInfo.currentOperationNum++;
                AppSettings.progressInfo.currentFileName = pfamId;

                dataWriter.WriteLine(pfamId);

                GetPfamDomainPairsToBeAligned (pfamId, dataWriter);
            }
            dataWriter.Close();

            int numOfDomainPairsCutoff = 10000;
            PfamLib.PfamDomainBuiler.DividDomainPairsToFiles(pfamDomainPairsFile, numOfDomainPairsCutoff);

            PfamLib.PfamDomainBuilder.FormatPfamDomainPairFiles("pfamDomainPairs.txt");

            AppSettings.progressInfo.progStrQueue.Enqueue("Done!");
            AppSettings.progressInfo.threadFinished = true;
        }

       /// <summary>
       /// 
       /// </summary>
       /// <param name="pfamAcc"></param>
       /// <param name="repChainTable"></param>
       /// <param name="dataWriter"></param>
        private void GetPfamDomainPairsToBeAligned (string pfamId, StreamWriter dataWriter)
        {
            string queryString = string.Format("Select Distinct PdbID, EntityID, DomainID" +
                " From {0} Where Pfam_ID = '{1}';", PfamLib.PfamLibSettings.pfamDbTable, pfamId);
            DataTable pfamDomainTable = dbQuery.Query(queryString);
            ArrayList entryDomainList = new ArrayList();
            string entryDomain = "";
            string pdbId = "";
            int entityId = 0;
            foreach (DataRow domainRow in pfamDomainTable.Rows)
            {
                pdbId = domainRow["PdbID"].ToString();
                entityId = Convert.ToInt32(domainRow["EntityID"].ToString ());
                if (IsRepEntity(pdbId, entityId))
                {
                    entryDomain = pdbId + chainRow["DomainID"].ToString();
                    if (!entryDomainList.Contains(entryDomain))
                    {
                        entryDomainList.Add(entryDomain);
                    }
                }
            }
            entryDomainList.Sort();
           string dataLine = "";
           for (int i = 0; i < entryDomainList.Count; i++)
           {
               dataLine += (entryDomainList[i] + ",");
           }
           dataWriter.WriteLine(dataLine.TrimEnd (','));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamDomain1"></param>
        /// <param name="pfamDomain2"></param>
        /// <returns></returns>
        private bool IsDomainAlignmentExist(string pfamDomain1, string pfamDomain2)
        {
            string queryString = string.Format("Select * From PfamDomainALignments " + 
                " Where QueryEntry = '{0}' AND QueryDomainID = {1} AND " + 
                " HitEntry = '{2}' AND HitDomainID = {3};", 
                pfamDomain1.Substring (0, 4), pfamDomain1.Substring (4, pfamDomain1.Length - 4),
                pfamDomain2.Substring (0, 4), pfamDomain2.Substring (4, pfamDomain2.Length - 4));
            DataTable alignmentTable = dbQuery.Query(AppSettings.alignmentDbConnection, queryString);
            if (alignmentTable.Rows.Count > 0)
            {
                return true;
            }
            queryString = string.Format("Select * From PfamDomainALignments " +
                " Where QueryEntry = '{0}' AND QueryDomainID = {1} AND " +
                " HitEntry = '{2}' AND HitDomainID = {3};",
                pfamDomain2.Substring(0, 4), pfamDomain2.Substring(4, pfamDomain2.Length - 4),
                pfamDomain1.Substring(0, 4), pfamDomain1.Substring(4, pfamDomain1.Length - 4));
            alignmentTable = dbQuery.Query(AppSettings.alignmentDbConnection, queryString);
            if (alignmentTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private bool IsRepEntity(string pdbId, int entityId)
        {
            string queryString = string.Format("Select * From RedundantPdbChains " + 
                " Where PdbID1 = '{0}' AND EntityID1 = {1};", pdbId, entityId);
            DataTable repEntityTable = dbQuery.Query(AppSettings.alignmentDbConnection, queryString);
            if (repEntityTable.Rows.Count > 0)
            {
                return true;
            }
            queryString = string.Format("Select * From RedundantPdbChains " +
                " Where PdbID2 = '{0}' AND EntityID2 = {1};", pdbId, entityId);
            repEntityTable = dbQuery.Query(AppSettings.alignmentDbConnection, queryString);
            if (repEntityTable.Rows.Count == 0) // somehow, the entity not in the table
            {
                return true;
            }
            return false;
        }
      
        #endregion

        #region read updated domain pairs       
        /// <summary>
        /// for structure alignments, only use representative chains from 
        /// Guoli Wang's non-redundant pdb chains
        /// </summary>
        public void ReadUpdateDomainPairs()
        {
            AppSettings.progressInfo.ResetCurrentProgressInfo();
            AppSettings.progressInfo.progStrQueue.Enqueue("Generate domain pair list file.");

         //   StreamReader dataReader = new StreamReader(Path.Combine (AppSettings.dirSettings.xmlPath, "newls-pdb.txt"));
            StreamReader dataReader = new StreamReader(@"E:\DbProjectData\PFam\domainFiles\newls-pfam.txt");
            string line = "";
            ArrayList updateEntryList = new ArrayList();
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line == "")
                {
                    continue;
                }
                updateEntryList.Add(line.Substring(0, 4));
            }
            dataReader.Close();
            Hashtable newFamilyDomainHash = new Hashtable();
            string domainFile = "";
            string pfamAcc = "";
            string[] updateEntries = new string [updateEntryList.Count];
            updateEntryList.CopyTo (updateEntries);
            AppSettings.progressInfo.progStrQueue.Enqueue("Retrieving the representative chains.");

            string[] updateRepChains = UpdateAlignmentDataInDb(updateEntries);

            AppSettings.progressInfo.totalOperationNum = updateRepChains.Length;
            AppSettings.progressInfo.totalStepNum = updateRepChains.Length;

            foreach (string repChain in updateRepChains)
            {
                AppSettings.progressInfo.currentOperationNum++;
                AppSettings.progressInfo.currentStepNum++;
                AppSettings.progressInfo.currentFileName = repChain;

                DataTable pfamDomainTable = GetChainPfamDomainsTable(repChain.Substring (0, 4), 
                    repChain.Substring (4, repChain.Length - 4));
                if (pfamDomainTable.Rows.Count > 0)
                {
                    foreach (DataRow domainRow in pfamDomainTable.Rows)
                    {
                        pfamAcc = domainRow["Pfam_ACC"].ToString().TrimEnd ();
                   //     domainFile = domainRow["PdbID"].ToString () + domainRow["AsymChain"].ToString().TrimEnd() +
                   //         domainRow["SeqStartPos"].ToString();
                        domainFile = domainRow["PdbID"].ToString() + domainRow["DomainID"].ToString();
                        if (newFamilyDomainHash.ContainsKey(pfamAcc))
                        {
                            ArrayList newDomainList = (ArrayList)newFamilyDomainHash[pfamAcc];
                            newDomainList.Add(domainFile);
                        }
                        else
                        {
                            ArrayList newDomainList = new ArrayList();
                            newDomainList.Add(domainFile);
                            newFamilyDomainHash.Add(pfamAcc, newDomainList);
                        }
                    }
                }
            }
            AppSettings.progressInfo.progStrQueue.Enqueue("Write data to file.");
            StreamWriter dataWriter = new StreamWriter("updatePfamDomainPairs.txt");
            foreach (string updatePfamAcc in newFamilyDomainHash.Keys)
            {
                ArrayList updateDomainList = (ArrayList)newFamilyDomainHash[updatePfamAcc];
                ArrayList familyRepDomainList = GetFamilyRepDomainFiles(updatePfamAcc);

                WriteUpdateDomainPairsToFile(updatePfamAcc, updateDomainList, familyRepDomainList, dataWriter);
            }
            dataWriter.Close();
            FormatPfamDomainPairFiles("updatePfamDomainPairs.txt");
            AppSettings.progressInfo.progStrQueue.Enqueue("Done!");
            AppSettings.progressInfo.threadFinished = true;
        }

       /// <summary>
       /// the domains of the pfam family
       /// </summary>
       /// <param name="pfamAcc"></param>
       /// <returns></returns>
        private ArrayList GetFamilyRepDomainFiles(string pfamAcc)
        {
            string queryString = string.Format("Select PdbID, AuthChain, AsymChain, SeqStartPos, DomainID From PfamPdb" + 
                " Where Pfam_ACC = '{0}';", pfamAcc);
            DataTable familyDomainTable = dbQuery.Query(queryString);
            ArrayList domainList = new ArrayList();
            foreach (DataRow domainRow in familyDomainTable.Rows)
            {
                if (domainRow["SeqStartPos"].ToString() == "-1")
                {
                    continue;
                }
                if (IsRepresentativeChain (domainRow["PdbID"].ToString (), domainRow["AuthChain"].ToString ().TrimEnd ()))
                {
                    domainList.Add(domainRow["PdbId"].ToString() +
                        domainRow["DomainID"].ToString ());
                 //   domainRow["AsymChain"].ToString().TrimEnd() +
                 //   domainRow["SeqStartPos"].ToString());
                }
            }
        /*  string[] domainFiles = new string[domainList.Count];
            domainList.CopyTo(domainFiles);
            return domainFiles;*/
            return domainList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamAcc"></param>
        /// <param name="updateDomainList"></param>
        /// <param name="familyDomainList"></param>
        /// <param name="dataWriter"></param>
        private void WriteUpdateDomainPairsToFile(string pfamAcc, ArrayList updateDomainList, ArrayList familyDomainList, 
            StreamWriter dataWriter)
        {
            dataWriter.WriteLine(pfamAcc);
            foreach (string updateDomain in updateDomainList)
            {
                foreach (string domain in familyDomainList)
                {
                    if (updateDomain == domain)
                    {
                        continue;
                    }
                    dataWriter.WriteLine(updateDomain + "   " + domain);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetEntryPfamDomainsTable(string pdbId)
        {
            string queryString = string.Format("Select * From PfamPdb Where PdbID = '{0}';", pdbId);
            DataTable pfamDomainTable = dbQuery.Query(queryString);
            return pfamDomainTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetChainPfamDomainsTable(string pdbId, string authChain)
        {
            string queryString = string.Format("Select * From PfamPdb " + 
                " Where PdbID = '{0}' AND AuthChain = '{1}';", pdbId, authChain);
            DataTable pfamDomainTable = dbQuery.Query(queryString);
            return pfamDomainTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool HasEntryPfamDomainDef(string pdbId)
        {
            string queryString = string.Format("Select * From PfamPdb Where PdbID = '{0}';", pdbId);
            DataTable pfamDomainTable = dbQuery.Query(queryString);
            if (pfamDomainTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="authChain"></param>
        /// <returns></returns>
        private bool IsRepresentativeChain(string pdbId, string authChain)
        {
            string queryString = string.Format("Select * From RedundantPdbChains " + 
                " Where PdbId1 = '{0}' AND ChainID1 = '{1}';", pdbId, authChain);
            DataTable repChainTable = dbQuery.Query(AppSettings.alignmentDbConnection, queryString);
            if (repChainTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamAcc"></param>
        /// <param name="updateRepDomainList"></param>
        /// <param name="familyRepDomainList"></param>
        private string[] UpdateAlignmentDataInDb(string[] updateEntries)
        {
       //     string deleteString = "";
       //     string[] domainFields = null;
            Hashtable updateRepReduntChainHash = GetRepUpdateEntryChains(updateEntries);
            foreach (string repChain in updateRepReduntChainHash.Keys)
            {
                DeleteAlignments(repChain.Substring(0, 4), repChain.Substring(4, repChain.Length - 4));
                ArrayList reduntChainList = (ArrayList)updateRepReduntChainHash[repChain];
                if (reduntChainList != null)
                {
                    foreach (string reduntChain in reduntChainList)
                    {
                        DeleteAlignments(reduntChain.Substring(0, 4), reduntChain.Substring(4, reduntChain.Length - 4));
                    }
                }
            }
            ArrayList repChainList = new ArrayList(updateRepReduntChainHash.Keys);
            string[] repChains = new string[repChainList.Count];
            repChainList.CopyTo(repChains);
            return repChains;
        }

        private void DeleteAlignments(string pdbId, string authChain)
        {
            string deleteString = string.Format("DELETE FROM FatcatAlignments " +
                    " WHERE (QueryEntry = '{0}' AND QueryChain = '{1}') OR " +
                    " (HitEntry = '{0}' AND HitChain = '{1}');", pdbId, authChain);
            dbQuery.Query(AppSettings.alignmentDbConnection, deleteString);
        }

        private string[] ParseDomainString(string domainString)
        {
            string[] domainFields = new string[3];
            domainFields[0] = domainString.Substring(0, 4);
            domainFields[1] = domainString.Substring(4, 1);
            domainFields[2] = domainString.Substring(5, domainString.Length - 5);
            return domainFields;
        }

        private Hashtable GetRepUpdateEntryChains(string[] updateEntries)
        {
            string queryString = "";
            Hashtable updateRepReduntChainHash = new Hashtable();
            string repChain = "";
            string reduntChain = "";
            foreach (string updateEntry in updateEntries)
            {
                queryString = string.Format("Select * From RedundantPdbChains " + 
                    " Where PdbID1 = '{0}';", updateEntry);
                DataTable repEntryChainTable = dbQuery.Query(AppSettings.alignmentDbConnection, queryString);

                foreach (DataRow repRow in repEntryChainTable.Rows)
                {
                    repChain = repRow["PdbID1"].ToString () + repRow["ChainID1"].ToString ().TrimEnd ();
                    if (repRow["PdbID2"].ToString().TrimEnd() != "-")
                    {
                        reduntChain = repRow["PdbID2"].ToString() + repRow["ChainID2"].ToString().TrimEnd();
                        if (updateRepReduntChainHash.ContainsKey(repChain))
                        {
                            ArrayList reduntChainList = (ArrayList)updateRepReduntChainHash[repChain];
                            reduntChainList.Add(reduntChain);
                        }
                        else
                        {
                            ArrayList reduntChainList = new ArrayList();
                            reduntChainList.Add(reduntChain);
                            updateRepReduntChainHash.Add(repChain, reduntChainList);
                        }
                    }
                    else // no redundant chains
                    {
                        updateRepReduntChainHash.Add(repChain, null);
                    }
                }
            }
            return updateRepReduntChainHash;
        }
        #endregion
    }
}
