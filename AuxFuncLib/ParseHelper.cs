using System;
using System.Collections.Generic;
using System.Collections;
using System.Data;
using System.Data.Odbc;
using System.Threading;
using System.Diagnostics;
using System.IO;

namespace AuxFuncLib
{	
	/// <summary>
	/// static helper functions used by the namespace
	/// </summary>
	public class ParseHelper
	{
		public ParseHelper()
		{
		}

        public static string chainLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        public static string exeDir = @"tools";

		#region string split
		/// <summary>
		/// can remove multiple occurences of certain delimiter character within the input string
		/// </summary>
		/// <param name="origStr"></param>
		/// <param name="delimiter"></param>
		/// <returns></returns>
		public static string [] SplitPlus(string wholeString, char delimiter)
		{
			List<string> stringArray = new List<string> ();
			string cellString = "";
            if (wholeString.Length > 0)
            {
                char preChar = wholeString[0];
                foreach (char ch in wholeString)
                {
                    if (ch != delimiter)
                    {
                        cellString += ch.ToString();
                    }
                    else if (preChar != ch)
                    {
                        // in case there are space at the beginning and end of the string
                        // and delimiter is not space
                        cellString.Trim();
                        stringArray.Add(cellString);
                        cellString = "";
                    }
                    preChar = ch;
                }
                // add the last item
                cellString.Trim();
                if (cellString != "")
                {
                    stringArray.Add(cellString);
                }
            }
            return stringArray.ToArray ();
		}
		#endregion

		#region unzip file
		/// <summary>
		/// unzip a PDB file
		/// </summary>
		/// <param name="fileName"></param>
		/// <returns></returns>
		public static string UnZipFile(string fileName, string tempDir)
		{	
			if (! File.Exists (fileName))
			{
				throw new IOException (string.Format ("File {0} not exist", fileName));
			}
			if (! Directory.Exists (tempDir))
			{
				Directory.CreateDirectory (tempDir);
			}
			int gzIndex = fileName.LastIndexOf (".");
			string extensionFileName = fileName.Substring (gzIndex + 1, fileName.Length - gzIndex - 1);

			try
			{
				ProcessStartInfo processInfo = new ProcessStartInfo();
				Process unzipProcess = null;

				// copy the file to the temporary directory
				// extract just the file name
				int indexOfJustFileName = fileName.LastIndexOf( "\\" );
				string justFileName = fileName.Substring(indexOfJustFileName + 1, fileName.Length - indexOfJustFileName - 1);
				string destFileNameAndPath = tempDir + "\\" + justFileName;
                if (! File.Exists(destFileNameAndPath))
                {
                    File.Copy(fileName, destFileNameAndPath, true);
                }
                if (extensionFileName == "gz")
                {
                    fileName = destFileNameAndPath.Substring(0, destFileNameAndPath.Length - extensionFileName.Length - 1);
                    if (!File.Exists(fileName))
                    {
                        // set properties for the process
                        string commandParam = "-d " + "\"" + destFileNameAndPath + "\"";
                        processInfo.CreateNoWindow = true;
                        processInfo.UseShellExecute = false;
                        processInfo.FileName = "tools\\minigzip.exe";
                        processInfo.Arguments = commandParam;
                        unzipProcess = Process.Start(processInfo);
                        unzipProcess.WaitForExit();
                    }
                }
                else
                {
                    fileName = destFileNameAndPath;
                }
			}
			catch (Exception e)
			{
				throw e;
			}
			return fileName;
		}

