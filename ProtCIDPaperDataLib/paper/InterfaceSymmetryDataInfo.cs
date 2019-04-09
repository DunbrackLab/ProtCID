using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data;
using ProtCidSettingsLib;
using AuxFuncLib;
using InterfaceClusterLib.DomainInterfaces;
using CrystalInterfaceLib.DomainInterfaces;

namespace ProtCIDPaperDataLib.paper
{
    public class InterfaceSymmetryDataInfo : PaperDataInfo
    {
        private DomainInterfaceRetriever domainInterfaceRetriever = new DomainInterfaceRetriever ();
        private string domainInterfaceFileDir = @"X:\Qifang\ProjectData\DbProjectData\InterfaceFiles\PfamDomain";
        int[] rangeAlpha3 = { 86, 105 };
        int[] rangeAlpha4 = { 125, 139 };

        public void PrintHomoInterfaceSymmetryIndexes ()
        {
            string queryString = "Select PdbID, InterfaceID, AsymChain1, AsymChain2, EntityID1, EntityID2, SurfaceArea, SymmetryIndex From CrystEntryInterfaces Where SymmetryIndex >= 0;";
            DataTable symIndexTable = ProtCidSettings.protcidQuery.Query(queryString);
            string symIndexFile = Path.Combine(dataDir, "InterfaceSymmetryInfo.txt");
            string headerLine = "";
            StreamWriter dataWriter = new StreamWriter(symIndexFile);
            foreach (DataColumn dCol in symIndexTable.Columns)
            {
                headerLine += (dCol.ColumnName + "\t");
            }
            dataWriter.WriteLine(headerLine.TrimEnd ('\t'));
            foreach (DataRow dataRow in symIndexTable.Rows)
            {
                dataWriter.WriteLine(ParseHelper.FormatDataRow (dataRow));
            }
            dataWriter.Close();
        }        

