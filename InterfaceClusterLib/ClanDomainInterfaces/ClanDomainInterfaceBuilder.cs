using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using InterfaceClusterLib.DomainInterfaces;
using ProtCidSettingsLib;

namespace InterfaceClusterLib.ClanDomainInterfaces
{
    public class ClanDomainInterfaceBuilder
    {
        public void BuildClanDomainInterfaces(int step)
        {
            ProtCidSettings.dataType = "pfam";
            DomainInterfaceBuilder.InitializeThread();

            DomainInterfaceTables.InitializeTables();
            ProtCidSettings.progressInfo.Reset();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieving domain-domain interactions.");

            switch (step)
            {
                case 1:
                    ClanDomainInterfaceComp domainInterfaceComp = new ClanDomainInterfaceComp();
                    int relSeqId1 = 11619;
                    int relSeqId2 = 11642;
                    int clanSeqId = 398;
                    domainInterfaceComp.CompareRepDomainInterfaces2PfamRelations(clanSeqId, relSeqId1, relSeqId2);
                    break;

                case 2:
                    ClanDomainInterfaceCluster clanInterfaceCluster = new ClanDomainInterfaceCluster();
                    clanInterfaceCluster.ClusterSuperDomainInterfaces();
                    break;

                case 3: // output coordinate domain interfaces for each cluster
                    break;

                default:
                    break;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void ClusterUserDefinedGroupDomainInterfaces()
        {
            ProtCidSettings.dataType = "pfam";
            DomainInterfaceBuilder.InitializeThread();

            DomainInterfaceTables.InitializeTables();
            ProtCidSettings.progressInfo.Reset();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieving domain-domain interactions for user-defined group.");

            // structure alignments between representative domains from PFAMs in a group
            int groupSeqId = 100000;
     /*     Hashtable groupPfamHash = new Hashtable();
            string[] groupPfams = { "Integrase_Zn", "zf-H2C2" };
            groupPfamHash.Add(groupSeqId, groupPfams);

            PfamLib.DomainAlign.PfamDomainAlign domainAlign = new PfamLib.DomainAlign.PfamDomainAlign();
            domainAlign.AlignPfamDomainsForUserDefinedGroups(groupPfamHash);
            */
            // calcuate the Q score between two different relations
            groupSeqId = 100000;
            Dictionary<int, int[]> groupRelationHash = new Dictionary<int,int[]> ();
            List<int> relSeqIdList = new List<int> ();
            relSeqIdList.Add(7956);
            relSeqIdList.Add(15374);
            int[] relSeqIds = new int[relSeqIdList.Count];
            relSeqIdList.CopyTo(relSeqIds);
            groupRelationHash.Add(groupSeqId, relSeqIds);

            groupSeqId++;
            relSeqIdList = new List<int> ();
            relSeqIdList.Add(7954);
            relSeqIdList.Add(15481);
            relSeqIds = new int[relSeqIdList.Count];
            relSeqIdList.CopyTo(relSeqIds);
            groupRelationHash.Add(groupSeqId, relSeqIds);

            Dictionary<string, string> pfamClanHash = new Dictionary<string,string> ();
            pfamClanHash.Add("Integrase_Zn", "Zn_N");
            pfamClanHash.Add("zf-H2C2", "Zn_N");

            ClanDomainInterfaceComp domainInterfaceComp = new ClanDomainInterfaceComp();
            domainInterfaceComp.CompareRepDomainInterfaces2PfamRelations (groupRelationHash, pfamClanHash);


        }

    }
}
