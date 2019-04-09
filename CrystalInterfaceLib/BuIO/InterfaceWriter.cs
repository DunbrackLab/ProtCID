using System;
using System.IO;
using System.Collections.Generic;
using CrystalInterfaceLib.Crystal;
using ProtCidSettingsLib;

namespace CrystalInterfaceLib.BuIO
{
	/// <summary>
	/// Summary description for InterfacewWriter.
	/// </summary>
	public class InterfaceWriter : BuWriter
	{
		public InterfaceWriter()
		{
		}

		/// <summary>
		/// write an interface to a pdb formatted text file
		/// </summary>
		/// <param name="fileName"></param>
		/// <param name="chain1"></param>
		/// <param name="chain2"></param>
		/// <param name="remark"></param>
		/// <param name="type"></param>
		/// <returns></returns>
		public string WriteInterfaceToFile (string fileName, string remark, AtomInfo[] chain1, AtomInfo[] chain2)
		{
			StreamWriter fileWriter = new StreamWriter (fileName);
			fileWriter.WriteLine (remark);
			int atomId = 0;
			WriteOneChain (chain1, "A", ref atomId, fileWriter);
			WriteOneChain (chain2, "B", ref atomId, fileWriter);
			fileWriter.WriteLine ("END");
			fileWriter.Close ();
			return fileName;
		}

		/// <summary>
		/// writer an interface to a pdb formatted file
		/// </summary>
		/// <param name="pdbId"></param>
		/// <param name="biolUnitId"></param>
		/// <param name="interfaceId"></param>
		/// <param name="chain1"></param>
		/// <param name="chain2"></param>
		/// <param name="remark"></param>
		/// <returns></returns>
		public string WriteInterfaceToFile (string pdbId, string biolUnitId, int interfaceId, AtomInfo[] chain1, AtomInfo[] chain2, string remark, string type)
		{
			string destFolder = GetHashFolderPath (pdbId, type);
			string fileName = Path.Combine (destFolder, 
				pdbId + biolUnitId + "_" + interfaceId.ToString () + "." + type);
			StreamWriter fileWriter = new StreamWriter (fileName);
			string header = "HEADER    Entry: " + pdbId + "    BiolUnitID: " + 
				biolUnitId + "   InterfaceID: " + interfaceId.ToString () + 
				"   " + DateTime.Today.ToShortDateString ();
			fileWriter.WriteLine (header);
			fileWriter.WriteLine (remark);
			int atomId = 0;
			WriteOneChain (chain1, "A", ref atomId, fileWriter);
			WriteOneChain (chain2, "B", ref atomId, fileWriter);
			fileWriter.WriteLine ("END");
			fileWriter.Close ();
			return fileName;
		}
		/// <summary>
		/// write an interface computed from a crystal to pdb formatted file
		/// </summary>
		/// <param name="pdbId">pdb code</param>
		/// <param name="interfaceId">Interface ID</param>
		/// <param name="chain1"></param>
		/// <param name="chain2"></param>
		/// <param name="remark"></param>
		/// <param name="type"></param>
		/// <returns></returns>
		public string WriteInterfaceToFile (string pdbId, int interfaceId, AtomInfo[] chain1, AtomInfo[] chain2, string remark, string type)
		{
			string destFolder = GetHashFolderPath (pdbId, type);
			string fileName = Path.Combine (destFolder, pdbId + "_" + interfaceId.ToString () + "." + type);
			StreamWriter fileWriter = new StreamWriter (fileName);
			string header = "HEADER    Entry: " + pdbId + "  InterfaceID: " + interfaceId.ToString () + 
				"   " + DateTime.Today.ToShortDateString ();
			fileWriter.WriteLine (header);
			fileWriter.WriteLine (remark);
			int atomId = 0;
			WriteOneChain (chain1, "A", ref atomId, fileWriter);
			WriteOneChain (chain2, "B", ref atomId, fileWriter);
			fileWriter.WriteLine ("END");
			fileWriter.Close ();
			return fileName;
		}

