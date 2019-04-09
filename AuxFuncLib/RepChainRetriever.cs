using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using DbLib;

namespace AuxFuncLib
{
    public class RepChainRetriever
    {
        #region member variables
        private DbQuery dbQuery = new DbQuery();
        #endregion

        #region rep entities
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="alignmentDbConnect"></param>
        /// <returns></returns>
        public string[] GetRepEntitiesForEntry(string pdbId, DbConnect alignmentDbConnect)
        {
            string queryString = string.Format("Select Distinct PdbId1, EntityID1 From RedundantPdbChains " +
                 " Where PdbID1 = '{0}' OR PdbID2 = '{0}';", pdbId);
            DataTable repEntityTable = dbQuery.Query(alignmentDbConnect, queryString);

            string[] repEntities = new string[repEntityTable.Rows.Count];
            int count = 0;
            foreach (DataRow repEntityRow in repEntityTable.Rows)
            {
                repEntities[count] = repEntityRow["PdbID1"].ToString() + repEntityRow["EntityID1"].ToString().TrimEnd();
                count++;
            }
            return repEntities;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="alignmentDbConnect"></param>
        /// <returns></returns>
        public string GetRepEntitiesForEntry(string pdbId, int entityId, DbConnect alignmentDbConnect)
        {
            string queryString = string.Format("Select Distinct PdbId1, EntityID1 From RedundantPdbChains " +
                 " Where (PdbID1 = '{0}' AND EntityID1 = {1}) " + 
                 " OR (PdbID2 = '{0}' AND EntityID2 = {1});", pdbId, entityId);
            DataTable repEntityTable = dbQuery.Query(alignmentDbConnect, queryString);

            string repEntity = "";
            if (repEntityTable.Rows.Count > 0)
            {
                repEntity = repEntityTable.Rows[0]["PdbID1"].ToString() +
                    repEntityTable.Rows[0]["EntityID1"].ToString();
            }
            return repEntity;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="dataDbConnect"></param>
        /// <param name="alignmentDbConnect"></param>
        /// <returns></returns>
        public string[] GetRepEntitiesForEntry(string pdbId, DbConnect dataDbConnect, DbConnect alignmentDbConnect)
        {
            string[] repEntities = GetRepEntitiesForEntry (pdbId, alignmentDbConnect);
            if (repEntities.Length == 0)
            {
                string queryString = string.Format("Select Distinct EntityID From AsymUnit " +
                    " Where PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
                DataTable entityTable = dbQuery.Query(dataDbConnect, queryString);
                repEntities = new string[entityTable.Rows.Count];
                int count = 0;
                foreach (DataRow entityRow in entityTable.Rows)
                {
                    repEntities[count] = pdbId + entityRow["AsymID"].ToString().TrimEnd();
                    count++;
                }
            }
            return repEntities;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="repPdbId"></param>
        /// <param name="repEntityId"></param>
        /// <returns></returns>
        public string[] GetRedundantEntities(string repPdbId, int repEntityId, DbConnect alignmentDbConnect)
        {
            string queryString = string.Format("Select Distinct PdbID2, EntityID2 From RedundantPdbChains " +
                " Where PdbID1 = '{0}' AND EntityID1 = {1} AND PdbID2 <> '-';", repPdbId, repEntityId);
            DataTable redundantEntityTable = dbQuery.Query(alignmentDbConnect, queryString);
            string[] redundantEntities = new string[redundantEntityTable.Rows.Count];
            int count = 0;
            foreach (DataRow reduntEntityRow in redundantEntityTable.Rows)
            {
                redundantEntities[count] = reduntEntityRow["PdbID2"].ToString() +
                    reduntEntityRow["EntityID2"].ToString();
                count++;
            }
            return redundantEntities;
        }
        #endregion

        #region rep asymmetric chains
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="alignmentDbConnect"></param>
        /// <returns></returns>
        public string[] GetRepAsymChainsForEntry(string pdbId, DbConnect alignmentDbConnect)
        {
            string queryString = string.Format("Select Distinct PdbId1, AsymChainID1 From RedundantPdbChains " +
                " Where PdbID1 = '{0}' OR PdbID2 = '{0}';", pdbId);
            DataTable repChainTable = dbQuery.Query(alignmentDbConnect, queryString);

            string[] repChains = new string[repChainTable.Rows.Count];
            int count = 0;
            foreach (DataRow repChainRow in repChainTable.Rows)
            {
                repChains[count] = repChainRow["PdbID1"].ToString() + repChainRow["AsymChainID1"].ToString().TrimEnd();
                count++;
            }
            return repChains;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="dataDbConnect"></param>
        /// <param name="alignmentDbConnect"></param>
        /// <returns></returns>
        public string[] GetRepAsymChainsForEntry(string pdbId, DbConnect dataDbConnect, DbConnect alignmentDbConnect)
        {
            string[] repAsymChains = GetRepAsymChainsForEntry(pdbId, alignmentDbConnect);
            if (repAsymChains.Length == 0)
            {
                string queryString = string.Format("Select Distinct AsymID From AsymUnit " +
                    " Where PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
                DataTable chainTable = dbQuery.Query(dataDbConnect, queryString);
                repAsymChains = new string[chainTable.Rows.Count];
                int count = 0;
                foreach (DataRow chainRow in chainTable.Rows)
                {
                    repAsymChains[count] = pdbId + chainRow["AsymID"].ToString().TrimEnd();
                    count++;
                }
            }
            return repAsymChains;
        }
        #endregion

        #region rep author chains
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="alignmentDbConnect"></param>
        /// <returns></returns>
        public string[] GetRepChainsForEntry(string pdbId, DbConnect alignmentDbConnect)
        {
            string queryString = string.Format("Select Distinct PdbId1, ChainID1 From RedundantPdbChains " +
                " Where PdbID1 = '{0}' OR PdbID2 = '{0}';", pdbId);
            DataTable repChainTable = dbQuery.Query(alignmentDbConnect, queryString);

            string[] repChains = new string[repChainTable.Rows.Count];
            int count = 0;
            foreach (DataRow repChainRow in repChainTable.Rows)
            {
                repChains[count] = repChainRow["PdbID1"].ToString() + repChainRow["ChainID1"].ToString().TrimEnd();
                count++;
            }
            return repChains;
        } 
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="dataDbConnect"></param>
        /// <param name="alignmentDbConnect"></param>
        /// <returns></returns>
        public string[] GetRepChainsForEntry(string pdbId, DbConnect dataDbConnect, DbConnect alignmentDbConnect)
        {
            string[] repChains = GetRepChainsForEntry(pdbId, alignmentDbConnect);
            if (repChains.Length == 0)
            {
                string queryString = string.Format("Select Distinct AuthorChain From AsymUnit " + 
                    " Where PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
                DataTable chainTable = dbQuery.Query(dataDbConnect, queryString);
                repChains = new string[chainTable.Rows.Count];
                int count = 0;
                foreach (DataRow chainRow in chainTable.Rows)
                {
                    repChains[count] = pdbId + chainRow["AuthorChain"].ToString().TrimEnd();
                    count++;
                }
            }
            return repChains;
        }
        #endregion
    }
}
