using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.IO;
using CrystalInterfaceLib.Settings;
using CrystalInterfaceLib.SimMethod;


namespace CrystalInterfaceLib.DomainInterfaces
{
    /// <summary>
    /// Compare domain-domain interfaces
    /// </summary>
    public class DomainInterfaceComp
    {
        #region member variables
        public StreamWriter nonAlignDomainWriter = null;
        private SuperpositionDomainInterfaces supDomainInterface = new SuperpositionDomainInterfaces();
        private SimScoreFunc qFunc = new SimScoreFunc();
        private DomainAlignment domainAlign = new DomainAlignment ();
        public static string domainAlignType = "";
        #endregion

        #region compare domain interfaces
        /// <summary>
        /// compare two domain interfaces of same entry
        /// </summary>
        /// <param name="domainInterfaces1"></param>
        /// <param name="domainInterfaces2"></param>
        public DomainInterfacePairInfo[] CompareEntryDomainInterfaces(DomainInterface[] domainInterfaces1, DomainInterface[] domainInterfaces2)
        {
            Dictionary<string, long[]> familyDomainHash = GetFamilyDomainHash(domainInterfaces1, domainInterfaces2);
            DataTable domainAlignInfoTable = GetDomainAlignmentTable(familyDomainHash);
            List<DomainInterfacePairInfo> interfaceCompList = new List<DomainInterfacePairInfo>();
            foreach (DomainInterface domainInterface1 in domainInterfaces1)
            {
                foreach (DomainInterface domainInterface2 in domainInterfaces2)
                {                    
                    try
                    {
                        DomainInterfacePairInfo compInfo =
                            CompareTwoDomainInterfaces(domainInterface1, domainInterface2, domainAlignInfoTable);
                        if (compInfo != null && // compInfo.qScore > -1)
                           compInfo.qScore >= AppSettings.parameters.contactParams.minQScore)
                        {
                            interfaceCompList.Add(compInfo);
                        }
                    }
                    catch (Exception ex)
                    {
                        nonAlignDomainWriter.WriteLine(domainInterface1.pdbId + " " + domainInterface1.domainInterfaceId +
                            " " + domainInterface2.pdbId + " " + domainInterface2.domainInterfaceId + " : " + ex.Message);
                    }
                }
            }
            nonAlignDomainWriter.Flush();
            DomainInterfacePairInfo[] pairCompInfos = new DomainInterfacePairInfo[interfaceCompList.Count];
            interfaceCompList.CopyTo(pairCompInfos);
            return pairCompInfos;
        }
        /// <summary>
        /// compare two domain interfaces
        /// </summary>
        /// <param name="domainInterfaces1"></param>
        /// <param name="domainInterfaces2"></param>
        public DomainInterfacePairInfo[] CompareDomainInterfaces(DomainInterface[] domainInterfaces1, DomainInterface[] domainInterfaces2)
        {
        /*    Hashtable familyDomainHash1 = GetFamilyDomainHash (domainInterfaces1);
            Hashtable familyDomainHash2 = GetFamilyDomainHash (domainInterfaces2);
            DataTable domainAlignInfoTable = GetDomainAlignmentTable(familyDomainHash1, familyDomainHash2);*/
            Dictionary<string, long[]> familyDomainHash = GetFamilyDomainHash(domainInterfaces1, domainInterfaces2);
            DataTable domainAlignInfoTable = GetDomainAlignmentTable(familyDomainHash);
            List<DomainInterfacePairInfo> interfaceCompList = new List<DomainInterfacePairInfo> ();
            foreach (DomainInterface domainInterface1 in domainInterfaces1)
            {
                foreach (DomainInterface domainInterface2 in domainInterfaces2)
                {
                    if (!IsDomainInterfaceAlignmentExist(domainInterface1, domainInterface2, domainAlignInfoTable))
                    {
                        nonAlignDomainWriter.WriteLine(domainInterface1.pdbId + " " + domainInterface1.domainInterfaceId + " " +
                            domainInterface1.domainId1 + " " + domainInterface1.domainId2 + " " +
                            " " + domainInterface2.pdbId + " " + domainInterface2.domainInterfaceId + " " +
                            domainInterface2.domainId1 + " " + domainInterface2.domainId2);
                        
                        continue;
                    }
                    try
                    {
                        DomainInterfacePairInfo compInfo =
                            CompareTwoDomainInterfaces(domainInterface1, domainInterface2, domainAlignInfoTable);
                        if (compInfo != null && // compInfo.qScore > -1)
                           compInfo.qScore >= AppSettings.parameters.contactParams.minQScore)
                        {
                            interfaceCompList.Add(compInfo);
                        }
                    }
                    catch (Exception ex)
                    {
                        nonAlignDomainWriter.WriteLine(domainInterface1.pdbId + " " + domainInterface1.domainInterfaceId + 
                            " " + domainInterface2.pdbId + " " + domainInterface2.domainInterfaceId + " : " + ex.Message);
                    }
                }
            }
            nonAlignDomainWriter.Flush();
            DomainInterfacePairInfo[] pairCompInfos = new DomainInterfacePairInfo[interfaceCompList.Count];
            interfaceCompList.CopyTo(pairCompInfos);
            return pairCompInfos;
        }

