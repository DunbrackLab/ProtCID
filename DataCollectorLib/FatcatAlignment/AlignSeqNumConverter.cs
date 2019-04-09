using System;
using System.IO;
using System.Collections.Generic;
using System.Data;
using DbLib;
using AuxFuncLib;
using ProtCidSettingsLib;

namespace DataCollectorLib.FatcatAlignment
{
	/// <summary>
	/// Convert PDB residue numbers to XML numbers from alignment 
	/// </summary>
	public class AlignSeqNumConverter
	{
		private DbQuery dbQuery = new DbQuery ();
        public DataTable asuSeqInfoTable = null;
		public AlignSeqNumConverter()
		{
            asuSeqInfoTable = GetSequenceTable();
        }

        #region chain alignments
        /// <summary>
		/// Add residues with no-coordinates or no-Calpha to the alignment
		/// </summary>
		/// <param name="alignInfo1"></param>
		/// <param name="alignInfo2"></param>
		public void AddDisorderResiduesToAlignment (ref AlignSeqInfo alignInfo1, ref AlignSeqInfo alignInfo2)
		{
			List<string> pdbList = new List<string>  ();
			pdbList.Add (alignInfo1.pdbId);
			if (! pdbList.Contains (alignInfo2.pdbId))
			{
				pdbList.Add (alignInfo2.pdbId);
			}
			DataTable seqTable = GetSequenceTable (pdbList, asuSeqInfoTable);
            string[] chainSequences1 = GetChainSequences(alignInfo1, seqTable);
            string[] chainSequences2 = GetChainSequences(alignInfo2, seqTable);
            // no disorder residues in the middle of the chain
            if (!IsSequenceWithDisorderResidues(chainSequences1[0]) && ! IsSequenceWithDisorderResidues(chainSequences2[0]))
            {
                return;
            }

			try
			{
				if (HasMissingResidues (alignInfo1.alignSequence, alignInfo1.alignStart, alignInfo1.alignEnd))
				{
                    int[] alignXmlSeqIndexes1 = GetXmlSeqIndexes (ref alignInfo1, chainSequences1[0]);
                    FillMissingResidues(ref alignInfo1, alignXmlSeqIndexes1, chainSequences1[1], ref alignInfo2);
				}
			}
			catch (Exception ex)
			{
				throw ex;
			}
			try
			{
				if (HasMissingResidues (alignInfo2.alignSequence, alignInfo2.alignStart, alignInfo2.alignEnd))
				{
                    int[] alignXmlSeqIndexes2 = GetXmlSeqIndexes (ref alignInfo2, chainSequences2[0]);
                    FillMissingResidues(ref alignInfo2, alignXmlSeqIndexes2, chainSequences2[1], ref alignInfo1);
				}	
			}
			catch (Exception ex)
			{
				throw ex;
			}
        }

