using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrystalInterfaceLib.Contacts;
using CrystalInterfaceLib.Crystal;
using CrystalInterfaceLib.DomainInterfaces;
using AuxFuncLib;
using DbLib;
using ProtCidSettingsLib;

namespace InterfaceClusterLib.AuxFuncs
{
    public class InterfaceSeqNumberConverter
    {
        private SeqNumbersMatchData seqNumMatchData = new SeqNumbersMatchData ();
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainInterface"></param>
        public void ConvertInterfaceSeqNumbersToUnpNumbers (InterfaceChains chainInterface)
        {
            string pdbId = chainInterface.pdbId;
            int[] entities = null;
            if (chainInterface.entityId1 == chainInterface.entityId2)
            {
                entities = new int[1];
                entities[0] = chainInterface.entityId1;
            }
            else 
            {
                entities = new int[2];
                entities[0] = chainInterface.entityId1;
                entities[1] = chainInterface.entityId2;
            }
            Dictionary<int, Dictionary<string, string>> entityPdbUnpSeqMatchDict = seqNumMatchData.ConvertPdbSeqNumbersToUnpNumbers(pdbId, entities);
            Dictionary<string, string> entityPdbUnpSeqMatch1 = entityPdbUnpSeqMatchDict[chainInterface.entityId1];
            for (int i = 0; i < chainInterface.chain1.Length; i ++ )
            {
                if (entityPdbUnpSeqMatch1.ContainsKey(chainInterface.chain1[i].seqId))
                {
                    chainInterface.chain1[i].seqId = entityPdbUnpSeqMatch1[chainInterface.chain1[i].seqId];
                }
                else
                {
                    chainInterface.chain1[i].seqId = "-1";
                }
            }

            Dictionary<string, string> entityPdbUnpSeqMatch2 = entityPdbUnpSeqMatchDict[chainInterface.entityId2];
            for (int i = 0; i < chainInterface.chain2.Length; i++)
            {
                if (entityPdbUnpSeqMatch2.ContainsKey(chainInterface.chain2[i].seqId))
                {
                    chainInterface.chain2[i].seqId = entityPdbUnpSeqMatch2[chainInterface.chain2[i].seqId];
                }
                else
                {
                    chainInterface.chain2[i].seqId = "-1";
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterface"></param>
        public void ConvertInterfaceSeqNumbersToUnpNumbers(DomainInterface domainInterface)
        {
            string pdbId = domainInterface.pdbId;
            int[] entities = null;
            if (domainInterface.entityId1 == domainInterface.entityId2)
            {
                entities = new int[1];
                entities[0] = domainInterface.entityId1;
            }
            else
            {
                entities = new int[2];
                entities[0] = domainInterface.entityId1;
                entities[1] = domainInterface.entityId2;
            }
            Dictionary<int, Dictionary<string, string>> entityPdbUnpSeqMatchDict = seqNumMatchData.ConvertPdbSeqNumbersToUnpNumbers(pdbId, entities);
            Dictionary<string, string> entityPdbUnpSeqMatch1 = entityPdbUnpSeqMatchDict[domainInterface.entityId1];
            for (int i = 0; i < domainInterface.chain1.Length; i++)
            {
                if (entityPdbUnpSeqMatch1.ContainsKey(domainInterface.chain1[i].seqId))
                {
                    domainInterface.chain1[i].seqId = entityPdbUnpSeqMatch1[domainInterface.chain1[i].seqId];
                }
                else
                {
                    domainInterface.chain1[i].seqId = "-1";
                }
            }

            Dictionary<string, string> entityPdbUnpSeqMatch2 = entityPdbUnpSeqMatchDict[domainInterface.entityId2];
            for (int i = 0; i < domainInterface.chain2.Length; i++)
            {
                if (entityPdbUnpSeqMatch2.ContainsKey(domainInterface.chain2[i].seqId))
                {
                    domainInterface.chain2[i].seqId = entityPdbUnpSeqMatch2[domainInterface.chain2[i].seqId];
                }
                else
                {
                    domainInterface.chain2[i].seqId = "-1";
                }
            }
        }

    }
}
