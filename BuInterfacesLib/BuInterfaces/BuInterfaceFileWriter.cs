using System;
using System.Data;
using System.IO;
using System.Collections;
using XtalLib.ProtInterfaces;
using XtalLib.Crystal;
using XtalLib.FileParser;
using XtalLib.Contacts;
using XtalLib.Settings;
using AuxFuncLib;
using DbLib;

namespace BuInterfacesLib.BuInterfaces
{
	/// <summary>
	/// Summary description for BuInterfaceFileWriter.
	/// </summary>
	public class BuInterfaceFileWriter : InterfaceFileWriter
	{
		private PdbBuGenerator pdbBuGen = new PdbBuGenerator ();
		private PqsBuFileParser pqsBuParser = new PqsBuFileParser ();

		public BuInterfaceFileWriter()
		{
		}

		#region retrieve interfaces data
		/// <summary>
		/// the interfaces and related info 
		/// for this pdb and bu
		/// </summary>
		/// <param name="interfaceTable"></param>
		/// <returns></returns>
		public ProtInterfaceInfo[] GetInterfaces (DataTable interfaceTable, string type)
		{
			string pdbId = interfaceTable.Rows[0]["PdbID"].ToString ();
			int buId = Convert.ToInt32 (interfaceTable.Rows[0]["BuID"].ToString ());
			ArrayList interfaceList = new ArrayList ();
			string symOpQueryString = "";
			string residueQueryString = "";
			string interfaceCompString = "";
			foreach (DataRow interfaceRow in interfaceTable.Rows)
			{
				interfaceList.Add (Convert.ToInt32 (interfaceRow["InterfaceID"].ToString ()));
			}
			if (type == "pdb")
			{
				symOpQueryString = string.Format ("SELECT * FROM PdbBuSameInterfaces " + 
					" WHERE PdbID = '{0}' AND BuID = {1};", pdbId, buId);
				
				residueQueryString = string.Format ("SELECT * FROM PdbInterfaceResidues " + 
					" WHERE PdbID = '{0}' AND BuID = {1};", pdbId, buId);

				interfaceCompString = string.Format ("SELECT * FROM PdbPqsBuInterfaceComp " + 
					" WHERE PdbID = '{0}' AND PdbBuID = '{1}' AND QScore > {2};", pdbId, buId, 
					AppSettings.parameters.simInteractParam.interfaceSimCutoff);
			}
			else if (type == "pqs")
			{
				symOpQueryString = string.Format ("SELECT * FROM PqsBuSameInterfaces " + 
					" WHERE PdbID = '{0}' AND BuID = {1};", pdbId, buId);
				
				residueQueryString = string.Format ("SELECT * FROM PqsInterfaceResidues " + 
					" WHERE PdbID = '{0}' AND BuID = {1};", pdbId, buId);

				interfaceCompString = string.Format ("SELECT * FROM PdbPqsBuInterfaceComp " + 
					" WHERE PdbID = '{0}' AND PqsBuID = '{1}' AND QScore > {2};", pdbId, buId, 
					AppSettings.parameters.simInteractParam.interfaceSimCutoff);
			}
			DataTable interfaceSymOpTable = dbQuery.Query (symOpQueryString);
			DataTable residueTable = dbQuery.Query (residueQueryString);
			DataTable interfaceCompTable = dbQuery.Query (interfaceCompString);

			ProtInterfaceInfo[] protInterfaceInfos = new ProtInterfaceInfo [interfaceList.Count];
			int i = 0;
			string existTypeString = "";
			DataRow[] interfaceCompRows = null;
			foreach (int interfaceId in interfaceList)
			{
				ProtInterfaceInfo protInterface = new ProtInterfaceInfo ();
				DataRow[] interfaceRows = interfaceTable.Select (string.Format ("InterfaceID = '{0}'", interfaceId));				
				DataRow[] symOpRows = interfaceSymOpTable.Select (string.Format ("InterfaceID = '{0}'", interfaceId));
				if (type == "pdb")
				{
					existTypeString = string.Format ("PdbInterfaceID = '{0}'", interfaceId);
				}
				else
				{
					existTypeString = string.Format ("PqsInterfaceID = '{0}'", interfaceId);
				}
				interfaceCompRows = interfaceCompTable.Select (existTypeString);
				if (interfaceCompRows.Length > 0)
				{
					protInterface.ExistType = type + "both";
				}
				else
				{
					protInterface.ExistType = type;
				}
				protInterface.InterfaceId = interfaceId;
				protInterface.ASA = Convert.ToDouble (interfaceRows[0]["SurfaceArea"].ToString ());
				protInterface.Remark = FormatRemark (interfaceRows[0], symOpRows[0], protInterface.ExistType);
				protInterface.Chain1 = symOpRows[0]["Chain1"].ToString ().Trim ();
				protInterface.Chain2 = symOpRows[0]["Chain2"].ToString ().Trim ();
				protInterface.SymmetryString1 = symOpRows[0]["SymmetryString1"].ToString ().Trim ();
				protInterface.SymmetryString2 = symOpRows[1]["SymmetryString2"].ToString ().Trim ();
				DataRow[] residueRows = residueTable.Select (string.Format ("InterfaceID = '{0}'", interfaceId));
				Hashtable[] residueSeqHash = GetResidueSeqs (residueRows);
				protInterface.ResiduesInChain1 = residueSeqHash[0];
				protInterface.ResiduesInChain2 = residueSeqHash[1];
				protInterface.Remark += protInterface.FormatResidues ("A");
				protInterface.Remark += protInterface.FormatResidues ("B");
				protInterfaceInfos[i] = protInterface;
				i ++;
			}
			return protInterfaceInfos;
		}