        /// <summary>
        /// residue numbers to xml numbers
        /// </summary>
        /// <param name="alignInfo"></param>
        /// <param name="seqTable"></param>
        /// <returns></returns>
        private int[] GetXmlSeqIndexes (ref AlignSeqInfo alignInfo, string coordSequence)
        {
            string nonGapAlignString = GetNonGapSequenceString(alignInfo.alignSequence);
            int[] xmlSeqIndexes = GetXmlIndexes(nonGapAlignString, coordSequence);
         /*   if (xmlSeqNumbers.Length == 0)
            {
          * since I used XML sequential numbers, no Blast needed.
                xmlSeqNumbers = MatchSequencesByBlast(nonGapAlignString, coordSequence);
            }*/
            if (xmlSeqIndexes.Length == 0)
            {
                return null;
            }
            alignInfo.alignStart = xmlSeqIndexes[0] + 1;  // add 1 on January 4, 2017
            alignInfo.alignEnd = xmlSeqIndexes[xmlSeqIndexes.Length - 1] + 1;
            return xmlSeqIndexes;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="alignInfo"></param>
        /// <returns></returns>
        private string[] GetChainSequences(AlignSeqInfo alignInfo, DataTable seqTable)
        {
            DataRow[] seqRows = seqTable.Select(string.Format("PdbID = '{0}' AND AsymID = '{1}'",
               alignInfo.pdbId, alignInfo.asymChainId));
            if (seqRows.Length == 0)
            {
                seqRows = seqTable.Select(string.Format("PdbID = '{0}' AND AuthorChain = '{1}'",
                                    alignInfo.pdbId, alignInfo.chainId));
                if (seqRows.Length == 0)
                {
                    return null;
                }
            }
            string[] chainSequences = new string[2];
            chainSequences[0] = seqRows[0]["SequenceInCoord"].ToString();
            chainSequences[1] = seqRows[0]["Sequence"].ToString();
            return chainSequences;
        }

        /// <summary>
        /// Fill out missing residues in aligned sequence
        /// </summary>
        /// <param name="alignInfo1">alignment info for first aligned chain</param>
        /// <param name="alignXmlSeq1">xml sequential numbers for first aligned chain</param>
        /// <param name="seqString">xml sequence for the first aligned chain</param>
        /// <param name="alignInfo2">alignment info for the second chain</param>
        private void FillMissingResidues(ref AlignSeqInfo alignInfo1, int[] xmlSeqIndexes, string seqString, ref AlignSeqInfo alignInfo2)
        {
            Dictionary<int, int> xmlSeqAlignIdxHash = new Dictionary<int,int> ();
            int startAlignIdx = 0;
            int alignIdx = -1;
            Array.Sort(xmlSeqIndexes);
            int seqIdx = 0;
            for (int i = 0; i < xmlSeqIndexes.Length; i++)
            {
                alignIdx = GetAlignIndex(alignInfo1.alignSequence, i, startAlignIdx, ref seqIdx);
                if (alignIdx < 0)
                {
                    throw new Exception("Get aligned index error for " + xmlSeqIndexes[i].ToString());
                }
                xmlSeqAlignIdxHash.Add(xmlSeqIndexes[i], alignIdx);
                startAlignIdx = alignIdx;
            }

            int endAlignIdx = -1;
            int xmlSeqDif = 0;
            int alignSeqDif = 0;
            try
            {
                for (int i = 0; i < xmlSeqIndexes.Length - 1; i++)
                {
                    if (xmlSeqIndexes[i + 1] > xmlSeqIndexes[i] + 1)
                    {
                        xmlSeqDif = xmlSeqIndexes[i + 1] - xmlSeqIndexes[i];
                        startAlignIdx = (int)xmlSeqAlignIdxHash[xmlSeqIndexes[i]];
                        endAlignIdx = (int)xmlSeqAlignIdxHash[xmlSeqIndexes[i + 1]];
                        alignSeqDif = endAlignIdx - startAlignIdx;
                        if (xmlSeqDif > alignSeqDif) // need inserted
                        {
                            // propogate the difference for following aligned residues
                            int dif = xmlSeqDif - alignSeqDif;
                            for (int j = i + 1; j < xmlSeqIndexes.Length; j++)
                            {
                                xmlSeqAlignIdxHash[xmlSeqIndexes[j]] = (int)xmlSeqAlignIdxHash[xmlSeqIndexes[j]] + dif;
                            }
                            string insertTemp = "";
                            int k = 0;
                            while (k < dif)
                            {
                                insertTemp += "-";
                                k++;
                            }
                            // place holder
                            alignInfo1.alignSequence = alignInfo1.alignSequence.Insert(startAlignIdx + 1, insertTemp);
                            alignInfo2.alignSequence = alignInfo2.alignSequence.Insert(startAlignIdx + 1, insertTemp);
                        }
                        // remove gaps first
                        // fill out by real residue names from asymunit 
                        string missingResidueString = seqString.Substring(xmlSeqIndexes[i] + 1, xmlSeqDif - 1);
                        alignInfo1.alignSequence = alignInfo1.alignSequence.Remove(startAlignIdx + 1, missingResidueString.Length);
                        alignInfo1.alignSequence = alignInfo1.alignSequence.Insert(startAlignIdx + 1, missingResidueString);
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        #endregion

        #region domain alignments
        /// <summary>
        /// Add residues with no-coordinates or no-Calpha to the alignment
        /// </summary>
        /// <param name="alignInfo1"></param>
        /// <param name="alignInfo2"></param>
        public void AddDisorderResiduesToDomainAlignment(ref DomainAlignSeqInfo alignInfo1, ref DomainAlignSeqInfo alignInfo2)
        {
            List<string> pdbList = new List<string> ();
            pdbList.Add(alignInfo1.pdbId);
            if (!pdbList.Contains(alignInfo2.pdbId))
            {
                pdbList.Add(alignInfo2.pdbId);
            }
            DataTable seqTable = GetSequenceTable(pdbList, asuSeqInfoTable);
            // the sequence in coordinate and sequence from the domain files which are used to FATCAT align
            string[] domainSequences1 = GetDomainFileSequences(alignInfo1.pdbId, alignInfo1.domainId, seqTable);
            string[] domainSequences2 = GetDomainFileSequences(alignInfo2.pdbId, alignInfo2.domainId, seqTable);
            // if there are no disorder residues in the domain sequences, do nothing
            if (!IsSequenceWithDisorderResidues(domainSequences1[0]) && !IsSequenceWithDisorderResidues(domainSequences2[0]))
            {
                return;
            }

            try
            {
                if (HasMissingResidues(alignInfo1.alignSequence, alignInfo1.alignStart, alignInfo1.alignEnd))
                {
                    int[] alignXmlSeq1 = ConvertSeqToXmlSeq(ref alignInfo1, domainSequences1[0]);
                    // from sequence of the domain file
                    string[] alignSequences = FillMissingResidues(alignInfo1.alignSequence, alignXmlSeq1, domainSequences1[1], alignInfo2.alignSequence);
                    alignInfo1.alignSequence = alignSequences[0];
                    alignInfo2.alignSequence = alignSequences[1];
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            try
            {
                if (HasMissingResidues(alignInfo2.alignSequence, alignInfo2.alignStart, alignInfo2.alignEnd))
                {
                    int[] alignXmlSeq2 = ConvertSeqToXmlSeq(ref alignInfo2, domainSequences2[0]);
                    string[] alignSequences = FillMissingResidues(alignInfo2.alignSequence, alignXmlSeq2, domainSequences2[1], alignInfo1.alignSequence);
                    alignInfo2.alignSequence = alignSequences[0];
                    alignInfo1.alignSequence = alignSequences[1];
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        /// <summary>
        /// residue numbers to xml numbers
        /// </summary>
        /// <param name="alignInfo"></param>
        /// <param name="seqTable"></param>
        /// <returns></returns>
        private int[] ConvertSeqToXmlSeq(ref DomainAlignSeqInfo alignInfo, string domainSeqInCoord)
        {
            string nonGapAlignString = GetNonGapSequenceString(alignInfo.alignSequence);

            int[] xmlSeqIndexes = GetXmlIndexes(nonGapAlignString, domainSeqInCoord);
       /*     if (xmlSeqNumbers.Length == 0)
            {
                xmlSeqNumbers = MatchSequencesByBlast(nonGapAlignString, domainSeqInCoord);
            }*/
            if (xmlSeqIndexes.Length == 0)
            {
                return null;
            }
            alignInfo.alignStart = xmlSeqIndexes[0] + 1; // added 1 on January 4, 2017
            alignInfo.alignEnd = xmlSeqIndexes[xmlSeqIndexes.Length - 1] + 1;
            return xmlSeqIndexes;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainId"></param>
        /// <param name="seqTable"></param>
        /// <returns></returns>
        private string[] GetDomainFileSequences(string pdbId, long domainId, DataTable seqTable)
        {
            DataTable domainFileTable = GetDomainFileInfoTable(pdbId, domainId);
            string asymChain = "";
            int seqStart = 0;
            int seqEnd = 0;
            string domainSequenceInCoord = "";
            string domainSequence = "";
            string seqInCoord = "";
            string sequence = "";
            foreach (DataRow fileRow in domainFileTable.Rows)
            {
                asymChain = fileRow["AsymChain"].ToString().TrimEnd();
                seqStart = Convert.ToInt32(fileRow["SeqStart"].ToString());
                seqEnd = Convert.ToInt32(fileRow["SeqEnd"].ToString());
                DataRow[] seqRows = seqTable.Select(string.Format("PdbID = '{0}' AND AsymID = '{1}'", pdbId, asymChain));
                seqInCoord = seqRows[0]["SequenceInCoord"].ToString();
                sequence = seqRows[0]["Sequence"].ToString();
                domainSequenceInCoord += seqInCoord.Substring(seqStart - 1, seqEnd - seqStart + 1);
                domainSequence += sequence.Substring(seqStart - 1, seqEnd - seqStart + 1);
            }
            string[] domainSequences = new string[2];
            domainSequences[0] = domainSequenceInCoord;
            domainSequences[1] = domainSequence;
            return domainSequences;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainId"></param>
        /// <returns></returns>
        private DataTable GetDomainFileInfoTable(string pdbId, long domainId)
        {
            string queryString = string.Format("Select * From PdbPfamDomainFileInfo Where DomainID = {0} Order By FileStart;", domainId);
            DataTable domainFileTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            DataTable chainDomainFileInfoTable = domainFileTable.Clone();
            List<int> chainDomainIdList = new List<int> ();
            int chainDomainId = 0;
            foreach (DataRow domainFileRow in domainFileTable.Rows)
            {
                chainDomainId = Convert.ToInt32(domainFileRow["ChainDomainID"].ToString ());
                if (!chainDomainIdList.Contains(chainDomainId))
                {
                    chainDomainIdList.Add(chainDomainId);
                }
            }
            chainDomainIdList.Sort();

            chainDomainId = (int)chainDomainIdList[0];
            DataRow[] chainDomainFileRows = domainFileTable.Select(string.Format ("ChainDomainID = '{0}'", chainDomainId));
            foreach (DataRow chainDomainFileRow in chainDomainFileRows)
            {
                DataRow newDataRow = chainDomainFileInfoTable.NewRow();
                newDataRow.ItemArray = chainDomainFileRow.ItemArray;
                chainDomainFileInfoTable.Rows.Add(newDataRow);
            }
            return chainDomainFileInfoTable;
        }
        /// <summary>
        /// Fill out missing residues in aligned sequence
        /// </summary>
        /// <param name="alignInfo1">alignment info for first aligned chain</param>
        /// <param name="alignXmlSeq1">xml sequential numbers for first aligned chain</param>
        /// <param name="seqString">xml sequence for the first aligned chain</param>
        /// <param name="alignInfo2">alignment info for the second chain</param>
        private string[] FillMissingResidues(string alignSequence1, int[] xmlSeqNumbers, string seqString, string alignSequence2)
        {
            Dictionary<int, int> xmlSeqAlignIdxHash = new Dictionary<int,int> ();
            int startAlignIdx = 0;
            int alignIdx = -1;
            Array.Sort(xmlSeqNumbers);
            int seqIdx = 0;
            for (int i = 0; i < xmlSeqNumbers.Length; i++)
            {
                alignIdx = GetAlignIndex(alignSequence1, i, startAlignIdx, ref seqIdx);
                if (alignIdx < 0)
                {
                    throw new Exception("Get aligned index error for " + xmlSeqNumbers[i].ToString());
                }
                xmlSeqAlignIdxHash.Add(xmlSeqNumbers[i], alignIdx);
                startAlignIdx = alignIdx;
            }

            int endAlignIdx = -1;
            int xmlSeqDif = 0;
            int alignSeqDif = 0;
            try
            {
                for (int i = 0; i < xmlSeqNumbers.Length - 1; i++)
                {
                    if (xmlSeqNumbers[i + 1] > xmlSeqNumbers[i] + 1)
                    {
                        xmlSeqDif = xmlSeqNumbers[i + 1] - xmlSeqNumbers[i];
                        startAlignIdx = (int)xmlSeqAlignIdxHash[xmlSeqNumbers[i]];
                        endAlignIdx = (int)xmlSeqAlignIdxHash[xmlSeqNumbers[i + 1]];
                        alignSeqDif = endAlignIdx - startAlignIdx;
                        if (xmlSeqDif > alignSeqDif) // need inserted
                        {
                            // propogate the difference for following aligned residues
                            int dif = xmlSeqDif - alignSeqDif;
                            for (int j = i + 1; j < xmlSeqNumbers.Length; j++)
                            {
                                xmlSeqAlignIdxHash[xmlSeqNumbers[j]] = (int)xmlSeqAlignIdxHash[xmlSeqNumbers[j]] + dif;
                            }
                            string insertTemp = "";
                            int k = 0;
                            while (k < dif)
                            {
                                insertTemp += "-";
                                k++;
                            }
                            // place holder
                            alignSequence1 = alignSequence1.Insert(startAlignIdx + 1, insertTemp);
                            alignSequence2 = alignSequence2.Insert(startAlignIdx + 1, insertTemp);
                        }
                        // remove gaps first
                        // fill out by real residue names from asymunit 
                        string missingResidueString = seqString.Substring(xmlSeqNumbers[i] + 1, xmlSeqDif - 1);
                        alignSequence1 = alignSequence1.Remove(startAlignIdx + 1, missingResidueString.Length);
                        alignSequence1 = alignSequence1.Insert(startAlignIdx + 1, missingResidueString);
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            string[] alignSequences = new string[2];
            alignSequences[0] = alignSequence1;
            alignSequences[1] = alignSequence2;
            return alignSequences;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainSequenceInCoord"></param>
        /// <returns></returns>
        public bool IsSequenceWithDisorderResidues (string sequenceInCoord)
        {
            int startIndex = 0;
            int endIndex = sequenceInCoord.Length - 1;
            while (startIndex < sequenceInCoord.Length && sequenceInCoord[startIndex] == '-')
            {
                startIndex++;
            }
            while (endIndex >= 0 && sequenceInCoord[endIndex] == '-')
            {
                endIndex--;
            }
            if (endIndex < startIndex) // no coordinates, no FATCAT alignments, skip it
            {
                return false;
            }
            string domainSeqWithNoEndGaps = sequenceInCoord.Substring(startIndex, endIndex - startIndex + 1);
            if (domainSeqWithNoEndGaps.IndexOf("-") > -1)
            {
                return true;
            }
            return false;
        }
        #endregion

        #region other functions
        /// <summary>
		/// the sequence info for the list of pdb
		/// </summary>
		/// <param name="pdbList"></param>
		/// <returns></returns>
		private DataTable GetSequenceTable (List<string> pdbList, DataTable asuSeqInfoTable)
		{
		/*	string queryString = string.Format ("SELECT PdbID, AsymID, AuthorChain, EntityID, Sequence, SequenceInCoord FROM AsymUnit " + 
				" WHERE PdbID IN ({0}) AND PolymerType = 'polypeptide';", 
				ParseHelper.FormatSqlListString (pdbList));
			return dbQuery.Query (queryString);*/
            DataTable seqInfoTable = asuSeqInfoTable.Clone();
            foreach (string pdbId in pdbList)
            {
                DataRow[] seqInfoRows = asuSeqInfoTable.Select(string.Format ("PdbID = '{0}'", pdbId));
                foreach (DataRow seqInfoRow in seqInfoRows)
                {
                    DataRow dataRow = seqInfoTable.NewRow();
                    dataRow.ItemArray = seqInfoRow.ItemArray;
                    seqInfoTable.Rows.Add(dataRow);
                }
            }
            return seqInfoTable;
		}

        /// <summary>
        /// the sequence info for the list of pdb
        /// </summary>
        /// <param name="pdbList"></param>
        /// <returns></returns>
        private DataTable GetSequenceTable()
        {
            string queryString = "SELECT PdbID, AsymID, AuthorChain, EntityID, Sequence, SequenceInCoord FROM AsymUnit " +
                " WHERE PolymerType = 'polypeptide';";
            DataTable asuSeqInfoTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            return asuSeqInfoTable;
        }

		/// <summary>
		/// xml sequential numbers for aligned sequence
		/// </summary>
		/// <param name="alignSeq">aligned sequence</param></param>
		/// <param name="coordSeq">sequence in coordinate</param>
		/// <returns>indexes of xml sequential numbers for aligned sequence</returns>
		private int[] GetXmlIndexes (string alignSeq, string coordSeq)
		{
			string nonGapCoordString = GetNonGapSequenceString (coordSeq);
			int startIdxInCoord = -1;
			bool removedM = false;
			startIdxInCoord = nonGapCoordString.IndexOf (alignSeq);
			if (startIdxInCoord < 0)
			{
				string nonMCoordString = GetNonMSequenceString (nonGapCoordString);
				startIdxInCoord = nonMCoordString.IndexOf (alignSeq);
				if (startIdxInCoord < 0)
				{
					return MapAlignedSequenceToXml (alignSeq, coordSeq);
				}
				else
				{
					removedM = true;
				}
			}
			
			int idxInCoord = -1;
			List<int> xmlIndexList = new List<int> ();
			bool alignStarted = false;
			for (int xmlIdx = 0; xmlIdx < coordSeq.Length; xmlIdx ++)
			{
				if (coordSeq[xmlIdx] == '-')
				{
					continue;
				}	
				if (removedM && coordSeq[xmlIdx] == 'M')
				{
					continue;
				}
				idxInCoord ++;
				if (idxInCoord == startIdxInCoord)
				{
					alignStarted = true;
				}
				if (alignStarted)
				{
                    xmlIndexList.Add(xmlIdx);
				}
				if (idxInCoord == startIdxInCoord + alignSeq.Length - 1)
				{
					break;
				}
			}
            return xmlIndexList.ToArray ();
		}

		/// <summary>
		/// find index in the aligned sequence including gaps from residue seqid 
		/// </summary>
		/// <param name="alignSeq"></param>
		/// <param name="seqId"></param>
		/// <param name="startAlignIdx"></param>
		/// <returns></returns>
		public int GetAlignIndex (string alignSeq, int seqId, int startAlignIdx, ref int seqIdx)
		{
			int alignIdx = -1;
			for(int i = startAlignIdx; i < alignSeq.Length; i ++)
			{	
				if (alignSeq[i] == '-' || alignSeq[i] == '.')
				{
					continue;
				}
				
				if (seqIdx == seqId)
				{
					alignIdx = i;
					break;
				}
				seqIdx ++;
			}
			return alignIdx;
		}
		/// <summary>
		/// are there missing residues in the aligned sequence
		/// </summary>
		/// <param name="alignSeq"></param>
		/// <param name="residueNums"></param>
		/// <returns></returns>
		private bool HasMissingResidues (string alignSeq, int startPos, int endPos)
		{
			string tempString = GetNonGapSequenceString (alignSeq);

			if (endPos - startPos + 1 == tempString.Length)
			{
				return false;
			}
			return true;
		}

		/// <summary>
		/// the sequence string without gaps or filling values
		/// </summary>
		/// <param name="alignedSeq"></param>
		/// <returns></returns>
		public string GetNonGapSequenceString (string alignedSeq)
		{
			string nonGapString = "";
			foreach (char ch in alignedSeq)
			{
				if (ch == '-' || ch == '.')
				{
					continue;
				}
				nonGapString += ch.ToString ();
			}
			return nonGapString;
		}
		/// <summary>
		/// the sequence string without gaps or filling values
		/// </summary>
		/// <param name="alignedSeq"></param>
		/// <returns></returns>
		public string GetNonMSequenceString (string alignedSeq)
		{
			string nonGapString = "";
			foreach (char ch in alignedSeq)
			{
				if (ch == 'M')
				{
					continue;
				}
				nonGapString += ch.ToString ();
			}
			return nonGapString;
        }
        #endregion

        #region simple sequence alignment
        /// <summary>
		/// match aligned sequence to xml sequence
		/// </summary>
		/// <param name="alignSeq"></param>
		/// <param name="coordSeq"></param>
		public int[] MapAlignedSequenceToXml (string alignSeq, string coordSeq)
		{
			int[] startIndexes = GetCoordIndexes (coordSeq, alignSeq);
			int[] xmlSeqNumbers = AlignSequences (alignSeq, coordSeq, startIndexes);
			return xmlSeqNumbers;
		}
		/// <summary>
		/// 
		/// </summary>
		/// <param name="coordSeq"></param>
		/// <param name="alignSeq"></param>
		/// <returns></returns>
		private int[] GetCoordIndexes (string coordSeq, string alignSeq)
		{
			List<int> idxList = new List<int>  ();
			char ch = alignSeq[0];
			for (int i = 0; i < coordSeq.Length - alignSeq.Length; i ++)
			{
				if (coordSeq[i] == ch)
				{
					idxList.Add (i);
				}
			}
			return idxList.ToArray ();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="alignSeq"></param>
		/// <param name="coordSeq"></param>
		/// <param name="startIdx"></param>
		private int[] AlignSequences (string alignSeq, string coordSeq, int[] startIndexes)
		{
			List<int> coordIdxList = null;
			int difIdx = 0;
			int gapNums = 0;
			int maxGapNums = 0;
			List<int> maxCoordIdxList = new List<int> ();
			foreach (int startIdx in startIndexes)
			{
				difIdx = startIdx;
				coordIdxList = new List<int> ();
				gapNums = 0;
				for (int i = 0; i < alignSeq.Length; i ++)
				{					
					while (i + difIdx < coordSeq.Length && coordSeq[i + difIdx] == '-')
					{
						difIdx ++;
					}
					if (i + difIdx >= coordSeq.Length)
					{
						break;
					}
					if (alignSeq[i] == coordSeq[i + difIdx])
					{
						coordIdxList.Add (i + difIdx);
					}
					else
					{
						while (i + difIdx < coordSeq.Length && alignSeq[i] != coordSeq[i + difIdx])
						{
							if (coordSeq[i + difIdx] != '-')
							{
								gapNums ++;
							}
							difIdx ++;	
						}
						if (i + difIdx == coordSeq.Length)
						{
							break;
						}
						coordIdxList.Add (i + difIdx);
					}
				}
				if (coordIdxList.Count > maxCoordIdxList.Count)
				{
					maxCoordIdxList = coordIdxList; // take the longest one
					maxGapNums = gapNums;
				}
				else if (coordIdxList.Count == maxCoordIdxList.Count)
				{
					if (maxGapNums > gapNums) // take the one with less gaps
					{
						maxCoordIdxList = coordIdxList;
						maxGapNums = gapNums;
					}
				}
			}
			return maxCoordIdxList.ToArray ();
		}
		#endregion

		#region align by blast
		public struct BlastAlignInfo
		{
			public string alignSequence;
			public int startPos;
			public int endPos;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="seq1"></param>
		/// <param name="seq2"></param>
		/// <param name="xmlSeqIndexHash"></param>
		/// <returns></returns>
		private int[] MatchSequencesByBlast (string alignSeq, string xmlCoordSeq)
		{
			string seq1File = "seq1File.txt";
			WriteSequenceToFile (seq1File, alignSeq);

			string seq2File = "seq2File.txt";
			string noMissingCoordSeq = GetNonGapSequenceString (xmlCoordSeq);
			WriteSequenceToFile (seq2File, noMissingCoordSeq);

			string outputFile = RunBlast2Sequence (seq1File, seq2File);

			File.Delete (seq1File);
			File.Delete (seq2File);
			BlastAlignInfo[] alignInfos = ParseAlignFile (outputFile);

			Dictionary<int, int> xmlIdxCoordHash = GetSeqIdForCoordSeq (xmlCoordSeq);
			
			List<int> xmlIdxList = new List<int>  ();
			int xmlStart = xmlIdxCoordHash[alignInfos[1].startPos] - (alignInfos[0].startPos - 1);
			int xmlEnd = xmlIdxCoordHash[alignInfos[1].endPos] + (alignSeq.Length - alignInfos[0].endPos);
			for (int i = xmlStart; i < xmlIdxCoordHash[alignInfos[1].startPos]; i ++)
			{
				xmlIdxList.Add (i);
			}
			int coordSeqId = -1;
			for (int i = 0; i < alignInfos[0].alignSequence.Length; i ++)
			{
				if (alignInfos[1].alignSequence[i] != '-')
				{
					coordSeqId ++;
					if (alignInfos[0].alignSequence[i] == '-')
					{
						continue;
					}
					xmlIdxList.Add (xmlIdxCoordHash[alignInfos[1].startPos + coordSeqId]);	
					
				}
			}
			for (int i = xmlIdxCoordHash[alignInfos[1].endPos]; i < xmlEnd; i ++)
			{
				xmlIdxList.Add (i);
			}

			return xmlIdxList.ToArray ();
		}

		/// <summary>
		/// the corresponding relationship between letter in xml sequene id
		/// and the index of the sequence without missing letters
		/// </summary>
		/// <param name="xmlCoordSeq"></param>
		/// <returns></returns>
		private Dictionary<int, int> GetSeqIdForCoordSeq (string xmlCoordSeq)
		{
			Dictionary<int, int> xmlIdxCoordHash = new Dictionary<int,int>  ();
			int coordIdx = 1;
			for (int xmlIdx = 0; xmlIdx < xmlCoordSeq.Length; xmlIdx ++)
			{
				if (xmlCoordSeq[xmlIdx] == '-')
				{
					continue;
				}
				xmlIdxCoordHash.Add (coordIdx, xmlIdx + 1); // sequence id starts from 1
				coordIdx ++;
			}
			return xmlIdxCoordHash;
		}
		/// <summary>
		/// 
		/// </summary>
		/// <param name="alignFile"></param>
		/// <returns></returns>
		private BlastAlignInfo[] ParseAlignFile (string alignFile)
		{
			StreamReader dataReader = new StreamReader (alignFile);
			string line = "";
		
			BlastAlignInfo queryAlignInfo = new BlastAlignInfo ();
			BlastAlignInfo hitAlignInfo = new BlastAlignInfo ();
			bool queryStart = true;
			bool hitStart = true;
			while ((line = dataReader.ReadLine ()) != null)
			{
				if (line.Length > "Query:".Length && line.Substring (0, "Query:".Length) == "Query:")
				{
					string[] fields = ParseHelper.SplitPlus (line, ' ');
					if (queryStart)
					{
						queryAlignInfo.startPos = Convert.ToInt32 (fields[1]);
						queryStart = false;
					}
					queryAlignInfo.alignSequence += fields[2];
					queryAlignInfo.endPos = Convert.ToInt32 (fields[3]);
				}
				if (line.Length > "Sbjct:".Length && line.Substring (0, "Sbjct:".Length) == "Sbjct:")
				{
					string[] fields = ParseHelper.SplitPlus (line, ' ');
					if (hitStart)
					{
						hitAlignInfo.startPos = Convert.ToInt32 (fields[1]);
						hitStart = false;
					}
					hitAlignInfo.alignSequence += fields[2];
					hitAlignInfo.endPos = Convert.ToInt32 (fields[3]);
				}
			}
			dataReader.Close ();
			BlastAlignInfo[] alignInfos = new BlastAlignInfo [2];
			alignInfos[0] = queryAlignInfo;
			alignInfos[1] = hitAlignInfo;
			return alignInfos;
		}
		/// <summary>
		/// 
		/// </summary>
		/// <param name="seqFile"></param>
		/// <param name="sequence"></param>
		private void WriteSequenceToFile (string seqFile, string sequence)
		{
			StreamWriter dataWriter = new StreamWriter (seqFile);
			dataWriter.WriteLine (sequence);
			dataWriter.Flush ();
		    dataWriter.Close ();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="seq1File"></param>
		/// <param name="seq2File"></param>
		/// <returns></returns>
		private string RunBlast2Sequence (string seq1File, string seq2File)
		{
			CmdLauncher cmdLauncher = new CmdLauncher ();
			cmdLauncher.AddCmdParameters ("p", "blastp");
			cmdLauncher.AddCmdParameters ("i", seq1File);
			cmdLauncher.AddCmdParameters ("j", seq2File);
		//	cmdLauncher.AddCmdParameters ("g", "F"); // no gaps
		//	cmdLauncher.AddCmdParameters ("G", "100");
			string outputFile = "seqAlign.txt";
			cmdLauncher.AddCmdParameters ("o", outputFile);
			string toolName = Path.Combine (ProtCidSettings.applicationStartPath, "tools\\bl2seq");
			cmdLauncher.LaunchCmd (toolName);
			return outputFile;
		}
		#endregion
	}
}
