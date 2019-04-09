using System;
using System.Data;
using System.Collections.Generic;
using System.IO;
using DbLib;
using AuxFuncLib;
using ProtCidSettingsLib;
using CrystalInterfaceLib.Contacts;
using CrystalInterfaceLib.Crystal;

namespace InterfaceClusterLib.DomainInterfaces
{
	/// <summary>
	/// Domain-domain interfaces within a protein
	/// </summary>
	public class DomainInterfaceWithinProtein
	{
		#region member variables
		private DbQuery dbQuery = new DbQuery ();
		private DbInsert dbInsert = new DbInsert ();

//		private DataTable domainInterfaceTable = null;
		private DataTable multDomainTable = null;

	//	private NonScopDomainDef nonScopDomainDef = new NonScopDomainDef ();
		private DomainAtomsReader domainReader = new DomainAtomsReader ();
		private ChainContact domainContact = new ChainContact ();
		private DomainInterfaceWriter domainWriter = new DomainInterfaceWriter ();
		#endregion

		public DomainInterfaceWithinProtein()
		{
		}
		/// <summary>
		/// public interface to detect domains interactions within a multi-domain protein chain
		/// </summary>
		public void RetrieveDomainInteractionsWithinProteins (string pdbId, DataTable domainDefTable, ref int dInterfaceId)
		{
			FormatDomainDefTable (pdbId, domainDefTable);
			List<string> chainList = new List<string> ();
            string asymChain = "";
			foreach (DataRow dRow in multDomainTable.Rows)
			{
                asymChain = dRow["AsymChain"].ToString().TrimEnd();
                if (!chainList.Contains(asymChain))
				{
                    chainList.Add(asymChain);
				}
			}	

            DetectDomainInteraction(pdbId, chainList.ToArray (), ref dInterfaceId);
			
			// delete the used coordinate files
			string[] xmlFiles = Directory.GetFiles (ProtCidSettings.tempDir, pdbId + "*");
			foreach (string xmlFile in xmlFiles)
			{
				File.Delete (xmlFile);
			}
		}

		/// <summary>
		/// detect domain-domain interactions among domains in a chain
		/// </summary>
		/// <param name="pdbId"></param>
		/// <param name="chain"></param>
		private void DetectDomainInteraction (string pdbId, string[] asymChains, ref int dInterfaceId)
		{
            string dInterfaceFileDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, ProtCidSettings.dataType + "Domain");
            string hashDir = Path.Combine(dInterfaceFileDir, pdbId.Substring(1, 2));
            if (!Directory.Exists(hashDir))
            {
                Directory.CreateDirectory(hashDir);
            }

            Dictionary<string, ChainAtoms> chainAtomsHash = domainReader.ReadAtomsOfChains(pdbId, asymChains);

            foreach (string asymChain in asymChains)
            {
                // location ranges of a domain in the chain
                Dictionary<long, string> domainRangeHash = new Dictionary<long,string> ();
                DataRow[] domainRows = multDomainTable.Select
                    (string.Format("PdbID = '{0}' AND AsymChain = '{1}'", pdbId, asymChain));
                foreach (DataRow domainRow in domainRows)
                {
                    domainRangeHash.Add(Convert.ToInt64 (domainRow["DomainID"]), domainRow["DomainRanges"].ToString());
                }
                if (domainRangeHash.Count < 2)
                {
                    return;
                }
                // read atom coordinates from a file
               Dictionary<long, AtomInfo[]> domainAtomsHash = domainReader.ReadAtomsOfDomains(pdbId, asymChain, domainRangeHash, chainAtomsHash);

                List<long> domainList = new List<long> (domainAtomsHash.Keys);
                domainList.Sort();

                string remark = "";
                string dInterfaceFileName = "";
                for (int i = 0; i < domainList.Count - 1; i++)
                {
                    for (int j = i + 1; j < domainList.Count; j++)
                    {
                        ChainContactInfo domainInteraction =
                            domainContact.GetChainContactInfo((AtomInfo[])domainAtomsHash[domainList[i]],
                            (AtomInfo[])domainAtomsHash[domainList[j]]);
                        if (domainInteraction == null)
                        {
                            continue;
                        }

                        dInterfaceFileName = Path.Combine
                            (hashDir, pdbId + "_d" + dInterfaceId.ToString() + ".cryst");
                        if (!File.Exists(dInterfaceFileName + ".gz"))
                        {
                            remark = FormatRemark(pdbId, asymChain, domainList[i].ToString(), domainList[j].ToString());
                            domainWriter.WriteDomainInterfaceToFile(dInterfaceFileName, remark,
                                (AtomInfo[])domainAtomsHash[domainList[i]], (AtomInfo[])domainAtomsHash[domainList[j]]);
                            ParseHelper.ZipPdbFile(dInterfaceFileName);
                        }
                        AssignDomainInterfaceToTable(pdbId, asymChain, domainList[i].ToString(), domainList[j].ToString(), ref dInterfaceId);
                    }
                }
            }
		}

