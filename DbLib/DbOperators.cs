using System;
using System.Collections.Generic;
using System.Text;
using System.Data;

namespace DbLib
{
    /// <summary>
    /// defined the common DB query functions used by the project
    /// </summary>
    public class DbOperators
    {
        private DbQuery dbQuery = new DbQuery();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainId"></param>
        /// <param name="dbConnect"></param>
        /// <returns></returns>
        public string GetRepresentativeAuthChain(string pdbId, string authChain, DbConnect dbConnect)
        {
            string queryString = string.Format("Select PdbID1, ChainID1 From RedundantPdbChains " +
                " Where (PdbID1 = '{0}' AND ChainID1 = '{1}') OR " +
                " (PdbID2 = '{0}' AND ChainID2 = '{1}');", pdbId, authChain);
            DataTable repChainTable = dbQuery.Query(dbConnect, queryString);
            string repChain = "";
            if (repChainTable.Rows.Count > 0)
            {
                repChain = repChainTable.Rows[0]["PdbID"].ToString() +
                    repChainTable.Rows[0]["ChainID1"].ToString();
            }
            return repChain;
        }
    }
}