        /// <summary>
        /// compare two domain interfaces
        /// </summary>
        /// <param name="domainInterfaces1"></param>
        /// <param name="domainInterfaces2"></param>
        public DomainInterfacePairInfo[] CompareDifRelationDomainInterfaces(DomainInterface[] domainInterfaces1, DomainInterface[] domainInterfaces2)
        {
            Dictionary<string, long[]> familyDomainHash1 = GetFamilyDomainHash(domainInterfaces1);
            Dictionary<string, long[]> familyDomainHash2 = GetFamilyDomainHash(domainInterfaces2);
            DataTable domainAlignInfoTable = GetDomainAlignmentTable(familyDomainHash1, familyDomainHash2);

            List<DomainInterfacePairInfo> interfaceCompList = new List<DomainInterfacePairInfo> ();
            foreach (DomainInterface domainInterface1 in domainInterfaces1)
            {
                foreach (DomainInterface domainInterface2 in domainInterfaces2)
                {
                    if (!IsDomainInterfaceAlignmentExist(domainInterface1, domainInterface2, domainAlignInfoTable))
                    {
                        nonAlignDomainWriter.WriteLine(domainInterface1.pdbId + " " + domainInterface1.domainInterfaceId + " " +
                            domainInterface1.domainId1 + " " + domainInterface1.domainId2 + " " +
                            " " + domainInterface2.pdbId + " " + domainInterface2.domainInterfaceId + " " +
                            domainInterface2.domainId1 + " " + domainInterface2.domainId2);

                  //      continue;
                    }
                    try
                    {
                        DomainInterfacePairInfo compInfo =
                            CompareTwoDomainInterfaces(domainInterface1, domainInterface2, domainAlignInfoTable);
                        if (compInfo != null && // compInfo.qScore > -1)
                           compInfo.qScore >= AppSettings.parameters.contactParams.minQScore)
                        {
                            interfaceCompList.Add(compInfo);
                        }
                    }
                    catch (Exception ex)
                    {
                        nonAlignDomainWriter.WriteLine(domainInterface1.pdbId + " " + domainInterface1.domainInterfaceId +
                            " " + domainInterface2.pdbId + " " + domainInterface2.domainInterfaceId + " : " + ex.Message);
                    }
                }
            }
            nonAlignDomainWriter.Flush();
            return interfaceCompList.ToArray ();
        }

        /// <summary>
        /// compare domain interfaces for one entry
        /// </summary>
        /// <param name="domainInterfaces1"></param>
        /// <param name="domainInterfaces2"></param>
        public DomainInterfacePairInfo[] CompareEntryDomainInterfaces(DomainInterface[] domainInterfaces)
        {
            Dictionary<string, long[]> familyDomainHash = GetFamilyDomainHash (domainInterfaces);
            DataTable domainAlignInfoTable = GetDomainAlignmentTable(familyDomainHash);
            List<DomainInterfacePairInfo> interfaceCompList = new List<DomainInterfacePairInfo> ();
            foreach (DomainInterface domainInterface1 in domainInterfaces)
            {
                foreach (DomainInterface domainInterface2 in domainInterfaces)
                {
                    if (domainInterface1.domainInterfaceId != domainInterface2.domainInterfaceId)
                    {
                        if (!IsDomainInterfaceAlignmentExist(domainInterface1, domainInterface2, domainAlignInfoTable))
                        {
                            nonAlignDomainWriter.WriteLine(domainInterface1.pdbId + " " + domainInterface1.domainInterfaceId + " " +
                                domainInterface1.domainId1 + " " + domainInterface1.domainId2 + " " +
                                " " + domainInterface2.pdbId + " " + domainInterface2.domainInterfaceId + " " +
                                domainInterface2.domainId1 + " " + domainInterface2.domainId2);
                            continue;
                        }
                        DomainInterfacePairInfo compInfo =
                            CompareTwoDomainInterfaces(domainInterface1, domainInterface2, domainAlignInfoTable);
                        if (compInfo != null && compInfo.qScore > -1)
                     //       compInfo.qScore >= AppSettings.parameters.contactParams.minQScore)
                        {
                            interfaceCompList.Add(compInfo);
                        }
                    }
                }
            }
            nonAlignDomainWriter.Flush();
            return interfaceCompList.ToArray ();
        }