		/// <summary>
		/// format the remark field
		/// </summary>
		/// <param name="pdbId"></param>
		/// <param name="chain"></param>
		/// <param name="DomainID1"></param>
		/// <param name="DomainID2"></param>
		/// <returns></returns>
		public string FormatRemark (string pdbId, string chain, string DomainID1, string DomainID2)
		{
			DataRow[] domainRows1 = multDomainTable.Select (string.Format ("DomainID = '{0}'", DomainID1));
			DataRow[] domainRows2 = multDomainTable.Select (string.Format ("DomainID = '{0}'", DomainID2));
			string remark = "";
			remark = "HEADER    " + pdbId + "                     " + DateTime.Today;
            remark += "\r\nREMARK    Asymmetric Chain   " + chain;
			remark += "\r\nREMARK    PFAM Domain 1 " + DomainID1 + "  Domain Ranges   " + domainRows1[0]["DomainRanges"].ToString ();
			remark += "\r\nREMARK    PFAM Domain 2 " + DomainID2 + "  Domain Ranges   " + domainRows2[0]["DomainRanges"].ToString ();
			return remark;
		}

		#region assign data to table
		/// <summary>
		/// assign data to a table
		/// </summary>
		private void AssignDomainInterfaceToTable (string pdbId, string chain, 
			string DomainID1, string DomainID2, ref int dInterfaceId)
		{
			DataRow domainInterfaceRow = 
				DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaces].NewRow ();

			DataRow[] rangeRows = multDomainTable.Select (string.Format ("DomainID = '{0}'", DomainID1));
	//		domainInterfaceRow["DomainRange1"] = rangeRows[0]["DomainRanges"];
			string family1 = rangeRows[0]["FamilyCode"].ToString ().Trim ();
			rangeRows = multDomainTable.Select (string.Format ("DomainID = '{0}'", DomainID2));
		//	domainInterfaceRow["DomainRange2"] = rangeRows[0]["DomainRanges"];
			string family2 = rangeRows[0]["FamilyCode"].ToString ().Trim ();
			char isReversed = '0';
            int relSeqId = DomainClassifier.GetRelSeqId(family1, family2, out isReversed);

			domainInterfaceRow["RelSeqID"] = relSeqId;
	//		domainInterfaceRow["GroupSeqID"] = groupId;
			domainInterfaceRow["PdbID"] = pdbId;
			domainInterfaceRow["InterfaceID"] = 0;
			domainInterfaceRow["DomainInterfaceId"] = dInterfaceId;
			domainInterfaceRow["DomainID1"] = DomainID1;
			domainInterfaceRow["DomainID2"] = DomainID2;
			dInterfaceId ++;
	//		domainInterfaceRow["NumOfSg"] = 1;
	//		domainInterfaceRow["NumOfSg_Identity"] = 1;
			domainInterfaceRow["AsymChain1"] = chain;
			domainInterfaceRow["AsymChain2"] = chain;
			domainInterfaceRow["IsReversed"] = isReversed;
            domainInterfaceRow["SurfaceArea"] = -1;
			
			DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaces].Rows.Add (domainInterfaceRow);
		}
		#endregion

		#region domain definition table
		/// <summary>
		/// format the returned domain definition table into multdomainTable
		/// </summary>
		/// <param name="nonScopMultDomainEntryTable"></param>
		private void FormatDomainDefTable (string pdbId, DataTable domainDefTable)
		{
			string familyCode = "";
            string domainRangeString = "";
			string authChain = "";
            string asymChain = "";
			int entityId = -1;
			if (multDomainTable == null)
			{
				InitializeTable ();
			}
			multDomainTable.Clear ();
			string queryString = string.Format ("SELECT AsymID, EntityID, AuthorChain FROM AsymUnit " + 
				" WHERE PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
            DataTable asymChainTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            long[] domainIds = GetListOfDomains(domainDefTable);
            foreach (long domainId in domainIds) 
            {
                DataRow[] domainRows = domainDefTable.Select("DomainID = " + domainId);
                domainRangeString = "";
                foreach (DataRow domainRow in domainRows)
                {
                    domainRangeString += (domainRow["SeqStart"].ToString().Trim() +
                                        "-" + domainRow["SeqEnd"].ToString().Trim() + ";");
                }
                domainRangeString = domainRangeString.TrimEnd(';');
                if (ProtCidSettings.dataType == "scop")
                {
                    familyCode = domainRows[0]["Class"].ToString().TrimEnd() + "." +
                        domainRows[0]["Fold"].ToString() + "." +
                        domainRows[0]["Superfamily"].ToString() + "." +
                        domainRows[0]["Family"].ToString();
                }
                else if (ProtCidSettings.dataType == "pfam")
                {
                    familyCode = domainRows[0]["Pfam_ID"].ToString().TrimEnd();
                }
				entityId = Convert.ToInt32 (domainRows[0]["EntityID"].ToString ());
				DataRow[] chainRows = asymChainTable.Select (string.Format ("EntityID = {0}", entityId));
                if (chainRows.Length == 0)
				{
					continue;
				}
                foreach (DataRow chainRow in chainRows)
                {
                    asymChain = chainRow["AsymID"].ToString().TrimEnd();
                    authChain = chainRow["AuthorChain"].ToString().TrimEnd();

                    DataRow newRow = multDomainTable.NewRow();
                    newRow["PdbID"] = pdbId;
                    newRow["AuthChain"] = authChain;
                    newRow["AsymChain"] = asymChain;
                    newRow["EntityID"] = entityId;
                    newRow["DomainID"] = domainId;
                    newRow["FamilyCode"] = familyCode;
                    newRow["DomainRanges"] = domainRangeString;
                    multDomainTable.Rows.Add(newRow);
                }
			}
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainTable"></param>
        /// <returns></returns>
        private long[] GetListOfDomains(DataTable domainTable)
        {
            List<long> domainList = new List<long> ();
            long domainId = 0;
            foreach (DataRow domainRow in domainTable.Rows)
            {
                domainId = Convert.ToInt64(domainRow["DomainID"].ToString ());
                if (! domainList.Contains(domainId))
                {
                    domainList.Add(domainId);
                }
            }
            return domainList.ToArray ();
        }
		#endregion

		#region initialization
		/// <summary>
		/// initialize memory tables and db tables
		/// </summary>
		/// <param name="isUpdate"></param>
		private void InitializeTable ()
		{
		/*	string[] cols = {"RelSeqID", "PdbID", "EntityID", "AsymChain", "DomainID1", "DomainID2", "DomainRange1", "DomainRange2"};
			domainInterfaceTable = new DataTable ("DomainInterfacesInProtein");
			foreach (string col in cols)
			{
				domainInterfaceTable.Columns.Add (new DataColumn (col));
			}*/
			string[] multDomainCols = {"PdbID", "AuthChain", "AsymChain", "EntityID", "DomainID", "DomainRanges", "FamilyCode"};
			multDomainTable = new DataTable ("MultDomainDef");
			foreach (string col in multDomainCols)
			{
				multDomainTable.Columns.Add (new DataColumn(col));
			}
		/*	if (isUpdate)
			{
				string createTableString = "CREATE TABLE DomainInterfacesInProtein ( " + 
					" RelSeqID INTEGER NOT NULL, PdbID CHAR(4) NOT NULL, " + 
					" EntityID INTEGER NOT NULL, DomainID1 INTEGER NOT NULL, DomainID2 INTEGER NOT NULL, " + 
					" DomainRange1 VARCHAR(50) NOT NULL, DomainRange2 VARCHAR(50) NOT NULL );";
				DbCreator dbCreate = new DbCreator ();
				dbCreate.CreateTableFromString (createTableString, "DomainInterfacesInProtein");
			}*/
		}
		#endregion
	}
}
