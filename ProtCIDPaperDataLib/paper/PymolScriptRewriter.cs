using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data;
using ProtCidSettingsLib;

namespace ProtCIDPaperDataLib.paper
{
    public class PymolScriptRewriter : PaperDataInfo
    {
        #region divid pymol script file
        public void DividPymolScriptFileToDifChainPfamArch()
        {
            int relSeqId = 602;
            int clusterId = 1;
            string relPfamId = "ACT";
            dataDir = @"X:\Qifang\Paper\protcid_update\data_v31\domainClusterInfo\ACT\ACT-ACT_602_1";
            string pymolScriptFile = Path.Combine(dataDir, relSeqId + "_" + clusterId + "_chain.pml");
            string queryString = string.Format("Select PdbID, DomainInterfaceId, ChainPfamArch From PfamDomainClusterInterfaces " +
                " Where RelSeqID = {0} AND ClusterID = {1} order by PdbId, DomainInterfaceID;", relSeqId, clusterId);
            DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
            Dictionary<string, List<string>> chainPfamArchEntryHash = new Dictionary<string, List<string>>();
            string chainPfamArch = "";
            string domainInterface = "";
            List<string> addedEntryList = new List<string>();
            string pdbId = "";
            foreach (DataRow interfaceRow in clusterInterfaceTable.Rows)
            {
                chainPfamArch = interfaceRow["ChainPfamArch"].ToString().TrimEnd();
                pdbId = interfaceRow["PdbID"].ToString();
                if (addedEntryList.Contains(pdbId))
                {
                    continue;
                }
                addedEntryList.Add(pdbId);
                domainInterface = interfaceRow["PdbID"].ToString() + "_d" + interfaceRow["DomainInterfaceID"].ToString() + ".cryst";

                string[] interfaceChainPfamArches = GetInterfaceChainPfamArch(chainPfamArch);
                if (interfaceChainPfamArches.Length == 1)
                {
                    if (chainPfamArchEntryHash.ContainsKey(interfaceChainPfamArches[0]))
                    {
                        chainPfamArchEntryHash[interfaceChainPfamArches[0]].Add(domainInterface);
                    }
                    else
                    {
                        List<string> domainInterfaceList = new List<string>();
                        domainInterfaceList.Add(domainInterface);
                        chainPfamArchEntryHash.Add(interfaceChainPfamArches[0], domainInterfaceList);
                    }
                }
                else
                {
                    foreach (string pfamArch in interfaceChainPfamArches)
                    {
                        if (pfamArch == "(" + relPfamId + ")")
                        {
                            continue;
                        }
                        if (chainPfamArchEntryHash.ContainsKey(pfamArch))
                        {
                            chainPfamArchEntryHash[pfamArch].Add(domainInterface);
                        }
                        else
                        {
                            List<string> domainInterfaceList = new List<string>();
                            domainInterfaceList.Add(domainInterface);
                            chainPfamArchEntryHash.Add(pfamArch, domainInterfaceList);
                        }
                    }
                }
            }
            string newPymolScriptFile = "";
    //        string accPfamId = "";
            foreach (string keyChainPfamArch in chainPfamArchEntryHash.Keys)
            {
       /*         accPfamId = GetAccessoryFileName(keyChainPfamArch, relPfamId);
                newPymolScriptFile = Path.Combine(dataDir, relSeqId.ToString() + "_" +
                    clusterId.ToString() + "_chain_" + accPfamId + ".pml");*/
                newPymolScriptFile = Path.Combine(dataDir, relSeqId.ToString() + "_" + clusterId.ToString() + "_" + keyChainPfamArch + ".pml");
                string[] domainInterfaces = chainPfamArchEntryHash[keyChainPfamArch].ToArray();
                WriteSubPymolScriptFile(pymolScriptFile, newPymolScriptFile, domainInterfaces);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="orgPymolScriptFile"></param>
        /// <param name="newPymolScriptFile"></param>
        /// <param name="domainInterfaces"></param>
        private void WriteSubPymolScriptFile(string orgPymolScriptFile, string newPymolScriptFile, string[] domainInterfaces)
        {
            StreamReader dataReader = new StreamReader(orgPymolScriptFile);
            StreamWriter dataWriter = new StreamWriter(newPymolScriptFile);
            string line = "";
            int lineCount = 0;
            string domainInterface = "";
            bool lineAdded = false;
            while ((line = dataReader.ReadLine()) != null)
            {
                if (lineCount < 6)
                {
                    dataWriter.WriteLine(line);
                }
                if (line.IndexOf("center") > -1)
                {
                    dataWriter.WriteLine(line);
                    continue;
                }
                if (line.IndexOf("load") > -1)
                {
                    domainInterface = line.Replace("load", "").Trim();
                    if (domainInterfaces.Contains(domainInterface))
                    {
                        lineAdded = true;
                    }
                    else
                    {
                        lineAdded = false;
                    }
                }
                if (lineAdded)
                {
                    dataWriter.WriteLine(line);
                }
                lineCount++;
            }

            dataReader.Close();
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainPfamArch"></param>
        /// <param name="relPfamId"></param>
        /// <returns></returns>
        private string GetAccessoryFileName(string chainPfamArch, string relPfamId)
        {
            string[] fields = chainPfamArch.Split(')');
            string firstPfamId = "self";
            foreach (string field in fields)
            {
                if (field.IndexOf(relPfamId) < 0)
                {
                    firstPfamId = field + ")";
                    break;
                }
            }
            firstPfamId = firstPfamId.Trim('_');
            return firstPfamId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="interfaceChainPfamArch"></param>
        /// <param name="relPfam"></param>
        /// <returns></returns>
        private string[] GetInterfaceChainPfamArch(string interfaceChainPfamArch)
        {
            string[] pfamArchFields = interfaceChainPfamArch.Split(';');
            List<string> chainPfamArchList = new List<string>();
            foreach (string chainPfamArch in pfamArchFields)
            {
                if (!chainPfamArchList.Contains(chainPfamArch))
                {
                    chainPfamArchList.Add(chainPfamArch);
                }
            }
            return chainPfamArchList.ToArray ();
        }
        #endregion

        #region rewrite pymol scripts
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pmlScriptFile"></param>
        public void RewritePymolScriptFileByAlign()
        {
            string pmlScriptFile = @"X:\Qifang\Paper\protcid_update\data_v31\domainClusterInfo\GP120\8712_1\8712_1_domain.pml";
            string newPmlScriptFile = pmlScriptFile.Replace(".pml", "_alignDimer.pml");
            StreamReader dataReader = new StreamReader(pmlScriptFile);
            StreamWriter dataWriter = new StreamWriter(newPmlScriptFile);
            string line = "";
            string dataLine = "";
            string[] dimers = null;
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("align ") > -1)
                {
                    dimers = GetTwoDimerStructs(line);
                    dataLine = "align " + dimers[0] + ", " + dimers[1] + "\n";
                    dataWriter.Write(dataLine);
                }
                else
                {
                    dataWriter.Write(line + "\n");
                }
            }
            dataWriter.Close();
            dataReader.Close();
        }

        public void RewritePymolScriptFileByRenameUnp()
        {
            int groupId = 8980;
        //    string pfamId = "HSP70";
            int clusterId = 1;
            string pmlScriptFile = @"X:\Qifang\Paper\protcid_update\data_v31\domainClusterInfo\Bromodomain\8980_1\8980_1.pml";
            string newPmlScriptFile = pmlScriptFile.Replace(".pml", "_unp.pml");
            string[] domainInterfaces = ReadDomainInterfacesFromPmlFile(pmlScriptFile);
            //    Dictionary<string, string> entryUnpDict = GetDomainInterfaceUnpDict(groupId, clusterId);
            Dictionary<string, string> entryUnpDict = GetChainInterfaceUnpDict(groupId, clusterId);
       //     Dictionary<string, string> entryUnpDict = GetPeptideInterfaceUnpDict(pfamId, clusterId);

            StreamWriter dataWriter = new StreamWriter(newPmlScriptFile);
            StreamReader dataReader = new StreamReader(pmlScriptFile);
            string pmlFileContent = dataReader.ReadToEnd();
            dataWriter.Write(pmlFileContent);
            dataReader.Close();
            dataWriter.Write('\n');
            string sortNames = "";
            string nameWithUnp = "";
            string pdbId = "";
            foreach (string domainInterface in domainInterfaces)
            {
                pdbId = domainInterface.Substring(0, 4);
                if (entryUnpDict.ContainsKey(pdbId))
                {
                    nameWithUnp = entryUnpDict[pdbId] + "_" + domainInterface;
                }
                else
                {
                    nameWithUnp = "UNP_UKN" + "_" + domainInterface;
                }
                dataWriter.Write("set_name " + domainInterface + ".cryst, " + nameWithUnp + "\n");
                sortNames += (nameWithUnp + " ");
            }
            sortNames = sortNames.TrimEnd();
            dataWriter.Write("order " + sortNames + ", yes, location=top\n");
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        /// <param name="domainInterfaces"></param>
        /// <returns></returns>
        private Dictionary<string, string> GetDomainInterfaceUnpDict(int relSeqId, int clusterId)
        {
            string queryString = string.Format("Select Distinct UnpCode, PdbID From PfamDomainClusterInterfaces Where RelSeqID = {0} AND ClusterID = {1};", relSeqId, clusterId);
            DataTable entryUnpTable = ProtCidSettings.protcidQuery.Query(queryString);
            Dictionary<string, string> entryUnpDict = new Dictionary<string, string>();
            foreach (DataRow unpRow in entryUnpTable.Rows)
            {
                entryUnpDict.Add(unpRow["PdbID"].ToString(), unpRow["UnpCode"].ToString().TrimEnd());
            }
            return entryUnpDict;
        }

        private Dictionary<string, string> GetPeptideInterfaceUnpDict(string pfamId, int clusterId)
        {
            string queryString = string.Format("Select Distinct PdbID From PfamPepInterfaceClusters Where PfamID = '{0}' AND ClusterID = {1};", pfamId, clusterId);
            DataTable entryTable = ProtCidSettings.protcidQuery.Query(queryString);
            string pdbId = "";
            Dictionary<string, string> entryUnpDict = new Dictionary<string, string>();
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                queryString = string.Format("Select Distinct PdbDbRefSifts.PdbID, DbCode From PdbDbRefSifts, PdbPfam " +
                    " Where PdbDbRefSifts.PdbID = '{0}' AND DbName = 'UNP' AND Pfam_ID = '{1}' " +
                    " AND PdbDbRefSifts.PdbID = PdbPfam.PdbID AND PdbDbRefSifts.EntityID = PdbPfam.EntityID;", pdbId, pfamId);
                DataTable unpTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                if (unpTable.Rows.Count > 0)
                {
                    entryUnpDict.Add(pdbId, unpTable.Rows[0]["DbCode"].ToString().TrimEnd());
                }
            }
            return entryUnpDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        /// <param name="domainInterfaces"></param>
        /// <returns></returns>
        private Dictionary<string, string> GetChainInterfaceUnpDict(int chainGroupId, int clusterId)
        {
            string queryString = string.Format("Select Distinct UnpCode, PdbID From PfamSuperClusterEntryInterfaces Where SuperGroupSeqID = {0} AND ClusterID = {1};", chainGroupId, clusterId);
            DataTable entryUnpTable = ProtCidSettings.protcidQuery.Query(queryString);
            Dictionary<string, string> entryUnpDict = new Dictionary<string, string>();
            foreach (DataRow unpRow in entryUnpTable.Rows)
            {
                entryUnpDict.Add(unpRow["PdbID"].ToString(), unpRow["UnpCode"].ToString().TrimEnd());
            }
            return entryUnpDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pmlScriptFile"></param>
        /// <returns></returns>
        public string[] ReadDomainInterfacesFromPmlFile(string pmlScriptFile)
        {
            List<string> interfaceList = new List<string>();
            string line = "";
            StreamReader dataReader = new StreamReader(pmlScriptFile);
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("load") > -1)
                {
                    string[] fields = line.Split();
                    interfaceList.Add(fields[1].Replace(".cryst", ""));
                }
            }
            dataReader.Close();
            return interfaceList.ToArray();


        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="alignLine"></param>
        /// <returns></returns>
        private string[] GetTwoDimerStructs(string alignLine)
        {
            string noAlignLine = alignLine.Replace("align ", "");
            string[] dimers = new string[2];
            string[] fields = noAlignLine.Split(',');
            int andIndex = fields[0].IndexOf("and");
            dimers[0] = fields[0].Substring(0, andIndex).TrimEnd();
            andIndex = fields[1].IndexOf("and");
            dimers[1] = fields[1].Substring(0, andIndex).TrimEnd();
            return dimers;
        }
        #endregion
    }
}
