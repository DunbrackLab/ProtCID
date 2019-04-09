using System;
using System.Collections;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace BuInterfacesLib.BuMatch
{
	/// <summary>
	/// for serialization
	/// </summary>
	[XmlRoot("BiolUnitComp")]
	public class BuChainMatchCategory
	{
		private ArrayList buChainMatchList = new ArrayList ();

		[XmlElement("BuPair")]
		public BuChainMatch[] BuChainMatchList
		{
			get
			{
				BuChainMatch[] chainMatchArray = new BuChainMatch [buChainMatchList.Count];
				buChainMatchList.CopyTo (chainMatchArray);
				return chainMatchArray;
			}
			set
			{
				if (value == null) return;
				buChainMatchList.Clear ();
				buChainMatchList.AddRange ((BuChainMatch[])value);
			}
		}

		/// <summary>
		/// add a pair of bu chains
		/// </summary>
		/// <param name="buMatch"></param>
		public void AddBuMatch (BuChainMatch buMatch)
		{
			buChainMatchList.Add (buMatch);
		}

		/// <summary>
		/// Match as Entry unit
		/// </summary>
		/// <returns></returns>
		public Hashtable MatchAtEntry ()
		{
			Hashtable entryMatchHash = new Hashtable ();
			foreach (object buChainMatch in buChainMatchList)
			{
				string pdbId = ((BuChainMatch)buChainMatch).pdbId;
				if (entryMatchHash.ContainsKey (pdbId))
				{
					ArrayList buChainsList = (ArrayList)entryMatchHash[pdbId];
					buChainsList.Add (buChainMatch);
				}
				else
				{
					ArrayList buChainsList = new ArrayList ();
					buChainsList.Add (buChainMatch);
					entryMatchHash.Add (pdbId, buChainsList);
				}
			}
			return entryMatchHash;
		}

		/// <summary>
		/// add a list of bu chains
		/// </summary>
		/// <param name="buMatches"></param>
		public void AddBuMatches (BuChainMatch[] buMatches)
		{
			buChainMatchList.AddRange (buMatches);
		}

		public void Save (string fileName)
		{
			// save the BUs in same entity format into a xml file
			BuChainMatchCategory buMatchCat = new BuChainMatchCategory();
			buMatchCat.BuChainMatchList = this.BuChainMatchList;
			XmlSerializer xmlSerializer = new XmlSerializer (typeof (BuChainMatchCategory)); 
			TextWriter buWriter = new StreamWriter (fileName);			
			xmlSerializer.Serialize (buWriter, buMatchCat);
			buWriter.Close ();
		}

		public void Load (string fileName)
		{
			BuChainMatchCategory buMatchCat = new BuChainMatchCategory ();
			XmlSerializer xmlSerializer = new XmlSerializer (typeof(BuChainMatchCategory));
			FileStream xmlFileStream = new FileStream(fileName, FileMode.Open);
			buMatchCat = (BuChainMatchCategory) xmlSerializer.Deserialize (xmlFileStream);
			xmlFileStream.Close ();
			this.BuChainMatchList = buMatchCat.BuChainMatchList;
		}
	}
}