        /// <summary>
        /// write an interface computed from a crystal to pdb formatted file
        /// </summary>
        /// <param name="pdbId">pdb code</param>
        /// <param name="interfaceId">Interface ID</param>
        /// <param name="chain1"></param>
        /// <param name="chain2"></param>
        /// <param name="remark"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public string WriteInterfaceToFile(string pdbId, int interfaceId, string interfaceFile, AtomInfo[] chain1, AtomInfo[] chain2, string remark, string type)
        {
            StreamWriter fileWriter = new StreamWriter(interfaceFile);
            string header = "HEADER    Entry: " + pdbId + "  InterfaceID: " + interfaceId.ToString() +
                "   " + DateTime.Today.ToShortDateString();
            fileWriter.WriteLine(header);
            fileWriter.WriteLine(remark);
            int atomId = 0;
            WriteOneChain(chain1, "A", ref atomId, fileWriter);
            WriteOneChain(chain2, "B", ref atomId, fileWriter);
            fileWriter.WriteLine("END");
            fileWriter.Close();
            return interfaceFile;
        }

        /// <summary>
        /// write an interface computed from a crystal to pdb formatted file
        /// </summary>
        /// <param name="pdbId">pdb code</param>
        /// <param name="interfaceId">Interface ID</param>
        /// <param name="chain1"></param>
        /// <param name="chain2"></param>
        /// <param name="remark"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public void WriteInterfaceToFile(string interfaceFileName, AtomInfo[] chain1, AtomInfo[] chain2, string remark, string type, Dictionary<string, AtomInfo[]>[] ligandAtomInfoHashes)
        {
            StreamWriter fileWriter = new StreamWriter(interfaceFileName);
            fileWriter.WriteLine(remark);
            int atomId = 0;
            WriteOneChain(chain1, "A", ref atomId, fileWriter);
            WriteOneChain(chain2, "B", ref atomId, fileWriter);
            foreach (string ligandAsymId in ligandAtomInfoHashes[0].Keys)
            {
                WriteOneChain(ligandAtomInfoHashes[0][ligandAsymId], "A", ref atomId, true, fileWriter);
            }
            if (ligandAtomInfoHashes.Length == 2)
            {
                foreach (string ligandAsymId in ligandAtomInfoHashes[1].Keys)
                {
                    WriteOneChain(ligandAtomInfoHashes[1][ligandAsymId], "B", ref atomId, true, fileWriter);
                }
            }
            fileWriter.WriteLine("END");
            fileWriter.Close();
        }      

        /// <summary>
        /// write an interface to a pdb formatted text file
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="chain1"></param>
        /// <param name="chain2"></param>
        /// <param name="remark"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public string WriteInterfaceToFile(string fileName, string remark, AtomInfo[] chain1, AtomInfo[] chain2, string heteroInfo)
        {
            StreamWriter fileWriter = new StreamWriter(fileName);
            fileWriter.WriteLine(remark);
            int atomId = 0;
            WriteOneChain(chain1, "A", ref atomId, fileWriter);
            WriteOneChain(chain2, "B", ref atomId, fileWriter);
            if (heteroInfo != "")
            {
                fileWriter.WriteLine(heteroInfo);
            }
            fileWriter.WriteLine("END");
            fileWriter.Close();
            return fileName;
        }

		private string GetHashFolderPath (string pdbId, string type)
		{
			string hashFolder = pdbId.Substring (1, 2);
			string dirName = ProtCidSettings.dirSettings.interfaceFilePath + "\\" + type + "\\" + hashFolder;
			if (! Directory.Exists (dirName))
			{
				Directory.CreateDirectory (dirName);
			}
			return dirName;
		}