        /// <summary>
        /// 
        /// </summary>
        public void RetrieveRasAlpha34Dimer ()
        {
            StreamWriter dataWriter = new StreamWriter("Ras_Alpha34Dimers.txt");
            string queryString = "Select Distinct PdbID, EntityID From PdbPfam Where Pfam_ID = 'Ras';";
            DataTable entryEntityTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string pdbId = "";
            int entityId = 0;
            foreach (DataRow entityRow in entryEntityTable.Rows)
            {
                pdbId = entityRow["PdbID"].ToString();
                entityId = Convert.ToInt32 (entityRow["EntityID"].ToString());
                int[] domainInterfaceIds = ReadDomainInterfaces(pdbId, entityId);
       //         int[] alpha34DomainInterfaceIds = GetAlpha34DimerDomainInterfaces(pdbId, domainInterfaceIds);
                GetAlpha34DimerDomainInterfaces(pdbId, domainInterfaceIds, dataWriter);
       /*         foreach (int domainInterfaceId in alpha34DomainInterfaceIds)
                {
                    dataWriter.WriteLine(pdbId + domainInterfaceId);
                }*/
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private int[] ReadDomainInterfaces (string pdbId, int entityId)
        {
            string queryString = string.Format("Select DomainInterfaceID From PfamDomainInterfaces, CrystEntryInterfaces " +
                " Where PfamDomainInterfaces.PdbID = '{0}' AND PfamDomainInterfaces.PdbID = CrystEntryInterfaces.PdbID AND " +
                " PfamDomainInterfaces.InterfaceID = CrystEntryInterfaces.InterfaceID AND EntityID1 = {1} AND EntityID2 = {1};", pdbId, entityId);
            DataTable domainInterfaceIdTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<int> domainInterfaceIdList = new List<int>();
            int domainInterfaceId = 0;
            foreach (DataRow dInterfaceIdRow in domainInterfaceIdTable.Rows)
            {
                domainInterfaceId = Convert.ToInt32(dInterfaceIdRow["DomainInterfaceID"].ToString ());
                domainInterfaceIdList.Add(domainInterfaceId);
            }
            return domainInterfaceIdList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceIds"></param>
        /// <returns></returns>
        private void GetAlpha34DimerDomainInterfaces(string pdbId, int[] domainInterfaceIds, StreamWriter dataWriter)
        {
            DomainInterface[] domainInterfaces = domainInterfaceRetriever.GetDomainInterfacesFromFiles(domainInterfaceFileDir, pdbId, domainInterfaceIds, "cryst");
            double surfaceArea = 0;
            foreach (DomainInterface domainInterface in domainInterfaces)
            {
                int numAlpha34Contacts = GetDimerAlpha34Contacts(domainInterface);
                if (numAlpha34Contacts >= 0)
                {
                    surfaceArea = GetDomainInterfaceSurfaceArea(pdbId, domainInterface.domainInterfaceId);
                    dataWriter.WriteLine(pdbId + domainInterface.domainInterfaceId + "\t" + numAlpha34Contacts + "\t" + domainInterface.seqDistHash.Count + 
                        "\t" + surfaceArea);
                }
            }
        }


        private double GetDomainInterfaceSurfaceArea (string pdbId, int domainInterfaceId)
        {
            string queryString = string.Format("Select SurfaceArea From PfamDomainInterfaces Where PdbID = '{0}' AND DomainInterfaceID = {1};", pdbId, domainInterfaceId);
            DataTable surfaceAreaTable = ProtCidSettings.protcidQuery.Query(queryString);
            if (surfaceAreaTable.Rows.Count > 0)
            {
                return Convert.ToDouble (surfaceAreaTable.Rows[0]["SurfaceArea"].ToString());
            }
            return -1;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceIds"></param>
        /// <returns></returns>
        private int[] GetAlpha34DimerDomainInterfaces (string pdbId, int[] domainInterfaceIds)
        {
            DomainInterface[] domainInterfaces = domainInterfaceRetriever.GetDomainInterfacesFromFiles(domainInterfaceFileDir, pdbId, domainInterfaceIds, "cryst");
            List<int> domainInterfaceIdList = new List<int>();
            foreach (DomainInterface domainInterface in domainInterfaces)
            {
                if (IsDimerAlpha34 (domainInterface))
                {
                    domainInterfaceIdList.Add(domainInterface.domainInterfaceId);
                }
            }
            return domainInterfaceIdList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterface"></param>
        /// <returns></returns>
        private int GetDimerAlpha34Contacts (DomainInterface domainInterface)
        {
            List<string> alpha34SeqPairList = new List<string>();
            int seqId1 = 0;
            int seqId2 = 0;
            foreach (string seqPair in domainInterface.seqDistHash.Keys)
            {
                string[] fields = seqPair.Split('_');
                seqId1 = Convert.ToInt32(fields[0]);
                seqId2 = Convert.ToInt32(fields[1]);
                if (((seqId1 >= rangeAlpha3[0] && seqId1 <= rangeAlpha3[1]) || (seqId1 >= rangeAlpha4[0] && seqId1 <= rangeAlpha4[1])) &&
                    ((seqId2 >= rangeAlpha3[0] && seqId2 <= rangeAlpha3[1]) || (seqId2 >= rangeAlpha4[0] && seqId2 <= rangeAlpha4[1])))
                {
                    alpha34SeqPairList.Add(seqPair);
                }
            }
            return alpha34SeqPairList.Count;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterface"></param>
        /// <returns></returns>
        private bool IsDimerAlpha34 (DomainInterface domainInterface)
        {
            List<string> alpha34SeqPairList = new List<string>();
            int seqId1 = 0;
            int seqId2 = 0;
            foreach (string seqPair in domainInterface.seqDistHash.Keys)
            {
                string[] fields = seqPair.Split('_');
                seqId1 = Convert.ToInt32(fields[0]);
                seqId2 = Convert.ToInt32(fields[1]);
                if (((seqId1 >= rangeAlpha3[0] && seqId1 <= rangeAlpha3[1]) || (seqId1 >= rangeAlpha4[0] && seqId1 <= rangeAlpha4[1])) &&
                    ((seqId2 >= rangeAlpha3[0] && seqId2 <= rangeAlpha3[1]) || (seqId2 >= rangeAlpha4[0] && seqId2 <= rangeAlpha4[1])))
                {
                    alpha34SeqPairList.Add(seqPair);
                }
            }
            double coverage = (double)alpha34SeqPairList.Count / (double)domainInterface.seqDistHash.Count;
            if (coverage >= 0.50)
            {
                return true;
            }
            return false;
        }
     }
}
