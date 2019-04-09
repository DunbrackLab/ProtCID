using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using ProtCidSettingsLib;

namespace InterfaceClusterLib.AuxFuncs
{
    public class ChainDomainUnpPfamArch
    {
        private double domainDbCoverageCutoff = 0.5;

        #region uniprots and pfams of domains
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainDomain"></param>
        /// <returns></returns>
        public string GetDomainChainPfamString(string pdbId, int chainDomainId)
        {
            string chainPfamString = "";
            string queryString = string.Format("Select Pfam_ID, Count(Distinct PdbPfam.DomainID) As DomainCount From PdbPfamChain, PdbPfam Where PdbPfamChain.PdbID = '{0}' AND ChainDomainId = {1} "
                 + " AND PdbPfamChain.PdbId = PdbPfam.PdbID AND PdbPfamChain.EntityID = PdbPfam.EntityID Group By Pfam_ID;", pdbId, chainDomainId);
            DataTable chainPfamInfoTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string pfamId = "";
            string formatPfamId = "";  // PyMOL accept '_' and '.' in an object name
            foreach (DataRow pfamNumRow in chainPfamInfoTable.Rows)
            {
                pfamId = pfamNumRow["Pfam_ID"].ToString();
                formatPfamId = pfamId.Replace('-', '_');  // replace all '-' by '_'
                chainPfamString += (formatPfamId + "." + pfamNumRow["DomainCount"].ToString() + ".");
            }
            return chainPfamString.TrimEnd('.'); ;
        }

        /// <summary>
        /// suppose one domain has one uniprot code
        /// </summary>
        /// <param name="chainDomain"></param>
        /// <returns></returns>
        public string GetDomainUnpCode(string pdbId, int chainDomainId)
        {
            string queryString = string.Format("Select dbcode, asymid, seqalignbeg, seqalignend, dbalignbeg, dbalignend, seqstart, seqend From PdbPfamChain, PdbPfam, PdbDbRefSifts, PdbDbRefSeqSifts " +
                " Where PdbPfamChain.pdbid = '{0}' and chaindomainid = {1} " +
                " and PdbPfamChain.pdbid = PdbPfam.pdbid and PdbPfamChain.domainid = PdbPfam.domainid and PdbPfamChain.entityid = PdbPfam.entityid " +
                "  and PdbPfamChain.pdbid = PdbDbRefSifts.pdbid and PdbPfam.entityid = PdbDbRefSifts.entityid and PdbDbRefSifts.pdbid = PdbDbRefSeqSifts.pdbid " +
                " and PdbDbRefSifts.refid = PdbDbRefSeqSifts.refid and PdbDbRefSeqSifts.asymid = PdbPfamChain.asymChain; ", pdbId, chainDomainId);
            DataTable domainUnpSeqRegionTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string unpCode = "";
            int seqDbBeg = 0;
            int seqDbEnd = 0;
            int seqDomainStart = 0;
            int seqDomainEnd = 0;
            foreach (DataRow unpSeqRow in domainUnpSeqRegionTable.Rows)
            {
                seqDbBeg = Convert.ToInt32(unpSeqRow["SeqAlignBeg"].ToString());
                seqDbEnd = Convert.ToInt32(unpSeqRow["SeqAlignEnd"].ToString());
                seqDomainStart = Convert.ToInt32(unpSeqRow["SeqStart"].ToString());
                seqDomainEnd = Convert.ToInt32(unpSeqRow["SeqEnd"].ToString());
                if (IsOverlap(seqDbBeg, seqDbEnd, seqDomainStart, seqDomainEnd))
                {
                    unpCode = unpSeqRow["DbCode"].ToString().TrimEnd();
                    break;
                }
            }
            return unpCode;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqDbStart"></param>
        /// <param name="seqDbEnd"></param>
        /// <param name="seqDomainStart"></param>
        /// <param name="seqDomainEnd"></param>
        /// <returns></returns>
        private bool IsOverlap(int seqDbStart, int seqDbEnd, int seqDomainStart, int seqDomainEnd)
        {
            int maxStart = Math.Max(seqDbStart, seqDomainStart);
            int minEnd = Math.Min(seqDbEnd, seqDomainEnd);

            int overlap = minEnd - maxStart;
            double coverage = (double)overlap / (double)(seqDomainEnd - seqDomainStart + 1);
            if (coverage >= domainDbCoverageCutoff)
            {
                return true;
            }
            return false;
        }
        #endregion
    }
}
