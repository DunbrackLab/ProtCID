using System;
using System.IO;
using System.Data;
using System.Collections.Generic;
using DbLib;
using ProtCidSettingsLib;

namespace DataCollectorLib.FatcatAlignment
{
	/// <summary>
	/// Summary description for FatcatEntryPairs.
	/// </summary>
	public class FatcatEntryPairs
	{
		public DbQuery dbQuery = new DbQuery ();

		public FatcatEntryPairs()
		{
            if (ProtCidSettings.dirSettings == null)
            {
                ProtCidSettings.LoadDirSettings();
            }
			ProtCidSettings.pdbfamDbConnection = new DbConnect ("DRIVER=Firebird/InterBase(r) driver;" +
                "UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" + ProtCidSettings.dirSettings.pdbfamDbPath);
            ProtCidSettings.protcidDbConnection = new DbConnect("DRIVER=Firebird/InterBase(r) driver;" +
                "UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" + ProtCidSettings.dirSettings.protcidDbPath);
        }

        #region chain pairs missing alignments
        /// <summary>
        /// chain pairs with missing alignments when classified on PFAM and/or SCOP,
        /// use FATCAT or CE to find the alingments of the list of chain pairs 
        /// </summary>
        /// <param name="alignType"></param>
        public void FindMissingChainPairs(string alignType)
        {
            ProtCidSettings.alignmentDbConnection = new DbConnect();
            ProtCidSettings.alignmentDbConnection.ConnectString =
                "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=C:\\FireBird\\xtal\\alignments\\alignments.fdb";
            ProtCidSettings.dataType = "pfam";
            Dictionary<string, string> entityAuthChainHash = new Dictionary<string,string> ();
            StreamWriter dataWriter = new StreamWriter("missing" + alignType + "ChainPairs.txt");
            string queryString = string.Format ("Select PdbId1, EntityID1, PdbID2, EntityID2 " +
                " From {0}HomoGroupEntryAlign Where QueryStart = -1;", ProtCidSettings.dataType);
            DataTable groupHomoAlignTable = dbQuery.Query(ProtCidSettings.protcidDbConnection, queryString);
            FindMissingChainPairs(groupHomoAlignTable, dataWriter, ref entityAuthChainHash, alignType);
            dataWriter.Flush();

            queryString = string.Format ("Select PdbId1, EntityID1, PdbID2, EntityID2 " + 
                " From {0}HomoRepEntryAlign Where QueryStart = -1;", ProtCidSettings.dataType);
            DataTable groupRepHomoAlignTable = dbQuery.Query(ProtCidSettings.protcidDbConnection, queryString);
            FindMissingChainPairs(groupRepHomoAlignTable, dataWriter, ref entityAuthChainHash, alignType);

            dataWriter.Close();
        }

        /// <summary>
        /// the missing representative chains for the chain pairs in the classification
        /// </summary>
        /// <param name="groupHomoAlignTable"></param>
        /// <param name="dataWriter"></param>
        /// <param name="entityAuthChainHash"></param>
        /// <param name="alignType"></param>
        private void FindMissingChainPairs(DataTable groupHomoAlignTable, StreamWriter dataWriter,
            ref Dictionary<string, string> entityAuthChainHash, string alignType)
        {
            string pdbId1 = "";
            int entityId1 = -1;
            string repAuthChain1 = "";
            string pdbId2 = "";
            int entityId2 = -1;
            string repAuthChain2 = "";
            List<string> missingAlignChainPairList = new List<string> ();
            foreach (DataRow dRow in groupHomoAlignTable.Rows)
            {
                pdbId1 = dRow["PdbId1"].ToString();
                entityId1 = Convert.ToInt16(dRow["EntityID1"].ToString());
                repAuthChain1 = GetRepAuthorChain(pdbId1, entityId1, ref entityAuthChainHash);
                if (repAuthChain1 == "-") // not representative chain
                {
                    continue;
                }
                pdbId2 = dRow["PdbID2"].ToString();
                entityId2 = Convert.ToInt16(dRow["EntityID2"].ToString());
                repAuthChain2 = GetRepAuthorChain(pdbId2, entityId2, ref entityAuthChainHash);
                if (repAuthChain2 == "-") // not representative chain
                {
                    continue;
                }

                if (repAuthChain1 == repAuthChain2)
                {
                    continue;
                }
                if (missingAlignChainPairList.Contains(repAuthChain1 + "   " + repAuthChain2))
                {
                    continue;
                }
               
                missingAlignChainPairList.Add(repAuthChain1 + "   " + repAuthChain2);
                dataWriter.WriteLine(repAuthChain1 + "   " + repAuthChain2);
            }
        }


        /// <summary>
        /// the representative author chain for the input entry entity
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <param name="entityAuthChainHash"></param>
        /// <returns></returns>
        private string GetRepAuthorChain(string pdbId, int entityId, ref Dictionary<string, string> entityAuthChainHash)
        {
            string repAuthChain = "-";
            if (entityAuthChainHash.ContainsKey (pdbId + entityId.ToString()))
            {
                repAuthChain = entityAuthChainHash[pdbId + entityId.ToString()];
            }
            else
            {
                repAuthChain = GetRepChain(pdbId, entityId);
                entityAuthChainHash.Add(pdbId + entityId.ToString(), repAuthChain);
            }
            return repAuthChain;
        }

        /// <summary>
        /// the representative chains of the input entry
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetEntryRepChains(string pdbId)
        {
            string queryString = string.Format("Select PdbID, AuthorChain From PdbCrcMap Where PdbID = '{0}' AND IsRep = '1';", pdbId);         
            DataTable entryRepChainTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            string[] repAuthChains = new string[entryRepChainTable.Rows.Count];
            int i = 0;
            foreach (DataRow repAuthChainRow in entryRepChainTable.Rows)
            {
                repAuthChains[i] = repAuthChainRow["AuthorChain"].ToString().TrimEnd();
                i++;
            }
            return repAuthChains;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private string GetRepChain(string pdbId, int entityId)
        {
            string repChain = "-";
            string queryString = string.Format("Select crc From PdbCrcMap Where PdbID = '{0}' AND EntityID = {1};", pdbId, entityId);
            DataTable crcTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            if (crcTable.Rows.Count > 0)
            {
                string crc = crcTable.Rows[0]["crc"].ToString();
                queryString = string.Format("Select PdbID, AuthorChain From PdbCrcMap where Crc = '{0}' AND IsRep = '1';", crc);
                DataTable repChainTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
                if (repChainTable.Rows.Count > 0)
                {
                    repChain = repChainTable.Rows[0]["PdbID"].ToString() +
                        repChainTable.Rows[0]["AuthorChain"].ToString().TrimEnd();
                }
            }

            if (repChain == "")
            {
                string authorChain = GetAuthorChain(pdbId, entityId);
                if (authorChain != "-")
                {
                    repChain = pdbId + authorChain;
                }
            }
            return repChain;
        }

        private string GetAuthorChain(string pdbId, int entityId)
        {
            string queryString = string.Format("Select AuthorChain From AsymUnit " + 
                "WHere PdbID = '{0}' AND EntityID = {1};", pdbId, entityId);
            DataTable authChainTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            string authorChain = "-";
            if (authChainTable.Rows.Count > 0)
            {
                authorChain = authChainTable.Rows[0]["AuthorChain"].ToString().TrimEnd();
            }
            return authorChain;
        }
        #endregion
    }
}
