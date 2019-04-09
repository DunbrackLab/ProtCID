using System;
using System.Collections;
using System.Xml;
using System.Xml.Serialization;

namespace XtalLib.Crystal
{
	/// <summary>
	/// Summary description for EntryAtoms.
	/// </summary>
	public class AtomCategory
	{
		// store the list of atoms
		private ArrayList cartnAtoms = null;
		public AtomCategory()
		{
			cartnAtoms = new ArrayList ();
		}
		[XmlElement("Atom")]
		public AtomInfo [] CartnAtoms
		{
			get
			{
				AtomInfo[] atomInfoList = new AtomInfo[cartnAtoms.Count];
				cartnAtoms.CopyTo (atomInfoList);
				return atomInfoList;
			}
			set
			{
				if (value == null)
				{
					return;
				}
				AtomInfo[] atomInfoList = (AtomInfo[]) value;
				cartnAtoms.Clear ();
				cartnAtoms.AddRange (atomInfoList);
			}
		}
		
		public int AddAtom(AtomInfo atom)
		{
			return cartnAtoms.Add (atom);
		}
	}
}
