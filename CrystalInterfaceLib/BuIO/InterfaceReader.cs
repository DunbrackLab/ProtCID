using System;
using System.IO;
using System.Collections.Generic;
using CrystalInterfaceLib.Contacts;
using CrystalInterfaceLib.Crystal;
using AuxFuncLib;

namespace CrystalInterfaceLib.BuIO
{
	/// <summary>
	/// Summary description for InterfaceReader.
	/// </summary>
	public class InterfaceReader
	{
		public InterfaceReader()
		{
        }

        #region regular chain interface
        /// <summary>
		/// read the interface file
		/// </summary>
		/// <param name="fileName"></param>
		/// <param name="theInterface"></param>
		/// <param name="atomType"></param>
		public string ReadInterfaceFromFile (string fileName, ref InterfaceChains theInterface, string atomType)
		{
			List<AtomInfo> chainAtomList = new List<AtomInfo> ();
			if (! File.Exists (fileName))
			{
				return "";
			}
			StreamReader interfaceReader = new StreamReader (fileName);
            string line = "";
            string currentChain = "";
            string preChain = "";
            string remarkString = "";
            bool atomStart = false;
			try
			{
				while ((line = interfaceReader.ReadLine ()) != null)
				{
					if (line.IndexOf ("HEADER") > -1)
					{
						int interfaceIdIndex = line.ToUpper ().IndexOf ("INTERFACEID");
						string interfaceIdStr = "";
						for (int idIndex = interfaceIdIndex + "INTERFACEID: ".Length; idIndex < line.Length; idIndex++)
						{
							if (char.IsDigit (line[idIndex]))
							{
								interfaceIdStr += line[idIndex].ToString ();
							}
							else
							{
								break;
							}
						}
						theInterface.interfaceId = Convert.ToInt32 (interfaceIdStr);
                        remarkString += (line + "\r\n");
						continue;
					}
					// get the asymmetric chain
					if (line.IndexOf ("Interface Chain A") > -1)
					{
						int asymChainIndex = line.IndexOf ("Asymmetric Chain");
						int asymChainEndIndex = line.IndexOf ("Author Chain");
						string chain = line.Substring (asymChainIndex + "Asymmetric Chain".Length + 1, 
							asymChainEndIndex - asymChainIndex - "Asymmetric Chain".Length - 1).Trim ();
						int symOpIndex = line.IndexOf  ("Symmetry Operator") + "Symmetry Operator".Length;
                        int symOpEndIndex = line.IndexOf("Full Symmetry Operator");
                        if (symOpEndIndex < 0)
                        {
                            symOpEndIndex = line.Length;
                        }
						string symOpString = line.Substring (symOpIndex, symOpEndIndex - symOpIndex).Trim ();
						theInterface.firstSymOpString = chain + "_" + symOpString;
						int entityIdx = line.IndexOf ("Entity") + "Entity".Length;
						int entityId = Convert.ToInt32 
							(line.Substring (entityIdx, line.IndexOf  ("Symmetry Operator") - entityIdx).Trim ());
						theInterface.entityId1 = entityId;
                        remarkString += (line + "\r\n");
						continue;
					}
					if (line.IndexOf ("Interface Chain B") > -1)
					{
						int asymChainIndex = line.IndexOf ("Asymmetric Chain");
						int asymChainEndIndex = line.IndexOf ("Author Chain");
						string chain = line.Substring (asymChainIndex + "Asymmetric Chain".Length + 1, 
							asymChainEndIndex - asymChainIndex - "Asymmetric Chain".Length - 1).Trim ();
						int symOpIndex = line.IndexOf  ("Symmetry Operator") + "Symmetry Operator".Length;
                        int symOpEndIndex = line.IndexOf("Full Symmetry Operator");
                        if (symOpEndIndex < 0)
                        {
                            symOpEndIndex = line.Length;
                        }
                        string symOpString = line.Substring(symOpIndex, symOpEndIndex - symOpIndex).Trim();
						theInterface.secondSymOpString = chain + "_" + symOpString;
						int entityIdx = line.IndexOf ("Entity") + "Entity".Length;
						int entityId = Convert.ToInt32 
							(line.Substring (entityIdx, line.IndexOf  ("Symmetry Operator") - entityIdx).Trim ());
						theInterface.entityId2 = entityId;
                        remarkString += (line + "\r\n");
						continue;
					}
					// only read protein chains
					if (line.Length > 6 && (line.ToUpper ().Substring (0, 6) == "ATOM  "||
						line.ToUpper ().Substring (0, 6) == "HETATM"))
					{
                        atomStart = true;
						AtomInfo atom = new AtomInfo ();
						string[] fields = ParseHelper.ParsePdbAtomLine (line);
						if (fields[4] == "HOH")
						{
							continue;
						}
						currentChain = fields[5];
						// the end of one chain
						if (currentChain != preChain && preChain != "" && preChain != " ")
						{
							// ignore the latter chain id with same chain names
							theInterface.chain1 = chainAtomList.ToArray ();
							chainAtomList = new List<AtomInfo> ();
							preChain = currentChain;
						}
						// add specified atom to current chain
						switch (atomType)
						{
							case "CA":
								if (fields[2] != "CA")
								{
									continue;
								}
								break;

							case "CB":
								if (fields[2] != "CB")
								{
									continue;
								}
								break;

							case "CA_CB":
								if (fields[2] != "CA" && fields[2] != "CB")
								{
									continue;
								}
								break;

							default:
								break;
						}					
						atom.atomName = fields[2];
						atom.atomId = Convert.ToInt32 (fields[1]);
						atom.residue = fields[4];
						if (fields[7] != " ")
						{
							atom.seqId = fields[6] + fields[7];
						}
						else
						{
							atom.seqId = fields[6];
						}
						try
						{
							atom.xyz = new Coordinate (Convert.ToDouble (fields[8]), Convert.ToDouble (fields[9]), 
								Convert.ToDouble (fields[10]));
							chainAtomList.Add (atom);
						}
						catch {}
					}
					preChain = currentChain;
                    if (!atomStart)
                    {
                        remarkString += (line + "\r\n");
                    }
				}	
                theInterface.chain2 = chainAtomList.ToArray ();
			}
			catch (Exception ex)
			{
				throw new Exception ("Parsing " + fileName + " Errors: " + ex.Message);
			}
			finally
			{
				interfaceReader.Close ();
			}
            remarkString = remarkString.TrimEnd("\r\n".ToCharArray ());
            return remarkString;
        }
        #endregion