        /// <summary>
        /// compare two domain interfaces
        /// </summary>
        /// <param name="domainInterfaces1"></param>
        /// <param name="domainInterfaces2"></param>
        public DomainInterfacePairInfo CompareDomainInterfaces(DomainInterface domainInterface1, DomainInterface domainInterface2)
        {
            Dictionary<string, long[]> familyDomainHash = GetFamilyDomainHash (domainInterface1, domainInterface2);
            DataTable domainAlignmentTable = GetDomainAlignmentTable(familyDomainHash);

            /*    if (domainAlignInfoTable == null || domainAlignInfoTable.Rows.Count == 0)
                {
                    return null;
                }*/
            DomainInterfacePairInfo compInfo =
                CompareTwoDomainInterfaces(domainInterface1, domainInterface2, domainAlignmentTable);

            domainAlignmentTable.Clear();

            return compInfo;
        }

        /// <summary>
        /// compare two domain interfaces
        /// </summary>
        /// <param name="domainInterface1"></param>
        /// <param name="domainInterface2"></param>
        /// <param name="domainAlignInfoTable"></param>
        /// <returns></returns>
        public DomainInterfacePairInfo CompareTwoDomainInterfaces(DomainInterface domainInterface1,
            DomainInterface domainInterface2, DataTable domainAlignInfoTable)
        {
            DomainInterface domainInterface2Copy = new DomainInterface(domainInterface2, true); // keep a deep copy
            double identity = supDomainInterface.SuperposeDomainInterfaces(domainInterface1, domainInterface2Copy, domainAlignInfoTable);
            double qScore = qFunc.WeightQFunc(domainInterface1, domainInterface2Copy);
        //    domainInterface2 = domainInterface2Copy; // shadow copy  
      //      supDomainInterface.ReverseSupDomainInterfaces(domainInterface1, domainInterface2, domainAlignInfoTable);

            bool interfaceReversed = false;
            if (qScore < AppSettings.parameters.simInteractParam.interfaceSimCutoff &&
                domainInterface1.familyCode1 == domainInterface2.familyCode2)
            {
                domainInterface2Copy = new DomainInterface (domainInterface2, true);

                domainInterface2Copy.Reverse();
                // keep deep copy
                double reversedIdentity =
                    supDomainInterface.SuperposeDomainInterfaces(domainInterface1, domainInterface2Copy, domainAlignInfoTable);
                double reversedQScore = qFunc.WeightQFunc(domainInterface1, domainInterface2Copy);
        //        domainInterface2 = domainInterface2Copy;
        //        supDomainInterface.ReverseSupDomainInterfaces(domainInterface1, domainInterface2, domainAlignInfoTable);
                if (qScore < reversedQScore)
                {
                    qScore = reversedQScore;
                    identity = reversedIdentity;
                    interfaceReversed = true;
                }
            }

            DomainInterfacePairInfo interfacePairInfo = new DomainInterfacePairInfo
                (new DomainInterfaceInfo(domainInterface1), new DomainInterfaceInfo(domainInterface2));
            interfacePairInfo.qScore = qScore;
            if (identity <= 0)
            {
                interfacePairInfo.identity = -1.0;
            }
            else
            {
                interfacePairInfo.identity = identity;
            }
            interfacePairInfo.isInterface2Reversed = interfaceReversed;

            return interfacePairInfo;
        }