		#region write interface files to temporary  directory
		/// <summary>
		/// writer an interface to a pdb formatted file
		/// </summary>
		/// <param name="pdbId"></param>
		/// <param name="biolUnitId"></param>
		/// <param name="interfaceId"></param>
		/// <param name="chain1"></param>
		/// <param name="chain2"></param>
		/// <param name="remark"></param>
		/// <returns></returns>
		public string WriteTempInterfaceToFile (string pdbId, string biolUnitId, int interfaceId, AtomInfo[] chain1, AtomInfo[] chain2, string type)
		{
			string fileName = Path.Combine (ProtCidSettings.tempDir, 
				pdbId + biolUnitId + "_" + interfaceId.ToString () + "." + type);
			StreamWriter fileWriter = new StreamWriter (fileName);
			int atomId = 0;
			WriteOneChain (chain1, "A", ref atomId, fileWriter);
			WriteOneChain (chain2, "B", ref atomId, fileWriter);
			fileWriter.WriteLine ("END");
			fileWriter.Close ();
			return fileName;
		}

		/// <summary>
		/// writer an interface to a pdb formatted file
		/// </summary>
		/// <param name="pdbId"></param>
		/// <param name="biolUnitId"></param>
		/// <param name="interfaceId"></param>
		/// <param name="chain1"></param>
		/// <param name="chain2"></param>
		/// <param name="remark"></param>
		/// <returns></returns>
		public string WriteTempInterfaceToFile (string pdbId, int interfaceId, AtomInfo[] chain1, AtomInfo[] chain2, string type)
		{
			string fileName = Path.Combine (ProtCidSettings.tempDir, 
				pdbId + "_" + interfaceId.ToString () + "." + type);
			StreamWriter fileWriter = new StreamWriter (fileName);
			int atomId = 0;
			WriteOneChain (chain1, "A", ref atomId, fileWriter);
			WriteOneChain (chain2, "B", ref atomId, fileWriter);
			fileWriter.WriteLine ("END");
			fileWriter.Close ();
			return fileName;
		}
		/// <summary>
		/// writer one chain to one interface file
		/// </summary>
		/// <param name="pdbId"></param>
		/// <param name="biolUnitId"></param>
		/// <param name="interfaceId"></param>
		/// <param name="chain"></param>
		/// <param name="type"></param>
		/// <returns></returns>
		public string WriterOneInterfaceChainToFile (string pdbId, string biolUnitId, string chainStr, AtomInfo[] chain, string type)
		{
			string fileName = Path.Combine (ProtCidSettings.tempDir, pdbId + biolUnitId + "_" + chainStr + "." + type);
			StreamWriter fileWriter = new StreamWriter (fileName);
			int atomId = 0;
			WriteOneChain (chain, "A", ref atomId, fileWriter);
			fileWriter.Close ();
			return fileName;
		}

		/// <summary>
		/// writer one chain to one interface file
		/// </summary>
		/// <param name="pdbId"></param>
		/// <param name="biolUnitId"></param>
		/// <param name="interfaceId"></param>
		/// <param name="chain"></param>
		/// <param name="type"></param>
		/// <returns></returns>
		public string WriterOneInterfaceChainToFile (string pdbId, string chainStr, AtomInfo[] chain, string type)
		{
			string fileName = Path.Combine (ProtCidSettings.tempDir, pdbId + "_" + chainStr + "." + type);
			StreamWriter fileWriter = new StreamWriter (fileName);
			int atomId = 0;
			WriteOneChain (chain, "A", ref atomId, fileWriter);
			fileWriter.Close ();
			return fileName;
		}

        /// <summary>
        /// writer one chain to one interface file
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="biolUnitId"></param>
        /// <param name="interfaceId"></param>
        /// <param name="chain"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public void WriterOneInterfaceChainToFile(string coorFile, AtomInfo[] chain, string chainId)
        {
            StreamWriter fileWriter = new StreamWriter(coorFile);
            int atomId = 0;
            WriteOneChain(chain, chainId, ref atomId, fileWriter);
            fileWriter.Close();
        }
		#endregion
	}
}