        #region read interacting chains and hetero info
        /// <summary>
        /// read the interface file
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="theInterface"></param>
        /// <param name="atomType"></param>
        public string[] ReadInterfaceFromFile(string fileName, ref InterfaceChains theInterface)
        {
            List<AtomInfo> chainAtomList = new List<AtomInfo> ();
            if (!File.Exists(fileName))
            {
                return null;
            }
            StreamReader interfaceReader = new StreamReader(fileName);
            string line = "";
            string currentChain = "";
            string preChain = "";
            string remarkString = "";
            string heteroInfoString = "";
            bool atomStart = false;
            bool protChainARead = false;
            bool protChainBRead = false;
            try
            {
                while ((line = interfaceReader.ReadLine()) != null)
                {
                    if (line.IndexOf("HEADER") > -1)
                    {
                        int interfaceIdIndex = line.ToUpper().IndexOf("INTERFACEID");
                        string interfaceIdStr = "";
                        for (int idIndex = interfaceIdIndex + "INTERFACEID: ".Length; idIndex < line.Length; idIndex++)
                        {
                            if (char.IsDigit(line[idIndex]))
                            {
                                interfaceIdStr += line[idIndex].ToString();
                            }
                            else
                            {
                                break;
                            }
                        }
                        theInterface.interfaceId = Convert.ToInt32(interfaceIdStr);
                        remarkString += (line + "\r\n");
                        continue;
                    }
                    // get the asymmetric chain
                    if (line.IndexOf("Interface Chain A") > -1)
                    {
                        if (!protChainARead)
                        {
                            remarkString += (line + "\r\n");

                            int asymChainIndex = line.IndexOf("Asymmetric Chain");
                            int asymChainEndIndex = line.IndexOf("Author Chain");
                            string chain = line.Substring(asymChainIndex + "Asymmetric Chain".Length + 1,
                                asymChainEndIndex - asymChainIndex - "Asymmetric Chain".Length - 1).Trim();
                            int symOpIndex = line.IndexOf("Symmetry Operator") + "Symmetry Operator".Length;
                            string symOpString = line.Substring(symOpIndex, line.Length - symOpIndex).Trim();
                            theInterface.firstSymOpString = chain + "_" + symOpString;
                            int entityIdx = line.IndexOf("Entity") + "Entity".Length;
                            int entityId = Convert.ToInt32
                                (line.Substring(entityIdx, line.IndexOf("Symmetry Operator") - entityIdx).Trim());
                            theInterface.entityId1 = entityId;
                            protChainARead = true;
                            continue;
                        }
                    }
                    if (line.IndexOf("Interface Chain B") > -1)
                    {
                        if (!protChainBRead)
                        {
                            remarkString += (line + "\r\n");

                            int asymChainIndex = line.IndexOf("Asymmetric Chain");
                            int asymChainEndIndex = line.IndexOf("Author Chain");
                            string chain = line.Substring(asymChainIndex + "Asymmetric Chain".Length + 1,
                                asymChainEndIndex - asymChainIndex - "Asymmetric Chain".Length - 1).Trim();
                            int symOpIndex = line.IndexOf("Symmetry Operator") + "Symmetry Operator".Length;
                            string symOpString = line.Substring(symOpIndex, line.Length - symOpIndex).Trim();
                            theInterface.secondSymOpString = chain + "_" + symOpString;
                            int entityIdx = line.IndexOf("Entity") + "Entity".Length;
                            int entityId = Convert.ToInt32
                                (line.Substring(entityIdx, line.IndexOf("Symmetry Operator") - entityIdx).Trim());
                            theInterface.entityId2 = entityId;
                            protChainBRead = true;
                            continue;
                        }
                    }
                    if (line.IndexOf ("Interface surface area") > -1)
                    {
                        int saIndex = line.IndexOf("Interface surface area: ") + "Interface surface area: ".Length;
                        double surfaceArea = -1;
                        string saField = line.Substring (saIndex, line.Length - saIndex);
                        if (System.Double.TryParse(saField, out surfaceArea))
                        {
                            theInterface.surfaceArea = surfaceArea;
                        }
                        else
                        {
                            theInterface.surfaceArea = -1;
                        }
                    }
                    // only read protein chains
                    if (line.Length > 6 && (line.ToUpper().Substring(0, 6) == "ATOM  "))
                    {
                        atomStart = true;
                        AtomInfo atom = new AtomInfo();
                        string[] fields = ParseHelper.ParsePdbAtomLine(line);
                        if (fields[4] == "HOH")
                        {
                            continue;
                        }
                        currentChain = fields[5];
                        // the end of one chain
                        if (currentChain != preChain && preChain != "" && preChain != " ")
                        {
                            // ignore the latter chain id with same chain names
                            theInterface.chain1 = chainAtomList.ToArray ();
                            chainAtomList = new List<AtomInfo> ();
                            preChain = currentChain;
                        }
                        atom.atomName = fields[2];
                        atom.atomId = Convert.ToInt32(fields[1]);
                        atom.residue = fields[4];
                        if (fields[7] != " ")
                        {
                            atom.seqId = fields[6] + fields[7];
                        }
                        else
                        {
                            atom.seqId = fields[6];
                        }
                        try
                        {
                            atom.xyz = new Coordinate(Convert.ToDouble(fields[8]), Convert.ToDouble(fields[9]),
                                Convert.ToDouble(fields[10]));
                            atom.bfactor = fields[12];
                            atom.occupancy = fields[11];
                            atom.atomType = fields[14];
                            chainAtomList.Add(atom);
                        }
                        catch { }
                    }
                    if (line.Length > 6 && line.ToUpper().Substring(0, 6) == "HETATM")
                    {
                        heteroInfoString += (line + "\r\n");
                    }
                    preChain = currentChain;
                    if (!atomStart)
                    {
                        remarkString += (line + "\r\n");
                    }
                }
                theInterface.chain2 = chainAtomList.ToArray ();
            }
            catch (Exception ex)
            {
                throw new Exception("Parsing " + fileName + " Errors: " + ex.Message);
            }
            finally
            {
                interfaceReader.Close();
            }
            remarkString = remarkString.TrimEnd("\r\n".ToCharArray());
            string[] interfaceFileInfo = new string[2];
            interfaceFileInfo[0] = remarkString;
            interfaceFileInfo[1] = heteroInfoString.TrimEnd("\r\n".ToCharArray());
            return interfaceFileInfo;
        }
        #endregion

