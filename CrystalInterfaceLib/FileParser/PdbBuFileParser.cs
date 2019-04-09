using System;using System.Collections.Generic;
using System.IO;
using CrystalInterfaceLib.Crystal;
using AuxFuncLib;

namespace CrystalInterfaceLib.FileParser
{
	/// <summary>
	/// Retrieve coordinates from pdb biol_unit file
	/// </summary>
	public class PdbBuFileParser
	{
		public PdbBuFileParser()
		{
		}

		public Dictionary<string, AtomInfo[]> ParsePdbBuFile (string fileName, string atomType)
		{
			int atomNum = 0;
			if (atomType == "CA_CB" )
			{
				atomNum = 6;
			}
			else
			{
				atomNum = 3;
			}
			// key: chainid
			// value: the list of atoms with specified atom type
			Dictionary<string, AtomInfo[]> buChainsHash = new Dictionary<string,AtomInfo[]> ();
			List<AtomInfo> chainAtomList = new List<AtomInfo> ();
			if (! File.Exists (fileName))
			{
				return null;
			}
			string tempDir = @"C:\pdb_temp";
			if (! Directory.Exists (tempDir))
			{
				Directory.CreateDirectory (tempDir);
			}
			string unzipFileName = ParseHelper.UnZipFile (fileName, tempDir);
			StreamReader pdbReader = new StreamReader (unzipFileName);

			try
			{
				string line = "";
				string currentChain = "";
				string modelNum = "";

				while ((line = pdbReader.ReadLine ()) != null)
				{
					if (line.Length >= 5 && line.Substring(0, 5) == "MODEL")
					{
						string[] fields = ParseHelper.SplitPlus (line, ' ');
						modelNum = fields[1];
					}
					// only read protein chains
					if (line.ToUpper ().IndexOf ("ATOM") > -1)
					{
						AtomInfo atom = new AtomInfo ();
						string[] fields = ParseHelper.ParsePdbAtomLine (line);
						if (fields[4] == "HOH")
						{
							continue;
						}
						currentChain = fields[5];
						// add specified atom to current chain
						// if atom type is all, add all atoms
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
						atom.seqId = fields[6];
						atom.xyz = new Coordinate (Convert.ToDouble (fields[8]), Convert.ToDouble (fields[9]), 
							Convert.ToDouble (fields[10]));
						chainAtomList.Add (atom);
					}
					// the end of one chain
					if (line.Length > 3 && line.ToUpper ().Substring (0, 3) == "TER")
					{
						if (chainAtomList.Count >= atomNum)
						{
							string keyString = currentChain;
							if (modelNum != "")
							{
								keyString += "_";
								keyString += modelNum;
							}
                            buChainsHash.Add(keyString, chainAtomList.ToArray ());
							chainAtomList = new List<AtomInfo> ();
						}
					}
				}				
			}
			catch (Exception ex)
			{
				throw new Exception ("Parsing " + fileName + " Errors: " + ex.Message);
			}
			finally
			{
				pdbReader.Close ();
			}
			return buChainsHash;
		}
	}
}