		/// <summary>
		/// unzip a PDB file
		/// </summary>
		/// <param name="fileName"></param>
		/// <returns></returns>
		public static string UnZipFile(string fileName)
		{
			if (! File.Exists (fileName))
			{
				throw new IOException (string.Format ("File {0} not exist", fileName));
			}
			int gzIndex = fileName.LastIndexOf (".");
			string extensionFileName = fileName.Substring (gzIndex + 1, fileName.Length - gzIndex - 1);

			try
			{
				ProcessStartInfo processInfo = new ProcessStartInfo();
				Process unzipProcess = null;

				if (extensionFileName == "gz")
				{
					// set properties for the process
					string commandParam = "-d " + "\"" + fileName + "\"";
					processInfo.CreateNoWindow = true;
					processInfo.UseShellExecute = false;
					processInfo.FileName = "tools\\minigzip.exe";
					processInfo.Arguments = commandParam;
					unzipProcess = Process.Start( processInfo );
					unzipProcess.WaitForExit ();
					fileName = fileName.Substring (0, gzIndex);
				}
			}
			catch (Exception e)
			{
				throw e;
			}
			return fileName;
		}
		#endregion

		#region zip file
		/// <summary>
		/// zip an Pdb file
		/// extension name is .gz
		/// </summary>
		/// <param name="fileName"></param>
		/// <returns></returns>
		public static void ZipPdbFile(string fileName)
		{		
			try
			{
				ProcessStartInfo processInfo = new ProcessStartInfo();
				Process zipProcess = null;	
				if (File.Exists (fileName))
				{
					if (File.Exists (fileName + ".gz"))
					{
						File.Delete (fileName + ".gz");
					}
					// set properties for the process
					string commandParam = "\"" + fileName + "\"";
					processInfo.CreateNoWindow = true;
					processInfo.UseShellExecute = false;
					processInfo.FileName =  Path.Combine (exeDir,  "minigzip.exe");
				//	processInfo.Arguments = commandParam;
                    processInfo.Arguments = fileName;
					zipProcess = Process.Start( processInfo );
					zipProcess.WaitForExit ();
				}
			}
			catch (Exception e)
			{
				throw e;
			}
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="srcFile"></param>
        /// <param name="gzFile"></param>
        public static void ZipFile(string srcFile, string gzFile)
        {
            try
            {
                ProcessStartInfo processInfo = new ProcessStartInfo();
                Process zipProcess = null;
                if (File.Exists(srcFile))
                {
                    if (File.Exists(gzFile))
                    {
                        File.Delete(gzFile);
                    }
                    // set properties for the process
                    string commandParam = " < " + srcFile + " > " + gzFile;
                    processInfo.CreateNoWindow = true;
                    processInfo.UseShellExecute = false ;
                //    processInfo.FileName = "CMD.exe";
                    processInfo.FileName = Path.Combine(exeDir, "gzip.exe");
                    processInfo.Arguments = commandParam;
                    zipProcess = Process.Start(processInfo); //gzip < file > file.gz
                    zipProcess.WaitForExit();
                }
            }
            catch (Exception e)
            {
                throw e;
            }
        }
		#endregion

		#region Parse PDB format 
        #region ATOM line
        /// <summary>
		/// parse PDB Fomated ATOM line
		/// </summary>
		/// <param name="line"></param>
		/// <returns></returns>
		public static string[] ParsePdbAtomLine (string line)
		{
			// the input string must have coordinates
			if (line.Length < 54)
			{
				throw new Exception ("Input string must have coordinates data");
			}
			List<string> fields = new List<string> ();
			// ATOM
			fields.Add (line.Substring (0, 6).Trim ());  // 0
			// Atom ID
			fields.Add (line.Substring (6, 5).Trim ());  // 1
			// Atom Name
			fields.Add (line.Substring (12, 4).Trim ());  // 2
			// Alt loc, can be a space
			fields.Add (line.Substring (16, 1));   // 3
			// residue name 
			fields.Add (line.Substring (17, 3));   // 4
			// chainID
			fields.Add (line.Substring (21, 1));  // 5
			// residue sequence id
			fields.Add (line.Substring (22, 4).Trim ());  // 6
			// insert code, can be space
			fields.Add (line.Substring (26,1));   // 7
			// x
			fields.Add (line.Substring (30, 8).Trim ());  // 8
			// Y
			fields.Add (line.Substring (38, 8).Trim ());  // 9
			// Z
			fields.Add (line.Substring (46, 8).Trim ());  // 10
			// occupancy
			if (line.Length >= 60)
			{
				fields.Add (line.Substring (54, 6).Trim ());   // 11
			}
			// tempFact
			if (line.Length >= 66)
			{
				fields.Add (line.Substring (60, 6).Trim ());  // 12
			}
			// segID
			if (line.Length >= 76)
			{
				fields.Add (line.Substring (72, 4).Trim ()); // 13
			}
			// element
			if (line.Length >= 78)  
			{
				fields.Add (line.Substring (76, 2).Trim ());  // 14
			}
			// charge
			if (line.Length >= 80)
			{
				fields.Add (line.Substring (78, 2).Trim ());   // 15
			}

			string[] strFields = new string [fields.Count];
			fields.CopyTo (strFields);
			return strFields;

        }

        /// <summary>
        /// parse PDB Fomated TER line
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        public static string[] ParsePdbTerLine(string line)
        {
            List<string> fields = new List<string> ();
            // TER
            fields.Add(line.Substring(0, 6).Trim());  // 0
            // Atom ID
            fields.Add(line.Substring(6, 5).Trim());  // 1
            // Atom Name
            fields.Add(line.Substring(12, 4).Trim());  // 2
            // Alt loc, can be a space
            fields.Add(line.Substring(16, 1));   // 3
            // residue name 
            fields.Add(line.Substring(17, 3));   // 4
            // chainID
            fields.Add(line.Substring(21, 1));  // 5
            // residue sequence id
            fields.Add(line.Substring(22, 4).Trim());  // 6

            string[] strFields = new string[fields.Count];
            fields.CopyTo(strFields);
            return strFields;
        }
        #endregion

        #region residue seq record
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainId"></param>
        /// <param name="residues"></param>
        /// <returns></returns>
        public static string FormatChainSeqResRecords(string chainId, string[] residues)
        {
            int lineCount = 1;
            string[] residuesLine = null;
            string seqresLine = "";
            string seqresRecords = "";
            for (int i = 0; i < residues.Length; i += 13)
            {
                if (i + 13 < residues.Length)
                {
                    residuesLine = new string[13];
                    Array.Copy(residues, i, residuesLine, 0, 13);
                }
                else
                {
                    residuesLine = new string[residues.Length - i];
                    Array.Copy(residues, i, residuesLine, 0, residues.Length - i);
                }
                seqresLine = GetResideStringInOneSeqResLine(residuesLine);
                seqresLine = "SEQRES" + lineCount.ToString().PadLeft(4, ' ') + " " + chainId +
                    residues.Length.ToString().PadLeft(5, ' ') + "  " + seqresLine;
             //   seqresRecords += (seqresLine + "\r\n");
                seqresRecords += (seqresLine + "\r\n");
                lineCount++;
            }
            return seqresRecords.TrimEnd ("\r\n".ToCharArray ());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="residues"></param>
        /// <returns></returns>
        private static string GetResideStringInOneSeqResLine(string[] residues)
        {
            string residueString = "";
            foreach (string residue in residues)
            {
                residueString += (residue + " ");
            }
            return residueString.TrimEnd(' ');
        }
        #endregion

        #endregion

        #region Format SQL string for a list of strings

        /// <summary>
		/// format a list of PDB IDs to a string
		/// which can be used in SQL IN statement
		/// For example Select ... From tableName Where PdbID IN (PdbID list)
		/// </summary>
		/// <param name="itemList"></param>
		/// <returns></returns>
		public static string FormatSqlListString (ArrayList itemList)
		{
			string listString = "";
			foreach (object item in itemList)
			{
				listString += "'";
				listString += item.ToString ();
				listString += "', ";
			}
			return listString.TrimEnd (", ".ToCharArray ());
		}

        /// <summary>
        /// format a list of PDB IDs to a string
        /// which can be used in SQL IN statement
        /// For example Select ... From tableName Where PdbID IN (PdbID list)
        /// </summary>
        /// <param name="itemList"></param>
        /// <returns></returns>
        public static string FormatSqlListString<T> (List<T> itemList)
        {
            string listString = "";
            foreach (T item in itemList)
            {
                listString += "'";
                listString += item.ToString();
                listString += "', ";
            }
            return listString.TrimEnd(", ".ToCharArray());
        }

		/// <summary>
		/// format a list of PDB IDs to a string
		/// which can be used in SQL IN statement
		/// For example Select ... From tableName Where PdbID IN (PdbID list)
		/// </summary>
		/// <param name="itemList"></param>
		/// <returns></returns>
		public static string FormatSqlListString (string[] itemList)
		{
			string listString = "";
			foreach (string item in itemList)
			{
				listString += "'";
				listString += item;
				listString += "', ";
			}
			return listString.TrimEnd (", ".ToCharArray ());
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="itemList"></param>
        /// <returns></returns>
        public static string FormatSqlListString(int[] itemList)
        {
            string listString = "";
            foreach (int item in itemList)
            {
                listString += item;
                listString += ", ";
            }
            return listString.TrimEnd(", ".ToCharArray());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="itemList"></param>
        /// <returns></returns>
        public static string FormatSqlListString(long[] itemList)
        {
            string listString = "";
            foreach (int item in itemList)
            {
                listString += item;
                listString += ", ";
            }
            return listString.TrimEnd(", ".ToCharArray());
        }
		#endregion

		#region Convert seq string to number
		/// <summary>
		/// convert sequence number into integer number
		/// </summary>
		/// <param name="seqIdString"></param>
		/// <returns></returns>
		public static int ConvertSeqToInt (string seqIdString)
		{
			string digitString = "";
			foreach (char ch in seqIdString)
			{
				if (char.IsDigit (ch))
				{
					digitString += ch.ToString ();
				}
			}
			if (digitString == "")
			{
				return -1;
			}
			return Convert.ToInt32 (digitString);
		}

        /// <summary>
        /// always starts one
        /// </summary>
        /// <param name="pdbSeqNumbers"></param>
        /// <returns></returns>
        public static int[] ConvertPdbSeqToXmlSeq(string[] pdbSeqNumbers)
        {
            int[] xmlSeqNumbers = new int[pdbSeqNumbers.Length];
            int count = 0;
            int seqId = 1;
            foreach (string pdbSeqNum in pdbSeqNumbers)
            {
                xmlSeqNumbers[count] = seqId;
                count++;
                seqId++;
            }
            return xmlSeqNumbers;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbSeqNumbers"></param>
        /// <param name="pdbSeqId"></param>
        /// <returns></returns>
        public static int GetXmlSeqIdFromPdbSeqNumbers(string[] pdbSeqNumbers, string pdbSeqId)
        {
            int xmlSeqId = 1;
            foreach (string pdbSeqNum in pdbSeqNumbers)
            {
                if (pdbSeqNum == pdbSeqId)
                {
                    return xmlSeqId;
                }
                xmlSeqId ++;
            }
            return -1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbSeqNumbers"></param>
        /// <param name="pdbSeqIds"></param>
        /// <returns></returns>
        public static int[] GetXmlSeqIdsFromPdbSeqNumbers(string[] pdbSeqNumbers, string[] pdbSeqIds)
        {
            int[] xmlSeqIds = new int[pdbSeqIds.Length];
            int count = 0;
            foreach (string pdbSeqId in pdbSeqIds)
            {
                int xmlSeqId = GetXmlSeqIdFromPdbSeqNumbers(pdbSeqNumbers, pdbSeqId);
                xmlSeqIds[count] = xmlSeqId;
                count++;
            }
            return xmlSeqIds;
        }
		#endregion

		#region entry list from file
		public static string[] GetEntryList (string lsFile)
		{
			List<string> entryList = new List<string> ();
			if (File.Exists (lsFile))
			{
				StreamReader dataReader = new StreamReader (lsFile);
				string line = "";
				while ((line = dataReader.ReadLine ()) != null)
				{
					entryList.Add (line.Substring (0, 4));
				}
				dataReader.Close ();
			}
			string[] entries = new string [entryList.Count];
			entryList.CopyTo (entries);
			return entries;
		}
		#endregion

        #region convert three-letter residue to one-letter residue
        const string three2OneTable = "ALA -A CYS -C ASP -D GLU -E PHE -F GLY -G " +
            "HIS -H ILE -I LYS -K LEU -L MET -M ASN -N " +
            "PRO -P GLN -Q ARG -R SER -S THR -T VAL -V " +
            "TRP -W TYR -Y ASX -N GLX -Q UNK -X INI -K " +
            "AAR -R ACE -X ACY -G AEI -T AGM -R ASQ -D " +
            "AYA -A BHD -D CAS -C CAY -C CEA -C CGU -E " +
            "CME -C CMT -C CSB -C CSD -C CSE -C CSO -C " +
            "CSP -C CSS -C CSW -C CSX -C CXM -M CYG -C " +
            "CYM -C DOH -D EHP -F FME -M FTR -W GL3 -G " +
            "H2P -H HIC -H HIP -H HTR -W HYP -P KCX -K " +
            "LLP -K LLY -K LYZ -K M3L -K MEN -N MGN -Q " +
            "MHO -M MHS -H MIS -S MLY -K MLZ -K MSE -M " +
            "NEP -H NPH -C OCS -C OCY -C OMT -M OPR -R " +
            "PAQ -Y PCA -Q PHD -D PRS -P PTH -Y PYX -C " +
            "SEP -S SMC -C SME -M SNC -C SNN -D SVA -S " +
            "TPO -T TPQ -Y TRF -W TRN -W TRO -W TYI -Y " +
            "TYN -Y TYQ -Y TYS -Y TYY -Y YOF -Y FOR -X";

        /// <summary>
        /// convert three letter aa code to one letter code
        /// </summary>
        /// <param name="threeLetters"></param>
        /// <returns></returns>
        public static string threeToOne(string threeLetters)
        {
            int threeIndex = three2OneTable.IndexOf(threeLetters);
            if (threeIndex == -1) // not found
            {
                return "X";
            }
            else
            {
                return three2OneTable.Substring(threeIndex + 5, 1);
            }
        }
        #endregion

        #region valid sequence
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sequence"></param>
        /// <returns></returns>
        public static bool IsSequenceValid(string sequence)
        {
            List<char> distinctCharList = new List<char> ();
            foreach (char ch in sequence)
            {
                if (!distinctCharList.Contains(ch))
                {
                    distinctCharList.Add(ch);
                }
            }
            if (distinctCharList.Count == 1)
            {
                return false;
            }
            return true;
        }
        #endregion

        #region file name handler
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static string GetFileRootName(string fileName)
        {
            int fileNameIndex = fileName.LastIndexOf("\\");
            int exeNameIndex = fileName.IndexOf(".");
            string rootName = "";
            if (exeNameIndex > 0)
            {
                rootName = fileName.Substring(fileNameIndex + 1, exeNameIndex - fileNameIndex - 1);
            }
            else
            {
                rootName = fileName.Substring(fileNameIndex + 1, fileName.Length - fileNameIndex - 1);
            }
            return rootName;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataRow"></param>
        /// <returns></returns>
        public static string FormatDataRow(DataRow dataRow)
        {
            string dataRowString = "";
            foreach (object item in dataRow.ItemArray)
            {
                dataRowString += (item.ToString() + "\t");
            }
            return dataRowString.TrimEnd('\t');
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataRows"></param>
        /// <returns></returns>
        public static string FormatDataRows(DataRow[] dataRows)
        {
            string dataRowsString = "";
            string dataRowString = "";
            foreach (DataRow dataRow in dataRows)
            {
                dataRowString = FormatDataRow(dataRow);
                dataRowsString += (dataRowString + "\r\n");
            }
            dataRowsString = dataRowsString.TrimEnd("\r\n".ToCharArray ());
            return dataRowsString;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataTable"></param>
        /// <returns></returns>
        public static string FormatTableColumns(DataTable dataTable)
        {
            string headerLine = "";
            foreach (DataColumn dCol in dataTable.Columns)
            {
                headerLine += (dCol.ColumnName + "\t");
            }
            return headerLine.TrimEnd('\t');
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileDir"></param>
        /// <param name="destDir"></param>
        /// <param name="dtCutoff"></param>
        public static void CopyNewFiles(string fileDir, string destDir, DateTime dtCutoff)
        {
            string[] subDirectories = Directory.GetDirectories(fileDir);
            string dirName = "";
            string newSubDir = "";
            foreach (string subDir in subDirectories)
            {
                DateTime lastWriteTime = Directory.GetLastWriteTime(subDir);
                if (DateTime.Compare(lastWriteTime, dtCutoff) >= 0)
                {
                    dirName = GetDirectoryName(subDir);
                    newSubDir = Path.Combine(destDir, dirName);
                    if (!Directory.Exists(newSubDir))
                    {
                        Directory.CreateDirectory(newSubDir);
                    }
                    string[] files = Directory.GetFiles(subDir);
                    foreach (string file in files)
                    {
                        FileInfo fileInfo = new FileInfo(file);
                        if (DateTime.Compare(fileInfo.LastWriteTime, dtCutoff) >= 0)
                        {
                            File.Copy(file, Path.Combine (newSubDir, fileInfo.Name), true);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileDir"></param>
        /// <returns></returns>
        private static string GetDirectoryName (string fileDir)
        {
            int lastIndex = fileDir.LastIndexOf("\\");
            string dirName = fileDir.Substring(lastIndex + 1, fileDir.Length - lastIndex - 1);
            return dirName;
        }
        #endregion

        #region add new table to exist table
        /// <summary>
        ///  tables must have exactly same data structure
        /// </summary>
        /// <param name="newTable"></param>
        /// <param name="existTable"></param>
        public static void AddNewTableToExistTable(DataTable newTable, ref DataTable existTable)
        {
            if (existTable == null)
            {
                existTable = newTable.Copy();
            }
            else
            {
                foreach (DataRow newRow in newTable.Rows)
                {
                    DataRow dataRow = existTable.NewRow();
                    foreach (DataColumn dCol in existTable.Columns)
                    {
                        dataRow[dCol.ColumnName] = newRow[dCol.ColumnName];
                    }
                    existTable.Rows.Add(dataRow);
                }
            }
            existTable.AcceptChanges();
        }
        #endregion

        #region format an array list to a string
        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataList"></param>
        /// <returns></returns>
        public static string FormatArrayString<T>(List<T> dataList, char sep)
        {
            string arrayString = "";
            foreach (T item in dataList)
            {
                arrayString += (item.ToString() + sep);
            }
            arrayString = arrayString.TrimEnd(sep);
            return arrayString;
        }

        /// <summary>
        /// /
        /// </summary>
        /// <param name="itemList"></param>
        /// <returns></returns>
        public static string FormatArrayString<T>(T[] itemList, char sep)
        {
            string arrayString = "";
            if (itemList.Length == 0)
            {
                return "-";
            }
            foreach (T item in itemList)
            {
                arrayString += (item.ToString() + sep);
            }
            arrayString = arrayString.TrimEnd(sep);
            return arrayString;
        } 

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fields"></param>
        public static string FormatStringFieldsToString (string[] fields)
        {
            string arrayString = "";
            foreach (string field in fields)
            {
                arrayString += (field + ",");
            }
            arrayString = arrayString.TrimEnd(',');
            return arrayString;
        }
        #endregion

        #region move files from src to dest
        /// <summary>
        /// 
        /// </summary>
        /// <param name="srcFolder"></param>
        /// <param name="destFolder"></param>
        public static void MoveFiles(string srcFolder, string destFolder)
        {
            string[] dataFiles = Directory.GetFiles(srcFolder);
            string destFile = "";
            foreach (string dataFile in dataFiles)
            {
                FileInfo fileInfo = new FileInfo(dataFile);
                destFile = Path.Combine(destFolder, fileInfo.Name);
                if (File.Exists(destFile))
                {
                    File.Delete(destFile);
                }
                File.Move(dataFile, destFile);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileNames"></param>
        /// <param name="srcFolder"></param>
        /// <param name="destFolder"></param>
        public static void MoveFiles(string[] fileNames, string srcFolder, string destFolder)
        {
            string srcFile = "";
            string destFile = "";
            foreach (string fileName in fileNames)
            {
                srcFile = Path.Combine(srcFolder, fileName);
                destFile = Path.Combine(destFolder, fileName);
                if (File.Exists(destFile))
                {
                    File.Delete(destFile);
                }
                File.Move(srcFile, destFile);
            }
        }
        #endregion

        #region abbreviation crystalization method
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sgAsuString"></param>
        /// <returns></returns>
        public static string GetNonXrayAbbrevMethod(string sgAsuString)
        {
            string[] sgAsuFields = sgAsuString.Split('_');
            if (sgAsuFields[0] == "NMR")
            {
                return "NMR";
            }
            string[] sgMethodFields = sgAsuFields[0].Split('(');
            string abbrevMethod = "-";
            if (sgMethodFields.Length == 2)
            {
                abbrevMethod = sgMethodFields[1].TrimEnd(')');
            }
            return abbrevMethod;
        }
        #endregion

        #region sub array, list
        public static string[] GetSubArray (string[] srcArray, int startIndex, int length)
        {
            string[] subArray = null;
            if (startIndex + length < srcArray .Length)
            {
                subArray = new string[length];
                Array.Copy(srcArray, startIndex, subArray, 0, length);
            }
            else
            {
                subArray = new string[srcArray.Length - startIndex];
                Array.Copy(srcArray, startIndex, subArray, 0, subArray.Length);
            }
            return subArray;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="srcArray"></param>
        /// <param name="startIndex"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static int[] GetSubArray(int[] srcArray, int startIndex, int length)
        {
            int[] subArray = null;
            if (startIndex + length < srcArray.Length)
            {
                subArray = new int[length];
                Array.Copy(srcArray, startIndex, subArray, 0, length);
            }
            else
            {
                subArray = new int[srcArray.Length - startIndex];
                Array.Copy(srcArray, startIndex, subArray, 0, subArray.Length);
            }
            return subArray;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="srcList"></param>
        /// <param name="startIndex"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static ArrayList GetSubList (ArrayList srcList, int startIndex, int length)
        {
            ArrayList subList = null;
            if (startIndex + length < srcList.Count)
            {
                subList = srcList.GetRange(startIndex, length);
            }
            else
            {
                subList = srcList.GetRange(startIndex, srcList.Count - startIndex);
            }
            return subList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="srcList"></param>
        /// <param name="startIndex"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static List<T> GetSubList<T> (List<T> srcList, int startIndex, int length)
        {
            List<T> subList = new List<T> ();
            int endIndex = startIndex + length;
            if (endIndex > srcList.Count)
            {
                endIndex = srcList.Count;
            }
            for (int i = startIndex; i < endIndex; i ++ )
            {
                subList.Add(srcList[i]);
            }               
            return subList;
        } 
        #endregion
    }
}
