using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data;
using DbLib;
using AuxFuncLib;
using ProtCidSettingsLib;

namespace BugFixingLib
{
    public class BugFixing
    {
        public BugFixing ()
        {
            Initialize();
            protcidUpdate = new DbUpdate(ProtCidSettings.protcidDbConnection);
        }

        #region delete entry info
        public void DeleteObsoleteStructureInfoInProtCID ()
        {

        }
        #endregion

        #region Pfams inconsistent
        public void FindInterfaceDomainsNotInV31 ()
        {
            StreamWriter dataWriter = new StreamWriter("InterfaceDomainsNotInPdbPfam.txt");
            string queryString = "Select Distinct PdbID From PfamDomainInterfaces;";
            DataTable entryTable = ProtCidSettings.protcidQuery.Query(queryString);
            string pdbId = "";
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                string[] interfaceDomains = GetEntryDomainsInInterfaces(pdbId);
                string[] pfamDomains = GetPfamDomains(pdbId);
                foreach (string interfaceDomain in interfaceDomains)
                {
                    if (Array.IndexOf (pfamDomains, interfaceDomain) < 0)
                    {
                        dataWriter.WriteLine(pdbId + interfaceDomain);                        
                    }
                }
                dataWriter.Flush();
            }
            dataWriter.Close();
        }

        public void InsertDomainsFromCopy ()
        {
            DbInsert pfamDbInsert = new DbInsert(ProtCidSettings.pdbfamDbConnection);
            string copyDbFile = @"X:\Firebird\Pfam30\pdbfam.fdb";
            DbConnect copyDbConnect = new DbConnect("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" + copyDbFile);
            DbQuery copyDbQuery = new DbQuery(copyDbConnect);
            StreamReader dataReader = new StreamReader("InterfaceDomainsNotInPdbPfam.txt");
            StreamWriter obsEntryWriter = new StreamWriter("ObsoleteEntries.txt");
            List<string> leftDomainList = new List<string>();
            string line = "";
            string pdbId = "";
            string domainId = "";
            bool domainAdded = false;
            List<string> obsEntryList = new List<string>();
            while ((line= dataReader.ReadLine ()) != null)
            {
                domainAdded = false;
                pdbId = line.Substring(0, 4);
                if (obsEntryList.Contains (pdbId))
                {
                    continue;
                }
                if (IsEntryObsolete (pdbId))
                {
                    obsEntryList.Add(pdbId);
                    obsEntryWriter.WriteLine(pdbId);
                    continue;
                }
                domainId = line.Substring(4, line.Length - 4);
                if (! IsDomainUsedInCurrentDb (domainId))
                {
                    DataTable domainCopyTable = GetDomainCopy(pdbId, domainId, copyDbQuery);
                    domainCopyTable.TableName = "PdbPfam";
                    if (domainCopyTable.Rows.Count > 0)
                    {
                        DataTable currDomainTable = GetCurrentDomain(pdbId);
                        if (CanDomainCopyBeAdded(domainCopyTable, currDomainTable))
                        {
                            pfamDbInsert.InsertDataIntoDBtables(domainCopyTable);
                            domainCopyTable.Clear();
                            domainAdded = true;
                        }
                    }
                }
                if (! domainAdded)
                {
                    leftDomainList.Add(line);
                }
            }
            dataReader.Close();
            obsEntryWriter.Close();

            StreamWriter dataWriter = new StreamWriter("InterfaceDomainsNotInPdbPfam_left.txt");
            foreach (string domain in leftDomainList)
            {
                dataWriter.WriteLine(domain);
            }
            dataWriter.Close();
        }