		/// <summary>
		/// the interfaces and related info 
		/// for this pdb and bu
		/// </summary>
		/// <param name="interfaceTable"></param>
		/// <returns></returns>
		public ProtInterfaceInfo[] GetInterfaces (DataRow[] interfaceRows, string type)
		{
			string pdbId = interfaceRows[0]["PdbID"].ToString ();
			string buId = interfaceRows[0]["BuID"].ToString ();
			Hashtable interfaceHash = new Hashtable ();
			string symOpQueryString = "";
			string residueQueryString = "";
			string interfaceCompString = "";
			foreach (DataRow interfaceRow in interfaceRows)
			{
				interfaceHash.Add (Convert.ToInt32 (interfaceRow["InterfaceID"].ToString ()), 
					interfaceRow);
			}
			if (type == "pdb")
			{
				symOpQueryString = string.Format ("SELECT * FROM PdbBuSameInterfaces " + 
					" WHERE PdbID = '{0}' AND BuID = '{1}';", pdbId, buId);
				
				residueQueryString = string.Format ("SELECT * FROM PdbInterfaceResidues " + 
					" WHERE PdbID = '{0}' AND BuID = '{1}';", pdbId, buId);

				interfaceCompString = string.Format ("SELECT * FROM PdbPqsBuInterfaceComp " + 
					" WHERE PdbID = '{0}' AND PdbBuID = '{1}' AND QScore > {2};", pdbId, buId, 
					AppSettings.parameters.simInteractParam.interfaceSimCutoff);
			}
			else if (type == "pqs")
			{
				symOpQueryString = string.Format ("SELECT * FROM PqsBuSameInterfaces " + 
					" WHERE PdbID = '{0}' AND BuID = '{1}';", pdbId, buId);
				
				residueQueryString = string.Format ("SELECT * FROM PqsInterfaceResidues " + 
					" WHERE PdbID = '{0}' AND BuID = '{1}';", pdbId, buId);

				interfaceCompString = string.Format ("SELECT * FROM PdbPqsBuInterfaceComp " + 
					" WHERE PdbID = '{0}' AND PqsBuID = '{1}' AND QScore > {2};", pdbId, buId, 
					AppSettings.parameters.simInteractParam.interfaceSimCutoff);
			}
			DataTable interfaceSymOpTable = dbQuery.Query (symOpQueryString);
			DataTable residueTable = dbQuery.Query (residueQueryString);
			DataTable interfaceCompTable = dbQuery.Query (interfaceCompString);

			ProtInterfaceInfo[] protInterfaceInfos = new ProtInterfaceInfo [interfaceHash.Count];
			int i = 0;
			string existTypeString = "";
			DataRow[] interfaceCompRows = null;
			foreach (int interfaceId in interfaceHash.Keys)
			{
				ProtInterfaceInfo protInterface = new ProtInterfaceInfo ();
				//	DataRow[] interfaceRows = interfaceTable.Select (string.Format ("InterfaceID = '{0}'", interfaceId));				
				DataRow[] symOpRows = interfaceSymOpTable.Select (string.Format ("InterfaceID = '{0}'", interfaceId));
				if (type == "pdb")
				{
					existTypeString = string.Format ("PdbInterfaceID = '{0}'", interfaceId);
				}
				else
				{
					existTypeString = string.Format ("PqsInterfaceID = '{0}'", interfaceId);
				}
				interfaceCompRows = interfaceCompTable.Select (existTypeString);
				if (interfaceCompRows.Length > 0)
				{
					protInterface.ExistType = type + "both";
				}
				else
				{
					protInterface.ExistType = type;
				}
				protInterface.InterfaceId = interfaceId;
				protInterface.Remark = FormatRemark ((DataRow)interfaceHash[interfaceId], 
					symOpRows[0], protInterface.ExistType);
				protInterface.ASA = Convert.ToDouble (((DataRow)interfaceHash[interfaceId])["SurfaceArea"].ToString ());
				protInterface.Chain1 = symOpRows[0]["Chain1"].ToString ().Trim ();
				protInterface.Chain2 = symOpRows[0]["Chain2"].ToString ().Trim ();
				protInterface.SymmetryString1 = symOpRows[0]["SymmetryString1"].ToString ().Trim ();
				protInterface.SymmetryString2 = symOpRows[0]["SymmetryString2"].ToString ().Trim ();
				DataRow[] residueRows = residueTable.Select (string.Format ("InterfaceID = '{0}'", interfaceId));
				Hashtable[] residueSeqHash = GetResidueSeqs (residueRows);
				protInterface.ResiduesInChain1 = residueSeqHash[0];
				protInterface.ResiduesInChain2 = residueSeqHash[1];
				protInterface.Remark += protInterface.FormatResidues ( "A");
				protInterface.Remark += protInterface.FormatResidues ("B");
				protInterfaceInfos[i] = protInterface;
				i ++;
			}
			return protInterfaceInfos;
		}
		/// <summary>
		/// 
		/// </summary>
		/// <param name="residueRows"></param>
		/// <returns></returns>
		private Hashtable[] GetResidueSeqs (DataRow[] residueRows)
		{
			Hashtable[] residueSeqHash = new Hashtable [2];
			residueSeqHash[0] = new Hashtable ();
			residueSeqHash[1] = new Hashtable ();
			foreach (DataRow dRow in residueRows)
			{
				if (! residueSeqHash[0].ContainsKey (dRow["SeqID1"].ToString ().Trim ()))
				{
					residueSeqHash[0].Add (dRow["SeqID1"].ToString ().Trim (), dRow["Residue1"].ToString ().Trim ());
				}
				if (! residueSeqHash[1].ContainsKey (dRow["SeqID2"].ToString ().Trim ()))
				{
					residueSeqHash[1].Add (dRow["SeqID2"].ToString ().Trim (), dRow["Residue2"].ToString ().Trim ());
				}
			}
			return residueSeqHash;
		}
		/// <summary>
		/// Remark fields in interface files
		/// asymmetric chains, author chains, entity ids and/or PQS chains
		/// symmetry operators, and
		/// surface area and number of copies in the biological unit
		/// </summary>
		/// <param name="interfaceRow">unique interface info</param>
		/// <param name="symOpRow">symmetry operators for the interface</param>
		/// <param name="type">pdb or pqs</param>
		/// <returns></returns>
		private string FormatRemark (DataRow interfaceRow, DataRow symOpRow, string type)
		{
			// chain A
			string symmetryString1 = "";
			string symmetryString2 = "";
			if (type.IndexOf ("pdb") > -1)
			{
				symmetryString1 = symOpRow["FullSymmetryString1"].ToString ().Trim ();
				symmetryString2 = symOpRow["FullSymmetryString2"].ToString ().Trim ();
			}
			else
			{
				symmetryString1 = symOpRow["SymmetryString1"].ToString ().Trim ();
				symmetryString2 = symOpRow["SymmetryString2"].ToString ().Trim ();
			}
			string remarkLine = "";
			if (type.IndexOf ("both") > -1)
			{
				remarkLine = "Remark 290  This interface exist in both in PDB and PQS. \r\n";
			}
			else
			{
				remarkLine = "Remark 290  Interface exist in " + type.ToUpper () + ".\r\n";
			}
			
			remarkLine += "Remark 300 Interface Chain A For ";			
			remarkLine += ("Asymmetric Chain " + interfaceRow["AsymChain1"].ToString ().Trim () + " ");
			remarkLine += ("Author Chain " + interfaceRow["AuthChain1"].ToString ().Trim () + " ");
			remarkLine += ("Entity " + interfaceRow["EntityID1"].ToString () + " ");
			if (type.IndexOf ("pqs") > -1)
			{
				remarkLine += ("PQS Chain " + interfaceRow["PqsChain1"].ToString ().Trim () + " ");
			}
			remarkLine += ("Symmetry Operator    " + symmetryString1);
			remarkLine += "\r\n";
			// chain B
			remarkLine += "Remark 300 Interface Chain B For ";
			remarkLine += ("Asymmetric Chain " + interfaceRow["AsymChain2"].ToString ().Trim () + " ");
			remarkLine += ("Author Chain " +  interfaceRow["AuthChain2"].ToString ().Trim () + " ");
			remarkLine += ("Entity " + interfaceRow["EntityID2"].ToString () + " ");
			if (type.IndexOf ("pqs") > -1)
			{
				remarkLine += ("PQS Chain " + interfaceRow["PqsChain2"].ToString ().Trim () + " ");
			}
			remarkLine += ("Symmetry Operator    " + symmetryString2);
			remarkLine += "\r\n";
			// number of copies
			remarkLine += ("Remark 300 Number of Copies in biological unit: " + interfaceRow["NumOfCopy"].ToString ());
			remarkLine += "\r\n";

			return remarkLine;
		}
		#endregion

