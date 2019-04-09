using System;
using System.Data;
using System.Collections.Generic;
using DbLib;
using ProtCidSettingsLib;

namespace CrystalInterfaceLib.DomainInterfaces
{
	/// <summary>
	/// domain-domain alignments
	/// </summary>
	public class DomainAlignment
	{
		#region member variables
		private DbQuery dbQuery = new DbQuery ();
		private DbInsert dbInsert = new DbInsert ();
        private DataTable domainAlignmentTable = null;
        private double fatcatCutoff = 0.5;
		#endregion

        #region constructor
        public DomainAlignment()
		{
            InitializeAlignTable();
		}

        private void InitializeAlignTable()
        {
            domainAlignmentTable = new DataTable("DomainAlignments");
            string[] domainAlignColumns = {"PdbID1", "DomainID1", "PdbID2", "DomainID2", "Identity", "Zscore",
						"QueryStart", "QueryEnd", "HitStart", "HitEnd", "QuerySequence", "HitSequence", "Evalue",
                        "QuerySeqNumbers", "HitSeqNumbers"};
            foreach (string colName in domainAlignColumns)
            {
                domainAlignmentTable.Columns.Add(new DataColumn(colName));
            }
        }
        #endregion

        #region Domain structure alignment in a family
        /// <summary>
        /// the alignments between any two domains in the same family
        /// </summary>
        /// <param name="repDomainList"></param>
        public DataTable GetStructDomainAlignments(long[] domainIds)
        {
            domainAlignmentTable.Clear();
            // get the corresponding domains located in the representative chains 
            // in the redundantpdbchains table, 
            // since only those representative domains are computed alignments
            Dictionary<long, long[]> repDomainHash = GetRepDomainHash (domainIds);
            List<long> repDomainList = new List<long> (repDomainHash.Keys);
            long[] reduntDomains1 = null;
            long[] reduntDomains2 = null;
            // for those domains with same representative domain
            for (int i = 0; i < repDomainList.Count; i++)
            {
                reduntDomains1 = (long[])repDomainHash[repDomainList[i]];
                for (int j = i ; j < repDomainList.Count; j++)
                {
                    reduntDomains2 = (long[])repDomainHash[repDomainList[j]];
                    GetStructDomainAlignment((long)repDomainList[i], (long)repDomainList[j], reduntDomains1, reduntDomains2);
                }
            }
            return domainAlignmentTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainList"></param>
        /// <returns></returns>
        private Dictionary<long, long[]>  GetRepDomainHash(long[] domainIds)
        {
            Dictionary<long, List<long>> repDomainListHash = new Dictionary<long, List<long>>();
            foreach (long domainId in domainIds)
            {
                // the corresponding domain from the representative chain of the chain this domain located
                long repDomainId = GetRepDomainID(domainId);
                if (repDomainListHash.ContainsKey(repDomainId))
                {
                    repDomainListHash[repDomainId].Add(domainId);
                }
                else
                {
                    List<long> reduntDomainList = new List<long> ();
                    reduntDomainList.Add(domainId);
                    repDomainListHash.Add(repDomainId, reduntDomainList);
                }
            }
            Dictionary<long, long[]> repDomainsHash = new Dictionary<long, long[]>();
            foreach (long repDomainId in repDomainListHash.Keys)
            {
                repDomainsHash.Add(repDomainId, repDomainListHash[repDomainId].ToArray ());
            }
            return repDomainsHash;
        }

        /// <summary>
        /// the alignment between two domains in the same family
        /// assign alignments to those redundant domains
        /// </summary>
        /// <param name="repDomainId1"></param>
        /// <param name="repDomainId2"></param>
        /// <param name="domainIds1">the redundant domains for repDomainId1</param>
        /// <param name="domainIds2">the redundant domains for repDomainId2</param>
        private void GetStructDomainAlignment(long repDomainId1, long repDomainId2, long[] domainIds1, long[] domainIds2)
        {
            if (repDomainId1 == repDomainId2)
            {
                foreach (long domainId1 in domainIds1)
                {
                    foreach (long domainId2 in domainIds2)
                    {
                        DataRow domainAlignRow = domainAlignmentTable.NewRow ();
                        domainAlignRow["DomainID1"] = domainId1;
                        domainAlignRow["DomainID2"] = domainId2;
                        domainAlignRow["QueryStart"] = 0;
                        domainAlignRow["QueryEnd"] = 0;
                        domainAlignRow["HitStart"] = 0;
                        domainAlignRow["HitEnd"] = 0;
                        domainAlignRow["QuerySequence"] = "-";
                        domainAlignRow["HitSequence"] = "-";
                        domainAlignRow["QuerySeqNumbers"] = "-";
                        domainAlignRow["HitSeqNumbers"] = "-";
                        domainAlignRow["Identity"] = 100.0;
                        domainAlignRow["Evalue"] = 0;
                        domainAlignmentTable.Rows.Add(domainAlignRow);
                    }
                }
                return;
            }
            // get the alignment between representative domains first, if no, then check the redundant domains
            DataTable domainAlignTable = GetDomainAlignTable(repDomainId1, repDomainId2);
            if (domainAlignTable == null || domainAlignTable.Rows.Count == 0)
            {
                return;  // no domain alignments available
            }

            // replace the alignments by the input domains
            foreach (DataRow alignRow in domainAlignTable.Rows)
            {
                foreach (long domainId1 in domainIds1)
                {
                    foreach (long domainId2 in domainIds2)
                    {
                        DataRow domainAlignRow = domainAlignmentTable.NewRow();
                        domainAlignRow["DomainID1"] = domainId1;
                        domainAlignRow["DomainID2"] = domainId2;
                        domainAlignRow["QueryStart"] = alignRow["QueryStart"];
                        domainAlignRow["QueryEnd"] = alignRow["QueryEnd"];
                        domainAlignRow["HitStart"] = alignRow["HitStart"];
                        domainAlignRow["HitEnd"] = alignRow["HitEnd"];
                        domainAlignRow["QuerySequence"] = alignRow["QuerySequence"];
                        domainAlignRow["HitSequence"] = alignRow["HitSequence"];
                        domainAlignRow["Identity"] = Convert.ToDouble(alignRow["Identity"].ToString());
                        domainAlignRow["Evalue"] = alignRow["Evalue"];
                        domainAlignRow["QuerySeqNumbers"] = alignRow["QuerySeqNumbers"];
                        domainAlignRow["HitSeqNumbers"] = alignRow["HitSeqNumbers"];
                        domainAlignmentTable.Rows.Add(domainAlignRow);
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainId1"></param>
        /// <param name="domainId2"></param>
        /// <returns></returns>
        private DataTable GetStructDomainAlignment(long domainId1, long domainId2)
        {
            if (domainId1 == domainId2)
            {
                DataTable sameDomainAlignTable = domainAlignmentTable.Clone();
                DataRow domainAlignRow = sameDomainAlignTable.NewRow();
                domainAlignRow["DomainID1"] = domainId1;
                domainAlignRow["DomainID2"] = domainId2;
                domainAlignRow["QueryStart"] = 0;
                domainAlignRow["QueryEnd"] = 0;
                domainAlignRow["HitStart"] = 0;
                domainAlignRow["HitEnd"] = 0;
                domainAlignRow["QuerySequence"] = "-";
                domainAlignRow["HitSequence"] = "-";
                domainAlignRow["QuerySeqNumbers"] = "-";
                domainAlignRow["HitSeqNumbers"] = "-";
                domainAlignRow["Identity"] = 100.0;
                domainAlignRow["Evalue"] = 0;
                sameDomainAlignTable.Rows.Add(domainAlignRow);
                return sameDomainAlignTable;
            }
            string queryString = string.Format("Select * From {0}DomainAlignments " +
              " Where (QueryDomainID = {1} AND HitDomainID = {2}) " +
              " OR (QueryDomainID = {2} AND HitDomainID = {1});",
              ProtCidSettings.dataType, domainId1, domainId2);
            DataTable domainAlignFlexTable = ProtCidSettings.alignmentQuery.Query(queryString);

            queryString = string.Format("Select * From {0}DomainAlignmentsRigid " +
              " Where (QueryDomainID = {1} AND HitDomainID = {2}) " +
              " OR (QueryDomainID = {2} AND HitDomainID = {1});",
              ProtCidSettings.dataType, domainId1, domainId2);
            DataTable domainAlignRigidTable = ProtCidSettings.alignmentQuery.Query(queryString);           

            DataTable selectedDomainAlignTable = null;
            double evalueFlex = 10000;
            double evalueRigid = 10000;
            if (domainAlignFlexTable.Rows.Count > 0)
            {
                evalueFlex = Convert.ToDouble(domainAlignFlexTable.Rows[0]["E_Value"].ToString ());
            }
            if (domainAlignRigidTable.Rows.Count > 0)
            {
                evalueRigid = Convert.ToDouble(domainAlignRigidTable.Rows[0]["E_Value"].ToString ());
            }
            if (evalueFlex <= evalueRigid)
            {
                selectedDomainAlignTable = domainAlignFlexTable;
            }
            else
            {
                selectedDomainAlignTable = domainAlignRigidTable;
            }
            // make sure the domain order of the alignments same as the input domains 
            DataTable domainAlignTable = SetDomainAlignTable(selectedDomainAlignTable, domainId1, domainId2);
            return domainAlignTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainAlignTable"></param>
        private DataTable  SetDomainAlignTable(DataTable domainAlignTable, long domainId1, long domainId2)
        {
            DataTable modifiedAlignTable = domainAlignmentTable.Clone();
            long queryDomainId = 0;
            long hitDomainId = 0;
            for (int i = 0; i < domainAlignTable.Rows.Count; i++)
            {
                DataRow alignRow = domainAlignTable.Rows[i];
                queryDomainId = Convert.ToInt64(alignRow["QueryDomainID"].ToString());
                hitDomainId = Convert.ToInt64(alignRow["HitDomainID"].ToString());
                if (queryDomainId == domainId2 && hitDomainId == domainId1)
                {
                    ReverseDataRow(alignRow);
                }
                DataRow domainAlignRow = modifiedAlignTable.NewRow();
                domainAlignRow["DomainID1"] = domainId1;
                domainAlignRow["DomainID2"] = domainId2;
                domainAlignRow["QueryStart"] = alignRow["QueryStart"];
                domainAlignRow["QueryEnd"] = alignRow["QueryEnd"];
                domainAlignRow["HitStart"] = alignRow["HitStart"];
                domainAlignRow["HitEnd"] = alignRow["HitEnd"];
                domainAlignRow["QuerySequence"] = alignRow["QuerySequence"];
                domainAlignRow["HitSequence"] = alignRow["HitSequence"];
                domainAlignRow["QuerySeqNumbers"] = alignRow["QuerySeqNumbers"];
                domainAlignRow["HitSeqNumbers"] = alignRow["HitSeqNumbers"];
                domainAlignRow["Identity"] = alignRow["Identity"];
                domainAlignRow["Evalue"] = alignRow["E_value"];
                modifiedAlignTable.Rows.Add(domainAlignRow);              
            }
            return modifiedAlignTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="alignRow"></param
        private void ReverseDataRow(DataRow alignRow)
        {
            string colName = "";
            string hitColName = "";
            foreach (DataColumn dCol in alignRow.Table.Columns)
            {
                colName = dCol.ColumnName.ToUpper();
                if (colName.IndexOf("QUERY") > -1)
                {
                    hitColName = colName.Replace("QUERY", "HIT");
                    object temp = alignRow[colName];
                    alignRow[colName] = alignRow[hitColName];
                    alignRow[hitColName] = temp;
                }
            }
        }
        /// <summary>
        /// in order to save computation time and storage size,
        /// only those alignments for representative chains are in the database
        /// To get the alignments, must find the corresponding domain ID for the input domain ID
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <param name="domainId"></param>
        /// <returns></returns>
        private long GetRepDomainID(long domainId)
        {
            string queryString =  string.Format("Select PdbID, EntityID, SeqStart, SeqEnd From PdbPfam Where DomainID = {0};", domainId);
            DataTable domainChainInfoTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string pdbId = "";
            int entityId = -1;
            int seqStartPos = -1;
            int seqEndPos = -1;
            Int64 repDomainId = -1;
            if (domainChainInfoTable.Rows.Count > 0)
            {
                pdbId = domainChainInfoTable.Rows[0]["PdbID"].ToString();
                entityId = Convert.ToInt32 (domainChainInfoTable.Rows[0]["EntityID"].ToString().TrimEnd());
                seqStartPos = Convert.ToInt32(domainChainInfoTable.Rows[0]["SeqStart"].ToString());
                seqEndPos = Convert.ToInt32(domainChainInfoTable.Rows[0]["SeqEnd"].ToString());

                string repPdbId = "";
                int repEntityId = -1;
                GetRepEntryEntity (pdbId, entityId, out repPdbId, out repEntityId);

                // itself is the representative chain
                if (repPdbId == pdbId && repEntityId == entityId)
                {
                    repDomainId = domainId;
                }
                else
                {
                    repDomainId = GetSameSeqDomainID(repPdbId, repEntityId, seqStartPos, seqEndPos);
                }
            }
            return repDomainId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <param name="repPdbId"></param>
        /// <param name="repAsymChain"></param>
        private void GetRepEntryEntity(string pdbId, int entityId, out string repPdbId, out int repEntityId)
        {
            repPdbId = "";
            repEntityId = -1;
            string crc = "";
            string queryString = string.Format("Select crc From PDBCRCMap Where PdbID = '{0}' AND EntityID = '{1}';", pdbId, entityId);
            DataTable crcTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (crcTable.Rows.Count > 0)
            {
                crc = crcTable.Rows[0]["crc"].ToString().Trim ();
                queryString = string.Format("Select PdbID, EntityID From PDBCRCMap Where crc = '{0}' AND IsRep = '1';", crc);
                DataTable repEntityTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                if (repEntityTable.Rows.Count > 0)
                {
                    repPdbId = repEntityTable.Rows[0]["PdbID"].ToString();
                    repEntityId = Convert.ToInt32(repEntityTable.Rows[0]["EntityID"].ToString());
                }
            }
        }

        private int devDomainLocation = 2;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="repPdbId"></param>
        /// <param name="repAsymChain"></param>
        /// <param name="seqStartPos"></param>
        /// <param name="seqEndPos"></param>
        /// <returns></returns>
        private Int64 GetSameSeqDomainID(string repPdbId, int repEntityId, int seqStartPos, int seqEndPos)
        {
            string queryString = "";
            int domainStartPos = -1;
            int domainEndPos = -1;
            Int64 repDomainId = -1;
            if (ProtCidSettings.dataType == "pfam")
            {
                queryString = string.Format("Select * From PdbPfam Where PdbID = '{0}' AND EntityId = '{1}';",
                    repPdbId, repEntityId);
                DataTable pfamDomainTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                foreach (DataRow domainRow in pfamDomainTable.Rows)
                {
                    domainStartPos = Convert.ToInt32(domainRow["SeqStart"].ToString());
                    domainEndPos = Convert.ToInt32(domainRow["SeqEnd"].ToString());
                    if ((domainStartPos == seqStartPos) ||
                        // in case there are deviation for those redundant chains, especially for 
                        // those data PFAM provides
                        (domainStartPos <= seqStartPos + devDomainLocation &&
                        domainStartPos >= seqStartPos - devDomainLocation &&
                        domainEndPos <= seqEndPos + devDomainLocation &&
                        domainEndPos >= seqEndPos - devDomainLocation) ||
                        (AreDomainOverlap (seqStartPos, seqEndPos, domainStartPos, domainEndPos)))
                    {
                        repDomainId = Convert.ToInt64(domainRow["DomainID"].ToString());
                    }
                }
            }
            return repDomainId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqStart1"></param>
        /// <param name="seqEnd1"></param>
        /// <param name="seqStart2"></param>
        /// <param name="seqEnd2"></param>
        /// <returns></returns>
        private bool AreDomainOverlap(int seqStart1, int seqEnd1, int seqStart2, int seqEnd2)
        {
            int minEnd = Math.Min(seqEnd1, seqEnd2);
            int maxStart = Math.Max(seqStart1, seqStart2);
            int overlap = minEnd - maxStart + 1;
            double coverage1 = (double)(overlap) / (double)(seqEnd1 - seqStart1 + 1);
            double coverage2 = (double)(overlap) / (double)(seqEnd2 - seqStart2 + 1);
            if (coverage1 >= 0.80 && coverage2 >= 0.80)
            {
                return true;
            }
            return false;
        }
        #endregion

        #region struct alignments from redundant domains
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainId1"></param>
        /// <param name="domainId2"></param>
        /// <returns></returns>
        public DataTable GetDomainAlignTable(long domainId1, long domainId2)
        {
            DataTable domainAlignTable = GetStructDomainAlignment(domainId1, domainId2);
            if (domainAlignTable.Rows.Count > 0)
            {
                return domainAlignTable;
            }
            long[] reduntDomainIds1 = GetRedundantDomains(domainId1);
            long[] reduntDoaminIds2 = GetRedundantDomains(domainId2);
            foreach (long reduntDomainId1 in reduntDomainIds1)
            {
                foreach (long reduntDomainId2 in reduntDoaminIds2)
                {
                    domainAlignTable = GetStructDomainAlignment(reduntDomainId1, reduntDomainId2);
                    if (domainAlignTable.Rows.Count > 0)
                    {
                        ReplaceByInputDomainIds(domainAlignTable, domainId1, domainId2);
                        return domainAlignTable;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainAlignTable"></param>
        /// <param name="domainId1"></param>
        /// <param name="domainId2"></param>
        private void ReplaceByInputDomainIds(DataTable domainAlignTable, long domainId1, long domainId2)
        {
            foreach (DataRow domainAlignRow in domainAlignTable.Rows)
            {
                domainAlignRow["DomainID1"] = domainId1;
                domainAlignRow["DomainID2"] = domainId2;
            }
            domainAlignTable.AcceptChanges();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="repDomainId"></param>
        /// <param name="seqStart"></param>
        /// <param name="seqEnd"></param>
        /// <returns></returns>
        private long[] GetRedundantDomains(long domainId)
        {
            int seqStart = 0;
            int seqEnd = 0;
            string domainEntity = GetDomainEntryEntity(domainId, out seqStart, out seqEnd);
            string[] reduntEntities = GetRedundantEntities(domainEntity.Substring(0, 4), Convert.ToInt32(domainEntity.Substring(4, domainEntity.Length - 4)));
            long reduntDomainId = 0;
            List<long> reduntDomainList = new List<long> ();
            foreach (string reduntEntity in reduntEntities)
            {
                reduntDomainId = GetSameSeqDomainID(reduntEntity.Substring(0, 4),
                    Convert.ToInt32(reduntEntity.Substring(4, reduntEntity.Length - 4)), seqStart, seqEnd);
                if (reduntDomainId > 0)
                {
                    reduntDomainList.Add(reduntDomainId);
                }
            }
            return reduntDomainList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="repPdbId"></param>
        /// <param name="repEntity"></param>
        /// <returns></returns>
        private string[] GetRedundantEntities(string pdbId, int entityId)
        {
            string queryString = string.Format("Select Crc From PdbCRCMap where PdbID = '{0}' AND EntityID = {1};", pdbId, entityId);
            DataTable crcTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string crc = "";
            string[] reduntEntities = null;
            if (crcTable.Rows.Count > 0)
            {
                crc = crcTable.Rows[0]["crc"].ToString().Trim();
                queryString = string.Format("Select Distinct PdbID, EntityID From PDBCRCMap Where crc = '{0}';", crc);
                DataTable entityTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                reduntEntities = new string[entityTable.Rows.Count - 1];
                int count = 0;
                string reduntEntity = "";
                foreach (DataRow entityRow in entityTable.Rows)
                {
                    reduntEntity = entityRow["PdbID"].ToString() + entityRow["EntityID"].ToString();
                    if (reduntEntity != pdbId + entityId.ToString())
                    {
                        reduntEntities[count] = reduntEntity;
                        count++;
                    }
                }
            }    
            if (reduntEntities == null)
            {
                reduntEntities = new string[0];
            }
            return reduntEntities;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="repDomainId"></param>
        /// <returns></returns>
        private string GetDomainEntryEntity(long domainId, out int seqStart, out int seqEnd)
        {
            string queryString = string.Format("Select PdbID, EntityID, AlignStart, AlignEnd From PdbPfam WHere DomainID = {0};", domainId);
            DataTable domainEntityTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string domainEntity = "";
            seqStart = 0;
            seqEnd = 0;
            if (domainEntityTable.Rows.Count > 0)
            {
                domainEntity = domainEntityTable.Rows[0]["PdbID"].ToString() + domainEntityTable.Rows[0]["EntityID"].ToString();
                seqStart = Convert.ToInt32(domainEntityTable.Rows[0]["AlignStart"].ToString());
                seqEnd = Convert.ToInt32(domainEntityTable.Rows[0]["AlignEnd"].ToString());
            }
            return domainEntity;
        }
        #endregion

        #region structure alignments in a clan

        /// <summary>
        /// the alignments between any two domains in the same family
        /// </summary>
        /// <param name="repDomainList"></param>
        public DataTable GetStructDomainAlignments(long[] domainIds1, long[] domainIds2)
        {
            domainAlignmentTable.Clear();
            // get the corresponding domains located in the representative chains 
            // in the redundantpdbchains table, 
            // since only those representative domains are computed alignments
            Dictionary<long, long[]> repDomainHash1 = GetRepDomainHash(domainIds1);
            Dictionary<long, long[]> repDomainHash2 = GetRepDomainHash(domainIds2);

            long[] inputDomains1 = null;
            long[] inputDomains2 = null;
            // for those domains with same representative domain
            foreach (long repDomainId1 in repDomainHash1.Keys)
            {
                inputDomains1 = (long[])repDomainHash1[repDomainId1];
                foreach (long repDomainId2 in repDomainHash2.Keys)
                {
                    inputDomains2 = (long[])repDomainHash2[repDomainId2];
                    GetStructDomainAlignment(repDomainId1, repDomainId2, inputDomains1, inputDomains2);
                }
            }
            return domainAlignmentTable;
        }
        #endregion

        #region domain alignments in a family by PFAM HMMs
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainList"></param>
        /// <returns></returns>
        public DataTable GetDomainAlignmentByPfamHmm(long[] domainIds)
        {
            domainAlignmentTable.Clear();
            DataTable domainTable = GetDomainTable(domainIds);
            Dictionary<long, int[][]> domainHmmSeqNumbersHash = new Dictionary<long,int[][]> ();
            bool isMultiChain = false;
            foreach (long domainId in domainIds)
            {
                DataRow[] domainRows = domainTable.Select(string.Format("DomainID = {0}", domainId),
                     "HmmStart ASC");
                isMultiChain = IsMultiChainDomain(domainRows);
                if (isMultiChain)
                {
                    int[][] hmmSeqNumbers = GetHmmDomainFileSeqNumbersHash(domainRows);
                    domainHmmSeqNumbersHash.Add(domainId, hmmSeqNumbers);
                }
                else
                {
                    int[][] hmmSeqNumbers = GetHmmDomainSeqNumbersHash(domainRows);
                    domainHmmSeqNumbersHash.Add(domainId, hmmSeqNumbers);
                }
            }
            long domainIdI = 0;
            long domainIdJ = 0;
            double identity = 0;
            for (int i = 0; i < domainIds.Length - 1; i++)
            {
                domainIdI = domainIds[i];
                int[][] hmmSeqNumbersI = (int[][])domainHmmSeqNumbersHash[domainIdI];
                for (int j = i + 1; j < domainIds.Length; j++)
                {
                    domainIdJ = domainIds[j];
                    int[][] hmmSeqNumbersJ = (int[][])domainHmmSeqNumbersHash[domainIdJ];
                    string[] alignSeqNumbers = GetSeqNumberAlignment(hmmSeqNumbersI, hmmSeqNumbersJ);
                    identity = GetDomainSeqIdentity(alignSeqNumbers, domainIdI, domainIdJ, domainTable);
                    DataRow domainAlignRow = domainAlignmentTable.NewRow();
               //     domainAlignRow["PdbID1"] = pdbId1;
                    domainAlignRow["DomainID1"] = domainIdI;
                //    domainAlignRow["PdbID2"] = pdbId2;
                    domainAlignRow["DomainID2"] = domainIdJ;
                    domainAlignRow["Identity"] = identity;
                    domainAlignRow["Zscore"] = -1;
                    domainAlignRow["QueryStart"] = -1;
                    domainAlignRow["QueryEnd"] = -1;
                    domainAlignRow["HitStart"] = -1;
                    domainAlignRow["HitEnd"] = -1;
                    domainAlignRow["QuerySequence"] = "";
                    domainAlignRow["HitSequence"] = "";
                    domainAlignRow["QuerySeqNumbers"] = alignSeqNumbers[0];
                    domainAlignRow["HitSeqNumbers"] = alignSeqNumbers[1];
                    domainAlignmentTable.Rows.Add(domainAlignRow);
                }
            }
            return domainAlignmentTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainList"></param>
        /// <returns></returns>
        public DataTable GetDomainAlignmentByPfamHmm(long[] domainIds1, long[] domainIds2)
        {
            domainAlignmentTable.Clear();
            List<long> domainIdList = new List<long> (domainIds1);
            foreach (long domainId2 in domainIds2)
            {
                if (!domainIdList.Contains(domainId2))
                {
                    domainIdList.Add(domainId2);
                }
            }
            long[] domainIds = new long[domainIdList.Count];
            domainIdList.CopyTo(domainIds);

            DataTable domainTable = GetDomainTable(domainIds);
            Dictionary<long, int[][]> domainHmmSeqNumbersHash = new Dictionary<long,int[][]> ();
            bool isMultiChain = false;
            foreach (long domainId in domainIds)
            {
                DataRow[] domainRows = domainTable.Select(string.Format("DomainID = {0}", domainId),
                     "HmmStart ASC");
                isMultiChain = IsMultiChainDomain(domainRows);
                if (isMultiChain)
                {
                    int[][] hmmSeqNumbers = GetHmmDomainFileSeqNumbersHash(domainRows);
                    domainHmmSeqNumbersHash.Add(domainId, hmmSeqNumbers);
                }
                else
                {
                    int[][] hmmSeqNumbers = GetHmmDomainSeqNumbersHash(domainRows);
                    domainHmmSeqNumbersHash.Add(domainId, hmmSeqNumbers);
                }
            }
            long domainIdI = 0;
            long domainIdJ = 0;
            double identity = 0;
            for (int i = 0; i < domainIds1.Length; i++)
            {
                domainIdI = domainIds[i];
                int[][] hmmSeqNumbersI = (int[][])domainHmmSeqNumbersHash[domainIdI];
                for (int j = 0; j < domainIds2.Length; j++)
                {
                    domainIdJ = domainIds2[j];
                    if (domainIdI == domainIdJ)
                    {
                        continue;
                    }
                    int[][] hmmSeqNumbersJ = (int[][])domainHmmSeqNumbersHash[domainIdJ];
                    string[] alignSeqNumbers = GetSeqNumberAlignment(hmmSeqNumbersI, hmmSeqNumbersJ);
                    identity = GetDomainSeqIdentity(alignSeqNumbers, domainIdI, domainIdJ, domainTable);
                    DataRow domainAlignRow = domainAlignmentTable.NewRow();
                    //     domainAlignRow["PdbID1"] = pdbId1;
                    domainAlignRow["DomainID1"] = domainIdI;
                    //    domainAlignRow["PdbID2"] = pdbId2;
                    domainAlignRow["DomainID2"] = domainIdJ;
                    domainAlignRow["Identity"] = identity;
                    domainAlignRow["Zscore"] = -1;
                    domainAlignRow["QueryStart"] = -1;
                    domainAlignRow["QueryEnd"] = -1;
                    domainAlignRow["HitStart"] = -1;
                    domainAlignRow["HitEnd"] = -1;
                    domainAlignRow["QuerySequence"] = "";
                    domainAlignRow["HitSequence"] = "";
                    domainAlignRow["QuerySeqNumbers"] = alignSeqNumbers[0];
                    domainAlignRow["HitSeqNumbers"] = alignSeqNumbers[1];
                    domainAlignmentTable.Rows.Add(domainAlignRow);
                }
            }
            return domainAlignmentTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="alignSeqNumbers"></param>
        /// <param name="domainId1"></param>
        /// <param name="domainId2"></param>
        /// <param name="domainTable"></param>
        /// <returns></returns>
        private double GetDomainSeqIdentity (string[] alignSeqNumbers, long domainId1, long domainId2, DataTable domainTable)
        {
            DataRow[] domainRows1 = domainTable.Select(string.Format ("DomainID = '{0}'", domainId1));
            DataRow[] domainRows2 = domainTable.Select(string.Format ("DomainID = '{0}'", domainId2));
            if (domainRows1.Length == 0 || domainRows2.Length == 0)
            {
                return -1.0;
            }
            string pfamSeqAlign1 = domainRows1[0]["QueryAlignment"].ToString ();
            int seqStartPos1 = Convert.ToInt32 (domainRows1[0]["AlignStart"].ToString ());
            string alignSeq1 = GetAlignSequence(alignSeqNumbers[0], pfamSeqAlign1, seqStartPos1);
            string pfamSeqAlign2 = domainRows2[0]["QueryAlignment"].ToString();
            int seqStartPos2 = Convert.ToInt32(domainRows2[0]["AlignStart"].ToString());
            string alignSeq2 = GetAlignSequence(alignSeqNumbers[1], pfamSeqAlign2, seqStartPos2);
            double identity = GetDomainSeqIdentity(alignSeq1, alignSeq2);
            return identity;
        }

        /// <summary>
        /// alignSequence1, alignSequence2 must have same length
        /// </summary>
        /// <param name="alignSequence1"></param>
        /// <param name="alignSequence2"></param>
        /// <returns></returns>
        private double GetDomainSeqIdentity (string alignSequence1, string alignSequence2)
        {
            int alignLen = 0;
            int numOfSameReses = 0;
            for (int i = 0; i < alignSequence1.Length; i++)
            {
                if (alignSequence1[i] == '-' || alignSequence2[i] == '-') // skip missing positions, not fair for long alignments
                {
                    continue;
                }
                alignLen++;
                if (alignSequence1[i] == alignSequence2[i])
                {
                    numOfSameReses++;
                }
            }
            return (double)numOfSameReses / (double)alignLen;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="alignSeqNumbers"></param>
        /// <param name="seqAlignment"></param>
        /// <param name="seqStartPos"></param>
        /// <returns></returns>
        private string GetAlignSequence (string alignSeqNumbers, string seqAlignment,  int seqStartPos)
        {
            string[] seqNumbers = alignSeqNumbers.Split(',');
            int seqId = seqStartPos - 1;
            char[] alignSequence = new char[seqNumbers.Length];
            for (int i = 0; i < alignSequence.Length; i ++)
            {
                alignSequence[i] = '-';
            }
            int seqIndex = 0;
            foreach (char ch in seqAlignment)
            {
                if (ch != '-' && ch != '.')
                {
                    seqId++;
                    seqIndex = Array.IndexOf (seqNumbers, seqId.ToString ());
                    if (seqIndex > -1)
                    {
                        alignSequence[seqIndex] = ch;
                    }
                }
            }
            return alignSequence.ToString ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainId"></param>
        /// <returns></returns>
        private DataTable GetDomainFileInfoTable(string pdbId, long domainId)
        {
            string queryString = string.Format ("Select Distinct DomainID, EntityID, SeqStart, SeqEnd, FileStart, FileEnd From PdbPfamDomainFileInfo " +
                " WHere PdbID = '{0}' AND DomainID = {1};", pdbId, domainId);
            DataTable domainFileInfoTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            return domainFileInfoTable;
        }
      
        /// <summary>
        /// 
        /// </summary>
        /// <param name="hmmSeqNumbers1"></param>
        /// <param name="hmmSeqNumbers2"></param>
        /// <returns></returns>
        private string[] GetSeqNumberAlignment(int[][] hmmSeqNumbers1, int[][] hmmSeqNumbers2)
        {
            string queryNumbers1 = "";
            string queryNumbers2 = "";

            int[] hmmNumbers1 = hmmSeqNumbers1[0];
            int[] seqNumbers1 = hmmSeqNumbers1[1];
            int[] hmmNumbers2 = hmmSeqNumbers2[0];
            int[] seqNumbers2 = hmmSeqNumbers2[1];
            int hmmNumber1 = 0;
            int seqNumber1 = 0;
            int seqNumber2 = 0;
            int[] hmmIndexes2 = null;
            for (int i = 0; i < hmmNumbers1.Length; i++)
            {
                hmmNumber1 = hmmNumbers1[i];
                seqNumber1 = seqNumbers1[i];

                if (hmmNumber1 == -1 || seqNumber1 == -1)
                {
                    continue;
                }
       //         hmmIndexes2 = Array.FindAll<int>(hmmNumbers2, element => element.Equals );
                hmmIndexes2 = GetIndexesForInputHmmNumber(hmmNumbers2, hmmNumber1);
                if (hmmIndexes2.Length > 0)
                {
                    foreach (int hmmIndex2 in hmmIndexes2)
                    {
                        seqNumber2 = seqNumbers2[hmmIndex2];
                        if (seqNumber2 > -1)
                        {
                            queryNumbers1 += (seqNumber1.ToString() + ",");
                            queryNumbers2 += (seqNumber2.ToString() + ",");
                        }
                    }
                }
            }
            string[] alignSeqNumbers = new string[2];
            alignSeqNumbers[0] = queryNumbers1.TrimEnd (',');
            alignSeqNumbers[1] = queryNumbers2.TrimEnd (',');
            return alignSeqNumbers;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hmmNumbers"></param>
        /// <param name="hmmNumber"></param>
        /// <returns></returns>
        private int[] GetIndexesForInputHmmNumber(int[] hmmNumbers, int hmmNumber)
        {
            List<int> hmmIndexList = new List<int> ();
            for (int i = 0; i < hmmNumbers.Length; i++)
            {
                if (hmmNumbers[i] == hmmNumber)
                {
                    hmmIndexList.Add(i);
                }
            }
            return hmmIndexList.ToArray ();
        }
        /// <summary>
        /// the sequential numbers in HMM order
        /// </summary>
        /// <param name="domainDefRows"></param>
        /// <returns></returns>
        private int[][] GetHmmDomainFileSeqNumbersHash (DataRow[] domainDefRows)
        {
            List<int> hmmNumberList = new List<int> ();
            List<int> seqNumberList = new List<int> ();
            int hmmStart = 0;
            int alignStart = 0;
            int seqNumber = 0;
            int hmmNumber = 0;
            string queryAlignment = "";
            string hmmAlignment = "";
            int entityId = 0;
            long domainId = Convert.ToInt64(domainDefRows[0]["DomainID"].ToString ());
            string pdbId = domainDefRows[0]["PdbID"].ToString();
            DataTable domainFileInfoTable = GetDomainFileInfoTable(pdbId, domainId);
            foreach (DataRow domainDefRow in domainDefRows)
            {
                entityId = Convert.ToInt32 (domainDefRow["EntityID"].ToString ());
                hmmStart = Convert.ToInt32(domainDefRow["HmmStart"].ToString ());
                alignStart = Convert.ToInt32(domainDefRow["AlignStart"].ToString ());
                queryAlignment = domainDefRow["QueryAlignment"].ToString();
                hmmAlignment = domainDefRow["HmmAlignment"].ToString();

                seqNumber = alignStart - 1;
                hmmNumber = hmmStart - 1;
                for (int i = 0; i < hmmAlignment.Length; i++)
                {
                    if (hmmAlignment[i] != '.' && hmmAlignment[i] != '-')
                    {
                        hmmNumber++;
                        hmmNumberList.Add(hmmNumber);
                    }
                    else
                    {
                        hmmNumberList.Add(-1);
                    }
                    if (queryAlignment[i] != '-' && queryAlignment[i] != '.')
                    {
                        seqNumber++;

                        // multi-chain, change the sequence numbers
                        int fileSeqNumber = ConvertXmlNumberToFileNumber(domainFileInfoTable, seqNumber);
                        seqNumberList.Add(fileSeqNumber);
                     //   seqNumberList.Add(seqNumber + entityId * AppSettings.sudoSeqNumber);
                    }
                    else
                    {
                        seqNumberList.Add(-1);
                    }
                } 
            }
            int[][] hmmSeqNumbers = new int[2][];
            hmmSeqNumbers[0] = hmmNumberList.ToArray ();
            hmmSeqNumbers[1] = seqNumberList.ToArray ();
            return hmmSeqNumbers;
        }

        /// <summary>
        /// the sequential numbers in HMM order
        /// </summary>
        /// <param name="domainDefRows"></param>
        /// <returns></returns>
        private int[][] GetHmmDomainSeqNumbersHash(DataRow[] domainDefRows)
        {
            List<int> hmmNumberList = new List<int> ();
            List<int> seqNumberList =new List<int> ();
            int hmmStart = 0;
            int alignStart = 0;
            int seqNumber = 0;
            int hmmNumber = 0;
            string queryAlignment = "";
            string hmmAlignment = "";
            int entityId = 0;
            foreach (DataRow domainDefRow in domainDefRows)
            {
                entityId = Convert.ToInt32(domainDefRow["EntityID"].ToString());
                hmmStart = Convert.ToInt32(domainDefRow["HmmStart"].ToString());
                alignStart = Convert.ToInt32(domainDefRow["AlignStart"].ToString());
                queryAlignment = domainDefRow["QueryAlignment"].ToString();
                hmmAlignment = domainDefRow["HmmAlignment"].ToString();

                seqNumber = alignStart - 1;
                hmmNumber = hmmStart - 1;
                for (int i = 0; i < hmmAlignment.Length; i++)
                {
                    if (hmmAlignment[i] != '.' && hmmAlignment[i] != '-')
                    {
                        hmmNumber++;
                        hmmNumberList.Add(hmmNumber);
                    }
                    else
                    {
                        hmmNumberList.Add(-1);
                    }
                    if (queryAlignment[i] != '-' && queryAlignment[i] != '.')
                    {
                        seqNumber++;
                        seqNumberList.Add(seqNumber);
                    }
                    else
                    {
                        seqNumberList.Add(-1);
                    }
                }
            }
            int[][] hmmSeqNumbers = new int[2][];
            hmmSeqNumbers[0] = hmmNumberList.ToArray ();
            hmmSeqNumbers[1] = seqNumberList.ToArray ();
            return hmmSeqNumbers;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainId"></param>
        /// <param name="xmlSeqId"></param>
        /// <returns></returns>
        private int ConvertXmlNumberToFileNumber(DataTable domainFileInfoTable, int xmlSeqId)
        {
            int fileSeqId = -1;
            int seqStart = 0;
            int seqEnd = 0;
            int fileStart = 0;
            foreach (DataRow fileInfoRow in domainFileInfoTable.Rows)
            {
                seqStart = Convert.ToInt32(fileInfoRow["SeqStart"].ToString ());
                seqEnd = Convert.ToInt32(fileInfoRow["SeqEnd"].ToString ());
                if (xmlSeqId <= seqEnd && xmlSeqId >= seqStart)
                {
                    fileStart = Convert.ToInt32(fileInfoRow["FileStart"].ToString ());
                    fileSeqId = xmlSeqId + (fileStart - seqStart);
                    break;
                }
            }
            return fileSeqId;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainDefRows"></param>
        /// <returns></returns>
        private bool IsMultiChainDomain(DataRow[] domainDefRows)
        {
            List<int> entityList = new List<int> ();
            int entityId = 0;
            foreach (DataRow domainDefRow in domainDefRows)
            {
                entityId = Convert.ToInt32(domainDefRow["EntityID"].ToString());
                if (!entityList.Contains(entityId))
                {
                    entityList.Add(entityId);
                }
            }
            if (entityList.Count > 1)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainList"></param>
        /// <returns></returns>
        private DataTable GetDomainTable(long[] domainIds)
        {
            DataTable domainTable = null;
            foreach (long domainId in domainIds)
            {
                DataTable domainDefTable = GetDomainTable(domainId);
                if (domainTable == null)
                {
                    domainTable = domainDefTable.Copy();
                }
                else
                {
                    foreach (DataRow domainDefRow in domainDefTable.Rows)
                    {
                        DataRow dataRow = domainTable.NewRow();
                        dataRow.ItemArray = domainDefRow.ItemArray;
                        domainTable.Rows.Add(dataRow);
                    }
                }
            }
            return domainTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainId"></param>
        /// <returns></returns>
        private DataTable GetDomainTable(long domainId)
        {
            string queryString = string.Format("Select * From PdbPfam Where DomainID = {0};", domainId);
            DataTable domainTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            return domainTable;
        }
        #endregion

        #region domain alignments combined from HMMs and FATCAT
        // the alignment between two domains by HMM must cover the shorter domain at least 80%
        private double hmmAlignCoverage = 0.80;  
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainIds"></param>
        /// <returns></returns>
        public DataTable GetDomainAlignments(long[] domainIds)
        {
            domainAlignmentTable.Clear();
            long[] multiChainDomains = null;
            try
            {
                Dictionary<long, long[]> leftDomainPairHash = GetHmmAlignedDomainAlignments(domainIds, out multiChainDomains);
                foreach (long domainId in leftDomainPairHash.Keys)
                {
                    foreach (long leftDomain in leftDomainPairHash[domainId])
                    {
                        DataTable structDomainAlignTable = GetDomainAlignTable(domainId, leftDomain);
                        if (IsStructAlignmentsOk(structDomainAlignTable))
                        {
                            RemoveDomainPairHmmAlignments(domainId, leftDomain, domainAlignmentTable);
                            // no need to change the sequence numbers
                            // use the sequence numbers in the file
                            /*     if (Array.IndexOf(multiChainDomains, domainId) > -1)
                                 {
                                     UpdateStructDomainAlignSeqNumbers(structDomainAlignTable.Rows[0], domainId);
                                 }
                                 if (Array.IndexOf(multiChainDomains, leftDomain) > -1)
                                 {
                                     UpdateStructDomainAlignSeqNumbers(structDomainAlignTable.Rows[0], leftDomain);
                                 }*/
                            foreach (DataRow structAlignRow in structDomainAlignTable.Rows)
                            {
                                DataRow dataRow = domainAlignmentTable.NewRow();
                                dataRow.ItemArray = structAlignRow.ItemArray;
                                domainAlignmentTable.Rows.Add(dataRow);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ProtCidSettings.logWriter.WriteLine("Retrieve alignments from Pfam HMM alignments error: " + ex.Message + ". first domain in the list " + domainIds[0].ToString ());
                ProtCidSettings.logWriter.WriteLine("Retrieve alignments from structure alignments");
                ProtCidSettings.logWriter.Flush();
                domainAlignmentTable = GetStructDomainAlignments(domainIds);
            }
            return domainAlignmentTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainId1"></param>
        /// <param name="domainId2"></param>
        /// <param name="domainAlignTable"></param>
        private void RemoveDomainPairHmmAlignments(long domainId1, long domainId2, DataTable domainAlignTable)
        {
            DataRow[] domainAlignRows = domainAlignmentTable.Select(string.Format ("DomainID1 = '{0}' AND DomainID2 = '{1}'", 
                domainId1, domainId2));
            foreach (DataRow domainAlignRow in domainAlignRows)
            {
                domainAlignTable.Rows.Remove(domainAlignRow);
            }
            domainAlignTable.AcceptChanges();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="structAlignTable"></param>
        /// <returns></returns>
        private bool IsStructAlignmentsOk(DataTable structAlignTable)
        {
            if (structAlignTable == null || structAlignTable.Rows.Count == 0)
            {
                return false;
            }

            double pValue = Convert.ToDouble(structAlignTable.Rows[0]["Evalue"].ToString());
            if (pValue <= fatcatCutoff)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        ///  
        /// </summary>
        /// <param name="domainIds"></param>
        /// <returns></returns>
        private Dictionary<long, long[]> GetHmmAlignedDomainAlignments(long[] domainIds, out long[] multiChainDomains)
        {
            DataTable domainTable = GetDomainTable(domainIds);
            Dictionary<long, int[][]> domainHmmSeqNumbersHash = new Dictionary<long,int[][]> ();
            List<long> multiChainDomainList = new List<long> ();
            bool isMultiChain = false;
            foreach (long domainId in domainIds)
            {
                DataRow[] domainRows = domainTable.Select(string.Format("DomainID = {0}", domainId),
                     "HmmStart ASC");
                isMultiChain = IsMultiChainDomain(domainRows);
                if (isMultiChain)
                {
                    multiChainDomainList.Add(domainId);
                    int[][] hmmSeqNumbers = GetHmmDomainFileSeqNumbersHash(domainRows);
                    domainHmmSeqNumbersHash.Add(domainId, hmmSeqNumbers);
                }
                else
                {
                    int[][] hmmSeqNumbers = GetHmmDomainSeqNumbersHash(domainRows);
                    domainHmmSeqNumbersHash.Add(domainId, hmmSeqNumbers);
                }
            }
            multiChainDomains = new long[multiChainDomainList.Count];
            multiChainDomainList.CopyTo(multiChainDomains);

            long domainIdI = 0;
            long domainIdJ = 0;
            Dictionary<long, long[]> leftDomainPairHash = new Dictionary<long,long[]> ();
            List<long> leftDomainList = null;
            double identity = 0;
            for (int i = 0; i < domainIds.Length - 1; i++)
            {
                domainIdI = domainIds[i];
                int[][] hmmSeqNumbersI = (int[][])domainHmmSeqNumbersHash[domainIdI];
                leftDomainList = new List<long> ();
                for (int j = i + 1; j < domainIds.Length; j++)
                {
                    domainIdJ = domainIds[j];
                    int[][] hmmSeqNumbersJ = (int[][])domainHmmSeqNumbersHash[domainIdJ];
                    string[] alignSeqNumbers = GetSeqNumberAlignment(hmmSeqNumbersI, hmmSeqNumbersJ);
                    identity = GetDomainSeqIdentity(alignSeqNumbers, domainIdI, domainIdJ, domainTable);
                    // add hmm alignments
                    DataRow domainAlignRow = domainAlignmentTable.NewRow();
                    //     domainAlignRow["PdbID1"] = pdbId1;
                    domainAlignRow["DomainID1"] = domainIdI;
                    //    domainAlignRow["PdbID2"] = pdbId2;
                    domainAlignRow["DomainID2"] = domainIdJ;
                    domainAlignRow["Identity"] = identity;
                    domainAlignRow["Zscore"] = -1;
                    domainAlignRow["QueryStart"] = -1;
                    domainAlignRow["QueryEnd"] = -1;
                    domainAlignRow["HitStart"] = -1;
                    domainAlignRow["HitEnd"] = -1;
                    domainAlignRow["QuerySequence"] = "";
                    domainAlignRow["HitSequence"] = "";
                    domainAlignRow["QuerySeqNumbers"] = alignSeqNumbers[0];
                    domainAlignRow["HitSeqNumbers"] = alignSeqNumbers[1];
                    domainAlignmentTable.Rows.Add(domainAlignRow);
                    if (! IsHmmAlignmentOk(alignSeqNumbers, domainIdI, domainIdJ, domainTable))
                    {
                        leftDomainList.Add(domainIdJ);
                    }
                }
                if (leftDomainList.Count > 0)
                {
                    long[] leftDomainIds = new long[leftDomainList.Count];
                    leftDomainList.CopyTo(leftDomainIds);
                    leftDomainPairHash.Add(domainIdI, leftDomainIds);
                }
            }
            return leftDomainPairHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="alignSeqNumbers"></param>
        /// <param name="domainId1"></param>
        /// <param name="domainId2"></param>
        /// <param name="domainTable"></param>
        /// <returns></returns>
        private bool IsHmmAlignmentOk(string[] alignSeqNumbers, long domainId1, long domainId2, DataTable domainTable)
        {
            string[] alignedSeqNumbers = alignSeqNumbers[0].Split(',');
            int commonHmmLength = alignedSeqNumbers.Length;
            int domainLength1 = GetDomainLength (domainId1, domainTable);
            int domainlength2 = GetDomainLength (domainId2, domainTable);
            int minDomainLength = Math.Min(domainLength1, domainlength2);
            double coverage = (double)commonHmmLength / (double)minDomainLength;
            if (coverage >= hmmAlignCoverage)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainId"></param>
        /// <param name="domainTable"></param>
        /// <returns></returns>
        private int GetDomainLength(long domainId, DataTable domainTable)
        {
            DataRow[] domainRows = domainTable.Select("DomainID = " + domainId);
            int domainLength = 0;
            foreach (DataRow domainRow in domainRows)
            {
                domainLength += (Convert.ToInt32(domainRow["AlignEnd"].ToString()) -
                    Convert.ToInt32(domainRow["AlignStart"].ToString()) + 1);
            }
            return domainLength;
        }
        #endregion
    }
}
