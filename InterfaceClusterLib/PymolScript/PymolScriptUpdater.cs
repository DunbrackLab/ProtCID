using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data;
using ProtCidSettingsLib;
using InterfaceClusterLib.AuxFuncs;
using AuxFuncLib;

namespace InterfaceClusterLib.PymolScript
{
    public class PymolScriptUpdater
    {
        #region member variables
        private ChainDomainUnpPfamArch objUnpPfamArch = new ChainDomainUnpPfamArch();
        private int pfamArchLenCutoff = 50;  // used for the object name with ChainPfamArch
        private SeqNumbersMatchData seqNumberMatch = new SeqNumbersMatchData();
        #endregion

        #region Rewrite PyMol script files
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pymolScriptFile"></param>
        /// <param name="coordDomains"></param>
        /// <returns></returns>
        public string ReWritePymolScriptFile(string pymolScriptFile, string[] coordDomains, string bestStructType)
        {
            string newPymolScriptFile = pymolScriptFile.Replace(".pml", "_" + bestStructType + ".pml");
            string newPymolScriptFileName = ReWritePymolScriptFile(pymolScriptFile, newPymolScriptFile, coordDomains);
            return newPymolScriptFileName;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pymolScriptFile"></param>
        /// <param name="newPymolScriptFile"></param>
        /// <param name="coordDomains"></param>
        /// <returns></returns>
        public string ReWritePymolScriptFile(string pymolScriptFile, string newPymolScriptFile, string[] coordDomains)
        {
            //         ProtCidSettings.logWriter.WriteLine("Rewriting pymol script file, file name: " + pymolScriptFile);
            //         ProtCidSettings.logWriter.Flush();
            StreamWriter dataWriter = new StreamWriter(newPymolScriptFile);
            StreamReader dataReader = new StreamReader(pymolScriptFile);
            string line = "";
            string coordDomain = "";
            bool isDomainNeeded = true;
            bool isCenterDomain = false;
            string updateSelString = "";
            List<string> coordDomainListInScript = new List<string>();
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("load") > -1)
                {
                    isDomainNeeded = false;
                    string[] fields = line.Split(' ');
                    coordDomain = fields[1].Replace(".pfam", "").Trim();
                    if (!isCenterDomain)
                    {
                        isCenterDomain = true;
                        isDomainNeeded = true;
                        coordDomainListInScript.Add(coordDomain);
                    }
                    else
                    {
                        if (coordDomains.Contains(coordDomain))
                        {
                            isDomainNeeded = true;
                            coordDomainListInScript.Add(coordDomain);
                        }
                    }
                }
                if (line.IndexOf("center") > -1)
                {
                    isDomainNeeded = true;
                }
                if (line.IndexOf("sele selectLigands,") > -1 || line.IndexOf("sele selectDnaRna,") > -1)
                {
                    updateSelString = GetSelectedLigands(line, coordDomains);
                    line = dataReader.ReadLine();  // show line
                    if (updateSelString != "")
                    {
                        dataWriter.WriteLine(updateSelString);
                        dataWriter.WriteLine(line);
                    }
                    continue;
                }
                if (line.IndexOf("sele cluster") > -1)// cluster lines
                {
                    updateSelString = GetSelectedLigands(line, coordDomains);
                    if (updateSelString != "")
                    {
                        dataWriter.WriteLine(updateSelString);
                    }
                    continue;
                }
                if (line.IndexOf("sele") > -1 && line.IndexOf("sele allhet") < 0)
                {
                    updateSelString = GetSelectedLigands(line, coordDomains);
                    if (updateSelString != "")
                    {
                        dataWriter.WriteLine(updateSelString);
                        continue;
                    }
                }
                if (isDomainNeeded)
                {
                    dataWriter.WriteLine(line);
                }
            }
            dataReader.Close();
            dataWriter.Close();
            FileInfo fileInfo = new FileInfo(newPymolScriptFile);
            return fileInfo.Name;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="selString"></param>
        /// <param name="coordDomains"></param>
        /// <returns></returns>
        private string GetSelectedLigands(string selString, string[] coordDomains)
        {
            if (selString == "")
            {
                return "";
            }
            int commonIndex = selString.IndexOf(',');
            string selPrefix = selString.Substring(0, commonIndex + 1);
            if (commonIndex + 1 > selString.Length)
            {
                ProtCidSettings.logWriter.WriteLine("Rewriting pymol script file error, " + selString + " no selected objects. return null");
                ProtCidSettings.logWriter.Flush();
                return "";
            }
            string chainString = selString.Substring(commonIndex + 1, selString.Length - commonIndex - 1);
            string[] chainFields = chainString.Split('+');
            int pfamIndex = 0;
            string chainDomain = "";
            string updateSelString = "";
            foreach (string chainField in chainFields)
            {
                pfamIndex = chainField.IndexOf(".pfam");
                chainDomain = chainField.Substring(0, pfamIndex).Trim();
                if (coordDomains.Contains(chainDomain))
                {
                    updateSelString += (chainField + "+");
                }
            }
            if (updateSelString != "")
            {
                updateSelString = selPrefix + updateSelString.TrimEnd('+');
            }
            return updateSelString;
        }        
        #endregion