        /// <summary>
        /// interface alignments exist in the input datatable
        /// </summary>
        /// <param name="dinterface1"></param>
        /// <param name="dinterface2"></param>
        /// <param name="alignInfoTable"></param>
        /// <returns></returns>
        public bool IsDomainInterfaceAlignmentExist(DomainInterface dinterface1, DomainInterface dinterface2, DataTable alignInfoTable)
        {
            if ((dinterface1.domainId1 == dinterface2.domainId1 && dinterface1.domainId2 == dinterface2.domainId2) ||
                (dinterface1.domainId1 == dinterface2.domainId2 && dinterface1.domainId2 == dinterface2.domainId1))
            {
                return true;
            }
            if (IsDomainAlignmentExist(dinterface1.domainId1, dinterface2.domainId1, alignInfoTable) &&
                IsDomainAlignmentExist(dinterface1.domainId2, dinterface2.domainId2, alignInfoTable))
            {
                return true;
            }
            else if (IsDomainAlignmentExist(dinterface1.domainId2, dinterface2.domainId1, alignInfoTable) &&
                IsDomainAlignmentExist(dinterface1.domainId1, dinterface2.domainId2, alignInfoTable))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// domain alignment exist in the input data table
        /// </summary>
        /// <param name="domainId1"></param>
        /// <param name="domainId2"></param>
        /// <param name="domainAlignInfoTable"></param>
        /// <returns></returns>
        public bool IsDomainAlignmentExist(long domainId1, long domainId2, DataTable domainAlignInfoTable)
        {
            string selectString = string.Format("DomainID1 = '{0}' AND DomainID2 = '{1}'", domainId1, domainId2);
            DataRow[] alignRows = domainAlignInfoTable.Select(selectString);
            if (alignRows.Length > 0)
            {
                return true;
            }
            selectString = string.Format("DomainID1 = '{0}' AND DomainID2 = '{1}'", domainId2, domainId1);
            alignRows = domainAlignInfoTable.Select(selectString);
            if (alignRows.Length > 0)
            {
                return true;
            }
    //        nonAlignDomainWriter.WriteLine(domainId1 + " " + domainId2);
            return false;
        }

        /// <summary>
        /// compare two domain interfaces
        /// </summary>
        /// <param name="domainInterface1"></param>
        /// <param name="domainInterface2"></param>
        /// <returns></returns>
        public DomainInterfacePairInfo CompareEntryDomainInterfaces(DomainInterface domainInterface1,
            DomainInterface domainInterface2)
        {
            double qScore = qFunc.WeightQFunc(domainInterface1, domainInterface2);
            bool interfaceReversed = false;
            if (qScore < AppSettings.parameters.simInteractParam.interfaceSimCutoff &&
                domainInterface1.familyCode1 == domainInterface2.familyCode2)
            {
                domainInterface2.Reverse();
                double reversedQScore = qFunc.WeightQFunc(domainInterface1, domainInterface2);
                if (qScore < reversedQScore)
                {
                    qScore = reversedQScore;
                    interfaceReversed = true;
                }
                else
                {
                    domainInterface2.Reverse();
                }
            }

            DomainInterfacePairInfo interfacePairInfo = new DomainInterfacePairInfo
                (new DomainInterfaceInfo(domainInterface1), new DomainInterfaceInfo(domainInterface2));
            interfacePairInfo.qScore = qScore;
            interfacePairInfo.isInterface2Reversed = interfaceReversed;
            return interfacePairInfo;
        }
        #endregion

        #region alignments 
        /// <summary>
        /// get domain alignment info from db
        /// </summary>
        /// <param name="familyDomainHash"></param>
        /// <returns></returns>
        public DataTable GetDomainAlignmentTable(Dictionary<string, long[]> familyDomainHash)
        {
            DataTable domainAlignInfoTable = null;
            DataTable familyDomainAlignInfoTable = null;
            foreach (string family in familyDomainHash.Keys)
            {
                long[] domainIds = familyDomainHash[family];
         
                if (domainAlignType == "pfam")
                {
                    familyDomainAlignInfoTable = domainAlign.GetDomainAlignmentByPfamHmm(domainIds);
                }
                else if (domainAlignType == "struct")
                {
                    familyDomainAlignInfoTable = domainAlign.GetStructDomainAlignments(domainIds);
                }
                else
                {
                    // the domain alignments combined from hmm-aligned and struct-aligned, 
                    // modified on August 29, 2012
                    familyDomainAlignInfoTable = domainAlign.GetDomainAlignments(domainIds);
                }
                if (domainAlignInfoTable == null)
                {
                    domainAlignInfoTable = familyDomainAlignInfoTable.Copy();
                }
                else
                {
                    foreach (DataRow alignInfoRow in familyDomainAlignInfoTable.Rows)
                    {
                        DataRow newRow = domainAlignInfoTable.NewRow();
                        newRow.ItemArray = alignInfoRow.ItemArray;
                        domainAlignInfoTable.Rows.Add(newRow);
                    }
                }
            }
            return domainAlignInfoTable;
        }

        /// <summary>
        /// get domain alignment info from db
        /// </summary>
        /// <param name="familyDomainHash"></param>
        /// <returns></returns>
        public DataTable GetDomainAlignmentTable(Dictionary<string, long[]> familyDomainHash1, Dictionary<string, long[]> familyDomainHash2)
        {
            DataTable domainAlignInfoTable = null;
            DataTable familyDomainAlignInfoTable = null;
            foreach (string family1 in familyDomainHash1.Keys)
            {
                long[] domainIds1 = familyDomainHash1[family1];
                foreach (string family2 in familyDomainHash2.Keys)
                {
                    long[] domainIds2 = familyDomainHash2[family2];
                    if (family1 == family2)
                    {
                        familyDomainAlignInfoTable = domainAlign.GetDomainAlignmentByPfamHmm(domainIds1, domainIds2);
                    }
                    else
                    {
                        familyDomainAlignInfoTable = domainAlign.GetStructDomainAlignments(domainIds1, domainIds2);
                    }

                    if (domainAlignInfoTable == null)
                    {
                        domainAlignInfoTable = familyDomainAlignInfoTable.Copy();
                    }
                    else
                    {
                        foreach (DataRow alignInfoRow in familyDomainAlignInfoTable.Rows)
                        {
                            DataRow newRow = domainAlignInfoTable.NewRow();
                            newRow.ItemArray = alignInfoRow.ItemArray;
                            domainAlignInfoTable.Rows.Add(newRow);
                        }
                    }
                }
            }
            return domainAlignInfoTable;
        }
        #endregion

        #region same domain interfaces
        /// <summary>
        /// same domain interfaces means two domain interfaces with same domain id components 
        /// and Q score >= unique interface q score which is defined to 0.90
        /// </summary>
        /// <param name="domainInterface1"></param>
        /// <param name="domainInterface2"></param>
        /// <returns></returns>
        public bool AreSameDomainInterfaces(DomainInterface domainInterface1, DomainInterface domainInterface2)
        {
            DomainInterfacePairInfo pairInfo =
                        CompareEntryDomainInterfaces(domainInterface1, domainInterface2);
            if (pairInfo.qScore >= AppSettings.parameters.simInteractParam.uniqueInterfaceCutoff
                && AreDomainInterfacesSameDomainIds(domainInterface1, domainInterface2))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// check if two domain interfaces with same asymmetric chain component or domain definition components
        /// e.g domain interface 1: A_1_555 C_1_555  
        /// domain interface 2: A_1_555 C_4_555 or C_4_555  A_1_555
        /// have same pfam domain definition, if domain interfaces are same, only keep one
        /// </summary>
        /// <param name="domainInterface1"></param>
        /// <param name="domainInterface2"></param>
        /// <returns></returns>
        public bool AreDomainInterfacesSameDomainIds(DomainInterface domainInterface1, DomainInterface domainInterface2)
        {
            if (domainInterface1.domainId1 == domainInterface2.domainId1 &&
                domainInterface1.domainId2 == domainInterface2.domainId2)
            {
                return true;
            }
            else if (domainInterface1.domainId1 == domainInterface2.domainId2 &&
                domainInterface1.domainId2 == domainInterface2.domainId1)
            {
                return true;
            }
            return false;
        }
        #endregion

        #region family domain list
        /// <summary>
        /// the domain list for each family in the interaction
        /// </summary>
        /// <param name="domainInterfaces1"></param>
        /// <param name="domainInterfaces2"></param>
        /// <returns></returns>
        public Dictionary<string, long[]> GetFamilyDomainHash(DomainInterface[] domainInterfaces1, DomainInterface[] domainInterfaces2)
        {
            Dictionary<string, List<long>> familyDomainListHash = new Dictionary<string, List<long>>();
            GetFamilyDomainHash(domainInterfaces1, ref familyDomainListHash);
            GetFamilyDomainHash(domainInterfaces2, ref familyDomainListHash);
            Dictionary<string, long[]> familyDomainsHash = new Dictionary<string, long[]>();
            foreach (string familyCode in familyDomainListHash.Keys)
            {
                familyDomainsHash.Add(familyCode, familyDomainListHash[familyCode].ToArray());
            }
            return familyDomainsHash;
        }

        /// <summary>
        /// the domain list for each family in the interaction
        /// </summary>
        /// <param name="domainInterfaces1"></param>
        /// <param name="domainInterfaces2"></param>
        /// <returns></returns>
        public Dictionary<string, long[]> GetFamilyDomainHash (DomainInterface domainInterface1, DomainInterface domainInterface2)
        {
            DomainInterface[] domainInterfaces = new DomainInterface[2];
            domainInterfaces[0] = domainInterface1;
            domainInterfaces[1] = domainInterface2;
            Dictionary<string, long[]> familyDomainsHash = GetFamilyDomainHash(domainInterfaces);
            return familyDomainsHash;
        }

        /// <summary>
        /// the domain list for each family in the interaction
        /// </summary>
        /// <param name="domainInterfaces"></param>
        /// <returns></returns>
        public Dictionary<string, long[]> GetFamilyDomainHash(DomainInterface[] domainInterfaces)
        {
            Dictionary<string, List<long>> familyDomainListHash = new Dictionary<string,List<long>> ();
            GetFamilyDomainHash(domainInterfaces, ref familyDomainListHash);
            Dictionary<string, long[]> familyDomainsHash = new Dictionary<string, long[]>();
            foreach (string familyCode in familyDomainListHash.Keys)
            {
                familyDomainsHash.Add(familyCode, familyDomainListHash[familyCode].ToArray());
            }
            return familyDomainsHash;
        }

        /// <summary>
        /// the domain list for all interactions in one entry interfaces
        /// </summary>
        /// <param name="domainInterfaces"></param>
        /// <param name="familyDomainHash"></param>
        public void GetFamilyDomainHash(DomainInterface[] domainInterfaces, ref Dictionary<string, List<long>> familyDomainHash)
        {
            foreach (DomainInterface domainInterface in domainInterfaces)
            {
                if (familyDomainHash.ContainsKey(domainInterface.familyCode1))
                {
                    if (! familyDomainHash[domainInterface.familyCode1].Contains(domainInterface.domainId1))
                    {
                        familyDomainHash[domainInterface.familyCode1].Add(domainInterface.domainId1);
                    }
                }
                else
                {
                    List<long> domainList = new List<long> ();
                    domainList.Add(domainInterface.domainId1);
                    familyDomainHash.Add(domainInterface.familyCode1, domainList);
                }
                if (familyDomainHash.ContainsKey(domainInterface.familyCode2))
                {
                    if (! familyDomainHash[domainInterface.familyCode2].Contains(domainInterface.domainId2))
                    {
                        familyDomainHash[domainInterface.familyCode2].Add(domainInterface.domainId2);
                    }
                }
                else
                {
                    List<long> domainList = new List<long> ();
                    domainList.Add(domainInterface.domainId2);
                    familyDomainHash.Add(domainInterface.familyCode2, domainList);
                }
            }
        }

        /// <summary>
        /// the domain list for all interactions in one entry interfaces
        /// </summary>
        /// <param name="domainInterfaces"></param>
        /// <param name="familyDomainHash"></param>
        public void GetFamilyDomainHash(DomainInterface domainInterface, ref Dictionary<string, List<long>> familyDomainHash)
        {
            if (familyDomainHash.ContainsKey(domainInterface.familyCode1))
            {
                if (!familyDomainHash[domainInterface.familyCode1].Contains(domainInterface.domainId1))
                {
                    familyDomainHash[domainInterface.familyCode1].Add(domainInterface.domainId1);
                }
            }
            else
            {
                List<long> domainList = new List<long> ();
                domainList.Add(domainInterface.domainId1);
                familyDomainHash.Add(domainInterface.familyCode1, domainList);
            }
            if (familyDomainHash.ContainsKey(domainInterface.familyCode2))
            {
                if (!familyDomainHash[domainInterface.familyCode2].Contains(domainInterface.domainId2))
                {
                    familyDomainHash[domainInterface.familyCode2].Add(domainInterface.domainId2);
                }
            }
            else
            {
                List<long> domainList = new List<long> ();
                domainList.Add(domainInterface.domainId2);
                familyDomainHash.Add(domainInterface.familyCode2, domainList);
            }
        }
        #endregion
    }
}