		#region Interfaces From PDB/PQS BU
		/// <summary>
		/// generate interface files for a biological unit
		/// </summary>
		/// <param name="pdbId">PDB Code</param>
		/// <param name="biolUnitId">biological unit id</param>
		/// <param name="interfaceList">the interfaces for the BU</param>
		public ProtInterface[] GenerateInterfaceFiles (string pdbId, string biolUnitId, 
			ProtInterfaceInfo[] interfaceInfoList, string type, out bool needAsaUpdated)
		{
			needAsaUpdated = false;
			Hashtable buChainsHash = null;
			if (type == "pdb")
			{
				string xmlFile = Path.Combine (AppSettings.dirSettings.coordXmlPath, pdbId + ".xml.gz");
				string coordXmlFile = AuxFuncLib.ParseHelper.UnZipFile (xmlFile, AppSettings.tempDir);			
				buChainsHash = pdbBuGen.BuildPdbBU (coordXmlFile, biolUnitId, "ALL");
				File.Delete (coordXmlFile);
			}
			else
			{
				string pqsFile = Path.Combine (AppSettings.dirSettings.pqsBuPath, pdbId + "_" + biolUnitId + ".mmol.gz");
				if (! File.Exists (pqsFile))
				{
					pqsFile = Path.Combine (AppSettings.dirSettings.pqsBuPath, pdbId + ".mmol.gz");
				}
				string unzippedPqsFile = AuxFuncLib.ParseHelper.UnZipFile (pqsFile, AppSettings.tempDir);
				buChainsHash = pqsBuParser.ParsePqsFile (unzippedPqsFile, "ALL");
				// should change PQS residue-numbering to PDB residue-numbering by using sequetial numbers
				//		ConvertPqsResidueNumToPdb (pdbId, ref buChainsHash);
			}
			string[] asaChains = GetChainsWithoutAsa (interfaceInfoList, type);
			if (asaChains.Length > 0)
			{
				needAsaUpdated = true;
			}
			Hashtable chainSaHash = GetChainsSurfaceAreaInBu (pdbId, biolUnitId, buChainsHash, asaChains, type);
			int i = 0;
			string chain1 = "";
			string chain2 = "";
			ProtInterface[] interfaces = new ProtInterface [interfaceInfoList.Length];
			foreach (ProtInterfaceInfo interfaceInfo in interfaceInfoList)
			{
				if (type == "pdb")
				{
					chain1 = interfaceInfo.Chain1 + "_" + interfaceInfo.SymmetryString1;
					chain2 = interfaceInfo.Chain2 + "_" + interfaceInfo.SymmetryString2;
				}
				else
				{
					chain1 = interfaceInfo.Chain1;
					chain2 = interfaceInfo.Chain2;
				}

				if (interfaceInfo.ASA < 0)
				{
					string interfaceComplexFile = interfaceWriter.WriteTempInterfaceToFile (pdbId, biolUnitId, interfaceInfo.InterfaceId, 
						(AtomInfo[])buChainsHash[chain1], (AtomInfo[])buChainsHash[chain2], type);
					double complexAsa = ComputeInterfaceSurfaceArea (interfaceComplexFile);
				
					interfaceInfo.ASA = ((double)chainSaHash[chain1] + (double)chainSaHash[chain2] - complexAsa) / 2;
				}
				interfaceInfo.Remark += FormatInterfaceAsa (interfaceInfo.ASA);

				string interfaceFile = interfaceWriter.WriteInterfaceToFile (pdbId, biolUnitId, interfaceInfo.InterfaceId, 
					(AtomInfo[])buChainsHash[chain1], (AtomInfo[])buChainsHash[chain2], interfaceInfo.Remark, type);
				ParseHelper.ZipPdbFile (interfaceFile);

				interfaces[i] = new ProtInterface (interfaceInfo);
				interfaces[i].ResidueAtoms1 = GetSeqAtoms ((AtomInfo[])buChainsHash[chain1]);
				interfaces[i].ResidueAtoms2 = GetSeqAtoms ((AtomInfo[])buChainsHash[chain2]);
				i ++;
			}
			return interfaces;
		}