        #region rename pdbid to uniprot+#domains+pdbid       
        /// <summary>
        /// rename all domains to unp+pfams+domain
        /// sort by names "order *,yes"
        /// </summary>
        /// <param name="pymolScriptFile"></param>
        /// <param name="newPymolScriptFile"></param>
        /// <param name="coordDomains"></param>
        /// <param name="domainUnpMapHash"></param>
        /// <param name="domainPfamHash"></param>
        /// <returns></returns>
        public string ReWritePymolScriptFileByUnpPfams(string pymolScriptFile, string unpPfamsRenameFile)
        {
            string extName = "";
            string[] coordDomains = ReadOjectNamesFromPmlFile(pymolScriptFile, out extName);
            Dictionary<string, string> domainNewPmlNameDict = GetDomainNewPyMolNameByUnpPfams(coordDomains);
            string newPmlFileName = WritePmlScriptRenamePmlObjects (coordDomains, unpPfamsRenameFile, domainNewPmlNameDict, extName);
            return newPmlFileName;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pymolScriptFile"></param>
        /// <param name="newPymolScriptFile"></param>
        /// <param name="domainNewPmlNameDict"></param>
        /// <returns></returns>
        public string WritePymolScriptToRenameByUnpPfams(string pymolScriptFile, string unpPfamsRenameFile, Dictionary<string, string> domainNewPmlNameDict)
        {
            string extName = "";
            string[] coordDomainsInFile = ReadOjectNamesFromPmlFile (pymolScriptFile, out extName);
            string renamePmlScriptFileName = WritePmlScriptRenamePmlObjects (coordDomainsInFile, unpPfamsRenameFile, domainNewPmlNameDict, extName);
            return renamePmlScriptFileName;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pmlObjs">objects in pymol with no extension name</param>
        /// <param name="renamePmlScriptFile"></param>
        /// <param name="objNewPmlNameDict">name map with no extension name</param>
        /// <param name="extName">extension name in pymol</param>
        /// <returns></returns>
        public string WritePmlScriptRenamePmlObjects (string[] pmlObjs, string renamePmlScriptFile, Dictionary<string, string> objNewPmlNameDict, string extName)
        {
            StreamWriter dataWriter = new StreamWriter(renamePmlScriptFile);
            string sortNames = "";
            foreach (string pmlObj in pmlObjs)
            {
                dataWriter.Write("set_name " + pmlObj + extName + ", " + objNewPmlNameDict[pmlObj] + "\n");
                sortNames += (objNewPmlNameDict[pmlObj] + " ");
            }
            sortNames = sortNames.TrimEnd();
            dataWriter.Write("order " + sortNames + ", yes, location=top\n");
            dataWriter.Close();
            FileInfo fileInfo = new FileInfo(renamePmlScriptFile);
            return fileInfo.Name;
        }

        /// <summary>
        /// extension name includes "."
        /// </summary>
        /// <param name="pmlScriptFile"></param>
        /// <param name="extname"></param>
        /// <returns></returns>
        public string[] ReadOjectNamesFromPmlFile(string pmlScriptFile, out string extname)
        {
            List<string> coordDomainList = new List<string>();
            string line = "";
            extname = "";
            StreamReader dataReader = new StreamReader(pmlScriptFile);
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("load") > -1)
                {
                    string[] fields = line.Split();
                    if (extname == "")
                    {
                        int extIndex = fields[1].IndexOf (".");
                        extname = fields[1].Substring(extIndex, fields[1].Length - extIndex);
                    }
                    coordDomainList.Add(fields[1].Replace(extname, ""));
                }
            }
            dataReader.Close();
            return coordDomainList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainDomain"></param>
        /// <param name="domainUnp"></param>
        /// <param name="domainChainPfams"></param>
        /// <returns></returns>
        public string FormatNewPymolObjectName(string pmlObjName, string unpName, string pfamArchName)
        {
            if (pfamArchName.Length > pfamArchLenCutoff)
            {
                pfamArchName = pfamArchName.Substring(0, pfamArchLenCutoff) + "...";
            }
            if (unpName == "")
            {
                unpName = "UNP_UKN";
            }
            string newPmlName = unpName + "." + pfamArchName + "." + pmlObjName;

            return newPmlName;
        }

        /// <summary>
        /// rename pymol object name by adding uniprots
        /// </summary>
        /// <param name="pmlObjName"></param>
        /// <param name="unpName"></param>
        /// <returns></returns>
        public string FormatNewPymolObjectName(string pmlObjName, string unpName)
        {
            if (unpName == "")
            {
                unpName = "UNP_UKN";
            }
            string newPmlName = unpName + "." + pmlObjName;

            return newPmlName;
        }

        #region domain to unp-pfams
        /// <summary>
        /// 
        /// </summary>
        /// <param name="coordDomains"></param>
        /// <returns></returns>
        public Dictionary<string, string> GetDomainNewPyMolNameByUnpPfams(string[] coordDomains)
        {
            Dictionary<string, string> domainUnpPfamNameDict = new Dictionary<string, string>();
            string domainUnp = "";
            string domainChainPfams = "";
            string newPmlObjectName = "";  // new PyMOL object name
            foreach (string chainDomain in coordDomains)
            {
                string pdbId = chainDomain.Substring(0, 4);
                int chainDomainId = Convert.ToInt32(chainDomain.Substring(4, chainDomain.Length - 4));
                domainUnp = objUnpPfamArch.GetDomainUnpCode(pdbId, chainDomainId);
                domainChainPfams = objUnpPfamArch.GetDomainChainPfamString(pdbId, chainDomainId);
                newPmlObjectName = FormatNewPymolObjectName(chainDomain, domainUnp, domainChainPfams);
                domainUnpPfamNameDict.Add(chainDomain, newPmlObjectName);
            }
            return domainUnpPfamNameDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="coordDomains"></param>
        /// <returns></returns>
        public Dictionary<string, string> GetDomainNewPyMolNameByUnps(string[] coordDomains)
        {
            Dictionary<string, string> domainUnpPfamNameDict = new Dictionary<string, string>();
            string domainUnp = "";
            string newPmlObjectName = "";  // new PyMOL object name
            foreach (string chainDomain in coordDomains)
            {
                string pdbId = chainDomain.Substring(0, 4);
                int chainDomainId = Convert.ToInt32(chainDomain.Substring(4, chainDomain.Length - 4));
                domainUnp = objUnpPfamArch.GetDomainUnpCode(pdbId, chainDomainId);
                newPmlObjectName = FormatNewPymolObjectName(chainDomain, domainUnp);
                domainUnpPfamNameDict.Add(chainDomain, newPmlObjectName);
            }
            return domainUnpPfamNameDict;
        }
        #endregion
                
        #endregion

        #region rename residue numbers
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chains">pdbId + asymchainId</param>
        /// <param name="renumberPmlScript"></param>
        public void RenumberChainResidueNumbersByUnpNumbers (string[] chains, string extName, string renumberPmlScript, string numberMatchFile)
        {
            string pdbId = "";
            string chainId = "";
            int entityId = 0;
            string[] uniprots = null;
            
            string renumberLine = "";
            int difBeg = 0;
            string renumberLines = "";
            string resiNumberMatchLine = "";
            StreamWriter resiNumMatchWriter = new StreamWriter(numberMatchFile);
            foreach (string entryChain in chains)
            {
                pdbId = entryChain.Substring(0, 4);
                chainId = entryChain.Substring(4, entryChain.Length - 4);
                entityId = GetChainEntityID(pdbId, chainId);
                List<int[]>[] seqDbRegions = seqNumberMatch.GetPdbSeqDbMatchRegions(pdbId, entityId, out uniprots);                
                resiNumberMatchLine = FormatResidueNumberMatchLine(entryChain, seqDbRegions, uniprots);
                resiNumMatchWriter.Write(resiNumberMatchLine + "\n");

                difBeg = seqDbRegions[1][0][0] - seqDbRegions[0][0][0];  // the difference of residue numbers of uniprot and pdb sequential numbers
                if (difBeg != 0)
                {
                    renumberLine = GetPmlRenumberLine(entryChain + extName, difBeg);
                    renumberLines += renumberLine + "\n";
                }
            }
            resiNumMatchWriter.Close();
            if (renumberLines != "")
            {
                StreamWriter pmlWriter = new StreamWriter(renumberPmlScript);
                pmlWriter.Write(renumberLines);
                pmlWriter.Close();
            }          
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pmlObj"></param>
        /// <param name="difNumber"></param>
        /// <returns></returns>
        private string GetPmlRenumberLine (string pmlObj, int difNumber)
        {
            string pmlLine = "alter " + pmlObj + ", resv+=" + difNumber;
            return pmlLine;
        } 

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domains"></param>
        /// <param name="renumberPmlScript"></param>
        public void RenumberDomainResidueNumbersByUnpNumbers (string[] domains, string extName, string renumberPmlScript, string numberMatchFile)
        {
            string pdbId = "";
            int chainDomainId = 0;
            int entityId = 0;
            string[] uniprots = null;
            string asymChain = "";
            string renumberLine = "";
            int difBeg = 0;
            string renumberLines = "";
            string resiNumberMatchLine = "";
            StreamWriter resiNumMatchWriter = new StreamWriter(numberMatchFile);
            foreach (string domain in domains)
            {
                pdbId = domain.Substring(0, 4);
                chainDomainId = Convert.ToInt32(domain.Substring (4, domain.Length - 4));
                entityId = GetDomainEntityId(pdbId, chainDomainId, out asymChain);               
                List<int[]>[] seqDbRegions = seqNumberMatch.GetPdbSeqDbMatchRegions (pdbId, entityId, out uniprots);
                resiNumberMatchLine = FormatResidueNumberMatchLine(domain, seqDbRegions, uniprots);
                resiNumMatchWriter.Write (resiNumberMatchLine + "\n");
                // if there are multiple uniprots for a chain, then it will not work
                difBeg = seqDbRegions[1][0][0] - seqDbRegions[0][0][0];  // the difference of residue numbers of uniprot and pdb sequential numbers
                if (difBeg != 0)
                {
                    renumberLine = GetPmlRenumberLine(domain + extName, difBeg);
                    renumberLines += renumberLine + "\n";
                }
            }
            resiNumMatchWriter.Close();
            if (renumberLines != "")
            {
                StreamWriter pmlWriter = new StreamWriter(renumberPmlScript);
                pmlWriter.Write(renumberLines);
                pmlWriter.Close();
            }    
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainDomainId"></param>
        /// <returns></returns>
        private int GetDomainEntityId (string pdbId, int chainDomainId, out string asymChain)
        {
            string queryString = string.Format("Select EntityID, AsymChain, AuthorChain From PdbPfamChain Where PdbID = '{0}' AND ChainDomainID = {1};", pdbId, chainDomainId);
            DataTable entityIdTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            int entityId = -1;
            asymChain = "";
            if (entityIdTable.Rows.Count > 0)
            {
                entityId = Convert.ToInt32 (entityIdTable.Rows[0]["EntityID"].ToString());
                asymChain = entityIdTable.Rows[0]["AsymChain"].ToString().TrimEnd();
            }
            return entityId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <returns></returns>
        private int GetChainEntityID (string pdbId, string asymChain)
        {
            string queryString = string.Format("Select EntityID From AsymUnit Where PdbID = '{0}' AND AsymID = '{1}';", pdbId, asymChain);
            DataTable entityTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            int entityId = -1;
            if (entityTable.Rows.Count > 0)
            {
                entityId = Convert.ToInt32(entityTable.Rows[0]["EntityID"].ToString ());
            }
            return entityId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbSeqBeg"></param>
        /// <param name="dbSeqBeg"></param>
        /// <returns></returns>
        private int GetBeginResidueNumDiff (int pdbSeqBeg, int dbSeqBeg)
        {
            return dbSeqBeg - pdbSeqBeg;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbDbSeqRegions"></param>
        /// <returns></returns>
        private bool ArePdbDbResNumbersSame (List<int[]>[] pdbDbSeqRegions)
        {
            for (int i = 0; i < pdbDbSeqRegions[0].Count; i ++)
            {
                if (pdbDbSeqRegions[0][i][0] != pdbDbSeqRegions[1][i][0] || 
                    pdbDbSeqRegions[0][i][1] != pdbDbSeqRegions[1][i][1])
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pmlObj"></param>
        /// <param name="pdbDbRegions"></param>
        /// <param name="uniprots"></param>
        /// <returns></returns>
        private string FormatResidueNumberMatchLine(string pmlObj, List<int[]>[] pdbDbRegions, string[] uniprots)
        {
            string residueNumMatchLines = "";
            for (int i = 0; i < pdbDbRegions[0].Count; i++)
            {
                residueNumMatchLines += (pmlObj + " " + uniprots[i] +  " [" + pdbDbRegions[0][i][0] + "-" + pdbDbRegions[0][i][1] + "] " +
                    "[" + pdbDbRegions[1][i][0] + "-" + pdbDbRegions[1][i][1] + "]\n");
            }
            return residueNumMatchLines;
        }
        #endregion
    }
}