        #region read domain interface
        /// <summary>
        /// read the interface file
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="theInterface"></param>
        /// <param name="atomType"></param>
        public string ReadInterfaceChainsFromFile(string fileName, ref InterfaceChains theInterface)
        {
            List<AtomInfo> chainAtomList = new List<AtomInfo> ();
            if (!File.Exists(fileName))
            {
                return "";
            }
            StreamReader interfaceReader = new StreamReader(fileName);
            string line = "";
            string currentChain = "";
            string preChain = "";
            string remarkString = "";
            bool atomStart = false;

            try
            {
                while ((line = interfaceReader.ReadLine()) != null)
                {
                    // only read protein chains
                    if (line.Length > 6 && (line.ToUpper().Substring(0, 6) == "ATOM  " ||
                        line.ToUpper().Substring(0, 6) == "HETATM"))
                    {
                        atomStart = true;
                        AtomInfo atom = new AtomInfo();
                        string[] fields = ParseHelper.ParsePdbAtomLine(line);
                        if (fields[4] == "HOH")
                        {
                            continue;
                        }
                        currentChain = fields[5];
                        // the end of one chain
                        if (currentChain != preChain && preChain != "" && preChain != " ")
                        {
                            // ignore the latter chain id with same chain names
                            theInterface.chain1 = chainAtomList.ToArray ();
                            chainAtomList = new List<AtomInfo> ();
                            preChain = currentChain;
                        }
                        atom.atomName = fields[2];
                        atom.atomId = Convert.ToInt32(fields[1]);
                        atom.residue = fields[4];
                        if (fields[7] != " ")
                        {
                            atom.seqId = fields[6] + fields[7];
                        }
                        else
                        {
                            atom.seqId = fields[6];
                        }
                        try
                        {
                            atom.xyz = new Coordinate(Convert.ToDouble(fields[8]), Convert.ToDouble(fields[9]),
                                Convert.ToDouble(fields[10]));
                            chainAtomList.Add(atom);
                        }
                        catch { }
                    }
                    if (!atomStart)
                    {
                        remarkString += (line + "\r\n");
                    }
                    preChain = currentChain;
                }
                theInterface.chain2 = chainAtomList.ToArray ();
            }
            catch (Exception ex)
            {
                throw new Exception("Parsing " + fileName + " Errors: " + ex.Message);
            }
            finally
            {
                interfaceReader.Close();
            }
            remarkString = remarkString.TrimEnd("\r\n".ToCharArray ());
            return remarkString;
        }