		/// <summary>
		/// convert PQS residue numbering to PQS sequential numbering
		/// </summary>
		/// <param name="pdbId"></param>
		/// <param name="buChainsHash"></param>
		private void ConvertPqsResidueNumToPdb (string pdbId, ref Hashtable buChainsHash)
		{
			ArrayList chainList = new ArrayList (buChainsHash.Keys);
			string residueNumQueryString = string.Format 
				("Select PqsChainID, AuthSeqNumbers From PqsPdbChainMap, AsymUnit" + 
				" WHERE PqsPdbChainMap.PdbID = '{0}' AND AsymUnit.PdbID = '{0}' AND " +
				" PqsPdbChainMap.PdbID = AsymUnit.PdbID AND " + 
				" PqsPdbChainMap.PdbChainID = AsymUnit.AuthorChain AND " + 
				" PolymerType = 'polypeptide' AND PqsChainID IN ({1})", 
				pdbId, ParseHelper.FormatSqlListString (chainList));
			DataTable residueNumTable = dbQuery.Query (residueNumQueryString);
			foreach (object chain in buChainsHash.Keys)
			{
				DataRow[] residueNumRows = residueNumTable.Select 
					(string.Format ("PqsChainID = '{0}'", chain.ToString ()));
				string residueNums = residueNumRows[0]["AuthSeqNumbers"].ToString ();
				string[] authNums = residueNums.Split (',');
				AtomInfo[] chainAtoms = (AtomInfo[])buChainsHash[chain];
				foreach (AtomInfo atom in chainAtoms)
				{
					int i = 0;
					for(; i < authNums.Length; i ++)
					{
						if (atom.seqId == authNums[i])
						{
							break;
						}
					}
					atom.seqId = (i + 1).ToString ();
				}
			}
		}
		#endregion
	}
}