        private bool IsDomainUsedInCurrentDb (string domainId)
        {
            string querystring = string.Format("Select * From PdbPfam Where DomainID = {0};", domainId);
            DataTable domainTable = ProtCidSettings.pdbfamQuery.Query(querystring);
            if (domainTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }

        private bool CanDomainCopyBeAdded (DataTable oldDomainTable, DataTable currentDomainTable)
        {
            DataRow oldDomainRow = oldDomainTable.Rows[0];
            DataRow[] currDomainRows = currentDomainTable.Select(string.Format ("EntityID = '{0}'", oldDomainRow["EntityID"].ToString ()));
            foreach (DataRow currDomainRow in currDomainRows)
            {
                double[] covs = GetCoverage(oldDomainRow, currDomainRow);
                if (covs[0] > 0.4 || covs[1] > 0.4)
                {
                    return false;
                }
            }
            return true;
        }

        private double[] GetCoverage (DataRow domainRow1, DataRow domainRow2)
        {
            int seqStart1 = Convert.ToInt32(domainRow1["SeqStart"].ToString());
            int seqEnd1 = Convert.ToInt32(domainRow1["SeqEnd"].ToString());
            int seqStart2 = Convert.ToInt32(domainRow2["SeqStart"].ToString ());
            int seqEnd2 = Convert.ToInt32(domainRow2["SeqEnd"].ToString ());
            int overlap = Math.Min(seqEnd2, seqEnd1) - Math.Max(seqStart2, seqStart1) + 1;
            double[] covs = new double[2];
            covs[0] = (double)overlap / (double)(seqEnd1 - seqStart1 + 1);
            covs[1] = (double)overlap / (double)(seqEnd2 - seqStart2 + 1);

            return covs;
        }

        private DataTable GetCurrentDomain(string pdbId)
        {
            string queryString = string.Format("Select PdbID, EntityID, DomainID, SeqStart, SeqEnd From PdbPfam Where PdbID = '{0}';", pdbId);
            DataTable entryDomainTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            return entryDomainTable;
        }

        private DataTable GetDomainCopy (string pdbId, string domainId, DbQuery copyDbQuery)
        {
            string queryString = string.Format("Select * From PdbPfamCopy Where PdbID = '{0}' AND DOmainID = {1};", pdbId, domainId);
            DataTable domainCopyTable = copyDbQuery.Query(queryString);
            return domainCopyTable;
        }

        private bool IsEntryObsolete (string pdbId)
        {
            string queryString = string.Format("Select PdbID From PdbEntry Where PdbID = '{0}';", pdbId);
            DataTable entryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (entryTable.Rows.Count == 0)
            {
                return true;
            }
            return false;
        }

        private string[] GetEntryDomainsInInterfaces (string pdbId)
        {
            string queryString = string.Format("Select DomainID1, DomainID2 From PfamDomainInterfaces Where PdbID = '{0}';", pdbId);
            DataTable interfaceDomainsTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> interfaceDomainList = new List<string>();
            string domainId = "";
            foreach (DataRow domainRow in interfaceDomainsTable.Rows)
            {
                domainId = domainRow["DomainID1"].ToString();
                if (! interfaceDomainList.Contains (domainId))
                {
                    interfaceDomainList.Add(domainId);
                }

                domainId = domainRow["DomainID2"].ToString();
                if (!interfaceDomainList.Contains(domainId))
                {
                    interfaceDomainList.Add(domainId);
                }
            }
            return interfaceDomainList.ToArray();
        }

        private string[] GetPfamDomains (string pdbId)
        {
            string queryString = string.Format("Select Distinct DomainID From PdbPfam Where PdbID = '{0}';", pdbId);
            DataTable pfamDomainTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string[] domains = new string[pfamDomainTable.Rows.Count];
            int count = 0;
            foreach (DataRow domainRow in pfamDomainTable.Rows)
            {
                domains[count] = domainRow["DomainID"].ToString();
                count++;
            }
            return domains;
        }

        public void FindPfamsNotInV31 ()
        {
            string prePfamDb = @"X:\Firebird\Pfam30\Pdbfam.fdb";
            DbConnect prePfamConnect = new DbConnect("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                    prePfamDb);
            DbQuery preDbQuery = new DbQuery(prePfamConnect);

            StreamWriter dataWriter = new StreamWriter("PfamNotInv31_PfamAccComp.txt");
            string queryString = "Select Distinct FamilyCode1 From PfamDomainFamilyRelation;";
            DataTable pfam1Table = ProtCidSettings.protcidQuery.Query(queryString);
            queryString = "Select Distinct FamilyCode2 From PfamDomainFamilyRelation;";
            DataTable pfam2Table = ProtCidSettings.protcidQuery.Query(queryString);

            List<string> notPfamList = new List<string>();
            List<string> pfamList = new List<string>();
            string pfamId = "";
            string currrentPfamId = "";
            string prePfamAcc = "";
            foreach (DataRow pfamRow in pfam1Table.Rows)
            {
               pfamId = pfamRow["FamilyCode1"].ToString().TrimEnd();
               pfamList.Add(pfamId);
                if (! IsPfamInCurrentVersion (pfamId))
                {
                    notPfamList.Add(pfamId);
                    prePfamAcc = GetPrevPfamAcc(pfamId, preDbQuery);
                    if (IsPfamAccInCurrentVersion(prePfamAcc, out currrentPfamId))
                    {
                        dataWriter.WriteLine(pfamId + "\t" + prePfamAcc + "\tYes\t" + currrentPfamId);
                    }
                    else
                    {
                        dataWriter.WriteLine(pfamId + "\t" + prePfamAcc + "\tNo");
                    }
                }
            }

            foreach (DataRow pfamRow in pfam2Table.Rows)
            {
                pfamId = pfamRow["FamilyCode2"].ToString().TrimEnd();
                if (! pfamList.Contains (pfamId))
                {
                    pfamList.Add(pfamId);
                    if (! IsPfamInCurrentVersion (pfamId))
                    {
                        notPfamList.Add(pfamId);
                        prePfamAcc = GetPrevPfamAcc(pfamId, preDbQuery);
                        if (IsPfamAccInCurrentVersion(prePfamAcc, out currrentPfamId))
                        {
                            dataWriter.WriteLine(pfamId + "\t" + prePfamAcc + "\tYes\t" + currrentPfamId);
                        }
                        else
                        {
                            dataWriter.WriteLine(pfamId + "\t" + prePfamAcc + "\tNo");
                        }
                    }
                }
            }
            dataWriter.Close();
        }

        private bool IsPfamInCurrentVersion (string pfamId)
        {
            string querystring = string.Format("Select Pfam_Acc From PfamHmm Where Pfam_ID = '{0}';", pfamId);
            DataTable pfamAccTable = ProtCidSettings.pdbfamQuery.Query(querystring);
            if (pfamAccTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }

        private string GetPrevPfamAcc (string pfamId, DbQuery preDbQuery)
        {
            string querystring = string.Format("Select Pfam_Acc From PfamHmm Where Pfam_ID = '{0}';", pfamId);
            DataTable pfamAccTable = preDbQuery.Query(querystring);
            if (pfamAccTable.Rows.Count > 0)
            {
                return pfamAccTable.Rows[0]["Pfam_Acc"].ToString().TrimEnd();
            }
            return "";
        }

        private bool IsPfamAccInCurrentVersion(string pfamAcc, out string currentPfamId)
        {
            currentPfamId = "";
            string querystring = string.Format("Select Pfam_Id From PfamHmm Where Pfam_Acc = '{0}';", pfamAcc);
            DataTable pfamAccTable = ProtCidSettings.pdbfamQuery.Query(querystring);
            if (pfamAccTable.Rows.Count > 0)
            {
                currentPfamId = pfamAccTable.Rows[0]["Pfam_ID"].ToString().TrimEnd();
                return true;
            }
            return false;
        }
        #endregion

        #region entry list
        public void GetEntryList ()
        {
            List<string> entryList = new List<string>();
            StreamWriter datawriter = new StreamWriter("NewPfamUpdateEntries.txt");
            StreamReader pfamReader = new StreamReader("PfamNotInv31_PfamAccComp.txt");
            string line = "";
            string obsPfam = "";
            string currPfam = "";
            while ((line = pfamReader.ReadLine ()) != null)
            {
                string[] fields = line.Split('\t');
                obsPfam = fields[0];
                if (fields.Length == 3)
                {
                    string[] pfamGroupEntries = GetPfamGroupEntries(obsPfam);
                    // string[] pfamRelEntries = GetDomainRelationEntries(obsPfam);
                    foreach (string pdbId in pfamGroupEntries)
                    {                        
                        if (!entryList.Contains(pdbId))
                        {
                            entryList.Add(pdbId);
                            datawriter.WriteLine(pdbId);
                        }
                    }
                }
                
                if(fields.Length == 4)
                {
                    currPfam = fields[3];
                    string[] pfamGroupEntries = GetPfamGroupEntries(currPfam);
       //             string[] pfamRelEntries = GetDomainRelationEntries(currPfam);
                    foreach (string pdbId in pfamGroupEntries)
                    {
                        if (!entryList.Contains(pdbId))
                        {
                            entryList.Add(pdbId);
                            datawriter.WriteLine(pdbId);
                        }
                    }
                }
            }
            pfamReader.Close();
            datawriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private string[] GetPfamGroupEntries (string pfamId)
        {
            string queryString = string.Format ("Select Distinct GroupSeqID From PfamSuperGroups " +
                    " Where ChainRelPfamArch Like '%({0})%';", pfamId);
            DataTable groupTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> entryList = new List<string>();
            foreach (DataRow groupRow in groupTable.Rows)
            {
                string[] groupEntries = GetGroupEntries(Convert.ToInt32(groupRow["GroupSeqID"].ToString()));
                foreach (string pdbId in groupEntries)
                {
                    if (! entryList.Contains (pdbId))
                    {
                        entryList.Add(pdbId);
                    }
                }
            }
            return entryList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        private string[] GetGroupEntries (int groupId)
        {
            List<string> entryList = new List<string>();
            string pdbId = "";
            string queryString = string.Format("Select Distinct PdbID From PfamHomoSeqInfo Where GroupSeqID = {0};", groupId);
            DataTable entryTable = ProtCidSettings.protcidQuery.Query(queryString);
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                entryList.Add(pdbId);
            }
            queryString = string.Format("Select Distinct PdbID2 As PdbID From PfamHomoRepEntryAlign Where GroupSeqID = {0};", groupId);
            entryTable = ProtCidSettings.protcidQuery.Query(queryString);
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                if (!entryList.Contains(pdbId))
                {
                    entryList.Add(pdbId);
                }
            }
            return entryList.ToArray();
        }

        private string[] GetDomainRelationEntries (string pfamId)
        {           
            string queryString = string.Format("Select Distinct PdbID From PfamDomainInterfaces Where RelSeqID IN " +
                    "(Select Distinct RelSeqID From PfamDomainFamilyRelation " +
                    "Where FamilyCode1 = '{0}' OR FamilyCode2 = '{0}');", pfamId);
            DataTable entryTable = ProtCidSettings.protcidQuery.Query(queryString);
            string[] pfamRelEntries = new string[entryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pfamRelEntries[count] = entryRow["PdbID"].ToString();
                count++;
            }
            return pfamRelEntries;
        }
        #endregion

        #region update pfams to current version
        private DbUpdate protcidUpdate = null;
        string[] updateTables = { "PfamGroups", "PfamChainArchRelation", "PfamChainPairInPdb", "PfamDomainChainArchRelation", "PfamSuperGroups", 
                                         "PfamDomainFamilyRelation", "PfamInterfaceHmmSiteComp", "PfamLigandClusters", 
                                "PfamLigandClustersHmm", "PfamLigandComAtoms", "PfamLigandComHmms", "PfamLigands", "PfamLigandsPairSumInfo", 
                                      "PfamPepClusterSumInfo", "PfamPepInterfaceClusters", "PfamPeptideHmmSites", "PfamPeptideInterfaces"};
        string[] updateTableColumns = {"EntryPfamArch", "ChainArch1,ChainArch2", "ChainArch1,ChainArch2", "ChainArch1,ChainArch2",
                                     "ChainRelPfamArch", "FamilyCode1,FamilyCode2", 
                                     "PfamID", "PfamID", "PfamID", "PfamID", "PfamID", "PfamID", "PfamID", "PfamID", "PfamID", "PfamID", "PfamID"};
        public void UpdatePfamsToCurrentPfams ()
        {
            StreamReader pfamReader = new StreamReader("PfamNotInv31_PfamAccComp.txt");
             string line = "";
            string obsPfam = "";
            string currPfam = "";
            while ((line = pfamReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                obsPfam = fields[0];
                if (fields.Length == 4)
                {
                    currPfam = fields[3];
                    UpdateSamePfamFromOrgToCurr(obsPfam, currPfam);
                }
            }
            pfamReader.Close();
        }

        
        private void UpdateSamePfamFromOrgToCurr (string orgPfam, string currPfam)
        {
            string updateString = "";
            string tableName = "";
            string updateColumnField = "";
            for (int i = 0; i < updateTables.Length; i ++)
            {
                tableName = updateTables[i];
                updateColumnField = updateTableColumns[i];
                if (updateColumnField.IndexOf(",") > -1)
                {
                    string[] updateColumns = updateColumnField.Split(',');
                    if (tableName == "PfamDomainFamilyRelation")
                    {
                        updateString = string.Format("Update {0} Set {1} = replace ({1}, '{2}', '{3}') Where {1} = '{2}';",
                            tableName, updateColumns[0], orgPfam, currPfam);
                        protcidUpdate.Update(updateString);

                        updateString = string.Format("Update {0} Set {1} = replace ({1}, '{2}', '{3}') Where {1} = '{2}';",
                            tableName, updateColumns[1], orgPfam, currPfam);
                        protcidUpdate.Update(updateString);
                    }
                    else
                    {
                        updateString = string.Format("Update {0} Set {1} = replace({1}, '{2}', '{3}') Where {1} Like '%({2})%';",
                            tableName, updateColumns[0], orgPfam, currPfam);
                        protcidUpdate.Update(updateString);

                        updateString = string.Format("Update {0} Set {1} = replace({1}, '{2}', '{3}') Where {1} Like '%({2})%';",
                            tableName, updateColumns[1], orgPfam, currPfam);
                        protcidUpdate.Update(updateString);
                    }
                }
                else
                {
                    if (tableName == "PfamGroups" || tableName == "PfamSuperGroups")
                    {
                        updateString = string.Format("Update {0} Set {1} = replace({1}, '{2}', '{3}') Where {1} Like '%({2})%';",
                            tableName, updateColumnField, orgPfam, currPfam);
                    }
                    else
                    {
                        updateString = string.Format("Update {0} Set {1} = replace({1}, '{2}', '{3}') Where {1} = '{2}';",
                            tableName, updateColumnField, orgPfam, currPfam);
                    }
                    protcidUpdate.Update(updateString);
                }                
            }
        }

        public void SaveUpdateGroupIds ()
        {
            string orgCurrPfamFile = "PfamNotInv31_PfamAccComp.txt";
            string[] orgPfams = ReadOrgPfams(orgCurrPfamFile);
            string queryString = "";
            StreamWriter updateEntryGroupWriter = new StreamWriter("UpdateEntryGroups.txt");
            StreamWriter updateChainGroupWriter = new StreamWriter("UpdateChainGroups.txt");
            StreamWriter updateDomainGroupWriter = new StreamWriter("UpdateDomainGroups.txt");
            string[] groupTables = { "PfamGroups", "PfamSuperGroups", "PfamDomainFamilyRelation" };
            foreach (string orgPfam in orgPfams)
            {
                queryString = string.Format("Select Distinct GroupSeqID, EntryPfamArch From PfamGroups Where EntryPfamArch Like '%({0})%';", orgPfam);
                DataTable entryGroupTable = ProtCidSettings.protcidQuery.Query(queryString);
                foreach (DataRow groupRow in entryGroupTable.Rows)
                {
                    updateEntryGroupWriter.WriteLine(groupRow["GroupSeqID"].ToString() + "\t" + groupRow["EntryPfamArch"].ToString ());
                }

                queryString = string.Format("Select Distinct SuperGroupSeqID, ChainRelPfamArch From PfamSuperGroups Where ChainRelPfamArch Like '%({0})%';", orgPfam);
                DataTable chainGroupTable = ProtCidSettings.protcidQuery.Query(queryString);
                foreach (DataRow groupRow in chainGroupTable.Rows)
                {
                    updateChainGroupWriter.WriteLine(groupRow["SuperGroupSeqID"].ToString() + "\t" + groupRow["ChainRelPfamArch"].ToString ());
                }

                queryString = string.Format("Select Distinct RelSeqID, FamilyCode1, FamilyCode2 From PfamDomainFamilyRelation Where FamilyCode1 = '{0}' OR FamilyCode2 = '{0}';", orgPfam);
                DataTable domainGroupTable = ProtCidSettings.protcidQuery.Query(queryString);
                foreach (DataRow groupRow in domainGroupTable.Rows)
                {
                    updateDomainGroupWriter.WriteLine(groupRow["RelSeqID"].ToString() + "\t" + 
                        groupRow["FamilyCode1"].ToString () + "\t" + groupRow["FamilyCode2"].ToString ());
                }

                updateEntryGroupWriter.Flush();
                updateChainGroupWriter.Flush();
                updateDomainGroupWriter.Flush();
            }
            updateEntryGroupWriter.Close();
            updateChainGroupWriter.Close();
            updateDomainGroupWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] ReadOrgPfams (string orgPfamFile)
        {
            StreamReader pfamReader = new StreamReader(orgPfamFile);
            string line = "";
            string obsPfam = "";
            List<string> orgPfamList = new List<string>();
            while ((line = pfamReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                obsPfam = fields[0];
                if (fields.Length == 4)
                {
                    orgPfamList.Add(obsPfam);
                }
            }
            pfamReader.Close();
            return orgPfamList.ToArray();
        }
        #endregion

        #region deletion
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] ReadObsPfams(string orgPfamFile)
        {
            StreamReader pfamReader = new StreamReader(orgPfamFile);
            string line = "";
            string obsPfam = "";
            List<string> obsPfamList = new List<string>();
            while ((line = pfamReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                obsPfam = fields[0];
                if (fields.Length == 3)
                {
                    obsPfamList.Add(obsPfam);
                }
            }
            pfamReader.Close();
            return obsPfamList.ToArray();
        }
        /// <summary>
        /// 
        /// </summary>
        public void SaveDeletedGroupIds()
        {
            string orgCurrPfamFile = "PfamNotInv31_PfamAccComp.txt";
            string[] obsPfams = ReadObsPfams(orgCurrPfamFile);
            string queryString = "";
            StreamWriter deleteEntryGroupWriter = new StreamWriter("DeletedEntryGroups.txt");
            StreamWriter deleteChainGroupWriter = new StreamWriter("DeletedChainGroups.txt");
            StreamWriter deleteDomainGroupWriter = new StreamWriter("DeleteDomainGroups.txt");
            string[] groupTables = { "PfamGroups", "PfamSuperGroups", "PfamDomainFamilyRelation" };
            foreach (string obsPfam in obsPfams)
            {
                queryString = string.Format("Select Distinct GroupSeqID, EntryPfamArch From PfamGroups Where EntryPfamArch Like '%({0})%';", obsPfam);
                DataTable entryGroupTable = ProtCidSettings.protcidQuery.Query(queryString);
                foreach (DataRow groupRow in entryGroupTable.Rows)
                {
                    deleteEntryGroupWriter.WriteLine(groupRow["GroupSeqID"].ToString() + "\t" + groupRow["EntryPfamArch"].ToString ());
                }

                queryString = string.Format("Select Distinct SuperGroupSeqID, ChainRelPfamArch From PfamSuperGroups Where ChainRelPfamArch Like '%({0})%';", obsPfam);
                DataTable chainGroupTable = ProtCidSettings.protcidQuery.Query(queryString);
                foreach (DataRow groupRow in chainGroupTable.Rows)
                {
                    deleteChainGroupWriter.WriteLine(groupRow["SuperGroupSeqID"].ToString() + "\t" + groupRow["ChainRelPfamArch"].ToString ());
                }

                queryString = string.Format("Select Distinct RelSeqID, FamilyCode1, FamilyCode2 From PfamDomainFamilyRelation Where FamilyCode1 = '{0}' OR FamilyCode2 = '{0}';", obsPfam);
                DataTable domainGroupTable = ProtCidSettings.protcidQuery.Query(queryString);
                foreach (DataRow groupRow in domainGroupTable.Rows)
                {
                    deleteDomainGroupWriter.WriteLine(groupRow["RelSeqID"].ToString() + "\t" + groupRow["FamilyCode1"].ToString () + "\t" + groupRow["FamilyCode2"].ToString ());
                }

                deleteEntryGroupWriter.Flush();
                deleteChainGroupWriter.Flush();
                deleteDomainGroupWriter.Flush();
            }
            deleteEntryGroupWriter.Close();
            deleteChainGroupWriter.Close();
            deleteDomainGroupWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        public void DeleteObsPfamGroups ()
        {
            string orgCurrPfamFile = "PfamNotInv31_PfamAccComp.txt";
            string[] obsPfams = ReadObsPfams(orgCurrPfamFile);
            foreach (string obsPfam in obsPfams)
            {
                DeleteObsEntryPfamInfo(obsPfam);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obsPfam"></param>
        private void DeleteObsEntryPfamInfo(string obsPfam)
        {
            int[] deleteGroupIds = GetGroupIds(obsPfam);
            foreach (int groupId in deleteGroupIds)
            {
                DeletePfamEntryGroupInfo(groupId);
            }
        }

        private int[] GetGroupIds (string obsPfam)
        {            
            string queryString = string.Format("Select Distinct GroupSeqID From PfamGroups Where EntryPfamArch Like '%({0})%';", obsPfam);
            DataTable groupTable = ProtCidSettings.protcidQuery.Query(queryString);
            int count = 0;
            int[] groupIds = new int[groupTable.Rows.Count];
            foreach (DataRow groupRow in groupTable.Rows)
            {
                groupIds[count] = Convert.ToInt32(groupRow["GroupSeqID"].ToString ());
                count++;
            }
            return groupIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupSeqId"></param>
        private void DeletePfamEntryGroupInfo(int groupSeqId)
        {
            string deleteString = string.Format("Delete From PfamGroups Where GroupSeqID = {0};", groupSeqId);
            protcidUpdate.Delete(deleteString);

            deleteString = string.Format("Delete From PfamHomoSeqInfo Where GroupSeqID = {0};", groupSeqId);
            protcidUpdate.Delete(deleteString);

            deleteString = string.Format("Delete From PfamHomoRepEntryAlign Where GroupSeqID = {0};", groupSeqId);
            protcidUpdate.Delete(deleteString);

            deleteString = string.Format("Delete From PfamHomoGroupEntryAlign Where GroupSeqID = {0};", groupSeqId);
            protcidUpdate.Delete(deleteString);

            deleteString = string.Format("Delete From PfamNonRedundantCfGroups Where GroupSeqID = {1};", groupSeqId);
            protcidUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);

            deleteString = string.Format("Delete From PfamReduntCrystForms Where GroupSeqID = {1};", groupSeqId);
            protcidUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);

            deleteString = string.Format("Delete From PfamSgInterfaces Where GroupSeqID = {1};", groupSeqId);
            protcidUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);

            deleteString = string.Format("Delete From PfamInterfaceClusters Where GroupSeqID = {1};", groupSeqId);
            protcidUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);

        }
        #endregion

        #region get list of entries with pfam-peptide messed
        public void GetMessyEntriesWithPfamPepInterfaces ()
        {
            StreamWriter entryWriter = new StreamWriter("PepInterfaceMessyEntries.txt");
            string queryString = "Select Distinct PdbID From PfamPeptideInterfaces;";
            DataTable pepEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            string pdbId = "";
            foreach (DataRow entryRow in pepEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                if (AreEntryPepInterfacesOverlapDomainInterfaces (pdbId))
                {
                    entryWriter.WriteLine(pdbId);
                }
            } 
            AddNoPfamPepButWithChainPepEntries(entryWriter);
            entryWriter.Close();
        }

        public void AddNoPfamPepButWithChainPepEntries (StreamWriter entryWriter)
        {
            string queryString = "Select Distinct PdbID From PfamPeptideInterfaces;";
            DataTable pepEntryTable = ProtCidSettings.protcidQuery.Query(queryString);

            queryString = "Select Distinct PdbID From ChainPeptideInterfaces;";
            DataTable chainPepEntryTable = ProtCidSettings.buCompQuery.Query(queryString);
            foreach (DataRow entryRow in chainPepEntryTable.Rows)
            {
                DataRow[] entryRows = pepEntryTable.Select(string.Format ("PdbID = '{0}'", entryRow["PdbID"].ToString ()));
                if (entryRows.Length == 0)
                {
                    entryWriter.WriteLine(entryRow["PdbID"]);
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool AreEntryPepInterfacesOverlapDomainInterfaces (string pdbId)
        {
            string queryString = string.Format("Select PdbID, InterfaceID, DomainInterfaceID, AsymChain, PepAsymChain " +
                " From PfamPeptideInterfaces Where PdbID = '{0}';", pdbId);
            DataTable pepInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
            queryString = string.Format("Select PdbID, InterfaceID, DomainInterfaceID, AsymChain1, AsymChain2 " +
                " From PfamDomainInterfaces Where PdbID = '{0}';", pdbId);
            DataTable domainInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
            bool isMessy = false;
            foreach (DataRow pepInterfaceRow in pepInterfaceTable.Rows)
            {
                DataRow[] domainInterfaceRows = domainInterfaceTable.Select
                    (string.Format ("DomainInterfaceID = '{0}'", pepInterfaceRow["DomainInterfaceID"].ToString ()));
                if (domainInterfaceRows.Length > 0)
                {
                    if (pepInterfaceRow["AsymChain"].ToString () == domainInterfaceRows[0]["AsymChain1"].ToString () && 
                        pepInterfaceRow["PepAsymChain"].ToString ()== domainInterfaceRows[0]["AsymChain2"].ToString ())
                    {
                        continue;
                    }
                    else if (pepInterfaceRow["AsymChain"].ToString() == domainInterfaceRows[0]["AsymChain2"].ToString () &&
                        pepInterfaceRow["PepAsymChain"].ToString() == domainInterfaceRows[0]["AsymChain1"].ToString())
                    {
                        continue;
                    }
                    else
                    {
                        isMessy = true;
                    }
                }
            }
            return isMessy;
        }
        #endregion

        #region list of entries inconsistent pfam domain files and pdbpfamchain
        /// <summary>
        /// 
        /// </summary>
        public void GetEntriesWithInconsistentDomainAndFileInfo ()
        {
            StreamWriter dataWriter = new StreamWriter("EntriesDomainFileInfoInconsistent2.txt");
            string queryString = "Select Distinct PdbID From PdbPfamChain;";
            DataTable entryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string pdbId = "";
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                string[] fileInconsistentDomains = GetDomainFileInconsistentChainDomains(pdbId);
                if (fileInconsistentDomains.Length > 0)
                {
                    dataWriter.WriteLine(pdbId + "," + ParseHelper.FormatStringFieldsToString(fileInconsistentDomains));
                }
            }
            dataWriter.Close();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetDomainFileInconsistentChainDomains (string pdbId)
        {
            string queryString = string.Format("Select Distinct ChainDomainID From PdbPfamDomainFileInfo Where PdbID = '{0}';", pdbId);
            DataTable chainDomainIdTable = ProtCidSettings.pdbfamQuery.Query(queryString);

            queryString = string.Format ("Select Distinct ChainDomainID, DomainID, EntityID, AsymChain From PdbPfamChain Where PdbID = '{0}' Order By ChainDomainId, AsymChain;", pdbId);
            DataTable domainChainTable = ProtCidSettings.pdbfamQuery.Query(queryString);

            queryString = string.Format("Select Distinct ChainDomainID, DomainID, EntityID, AsymChain From PdbPfamDomainFileInfo Where PdbID = '{0}' Order By ChainDomainId, AsymChain;", pdbId);
            DataTable domainFileTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string chainDomainRowsString = "";
            string fileDomainRowsString = "";
            List<string> chainDomainList = new List<string>();
            foreach (DataRow chainDomainIdRow in chainDomainIdTable.Rows)
            {
                DataRow[] chainDomainRows = domainChainTable.Select("ChainDomainID = " + chainDomainIdRow["ChainDomainID"].ToString ());
                DataRow[] fileDomainRows = domainFileTable.Select("ChainDomainID = " + chainDomainIdRow["ChainDomainID"].ToString ());
                chainDomainRowsString = ParseHelper.FormatDataRows(chainDomainRows);
                fileDomainRowsString = ParseHelper.FormatDataRows(fileDomainRows);
                if (chainDomainRowsString != fileDomainRowsString)
                {
                    chainDomainList.Add(chainDomainIdRow["ChainDomainID"].ToString());
                }
            }
            return chainDomainList.ToArray();
        }       
        #endregion

        #region check inpdb/inpisa
        public void CheckChainInterfaceInPdbPisa()
        {
            string dataDir = @"X:\Qifang\Paper\protcid_update\data_v31";
            string domainInPdbPisaFile = Path.Combine(dataDir, "ChainInterfacesInPdbPisaCheck.txt");
            StreamWriter dataWriter = new StreamWriter(domainInPdbPisaFile);
            string tableName = "PfamSuperClusterEntryInterfaces";
            string queryString = string.Format("Select SuperGroupSeqID, PdbID, InterfaceID, InPdb, InPisa, InAsu From {0};", tableName);
            DataTable chainInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
            string pdbId = "";
            int chainInterfaceId = 0;
            string inPdb = "0";
            string inPisa = "0";
            bool samePdb = true;
            bool samePisa = true;
            string dataLine = "";
            foreach (DataRow interfaceRow in chainInterfaceTable.Rows)
            {
                pdbId = interfaceRow["PdbID"].ToString();
                chainInterfaceId = Convert.ToInt32(interfaceRow["InterfaceID"].ToString());
                inPdb = interfaceRow["InPDB"].ToString();
                inPisa = interfaceRow["InPisa"].ToString();
                dataLine = ParseHelper.FormatDataRow(interfaceRow);
                samePdb = true;
                samePisa = true;
                if (IsChainInterfaceInBAs(pdbId, chainInterfaceId, "pdb"))
                {
                    if (inPdb == "0")
                    {
                        samePdb = false;
                        dataLine += "\t1";
                    }
                    else
                    {
                        dataLine += "\t=";
                    }
                }
                else
                {
                    if (inPdb == "1")
                    {
                        samePdb = false;
                        dataLine += "\t0";
                    }
                    else
                    {
                        dataLine += "\t=";
                    }
                }


                if (IsChainInterfaceInBAs(pdbId, chainInterfaceId, "pisa"))
                {
                    if (inPisa == "0")
                    {
                        samePisa = false;
                        dataLine += "\t1";
                    }
                    else
                    {
                        dataLine += "\t=";
                    }
                }
                else
                {
                    if (inPisa == "1")
                    {
                        samePisa = false;
                        dataLine += "\t0";
                    }
                    else
                    {
                        dataLine += "\t=";
                    }
                }

                if (!samePdb || !samePisa)
                {
                    dataWriter.WriteLine(dataLine);
                }
            }
            dataWriter.Close();
        }

        public void CheckDomainInterfacesInPdbPisa()
        {
            string dataDir = @"X:\Qifang\Paper\protcid_update\data_v31";
            string domainInPdbPisaFile = Path.Combine(dataDir, "DomainInPdbPisaCheck2.txt");
            StreamWriter dataWriter = new StreamWriter(domainInPdbPisaFile);
            string tableName = "PfamDomainClusterInterfaces";
            string queryString = string.Format("Select RelSeqId, PdbID, DomainInterfaceID, InPdb, InPisa, InAsu From {0};", tableName);
            DataTable relInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
            string pdbId = "";
            int domainInterfaceId = 0;
            string inPdb = "0";
            string inPisa = "0";
            bool samePdb = true;
            bool samePisa = true;
            string dataLine = "";
            foreach (DataRow interfaceRow in relInterfaceTable.Rows)
            {
                pdbId = interfaceRow["PdbID"].ToString();
                domainInterfaceId = Convert.ToInt32(interfaceRow["DomainInterfaceID"].ToString());
                inPdb = interfaceRow["InPDB"].ToString();
                inPisa = interfaceRow["InPisa"].ToString();
                dataLine = ParseHelper.FormatDataRow(interfaceRow);
                samePdb = true;
                samePisa = true;
                if (IsDomainInterfaceInBAs(pdbId, domainInterfaceId, "pdb"))
                {
                    if (inPdb == "0")
                    {
                        samePdb = false;
                        dataLine += "\t1";
                    }
                    else
                    {
                        dataLine += "\t=";
                    }
                }
                else
                {
                    if (inPdb == "1")
                    {
                        samePdb = false;
                        dataLine += "\t0";
                    }
                    else
                    {
                        dataLine += "\t=";
                    }
                }


                if (IsDomainInterfaceInBAs(pdbId, domainInterfaceId, "pisa"))
                {
                    if (inPisa == "0")
                    {
                        samePisa = false;
                        dataLine += "\t1";
                    }
                    else
                    {
                        dataLine += "\t=";
                    }
                }
                else
                {
                    if (inPisa == "1")
                    {
                        samePisa = false;
                        dataLine += "\t0";
                    }
                    else
                    {
                        dataLine += "\t=";
                    }
                }

                if (!samePdb || !samePisa)
                {
                    dataWriter.WriteLine(dataLine);
                }
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceID"></param>
        /// <param name="baType"></param>
        /// <returns></returns>
        private bool IsDomainInterfaceInBAs(string pdbId, int domainInterfaceID, string baType)
        {
            string queryString = string.Format("Select Qscore From Cryst{0}BuDomainInterfaceComp Where PdbID = '{1}' AND DomainInterfaceID = {2} Order By Qscore DESC;",
                baType, pdbId, domainInterfaceID);
            DataTable qscoreTable = ProtCidSettings.protcidQuery.Query(queryString);
            if (qscoreTable.Rows.Count > 0)
            {
                double qscore = Convert.ToDouble(qscoreTable.Rows[0]["Qscore"].ToString());
                if (qscore >= 0.45)
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsChainInterfaceInBAs(string pdbId, int interfaceId, string baType)
        {
            string queryString = string.Format("Select Qscore From Cryst{0}BuInterfaceComp Where PdbID = '{1}' AND InterfaceID = {2} Order By Qscore DESC;",
                baType, pdbId, interfaceId);
            DataTable qscoreTable = ProtCidSettings.protcidQuery.Query(queryString);
            if (qscoreTable.Rows.Count > 0)
            {
                double qscore = Convert.ToDouble(qscoreTable.Rows[0]["Qscore"].ToString());
                if (qscore >= 0.45)
                {
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region initialize
        private void Initialize()
        {
            if (ProtCidSettings.dirSettings == null)
            {
                ProtCidSettings.LoadDirSettings();
            }

            if (ProtCidSettings.protcidDbConnection == null)
            {
                ProtCidSettings.protcidDbConnection = new DbConnect("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                    ProtCidSettings.dirSettings.protcidDbPath);
                //          ProtCidSettings.protcidDbConnection = new DbConnect ("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=X:\\Firebird\\Pfam30\\protcid.fdb");
            }
            if (ProtCidSettings.pdbfamDbConnection == null)
            {
                ProtCidSettings.pdbfamDbConnection = new DbConnect("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                    ProtCidSettings.dirSettings.pdbfamDbPath);
            }
            if (ProtCidSettings.alignmentDbConnection == null)
            {
                ProtCidSettings.alignmentDbConnection = new DbConnect("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                    ProtCidSettings.dirSettings.alignmentDbPath);
            }

            ProtCidSettings.buCompConnection = new DbConnect();
            ProtCidSettings.buCompConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                ProtCidSettings.dirSettings.baInterfaceDbPath;

            ProtCidSettings.protcidQuery = new DbQuery(ProtCidSettings.protcidDbConnection);
            ProtCidSettings.pdbfamQuery = new DbQuery(ProtCidSettings.pdbfamDbConnection);
            ProtCidSettings.buCompQuery = new DbQuery(ProtCidSettings.buCompConnection);
            ProtCidSettings.alignmentQuery = new DbQuery(ProtCidSettings.alignmentDbConnection);
        }
        #endregion
      
        #region fix histone pfam assignments
        private string[] H4 = { "H4_CHICK", "H4_DROME", "H4_HUMAN", "H4_MOUSE", "H4_XENLA", "H4_YEAST"};
        private string[] H3 = { "H31T_HUMAN","H31_HUMAN","H31_MOUSE","H31_SCHPO","H32_ARATH","H32_CHICK","H32_HUMAN","H32_MOUSE",
                                "H32_XENLA","H33_ARATH","H33_CHICK","H33_HUMAN","H33_MOUSE","H33_XENLA","H33_XENTR","H3C_HUMAN",
                                "H3C_XENLA","H3_CAEEL","H3_DROME","H3_KLULA","H3_STRPU","H3_VACCW","H3_YEAST"};
        private string[] H2A = {"H2A1A_HUMAN","H2A1B_HUMAN","H2A1D_HUMAN","H2A1D_MOUSE","H2A1H_MOUSE","H2A1_HUMAN","H2A1_XENLA","H2A1_YEAST","H2A2_YEAST",
                                "H2A4_CHICK","H2AJ_HUMAN","H2AV_HUMAN","H2AW_HUMAN","H2AX_HUMAN","H2AY_HUMAN","H2AY_RAT","H2AZ_HUMAN","H2AZ_YEAST","H2A_DROME" };
        private string[] H2B = {"H2B11_XENLA","H2B1A_HUMAN","H2B1A_MOUSE","H2B1B_HUMAN","H2B1C_HUMAN","H2B1J_HUMAN","H2B1K_HUMAN","H2B1_CHICK","H2B1_YEAST",
                                "H2B2E_HUMAN","H2B2_YEAST","H2B3A_MOUSE","H2B5_CHICK","H2B7_CHICK","H2BD96_9CAUD","H2B_DROME"};
        public void PrintEntityWithHistoneGnNoPfam ()
        {
            List<string> entityList = new List<string>();
            List<string> h4EntityList = GetEntityWithUnpCodesNoHistonePfam(H4);
            AddFirstListToSecond(h4EntityList, entityList);
            List<string> h3EntityList = GetEntityWithUnpCodesNoHistonePfam(H3);
            AddFirstListToSecond(h3EntityList, entityList);
            List<string> h2AEntityList = GetEntityWithUnpCodesNoHistonePfam(H2A);
            AddFirstListToSecond(h2AEntityList, entityList);
            List<string> h2BEntityList = GetEntityWithUnpCodesNoHistonePfam(H2B);
            AddFirstListToSecond(h2BEntityList, entityList);

            StreamWriter entityWriter = new StreamWriter("HistoneEntitiesNoPfam.txt");
            string pdbId = "";
            int entityId = 0;
            string unpCode = "";
            string crc = "";
            foreach (string entity in entityList)
            {
                pdbId = entity.Substring(0, 4);
                entityId = Convert.ToInt32(entity.Substring(4, entity.Length - 4));

                
                string[] pfams = GetEntityPfams(pdbId, entityId);
                if (! pfams.Contains ("Histone") && pfams.Length > 0)
                {
                    unpCode = GetEntityUnpCode(pdbId, entityId);
                    crc = GetEntityCrc(pdbId, entityId);
                    entityWriter.WriteLine(entity + "\t"  + crc + "\t" + unpCode + "\t" + ParseHelper.FormatStringFieldsToString(pfams));
                } 
            }
            entityWriter.Close();
        }

        public void GetChangedPfamHistones ()
        {
            string orgEntityFileName = "HistoneEntitiesNoPfam.txt";
            string changeEntityFileName = "HistoneEntitiesChangedPfams.txt";
            StreamReader dataReader = new StreamReader(orgEntityFileName);
            StreamWriter changeEntityWriter = new StreamWriter(changeEntityFileName);
            string line = "";
            string pdbId = "";
            int entityId = 0;
            string entityPfamStr = "";
            while ((line =dataReader.ReadLine ()) != null)
            {
                string[] fields = line.Split('\t');
                pdbId = fields[0].Substring(0, 4);
                entityId = Convert.ToInt32(fields[0].Substring(4, fields[0].Length - 4));
                string[] entityPfams = GetEntityPfams(pdbId, entityId);
                entityPfamStr = ParseHelper.FormatStringFieldsToString(entityPfams);
                if (entityPfamStr != fields[3])
                {
                    changeEntityWriter.WriteLine(line + "\t" + entityPfamStr);
                }
            }
            dataReader.Close();
            changeEntityWriter.Close();
        }

        private string GetEntityCrc (string pdbId, int entityId)
        {
            string queryString = string.Format("Select Crc From PdbCrcMap Where PdbID = '{0}' AND EntityID = {1};", pdbId, entityId);
            DataTable crcTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (crcTable.Rows.Count > 0)
            {
                return crcTable.Rows[0]["Crc"].ToString().TrimEnd();
            }
            return "";
        }

        private string[] GetEntityPfams (string pdbId, int entityId)
        {
            string queryString = string.Format("Select Distinct Pfam_ID From PdbPfam Where PdbID = '{0}' AND EntityID = {1};", pdbId, entityId);
            DataTable pfamTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<string> pfamList = new List<string>();
            string pfamId = "";
            foreach (DataRow pfamIdRow in pfamTable.Rows)
            {
                pfamId = pfamIdRow["Pfam_ID"].ToString().TrimEnd();
                pfamList.Add(pfamId);
            }
            return pfamList.ToArray();
        }

        private string GetEntityUnpCode (string pdbId, int entityId)
        {
            string queryString = string.Format("Select DbCode From PdbDbRefSifts Where PdbID = '{0}' AND EntityID = {1} AND DbName = 'UNP';", pdbId, entityId);
            DataTable unpCodeTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (unpCodeTable.Rows.Count > 0)
            {
                return unpCodeTable.Rows[0]["DbCode"].ToString().TrimEnd();
            }
            return "";
        }

        private void AddFirstListToSecond (List<string> firstList, List<string> secondList)
        {
            foreach (string item in firstList)
            {
                if (! secondList.Contains (item))
                {
                    secondList.Add(item);
                }
            }
        }
        public List<string> GetEntityWithUnpCodesNoHistonePfam (string[] unpCodes)
        {
            string queryString = string.Format("Select Distinct PdbID, EntityID From PdbDbRefSifts Where DbCode In ({0});", ParseHelper.FormatSqlListString (unpCodes));
            DataTable unpEntityTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<string> unpEntityList = new List<string>();
            foreach (DataRow entityRow in unpEntityTable.Rows)
            {
                unpEntityList.Add(entityRow["PdbID"].ToString() + entityRow["EntityID"].ToString());
            }
            return unpEntityList;
        }
        #endregion

        #region domain interface cluster cf groups
        public void FixDomainClusterInterfaceCfGroups ()
        {
            string queryString = "Select RelSeqID, PdbID, count(distinct relcfgroupId) as cfCount From PfamDomainClusterInterfaces Group By RelSeqID, PdbID;";
            DataTable domainEntryCfCountTable = ProtCidSettings.protcidQuery.Query(queryString);
            string pdbId = "";
            int relCfGroupId = 0;
            string repPdbId = "";
            int repCfGroupId = 0;
            int relSeqId = 0;
            string updateString = "";
            foreach (DataRow entryRow in domainEntryCfCountTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                repPdbId = GetRepEntry(pdbId);
                relSeqId = Convert.ToInt32(entryRow["RelSeqID"].ToString ());
                repCfGroupId = GetRepRelationCfGroupId(relSeqId, repPdbId);
                queryString = string.Format ("Select RelSeqID, PdbID, DomainInterfaceID, RelCfGroupID From PfamDomainClusterInterfaces Where RelSeqID = {0} AND PdbID = '{1}';", relSeqId, pdbId);
                DataTable entryDinterfaceTable = ProtCidSettings.protcidQuery.Query (queryString);
                foreach (DataRow interfaceRow in entryDinterfaceTable.Rows )
                {
                    relCfGroupId = Convert.ToInt32(interfaceRow["RelCfGroupId"].ToString());
                    if (repCfGroupId > 0 && relCfGroupId != repCfGroupId)
                    {
                        updateString = string.Format("Delete From PfamDomainClusterInterfaces Where RelSeqID = {0} AND PdbID = '{1}' AND DomainInterfaceID = {2} AND RelCfGroupID = {3};",
                            relSeqId, pdbId, interfaceRow["DomainInterfaceID"], relCfGroupId);
                        protcidUpdate.Delete(updateString);
                    }
                }
 //               updateString = string.Format("Update PfamDOmainClusterInterfaces Set RelCfGroupID = {0} Where RelSeqID = {1} AND PdbID = '{2}';", repCfGroupId, relSeqId, pdbId);
 //               protcidUpdate.Update(updateString);
            }
        }

        private int GetRepRelationCfGroupId (int relSeqId, string pdbId)
        {
            string queryString = string.Format("Select CfGroupId From PfamDomainInterfaceCluster Where RelSeqID = {0} AND PdbID = '{1}';", relSeqId, pdbId);
            DataTable cfGroupIdTable = ProtCidSettings.protcidQuery.Query(queryString);
            if (cfGroupIdTable.Rows.Count > 0)
            {
                return Convert.ToInt32(cfGroupIdTable.Rows[0]["CfGroupID"].ToString ());
            }
            return -1;
        }

        private string GetRepEntry (string pdbId)
        {
            string queryString = string.Format("Select PdbID1 From PfamHomoRepEntryAlign Where PdbID2 = '{0}';", pdbId);
            DataTable repEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            if (repEntryTable.Rows.Count > 0)
            {
                return repEntryTable.Rows[0]["PdbID1"].ToString().TrimEnd();
            }
            return "";
        }
        public void PrintDifCfSameUnitCellEntries ()
        {
            string queryString = "Select Distinct PfamHomoSeqInfo.GroupSeqID, SpaceGroup, Asu, PdbID1, PdbID2 From PfamHomoSeqInfo, PfamHomoRepEntryAlign Where PfamHomoSeqInfo.PdbID = PfamHomoRepEntryAlign.PdbID2;";
            DataTable entryTable = ProtCidSettings.protcidQuery.Query(queryString);
            string pdbId1 = "";
            string pdbId2 = "";
            StreamWriter dataWriter = new StreamWriter("CfErrorEntries.txt");
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId1 = entryRow["PdbID1"].ToString();
                pdbId2 = entryRow["PdbID2"].ToString();
                if (!AreSameCrystForm(pdbId1, pdbId2))
                {
                    dataWriter.WriteLine( ParseHelper.FormatDataRow (entryRow) + "\tFalse");
                    UpdateCfGroupTable(pdbId1, pdbId2);
                }
                else
                {
                    dataWriter.WriteLine(ParseHelper.FormatDataRow(entryRow) + "\tTrue");
                }
            }
            dataWriter.Close();
        }

        public void UpdateRepHomoEntryCfGroups ()
        {
            string queryString = "Select Distinct PdbID1 From PfamHomoRepEntryAlign;";
            DataTable repEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            StreamWriter dataWriter = new StreamWriter("CfRepHomoUpdateEntries.txt");
            dataWriter.WriteLine("PdbID1\tPdbID2\tCfGroupID1\tCfGroupID2");
            string pdbId1 = "";
            string pdbId2 = "";
            int cfGroupId1 = 0;
            int cfGroupId2 = 0;
            foreach (DataRow repRow  in repEntryTable.Rows)
            {
                pdbId1 = repRow["PdbID1"].ToString().TrimEnd();
                cfGroupId1 = GetCfGroupId(pdbId1);
                queryString = string.Format("Select Distinct PdbID2 From PfamHomoRepEntryAlign Where PdbID1 = '{0}';", pdbId1);
                DataTable homoEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
                foreach (DataRow homoEntryRow in homoEntryTable.Rows)
                {
                    pdbId2 = homoEntryRow["PdbID2"].ToString().TrimEnd();
                    cfGroupId2 = GetCfGroupId(pdbId2);
                    if (cfGroupId1 != -1 && cfGroupId2 != -1 && cfGroupId1 != cfGroupId2)
                    {
                        string updateString = string.Format("Update PfamNonRedundantCfGroups Set CfGroupId = {0} Where PdbID = '{1}';", cfGroupId1, pdbId2);
                        protcidUpdate.Update(updateString);
                        dataWriter.WriteLine(pdbId1 + "\t" + pdbId2 + "\t" + cfGroupId1 + "\t" + cfGroupId2);
                    }
                }
            }
            dataWriter.Close();
        }

        private void UpdateCfGroupTable (string pdbId1, string pdbId2)
        {
            int cfGroupId1 = GetCfGroupId(pdbId1);
            int cfGroupId2 = GetCfGroupId(pdbId2);

            if (cfGroupId1 != -1 && cfGroupId2 != -1 && cfGroupId1 != cfGroupId2)
            {
                string updateString = string.Format("Update PfamNonRedundantCfGroups Set CfGroupId = {0} Where PdbID = '{1}';", cfGroupId1, pdbId2);
                protcidUpdate.Update(updateString);
            }
        }

        private bool AreSameCrystForm (string pdbId1, string pdbId2)
        {
            int cfGroupId1 = GetCfGroupId(pdbId1);
            int cfGroupId2 = GetCfGroupId(pdbId2);

            if(cfGroupId1 != -1 && cfGroupId2 != -1 && cfGroupId1 == cfGroupId2)
            {
                return true;
            }
            return false;
        }

        private int GetCfGroupId (string pdbId)
        {
            string queryString = string.Format("Select GroupSeqId, CfGroupID, PdbID From PfamNonRedundantCfGroups Where PdbID = '{0}';", pdbId);
            DataTable cfGroupTable = ProtCidSettings.protcidQuery.Query(queryString);
            int cfGroupId = -1;
            if (cfGroupTable.Rows.Count > 0)
            {
                cfGroupId = Convert.ToInt32(cfGroupTable.Rows[0]["CfGroupID"].ToString());
            }
            return cfGroupId;
        }
        #endregion

        #region pfam domainid not consistent
        public void PrintDomainIdsNotConsistent()
        {
            string queryString = "Select Distinct PdbID, EntityID, AsymChain, AuthChain From PdbPfamChain;";
            DataTable pfamChainTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            StreamWriter dataWriter = new StreamWriter ("DomainIdInConsistent_asu.txt");
            foreach (DataRow domainRow in pfamChainTable.Rows)
            {
                queryString = string.Format("Select PdbID, EntityID, AsymID, AuthorChain From AsymUnit Where PdbID = '{0}' AND EntityID = {1} AND AsymID = '{2}' AND AuthorChain = '{3}';", 
                    domainRow["PdbID"], domainRow["EntityID"], domainRow["AsymChain"], domainRow["AuthChain"]);
                DataTable domainTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                if (domainTable.Rows.Count > 0)
                {
                    continue;
                }
                dataWriter.WriteLine(ParseHelper.FormatDataRow (domainRow));
            }
            dataWriter.Close();
        }

        #endregion       

        #region domain files
        public void UpdateAsuString ()
        {
            string queryString = "Select PdbID, Asu From PfamHomoSeqInfo Where Asu Like '%)(%';";
            DataTable asuTable = ProtCidSettings.protcidQuery.Query(queryString);
            string pdbId = "";
            string asu = "";
            string newAsu = "";
            int asuNo1 = 0;
            int asuNo2 = 0;
            string updateString = "";
            StreamWriter dataWriter = new StreamWriter("AsuErrorEntries.txt");
            foreach (DataRow entryRow in asuTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                asu = entryRow["ASU"].ToString().TrimEnd();
                string[] fields = asu.Split('(');
                asuNo1 = Convert.ToInt32(fields[1].TrimEnd(')'));
                asuNo2 = Convert.ToInt32(fields[2].TrimEnd(')'));
                if (asuNo2 == asuNo1)
                {
                    newAsu = fields[0] + "(" + asuNo1 + ")";
                    updateString = string.Format("Update PfamHomoSeqInfo Set Asu = '{0}' WHere PdbId = '{1}';", newAsu, pdbId);
                    protcidUpdate.Update(updateString);
                    updateString = string.Format("Update PfamNonRedundantCfGroups Set Asu = '{0}' Where PdbID = '{1}';", newAsu, pdbId);
                    protcidUpdate.Update(updateString);
                }

                dataWriter.WriteLine(ParseHelper.FormatDataRow(entryRow));
            }
            dataWriter.Close();
        }

        public void ReadMissingDomainFileList ()
        {
            string screenLogFile = @"X:\Qifang\Projects\PdbPfam\missingDomainFileList.txt";
            string missingListFile = @"X:\Qifang\Projects\PdbPfam\missingDomainlsFile.txt";
            StreamReader dataReader = new StreamReader(screenLogFile);
            StreamWriter dataWriter = new StreamWriter(missingListFile);
            string line = "";
            int domainBeg = 0;
            int domainEnd = 0;
            string domainLs = "";
            while ((line = dataReader.ReadLine ()) != null)
            {
                domainBeg = line.LastIndexOf("/") + 1;
                domainEnd = line.IndexOf(": No such");
                domainLs += (line.Substring(domainBeg, domainEnd - domainBeg) + ",");
            }
            dataWriter.Write(domainLs.TrimEnd(','));
            dataReader.Close();
            dataWriter.Close();
        }
        public void CopyDomainFilesToWeakDomainFileUpdateDir ()
        {
            string destFileDir = @"X:\Qifang\ProjectData\Pfam\UpdateWeakDomainFiles";
            string srcFileDir = @"X:\Qifang\ProjectData\Pfam\domainFiles";
            string checkFileDir = @"X:\Qifang\ProjectData\Pfam\weakDomainFiles";
            string[] lsFiles = { @"X:\Qifang\ProjectData\Pfam\FatcatAlign\newPfamStructAlignPairs.txt",
                                   @"X:\Qifang\ProjectData\Pfam\FatcatAlign\newClanStructAlignPairs.txt" };
            string srcFile = "";
            string destFile = "";
            StreamWriter lsFileWriter = new StreamWriter(@"X:\Qifang\ProjectData\Pfam\FatcatAlign\missingLsFile.txt");
            string lsDataLine = "";
            foreach (string lsFile in lsFiles)
            {
                string[] missingDomains = GetDomainFilesNotInWeakDomainFileList(checkFileDir, lsFile);
                foreach (string domain in missingDomains)
                {
                    srcFile = Path.Combine(srcFileDir, domain.Substring(1, 2) + "\\" + domain + ".pfam.gz");
                    destFile = Path.Combine(destFileDir, domain.Substring(1, 2) + "\\" + domain + ".pfam.gz");
                    if (! Directory.Exists (Path.Combine(destFileDir, domain.Substring(1, 2))))
                    {
                        Directory.CreateDirectory(Path.Combine(destFileDir, domain.Substring(1, 2)));
                    }
                    File.Copy(srcFile, destFile, true);
                    lsDataLine += (domain + ",");
                }
            }
            lsFileWriter.Write(lsDataLine.TrimEnd (','));
            lsFileWriter.Close();
        }

        private string[] GetDomainFilesNotInWeakDomainFileList (string checDir, string lsFile)
        {
            StreamReader dataReader = new StreamReader(lsFile);
            string line = "";
            string domainFile = "";
            List<string> missingDomainList = new List<string>();
            while ((line = dataReader.ReadLine ()) != null)
            {
                string[] fields = line.Split(',');
                if (fields.Length < 2)
                {
                    continue;
                }
                
                domainFile = Path.Combine(checDir, fields[0].Substring(1, 2) + "\\" + fields[0] + ".pfam.gz");
                if (File.Exists (domainFile))
                {
                    continue;
                }
                missingDomainList.Add(fields[0]);
            }
            dataReader.Close();
            return missingDomainList.ToArray();
        }
        #endregion

        #region update protcid
        public void MergeTwoUpdateSuperGroupsFiles ()
        {
            string dataDir = @"X:\Qifang\Projects\ProtCidDbProjects\ProtCidLibraries\ProtCidLibraries\bin";
            string resultFile = Path.Combine(dataDir, "UpdateSuperGroups_all.txt");
            Dictionary<int, Dictionary<int, List<string>>> superGroupDict = ReadFromSuperGroupFile (Path.Combine (dataDir, "UpdateSuperGroups.txt"));
            Dictionary<int, Dictionary<int, List<string>>> addedSuperGroupDict = ReadFromSuperGroupFile(Path.Combine(dataDir, "UpdateSuperGroups_addMissing.txt"));

            MergeSecondToFirstSuperDictionary(superGroupDict, addedSuperGroupDict);
            WriteSuperDictionaryToFile(resultFile, superGroupDict);

            resultFile = Path.Combine (dataDir, "UpdateGroups.txt");


            resultFile = Path.Combine(dataDir, "UpdateGroupEntries.txt");

        }

        private void WriteSuperDictionaryToFile (string txtFile, Dictionary<int, Dictionary<int, List<string>>> superGroupDict)
        {
            StreamWriter updateSuperGroupWriter = new StreamWriter(txtFile);
            string dataLine = "";
            foreach (int keySuperGroupId in superGroupDict.Keys)
            {
                updateSuperGroupWriter.WriteLine("#" + keySuperGroupId.ToString());

                Dictionary<int, List<string>> thisUpdateGroupsDict = superGroupDict[keySuperGroupId];
                foreach (int groupId in thisUpdateGroupsDict.Keys)
                {
                    dataLine = groupId.ToString() + ":";
                    foreach (string entry in thisUpdateGroupsDict[groupId])
                    {
                        dataLine += (entry + ",");
                    }
                    updateSuperGroupWriter.WriteLine(dataLine.TrimEnd(','));
                }
            }
            updateSuperGroupWriter.Flush();
        }

        private void MergeSecondToFirstSuperDictionary (Dictionary<int, Dictionary<int, List<string>>> superGroupDict,
            Dictionary<int, Dictionary<int, List<string>>> addedSuperGroupDict)
        {
            List<int> groupIdList = new List<int>(superGroupDict.Keys);
            foreach (int superGroupId in groupIdList)
            {
                Dictionary<int, List<string>> groupEntryDict = superGroupDict[superGroupId];
                if (addedSuperGroupDict.ContainsKey(superGroupId))
                {
                    foreach (int groupId in addedSuperGroupDict[superGroupId].Keys)
                    {
                        if (groupEntryDict.ContainsKey(groupId))
                        {
                            foreach (string entry in addedSuperGroupDict[superGroupId][groupId])
                            {
                                if (!groupEntryDict[groupId].Contains(entry))
                                {
                                    groupEntryDict[groupId].Add(entry);
                                }
                            }
                        }
                        else
                        {
                            groupEntryDict.Add(groupId, addedSuperGroupDict[superGroupId][groupId]);
                        }
                    }
                }
            }
            foreach (int superGroupId in addedSuperGroupDict.Keys)
            {
                if (!superGroupDict.ContainsKey(superGroupId))
                {
                    superGroupDict.Add(superGroupId, addedSuperGroupDict[superGroupId]);
                }
            }
        }

        private Dictionary<int, Dictionary<int, List<string>>> ReadFromSuperGroupFile (string groupFile)
        {
            Dictionary<int, Dictionary<int, List<string>>> updateSuperGroupDict = new Dictionary<int, Dictionary<int, List<string>>>();
            StreamReader dataReader = new StreamReader(groupFile);
            string line = "";
            int superGroupId = -1;
            Dictionary<int, List<string>> updateGroupEntryDict = null;
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line == "")
                {
                    continue;
                }
                if (line.Substring(0, 1) == "#")
                {
                    if (superGroupId != -1)
                    {
                        updateSuperGroupDict.Add(superGroupId, updateGroupEntryDict);
                    }
                    superGroupId = Convert.ToInt32(line.Substring(1, line.Length - 1));
                    updateGroupEntryDict = new Dictionary<int, List<string>>();
                }
                if (line.IndexOf(":") > -1)
                {
                    string[] fields = line.Split(':');
                    List<string> entries = new List<string>(fields[1].Split(','));
                    updateGroupEntryDict.Add(Convert.ToInt32(fields[0]), entries);
                }
            }
            if (superGroupId > -1)
            {
                updateSuperGroupDict.Add(superGroupId, updateGroupEntryDict);
            }
            dataReader.Close();

            return updateSuperGroupDict;
        }
        
        #endregion

        public void DeleteDuplicateRowsInBioAssemDB ()
        {
            DbConnect assemDbConnect = new DbConnect("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=X:\\Firebird\\BioAssem\\BioAssembly.fdb");
            DbQuery assemQuery = new DbQuery(assemDbConnect);
            DbUpdate assemUpdate = new DbUpdate(assemDbConnect);

            
            int groupId = 1953;
            string updateString = "";

            /* string queryString = "Select Distinct GroupID From  AssemblyClusterSumInfo;";         
             * DataTable groupTable = assemQuery.Query(queryString);
            
                       foreach (DataRow groupRow in groupTable.Rows)
                       {
                           groupId = Convert.ToInt32(groupRow["GroupID"].ToString ());*/
                int[] groupNumbers = GetGroupSummaryInfo(groupId, assemQuery);
                updateString = string.Format("Update AssemblyClusterSumInfo Set NumEntriesGroup = {0}, NumAssembliesGroup = {1} Where GroupID = {2};", groupNumbers[0], groupNumbers[1], groupId);
                assemUpdate.Update(updateString);
      //      }
          
 /*           DbInsert dbInsert = new DbInsert(assemDbConnect);

            string tableName = "AssemblyComp";
            string queryString = "Select * From AssemblyComp Where GroupID <= 21";
            DataTable assemCompTable = assemQuery.Query(queryString);
            DataTable noDupAssemCompTable = assemCompTable.Clone();
            noDupAssemCompTable.TableName = tableName;
            List<string> groupAssemPairList = new List<string>();
            string groupAssemPair = "";
            foreach (DataRow dataRow in assemCompTable.Rows)
            {
                groupAssemPair = dataRow["GroupID"] + "_" + dataRow["PdbID1"] + dataRow["AssemID1"] + "_" + dataRow["PdbID2"] + dataRow["AssemID2"];
                if (groupAssemPairList.Contains (groupAssemPair ))
                {
                    continue;
                }
                groupAssemPairList.Add(groupAssemPair);
                DataRow noDupRow = noDupAssemCompTable.NewRow();
                noDupRow.ItemArray = dataRow.ItemArray;
                noDupAssemCompTable.Rows.Add(noDupRow);
            }

            string deleteString = "Delete From AssemblyComp Where GroupID <= 21";
            dbUpdate.Delete(deleteString);

            dbInsert.InsertDataIntoDBtables(noDupAssemCompTable);*/
        }

        /// <summary>
        /// three numbers of a group
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns>#entries, #assemblies, #CFs</returns>
        public int[] GetGroupSummaryInfo(int groupId, DbQuery assemQuery)
        {
            int[] groupNumbers = new int[3];
            string queryString = string.Format("Select Distinct PdbID From AssemblyGroupInfo Where GroupID = {0};", groupId);
            DataTable entryTable = assemQuery.Query(queryString);
            groupNumbers[0] = entryTable.Rows.Count;

            queryString = string.Format("Select Distinct PdbID, AssemID From AssemblyGroupInfo Where GroupID = {0};", groupId); // not neccesary to use distinct, but just in case
            DataTable assemblyTable = assemQuery.Query(queryString);
            groupNumbers[1] = assemblyTable.Rows.Count;

            queryString = string.Format("Select Distinct CfGroupID From AssemblyGroupInfo Where GroupID = {0};", groupId);
            DataTable cfTable = assemQuery.Query(queryString);
            groupNumbers[2] = cfTable.Rows.Count;

            return groupNumbers;
        }
    }
}