        #region parse remark string in the domain interface file
        /// <summary>
        /// domainFileRemark: 
        /* HEADER    201l                     1/31/2013 12:00:00 AM
            REMARK  2 Domain Interface ID:1
            REMARK  2 Interface ID:1
            REMARK  3 Asymmetric Chain1:A_X,Y,Z; Asymmetric Chain2:B_X,Y,Z
            REMARK  3 Entity ID1:1; Entity ID2:1
            REMARK  4 PFAM Domain 1:100959387  Domain Ranges:24-157
            REMARK  4 PFAM Domain 2:100959387  Domain Ranges:24-157*/
        /* for multi-Chain domain interface file
         * HEADER 2z2t 8  2/4/2013
        REMARK 3  Interface Chain A
        REMARK 3  Chain DomainID 1 DomainID 10051748  Symmetry Operator: 1_555
        REMARK 3  EntityID: 1 Asymmetric Chain: A
        REMARK 3  Sequence Start: 1 Sequence End: 38 File Start: 1 File End: 38
        REMARK 3  EntityID: 2 Asymmetric Chain: D
        REMARK 3  Sequence Start: 1 Sequence End: 36 File Start: 39 File End: 74
        REMARK 3  Interface Chain B
        REMARK 3  Chain DomainID 3 DomainID 10051748  Symmetry Operator: 2_655
        REMARK 3  EntityID: 1 Asymmetric Chain: C
        REMARK 3  Sequence Start: 1 Sequence End: 38 File Start: 1 File End: 38
        REMARK 3  EntityID: 2 Asymmetric Chain: F
        REMARK 3  Sequence Start: 1 Sequence End: 36 File Start: 39 File End: 74
         * */
        /// </summary>
        /// <param name="domainFileRemark"></param>
        /// <returns></returns>
        public string[] GetDomainChainStrings(string domainInterfaceFileRemark)
        {
            string[] fields = domainInterfaceFileRemark.Split("\r\n".ToCharArray());
            if (domainInterfaceFileRemark.IndexOf("Peptide Chain") > -1) // domain-peptide interface
            {
                string[] domainChainInfo = GetDomainPeptideChainStrings(fields);
                return domainChainInfo;
            }
            string[] asymChains = new string[2];
            int chainCount = 0;
            string[] domainIds = new string[2];
            int domainCount = 0;
            string dataLine = "";
            int pfamDomainIndex = 0;
            foreach (string field in fields)
            {
                if (field == "")
                {
                    continue;
                }

                int asymChainIndex1 = field.IndexOf("Asymmetric Chain1:");
                if (asymChainIndex1 > -1)
                {
                    dataLine = field.Substring(asymChainIndex1, field.Length - asymChainIndex1);
                    string[] asymChainFields = dataLine.Split(';');
                    asymChains[chainCount] = GetAsymmetricChain(asymChainFields[0]);
                    chainCount++;
                    asymChains[chainCount] = GetAsymmetricChain(asymChainFields[1]);
                    continue;
                }
                pfamDomainIndex = field.IndexOf("PFAM Domain 1");
                if (pfamDomainIndex > -1)
                {
                    pfamDomainIndex = pfamDomainIndex + "PFAM Domain 1".Length;
                    dataLine = field.Substring(pfamDomainIndex, field.Length - pfamDomainIndex);
                    domainIds[domainCount] = GetDomainString(dataLine);
                    domainCount++;
                    continue;
                }
                pfamDomainIndex = field.IndexOf("PFAM Domain 2");
                if (pfamDomainIndex > -1)
                {
                    pfamDomainIndex = pfamDomainIndex + "PFAM Domain 2".Length;
                    dataLine = field.Substring(pfamDomainIndex, field.Length - pfamDomainIndex);
                    domainIds[domainCount] = GetDomainString(dataLine);
                    continue;
                }
            }
            if (domainCount == 1 && chainCount == 0)  // intra-chain domain interface
            {
                foreach (string field in fields)
                {
                    if (field == "")
                    {
                        continue;
                    }

                    int asymChainIndex = field.IndexOf("Asymmetric Chain");
                    if (asymChainIndex > -1)
                    {
                        int chainIndex = asymChainIndex + "Asymmetric Chain".Length;
                        string asymChain = field.Substring(chainIndex, field.Length - chainIndex).Trim();
                        asymChains[chainCount] = asymChain;
                        chainCount++;
                        asymChains[chainCount] = asymChain;
                        break;
                    }
                }
            }
            if (domainCount == 0)
            {
                domainCount = -1;
                foreach (string field in fields)
                {
                    int domainIdIndex = field.LastIndexOf ("DomainID");
                    if (domainIdIndex > -1)
                    {
                        domainCount++;
                        domainIds[domainCount] = GetDomainStringOtherFormat(field);
                        continue;
                    }
                    int asymChainIndex = field.IndexOf("Asymmetric Chain");
                    if (asymChainIndex > -1)
                    {
                        asymChains[domainCount] += (GetAsymmetricChainOtherFormat(field) + ",");
                    }
                }
            }
            string[] domainChainStrings = new string[2];
            domainChainStrings[0] = domainIds[0] + "_" + asymChains[0].TrimEnd(',');
            domainChainStrings[1] = domainIds[1] + "_" + asymChains[1].TrimEnd(',');
            return domainChainStrings;
        }
        /*
         * "HEADER 148l 6/25/2013\r\n
            REMARK 1 Domain Interface ID: 7\r\n
            REMARK 3  Chain DomainID 1 DomainID 10095935 Symmetry Operator: 1_555\r\n
            REMARK 3  EntityID: 1 Asymmetric Chain: A\r\n
            REMARK 3  Sequence Start: 1 Sequence End: 164 File Start: 1 File End: 164\r\n
            REMARK 3  Peptide Chain: B\r\n
            REMARK 4   #AtomPairs: 24 #ResiduePairs: 75"


            "HEADER 1a07 6/25/2013\r\n
            REMARK 1 Domain Interface ID: 6\r\n
            REMARK 3  Chain DomainID 2 DomainID 1000170 Symmetry Operator: 1_555\r\n
            REMARK 3  Peptide Chain: D\r\n
            REMARK 4   #AtomPairs: 16 #ResiduePairs: 35"
         * */
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fields"></param>
        /// <returns></returns>
        private string[] GetDomainPeptideChainStrings(string[] fields)
        {
            string[] domainChainStrings = new string[2];
            foreach (string field in fields)
            {
                int chainDomainIndex = field.IndexOf("Chain DomainID");
                if (chainDomainIndex > -1)
                {
                    domainChainStrings[0] = GetProtDomainIdString(field);
                }
                int pepChainIndex = field.IndexOf("Peptide Chain");
                if (pepChainIndex > -1)
                {
                    domainChainStrings[1] = GetPeptideChainOtherFormat(field);
                }
            }
            return domainChainStrings;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="protInfoLine"></param>
        /// <returns></returns>
        private string GetProtDomainIdString(string protInfoLine)
        {
            string[] fields = ParseHelper.SplitPlus(protInfoLine, ' ');
            string chainDomainId = fields[4];
            string domainId = fields[6];
            return domainId + "_" + chainDomainId;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterfaceFileRemark"></param>
        /// <returns></returns>
        public long[] GetDomainIds (string domainInterfaceFileRemark)
        {
            string[] fields = domainInterfaceFileRemark.Split("\r\n".ToCharArray());
            string[] domainIds = new string[2];
            int domainCount = 0;
            string dataLine = "";
            int pfamDomainIndex = 0;
            foreach (string field in fields)
            {
                if (field == "")
                {
                    continue;
                }
                pfamDomainIndex = field.IndexOf("PFAM Domain 1");
                if (pfamDomainIndex > -1)
                {
                    pfamDomainIndex = pfamDomainIndex + "PFAM Domain 1".Length;
                    dataLine = field.Substring(pfamDomainIndex, field.Length - pfamDomainIndex);
                    domainIds[domainCount] = GetDomainString(dataLine);
                    domainCount++;
                    continue;
                }
                pfamDomainIndex = field.IndexOf("PFAM Domain 2");
                if (pfamDomainIndex > -1)
                {
                    pfamDomainIndex = pfamDomainIndex + "PFAM Domain 2".Length;
                    dataLine = field.Substring(pfamDomainIndex, field.Length - pfamDomainIndex);
                    domainIds[domainCount] = GetDomainString(dataLine);
                    continue;
                }
            }
            if (domainCount == 0)
            {
                domainCount = -1;
                foreach (string field in fields)
                {
                    int domainIdIndex = field.LastIndexOf ("DomainID");
                    if (domainIdIndex > -1)
                    {
                        domainCount++;
                        domainIds[domainCount] = GetDomainStringOtherFormat(field);
                        continue;
                    }
                }
            }
            long[] domainNumbers = new long[2];
            domainNumbers[0] = Convert.ToInt64(domainIds[0]);
            domainNumbers[1] = Convert.ToInt64(domainIds[1]);
            return domainNumbers;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="asymChainField"></param>
        /// <returns></returns>
        private string GetAsymmetricChain(string asymChainField)
        {
            string[] fields = asymChainField.Split(':');
            string[] chainSymOpFields = fields[1].Split('_');
            return chainSymOpFields[0];
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainField"></param>
        /// <returns></returns>
        private string GetDomainString(string domainField)
        {
            int rangeIndex = domainField.IndexOf("Domain Ranges");
            int domainIndex = domainField.IndexOf(":");
            string domainString = "";
            if (domainIndex > -1)
            {
                domainString = domainField.Substring(domainIndex + 1, rangeIndex - domainIndex - 1).Trim();
            }
            else
            {
                domainString = domainField.Substring(0, rangeIndex).Trim();
            }
            return domainString;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainIdLine"></param>
        /// <returns></returns>
        private string GetDomainStringOtherFormat(string domainIdLine)
        {
            int domainIdIndex = domainIdLine.LastIndexOf ("DomainID");
            int symOpIndex = domainIdLine.IndexOf("Symmetry Operator");
            string domainIdString = domainIdLine.Substring(domainIdIndex + "DomainID".Length, symOpIndex - domainIdIndex - "DomainID".Length).TrimEnd();
            return domainIdString;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainLine"></param>
        /// <returns></returns>
        private string GetAsymmetricChainOtherFormat(string chainLine)
        {
            int asymChainIndex = chainLine.IndexOf("Asymmetric Chain: ");
            string asymChain = chainLine.Substring(asymChainIndex + "Asymmetric Chain: ".Length,
                            chainLine.Length - asymChainIndex - "Asymmetric Chain: ".Length).Trim();
            return asymChain;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainLine"></param>
        /// <returns></returns>
        private string GetPeptideChainOtherFormat(string chainLine)
        {
            int pepChainIndex = chainLine.IndexOf("Peptide Chain: ");
            string pepChain = chainLine.Substring(pepChainIndex + "Peptide Chain: ".Length,
                            chainLine.Length - pepChainIndex - "Peptide Chain: ".Length).Trim();
            return pepChain;
        }
        #endregion
        #endregion

        #region Interfaces From BU
        /// <summary>
		/// read the interface file from PDB and PQS interface files
		/// temporary functions
		/// due to a bug in the previous interface writer
		/// </summary>
		/// <param name="fileName"></param>
		/// <param name="theInterface"></param>
		/// <param name="atomType"></param>
		public void ReadBuInterfaceFromFile (string fileName, ref InterfaceChains theInterface, string atomType)
		{
			if (! File.Exists (fileName))
			{
				return;
			}
			StreamReader interfaceReader = new StreamReader (fileName);
			try
			{
				string line = "";
				string currentChain = "";
				List<AtomInfo> chain1Atoms = new List<AtomInfo> ();
				List<AtomInfo> chain2Atoms = new List<AtomInfo> ();

				while ((line = interfaceReader.ReadLine ()) != null)
				{
					if (line.IndexOf ("HEADER") > -1)
					{
						int interfaceIdIndex = line.ToUpper ().IndexOf ("INTERFACEID");
						string interfaceIdStr = "";
						for (int idIndex = interfaceIdIndex + "INTERFACEID: ".Length; idIndex < line.Length; idIndex++)
						{
							if (char.IsDigit (line[idIndex]))
							{
								interfaceIdStr += line[idIndex].ToString ();
							}
							else
							{
								break;
							}
						}
						theInterface.interfaceId = Convert.ToInt32 (interfaceIdStr);
						continue;
					}
					// get the asymmetric chain
					if (line.IndexOf ("Interface Chain A") > -1)
					{
						int asymChainIndex = line.IndexOf ("Asymmetric Chain");
						int asymChainEndIndex = line.IndexOf ("Author Chain");
						string chain = line.Substring (asymChainIndex + "Asymmetric Chain".Length + 1, 
							asymChainEndIndex - asymChainIndex - "Asymmetric Chain".Length - 1).Trim ();
						int symOpIndex = line.IndexOf  ("Symmetry Operator") + "Symmetry Operator".Length;
						string symOpString = line.Substring (symOpIndex, line.Length - symOpIndex).Trim ();
						theInterface.firstSymOpString = chain + "_" + symOpString;
						int entityIdx = line.IndexOf ("Entity") + "Entity".Length;
						int pqsIdx = line.IndexOf ("PQS Chain");
						int entityId = -1;
						if (pqsIdx > -1)
						{
							entityId = Convert.ToInt32 (line.Substring (entityIdx, pqsIdx - entityIdx));
						}
						else
						{
							entityId = Convert.ToInt32 
								(line.Substring (entityIdx, line.IndexOf  ("Symmetry Operator") - entityIdx).Trim ());
						}
						theInterface.entityId1 = entityId;
						continue;
					}
					if (line.IndexOf ("Interface Chain B") > -1)
					{
						int asymChainIndex = line.IndexOf ("Asymmetric Chain");
						int asymChainEndIndex = line.IndexOf ("Author Chain");
						string chain = line.Substring (asymChainIndex + "Asymmetric Chain".Length + 1, 
							asymChainEndIndex - asymChainIndex - "Asymmetric Chain".Length - 1).Trim ();
						int symOpIndex = line.IndexOf  ("Symmetry Operator") + "Symmetry Operator".Length;
						string symOpString = line.Substring (symOpIndex, line.Length - symOpIndex).Trim ();
						theInterface.secondSymOpString = chain + "_" + symOpString;
						int entityIdx = line.IndexOf ("Entity") + "Entity".Length;
						int pqsIdx = line.IndexOf ("PQS Chain");
						int entityId = -1;
						if (pqsIdx > -1)
						{
							entityId = Convert.ToInt32 (line.Substring (entityIdx, pqsIdx - entityIdx));
						}
						else
						{
							entityId = Convert.ToInt32 
								(line.Substring (entityIdx, line.IndexOf  ("Symmetry Operator") - entityIdx).Trim ());
						}
						theInterface.entityId2 = entityId;
						continue;
					}
					// only read protein chains
					if (line.Length > 6 && (line.ToUpper ().Substring (0, 6) == "ATOM  "||
						line.ToUpper ().Substring (0, 6) == "HETATM"))
					{
						AtomInfo atom = new AtomInfo ();
						string[] fields = ParseHelper.ParsePdbAtomLine (line);
						if (fields[4] == "HOH")
						{
							continue;
						}
						currentChain = fields[5];
						// add specified atom to current chain
						switch (atomType)
						{
							case "CA":
								if (fields[2] != "CA")
								{
									continue;
								}
								break;

							case "CB":
								if (fields[2] != "CB")
								{
									continue;
								}
								break;

							case "CA_CB":
								if (fields[2] != "CA" && fields[2] != "CB")
								{
									continue;
								}
								break;

							default:
								break;
						}					
						atom.atomName = fields[2];
						atom.atomId = Convert.ToInt32 (fields[1]);
						atom.residue = fields[4];
						if (fields[7] != " ")
						{
							atom.seqId = fields[6] + fields[7];
						}
						else
						{
							atom.seqId = fields[6];
						}
						try
						{
							atom.xyz = new Coordinate (Convert.ToDouble (fields[8]), Convert.ToDouble (fields[9]), 
								Convert.ToDouble (fields[10]));
							if (currentChain == "A")
							{
								chain1Atoms.Add (atom);
							}
							else if (currentChain == "B")
							{
								chain2Atoms.Add (atom);
							}
						}
						catch {}
					}
				}	
                theInterface.chain1 = chain1Atoms.ToArray ();
                theInterface.chain2 = chain2Atoms.ToArray ();
			}
			catch (Exception ex)
			{
				throw new Exception ("Parsing " + fileName + " Errors: " + ex.Message);
			}
			finally
			{
				interfaceReader.Close ();
			}
        }
        #endregion
    }
}
