using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using DbLib;
using ProtCidSettingsLib;

namespace AuxFuncLib
{
    public class SeqNumbersMatchData
    {
        #region one-to-one residue number matching
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <param name="uniprot"></param>
        /// <param name="pdbSeqNubmers"></param>
        /// <returns></returns>
        public string[] ConvertPdbSeqNumbersToUnpNumbers (string pdbId, int entityId, string uniprot, string[] pdbSeqNubmers)
        {
            Dictionary<string, string> seqNumberMatchDict = GetEntityPdbUnpAlignmentFromSifts(pdbId, entityId, uniprot);
            string[] unpSeqNumbers = new string[pdbSeqNubmers.Length];
            if (seqNumberMatchDict.Count > 0)
            {
                for (int i = 0; i < pdbSeqNubmers.Length; i ++)
                {
                    if (seqNumberMatchDict.ContainsKey (pdbSeqNubmers[i]))
                    {
                        unpSeqNumbers[i] = seqNumberMatchDict [pdbSeqNubmers[i]];
                    }
                    else
                    {
                        unpSeqNumbers[i] = "-1";
                    }
                }
            }
            else
            {
                List<int[]>[] pdbUnpStartEndPosLists = GetPdbEntityUnpSeqMatchRegionFromXml(pdbId, entityId, uniprot);
                List<int[]> startEndPosPdbList = pdbUnpStartEndPosLists[0];
                List<int[]> startEndPosUnpList = pdbUnpStartEndPosLists[1];
                int pdbSeqNum = 0;
                int dbSeqNum = 0;
                for (int i = 0; i < pdbSeqNubmers.Length; i ++)
                {
                    if (Int32.TryParse(pdbSeqNubmers[i], out pdbSeqNum))
                    {
                        dbSeqNum = GetDbSeqNumber(pdbSeqNum, startEndPosPdbList, startEndPosUnpList);
                        unpSeqNumbers[i] = dbSeqNum.ToString();
                    }
                    else
                    {
                        unpSeqNumbers[i] = "-1";
                    }
                }
            }
            return unpSeqNumbers;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <param name="uniprot"></param>
        /// <param name="pdbSeqNubmers"></param>
        /// <returns></returns>
        public string[] ConvertPdbAuthorSeqNumbersToUnpNumbers(string pdbId, int entityId, string uniprot, string[] authorSeqNubmers)
        {
            Dictionary<string, string> seqNumberMatchDict = GetEntityPdbAuthUnpAlignmentFromSifts(pdbId, entityId, uniprot);
            string[] unpSeqNumbers = new string[authorSeqNubmers.Length];
            if (seqNumberMatchDict.Count > 0)
            {
                for (int i = 0; i < authorSeqNubmers.Length; i++)
                {
                    if (seqNumberMatchDict.ContainsKey(authorSeqNubmers[i]))
                    {
                        unpSeqNumbers[i] = seqNumberMatchDict[authorSeqNubmers[i]];
                    }
                    else
                    {
                        unpSeqNumbers[i] = "-1";
                    }
                }
            }
            else
            {
                List<int[]>[] pdbUnpStartEndPosLists = GetPdbEntityUnpAuthorSeqMatchRegionFromXml (pdbId, entityId, uniprot);
                List<int[]> startEndPosPdbList = pdbUnpStartEndPosLists[0];
                List<int[]> startEndPosUnpList = pdbUnpStartEndPosLists[1];
                int pdbSeqNum = 0;
                int dbSeqNum = 0;
                for (int i = 0; i < authorSeqNubmers.Length; i++)
                {
                    if (Int32.TryParse(authorSeqNubmers[i], out pdbSeqNum))
                    {
                        dbSeqNum = GetDbSeqNumber(pdbSeqNum, startEndPosPdbList, startEndPosUnpList);
                        unpSeqNumbers[i] = dbSeqNum.ToString();
                    }
                    else
                    {
                        unpSeqNumbers[i] = "-1";
                    }
                }
            }
            return unpSeqNumbers;
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entities"></param>
        /// <returns></returns>
        public Dictionary<int, Dictionary<string, string>> ConvertPdbSeqNumbersToUnpNumbers (string pdbId, int[] entities)
        {
            Dictionary<int, Dictionary<string, string>> entitySeqNumbersMatchDict = GetEntityPdbUnpAlignmentsFromSifts (pdbId, entities);
            if (entitySeqNumbersMatchDict.Count == 0)
            {
                entitySeqNumbersMatchDict = GetPdbEntityUnpSeqMatchFromXml (pdbId, entities);
            }
            return entitySeqNumbersMatchDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <param name="uniprot"></param>
        /// <returns></returns>
        public List<int[]>[] GetPdbEntityUnpSeqMatchRegions (string pdbId, int entityId, string uniprot)
        {
            List<int[]>[] entityUnpMatchRegions = GetPdbEntityUnpSeqMatchRegionFromSifts(pdbId, entityId, uniprot);
            if (entityUnpMatchRegions[0].Count == 0)
            {
                entityUnpMatchRegions = GetPdbEntityUnpSeqMatchRegionFromXml (pdbId, entityId, uniprot);
            }
            return entityUnpMatchRegions;
        }
        #endregion

        #region match regions from XML and Sifts
        /// <summary>
        /// regions match
        /// pdb sequential residues numbers - uniprot residue numbers 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <param name="uniprot"></param>
        /// <returns></returns>
        public List<int[]>[] GetPdbSeqDbMatchRegions(string pdbId, int entityId, string uniprot)
        {
            List<int[]>[] pdbDbSeqNumberMatchRegions = GetPdbEntityUnpSeqMatchRegionFromSifts (pdbId, entityId, uniprot);
            if (pdbDbSeqNumberMatchRegions == null)
            {
                pdbDbSeqNumberMatchRegions = GetPdbEntityUnpAuthorSeqMatchRegionFromXml(pdbId, entityId, uniprot);
            }
            return pdbDbSeqNumberMatchRegions;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <param name="uniprots">the corresponding uniprots </param>
        /// <returns>0: pdb sequential numbers 1: uniprot residue numbers start and end positions</returns>
        public List<int[]>[] GetPdbSeqDbMatchRegions (string pdbId, int entityId, out string[] uniprots)
        {
            List<int[]>[] pdbDbSeqNumberMatchRegions = GetPdbEntityUnpSeqMatchRegionFromSifts (pdbId, entityId, out uniprots);
            if (pdbDbSeqNumberMatchRegions == null)
            {
                pdbDbSeqNumberMatchRegions = GetPdbEntityUnpSeqMatchRegionFromXml(pdbId, entityId, out uniprots);
            }
            return pdbDbSeqNumberMatchRegions;
        }       
        #endregion

        #region sequence numbers matching from XML tables: PdbDbRefXml, PdbDbRefSeqXml
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entities"></param>
        /// <returns></returns>
        public Dictionary<int, Dictionary<string, string>> GetPdbEntityUnpSeqMatchFromXml (string pdbId, int[] entities)
        {
            Dictionary<int, Dictionary<string, string>> pdbUnpSeqMatchDict = new Dictionary<int, Dictionary<string, string>>();
            foreach (int entityId in entities)
            {
                Dictionary<string, string> entityUnpSeqNumberDict = GetPdbEntityUnpSeqMatchFromXml(pdbId, entityId);
                pdbUnpSeqMatchDict.Add(entityId, entityUnpSeqNumberDict);
            }
            return pdbUnpSeqMatchDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        public Dictionary<string, string> GetPdbEntityUnpSeqMatchFromXml (string pdbId, int entityId)
        {
            Dictionary<string, string> pdbUnpSeqNumberDict = new Dictionary<string, string>();
            string[] uniprots = null;
            List<int[]>[] startEndPosLists = GetPdbEntityUnpSeqMatchRegionFromXml (pdbId, entityId, out uniprots);
            int seqLength = GetEntitySequenceLength(pdbId, entityId);
            int unpSeqNum = 0;
            for (int i = 1; i <= seqLength; i ++)
            {
                unpSeqNum = GetDbSeqNumber(i, startEndPosLists[0], startEndPosLists[1]);
                pdbUnpSeqNumberDict.Add(i.ToString(), unpSeqNum.ToString());
            }
            return pdbUnpSeqNumberDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private int GetEntitySequenceLength (string pdbId, int entityId)
        {
            string queryString = string.Format("Select Sequence From AsymUnit Where PdbID = '{0}' AND EntityID = {1};", pdbId, entityId);
            DataTable seqTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string sequence = "";
            if (seqTable.Rows.Count > 0)
            {
                sequence = seqTable.Rows[0]["Sequence"].ToString().TrimEnd();
            }
            return sequence.Length;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbSeqNumber"></param>
        /// <param name="startEndPosPdbList"></param>
        /// <param name="startEndPosUnpList"></param>
        /// <returns></returns>
        private int GetDbSeqNumber(int pdbSeqNumber, List<int[]> startEndPosPdbList, List<int[]> startEndPosUnpList)
        {
            int unpSeqNumber = -1;
            for (int i = 0; i < startEndPosPdbList.Count; i ++)
            {
                if (pdbSeqNumber <= startEndPosPdbList[i][1] && pdbSeqNumber >= startEndPosPdbList[i][0])
                {
                    unpSeqNumber = startEndPosUnpList[i][0] + pdbSeqNumber - startEndPosPdbList[i][0];
                    break;
                }
            }
            return unpSeqNumber;
        }
        /// <summary>
        /// the different of start sequence number between pdb entity and uniprot
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <param name="uniprot"></param>
        /// <returns></returns>
        public List<int[]>[] GetPdbEntityUnpSeqMatchRegionFromXml (string pdbId, int entityId, string uniprot)
        {
            string queryString = string.Format("Select Distinct PdbDbRefXml.PdbID, EntityID, DbCode As UnpID, DbAlignBeg, DbAlignEnd, SeqAlignBeg, SeqAlignEnd " +
                       " From PdbDbRefXml, PdbDbRefSeqXml " +
                       " Where PdbDbRefXml.PdbID = '{0}' AND  EntityID = {1} AND DbCode = '{2}' AND " +
                       " PdbDbRefXml.PdbID = PdbDbRefSeqXml.PdbID  AND PdbDbRefXml.RefID = PdbDbRefSeqXml.RefID " +
                       " Order By PdbDbRefXml.PdbID, EntityID;", pdbId, entityId, uniprot);
            DataTable pdbUnpRegionMatchTable = ProtCidSettings.pdbfamQuery.Query(queryString);

            List<int[]> pdbEntityPosList =  new List<int[]> ();
            List<int[]> dbEntityPosList = new List<int[]> ();
            foreach (DataRow matchRow in pdbUnpRegionMatchTable.Rows)
            {
                int[] startEndPosPdb = new int[2];
                startEndPosPdb[0] = Convert.ToInt32(matchRow["SeqAlignBeg"].ToString ());
                startEndPosPdb[1] = Convert.ToInt32(matchRow["SeqAlignEnd"].ToString ());
                int[] startEndPosUnp = new int[2];
                startEndPosUnp[0] = Convert.ToInt32(matchRow["DbAlignBeg"].ToString ());
                startEndPosUnp[1] = Convert.ToInt32(matchRow["DbAlignEnd"].ToString ());
                pdbEntityPosList.Add(startEndPosPdb);
                dbEntityPosList.Add(startEndPosUnp);
            }
            List<int[]>[] startEndPosLists = new List<int[]>[2];
            startEndPosLists[0] = pdbEntityPosList;
            startEndPosLists[1] = dbEntityPosList;
            return startEndPosLists;
        }

        /// <summary>
        /// the different of start sequence number between pdb entity and uniprot
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <param name="uniprot"></param>
        /// <returns></returns>
        public List<int[]>[] GetPdbEntityUnpAuthorSeqMatchRegionFromXml(string pdbId, int entityId, string uniprot)
        {
            string queryString = string.Format("Select Distinct PdbDbRefXml.PdbID, EntityID, DbCode As UnpID, DbAlignBeg, DbAlignEnd, AuthorAlignBeg, AuthorAlignEnd " +
                       " From PdbDbRefXml, PdbDbRefSeqXml " +
                       " Where PdbDbRefXml.PdbID = '{0}' AND  EntityID = {1} AND DbCode = '{2}' AND " +
                       " PdbDbRefXml.PdbID = PdbDbRefSeqXml.PdbID  AND PdbDbRefXml.RefID = PdbDbRefSeqXml.RefID " +
                       " Order By PdbDbRefXml.PdbID, EntityID;", pdbId, entityId, uniprot);
            DataTable pdbUnpRegionMatchTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<int[]> pdbEntityPosList = new List<int[]>();
            List<int[]> dbEntityPosList = new List<int[]>();
            foreach (DataRow matchRow in pdbUnpRegionMatchTable.Rows)
            {
                int[] startEndPosPdb = new int[2];
                startEndPosPdb[0] = Convert.ToInt32(matchRow["AuthorAlignBeg"].ToString());
                startEndPosPdb[1] = Convert.ToInt32(matchRow["AuthorAlignEnd"].ToString());
                int[] startEndPosUnp = new int[2];
                startEndPosUnp[0] = Convert.ToInt32(matchRow["DbAlignBeg"].ToString());
                startEndPosUnp[1] = Convert.ToInt32(matchRow["DbAlignEnd"].ToString());
                pdbEntityPosList.Add(startEndPosPdb);
                dbEntityPosList.Add(startEndPosUnp);
            }
            List<int[]>[] startEndPosLists = new List<int[]>[2];
            startEndPosLists[0] = pdbEntityPosList;
            startEndPosLists[1] = dbEntityPosList;
            return startEndPosLists;
        }

        /// <summary>
        /// the different of start sequence number between pdb entity and uniprot
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <param name="uniprot"></param>
        /// <returns></returns>
        public List<int[]>[] GetPdbEntityUnpSeqMatchRegionFromXml(string pdbId, int entityId, out string[] uniprots)
        {
            string queryString = string.Format("Select Distinct PdbDbRefXml.PdbID, EntityID, DbCode As UnpID, DbAlignBeg, DbAlignEnd, SeqAlignBeg, SeqAlignEnd " +
                       " From PdbDbRefXml, PdbDbRefSeqXml " +
                       " Where PdbDbRefXml.PdbID = '{0}' AND  EntityID = {1} AND " +
                       " PdbDbRefXml.PdbID = PdbDbRefSeqXml.PdbID  AND PdbDbRefXml.RefID = PdbDbRefSeqXml.RefID " +
                       " Order By PdbDbRefXml.PdbID, EntityID, SeqAlignBeg;", pdbId, entityId);
            DataTable pdbUnpRegionMatchTable = ProtCidSettings.pdbfamQuery.Query(queryString);

            List<int[]> pdbEntityPosList = new List<int[]>();
            List<int[]> dbEntityPosList = new List<int[]>();
            List<string> entityUnpList = new List<string>();
            foreach (DataRow matchRow in pdbUnpRegionMatchTable.Rows)
            {
                int[] startEndPosPdb = new int[2];
                startEndPosPdb[0] = Convert.ToInt32(matchRow["SeqAlignBeg"].ToString());
                startEndPosPdb[1] = Convert.ToInt32(matchRow["SeqAlignEnd"].ToString());
                int[] startEndPosUnp = new int[2];
                startEndPosUnp[0] = Convert.ToInt32(matchRow["DbAlignBeg"].ToString());
                startEndPosUnp[1] = Convert.ToInt32(matchRow["DbAlignEnd"].ToString());
                pdbEntityPosList.Add(startEndPosPdb);
                dbEntityPosList.Add(startEndPosUnp);
                entityUnpList.Add(matchRow["DbCode"].ToString ().TrimEnd ());
            }
            uniprots = entityUnpList.ToArray();

            List<int[]>[] startEndPosLists = new List<int[]>[2];
            startEndPosLists[0] = pdbEntityPosList;
            startEndPosLists[1] = dbEntityPosList;
            return startEndPosLists;
        }
        #endregion

        #region sequence numbers matching from sifts tables: PdbDbRefSifts, PdbDbRefSeqSifts, PdbDbRefSeqAlignSifts
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        public Dictionary<string, string> GetEntityPdbUnpAlignmentFromSifts (string pdbId, int entityId, string uniprot)
        {
            string asymChain = GetAsymChainForEntityId(pdbId, entityId);
            string queryString = string.Format("Select PdbDbRefSeqAlignSifts.PdbID, " +
                " DbCode, AsymID, AuthorChain, SeqNumbers, DbSeqNumbers" +
                " From PdbDbRefSeqAlignSifts, PdbDbRefSifts " +
                " Where PdbDbRefSeqAlignSifts.PdbID = '{0}' AND AsymID = '{1}' AND DbCode = '{2}' AND " +
                " PdbDbRefSeqAlignSifts.PdbID = PdbDbRefSifts.PdbID AND " +
                " PdbDbRefSeqAlignSifts.RefID = PdbDbRefSifts.RefID ;", pdbId, asymChain, uniprot);
            DataTable alignmentTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            Dictionary<string, string> pdbUnpMapHash = new Dictionary<string, string>();
            string seqNumbers = "";
            string dbSeqNumbers = "";
            foreach (DataRow alignmentRow in alignmentTable.Rows)
            {
                seqNumbers = alignmentRow["SeqNumbers"].ToString();
                string[] seqNumberFields = seqNumbers.Split(',');
                dbSeqNumbers = alignmentRow["DbSeqNumbers"].ToString();
                string[] dbSeqNumberFields = dbSeqNumbers.Split(',');
                // the length of seqNumbers and dbSeqNumbers should be same
                for (int i = 0; i < seqNumberFields.Length; i++)
                {
                    if (seqNumberFields[i] != "-" && dbSeqNumberFields[i] != "-")
                    {
                        pdbUnpMapHash.Add(seqNumberFields[i], dbSeqNumberFields[i]);
                    }
                }
            }
            return pdbUnpMapHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        public Dictionary<string, string> GetEntityPdbAuthUnpAlignmentFromSifts(string pdbId, int entityId, string uniprot)
        {
            string asymChain = GetAsymChainForEntityId(pdbId, entityId);
            string queryString = string.Format("Select PdbDbRefSeqAlignSifts.PdbID, " +
                " DbCode, AsymID, AuthorChain, AuthorSeqNumbers, DbSeqNumbers" +
                " From PdbDbRefSeqAlignSifts, PdbDbRefSifts " +
                " Where PdbDbRefSeqAlignSifts.PdbID = '{0}' AND AsymID = '{1}' AND DbCode = '{2}' AND " +
                " PdbDbRefSeqAlignSifts.PdbID = PdbDbRefSifts.PdbID AND " +
                " PdbDbRefSeqAlignSifts.RefID = PdbDbRefSifts.RefID ;", pdbId, asymChain, uniprot);
            DataTable alignmentTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            Dictionary<string, string> pdbUnpMapHash = new Dictionary<string, string>();
            string seqNumbers = "";
            string dbSeqNumbers = "";
            foreach (DataRow alignmentRow in alignmentTable.Rows)
            {
                seqNumbers = alignmentRow["AuthorSeqNumbers"].ToString();
                string[] seqNumberFields = seqNumbers.Split(',');
                dbSeqNumbers = alignmentRow["DbSeqNumbers"].ToString();
                string[] dbSeqNumberFields = dbSeqNumbers.Split(',');
                // the length of seqNumbers and dbSeqNumbers should be same
                for (int i = 0; i < seqNumberFields.Length; i++)
                {
                    if (seqNumberFields[i].ToLower () == "null")
                    {
                        continue;
                    }
                    if (seqNumberFields[i] != "-" && dbSeqNumberFields[i] != "-")
                    {
                        pdbUnpMapHash.Add(seqNumberFields[i], dbSeqNumberFields[i]);
                    }
                }
            }
            return pdbUnpMapHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entities"></param>
        /// <returns></returns>
        public Dictionary<int, Dictionary<string, string>> GetEntityPdbUnpAlignmentsFromSifts (string pdbId)
        {
            string queryString = string.Format("Select Distinct EntityID From PdbDbRefSifts Where PdbID = '{0}' AND DbName = 'UNP';", pdbId);
            DataTable entityTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            int[] entities = new int[entityTable.Rows.Count];
            int count = 0;
            foreach (DataRow entityRow in entityTable.Rows)
            {
                entities[count] = Convert.ToInt32(entityRow["EntityID"].ToString ());
                count++;
            }
            Dictionary<int, Dictionary<string, string>> entityPdbUnpMapHash = GetEntityPdbUnpAlignmentsFromSifts (pdbId, entities);
            return entityPdbUnpMapHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entities"></param>
        /// <returns></returns>
        public Dictionary<int, Dictionary<string, string>> GetEntityPdbUnpAlignmentsFromSifts (string pdbId, int[] entities)
        {
            Dictionary<int, Dictionary<string, string>> entityPdbUnpMapHash = new Dictionary<int, Dictionary<string, string>>();
            foreach (int entityId in entities)
            {
                Dictionary<string, string> pdbUnpMapHash = GetEntityPdbUnpAlignmentFromSifts (pdbId, entityId);
                entityPdbUnpMapHash.Add(entityId, pdbUnpMapHash);
            }
            return entityPdbUnpMapHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        public Dictionary<string, string> GetEntityPdbUnpAlignmentFromSifts (string pdbId, int entityId)
        {
            string asymChain = GetAsymChainForEntityId(pdbId, entityId);
            string queryString = string.Format("Select PdbDbRefSeqAlignSifts.PdbID, " +
                " DbCode, AsymID, AuthorChain, SeqNumbers, DbSeqNumbers" +
                " From PdbDbRefSeqAlignSifts, PdbDbRefSifts " +
                " Where PdbDbRefSeqAlignSifts.PdbID = '{0}' AND AsymID = '{1}' AND" +
                " PdbDbRefSeqAlignSifts.PdbID = PdbDbRefSifts.PdbID AND " +
                " PdbDbRefSeqAlignSifts.RefID = PdbDbRefSifts.RefID ;", pdbId, asymChain);
            DataTable alignmentTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            Dictionary<string, string> pdbUnpMapHash = new Dictionary<string, string>();
            string seqNumbers = "";
            string dbSeqNumbers = "";
            foreach (DataRow alignmentRow in alignmentTable.Rows)
            {
                seqNumbers = alignmentRow["SeqNumbers"].ToString();
                string[] seqNumberFields = seqNumbers.Split(',');
                dbSeqNumbers = alignmentRow["DbSeqNumbers"].ToString();
                string[] dbSeqNumberFields = dbSeqNumbers.Split(',');
                // the length of seqNumbers and dbSeqNumbers should be same
                for (int i = 0; i < seqNumberFields.Length; i++)
                {
                    if (seqNumberFields[i] != "-" && dbSeqNumberFields[i] != "-")
                    {
                        pdbUnpMapHash.Add(seqNumberFields[i], dbSeqNumberFields[i]);
                    }
                }
            }
            return pdbUnpMapHash;
        }

        /// <summary>
        /// the different of start sequence number between pdb entity and uniprot
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <param name="uniprot"></param>
        /// <returns></returns>
        public List<int[]>[] GetPdbEntityUnpSeqMatchRegionFromSifts (string pdbId, int entityId, string uniprot)
        {
            string queryString = string.Format("Select Distinct PdbDbRefSifts.PdbID, EntityID, DbCode As UnpID, DbAlignBeg, DbAlignEnd, SeqAlignBeg, SeqAlignEnd " +
                       " From PdbDbRefSifts, PdbDbRefSeqSifts " +
                       " Where PdbDbRefSifts.PdbID = '{0}' AND  EntityID = {1} AND DbCode = '{2}' AND " +
                       " PdbDbRefSifts.PdbID = PdbDbRefSeqSifts.PdbID  AND PdbDbRefSifts.RefID = PdbDbRefSeqSifts.RefID " +
                       " Order By PdbDbRefSifts.PdbID, EntityID;", pdbId, entityId, uniprot);
            DataTable pdbUnpRegionMatchTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (pdbUnpRegionMatchTable.Rows.Count == 0)
            {
                return null;
            }
            List<int[]> pdbEntityPosList = new List<int[]>();
            List<int[]> dbEntityPosList = new List<int[]>();
            foreach (DataRow matchRow in pdbUnpRegionMatchTable.Rows)
            {
                int[] startEndPosPdb = new int[2];
                startEndPosPdb[0] = Convert.ToInt32(matchRow["SeqAlignBeg"].ToString());
                startEndPosPdb[1] = Convert.ToInt32(matchRow["SeqAlignEnd"].ToString());
                int[] startEndPosUnp = new int[2];
                startEndPosUnp[0] = Convert.ToInt32(matchRow["DbAlignBeg"].ToString());
                startEndPosUnp[1] = Convert.ToInt32(matchRow["DbAlignEnd"].ToString());
                pdbEntityPosList.Add(startEndPosPdb);
                dbEntityPosList.Add(startEndPosUnp);
            }
            List<int[]>[] startEndPosLists = new List<int[]>[2];
            startEndPosLists[0] = pdbEntityPosList;
            startEndPosLists[1] = dbEntityPosList;
            return startEndPosLists;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private string GetAsymChainForEntityId(string pdbId, int entityId)
        {
            string queryString = string.Format("Select AsymID From AsymUnit WHERE PdbID = '{0}' AND EntityID = {1};",
                pdbId, entityId);
            DataTable asymChainTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (asymChainTable.Rows.Count > 0)
            {
                return asymChainTable.Rows[0]["AsymID"].ToString().TrimEnd();
            }
            return "";
        }

           /// <summary>
        /// the different of start sequence number between pdb entity and uniprot
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <param name="uniprot"></param>
        /// <returns></returns>
        public List<int[]>[] GetPdbEntityUnpSeqMatchRegionFromSifts(string pdbId, int entityId, out string[] uniprots)
        {
            string queryString = string.Format("Select Distinct PdbDbRefSifts.PdbID, EntityID, DbCode As UnpID, DbAlignBeg, DbAlignEnd, SeqAlignBeg, SeqAlignEnd " +
                       " From PdbDbRefSifts, PdbDbRefSeqSifts " +
                       " Where PdbDbRefSifts.PdbID = '{0}' AND  EntityID = {1} AND " +
                       " PdbDbRefSifts.PdbID = PdbDbRefSeqSifts.PdbID  AND PdbDbRefSifts.RefID = PdbDbRefSeqSifts.RefID " +
                       " Order By PdbDbRefSifts.PdbID, EntityID, SeqAlignBeg;", pdbId, entityId);
            DataTable pdbUnpRegionMatchTable = ProtCidSettings.pdbfamQuery.Query(queryString);

            List<int[]> pdbEntityPosList = new List<int[]>();
            List<int[]> dbEntityPosList = new List<int[]>();
            List<string> entityUnpList = new List<string>();
            foreach (DataRow matchRow in pdbUnpRegionMatchTable.Rows)
            {
                int[] startEndPosPdb = new int[2];
                startEndPosPdb[0] = Convert.ToInt32(matchRow["SeqAlignBeg"].ToString());
                startEndPosPdb[1] = Convert.ToInt32(matchRow["SeqAlignEnd"].ToString());
                int[] startEndPosUnp = new int[2];
                startEndPosUnp[0] = Convert.ToInt32(matchRow["DbAlignBeg"].ToString());
                startEndPosUnp[1] = Convert.ToInt32(matchRow["DbAlignEnd"].ToString());
                pdbEntityPosList.Add(startEndPosPdb);
                dbEntityPosList.Add(startEndPosUnp);
                entityUnpList.Add(matchRow["DbCode"].ToString().TrimEnd());
            }
            uniprots = entityUnpList.ToArray();

            List<int[]>[] startEndPosLists = new List<int[]>[2];
            startEndPosLists[0] = pdbEntityPosList;
            startEndPosLists[1] = dbEntityPosList;
            return startEndPosLists;
        }        
        #endregion     
    }
}
