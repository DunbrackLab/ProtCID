using System;
using System.Collections;
using XtalLib.Settings;
using XtalLib.KDops;
using XtalLib.Crystal;
using XtalLib.ProtInterfaces;

namespace BuInterfacesLib.BuInterfaces
{
	/// <summary>
	/// Interface Contacts
	/// </summary>
	public class ContactsFinder
	{
		public ContactsFinder()
		{
		}

		#region compute interface contacts
		/// <summary>
		/// all inter-atomic contacts of interfaces
		/// </summary>
		/// <param name="protInterfaces">protein interfaces</param>
		public void ComputeAllInterfaceContacts (ref ProtInterface[] protInterfaces)
		{
			ArrayList contactsList = new ArrayList ();
			foreach (ProtInterface protInterface in protInterfaces)
			{
				protInterface.Contacts = ComputeInterfaceContacts (protInterface, true);
			}
		}
		/// <summary>
		/// contacts of interfaces
		/// </summary>
		/// <param name="protInterfaces">protein interfaces</param>
		public void ComputeInterfaceContacts (ref ProtInterface[] protInterfaces)
		{
			ArrayList contactsList = new ArrayList ();
			foreach (ProtInterface protInterface in protInterfaces)
			{
				protInterface.Contacts = ComputeInterfaceContacts (protInterface, false);
			}
		}
		/// <summary>
		/// contacts of interfaces
		/// </summary>
		/// <param name="protInterfaces">protein interfaces</param>
		public void ComputeInterfaceContacts (ref ProtInterface[] protInterfaces, bool isAllContacts)
		{
			ArrayList contactsList = new ArrayList ();
			foreach (ProtInterface protInterface in protInterfaces)
			{
				protInterface.Contacts = ComputeInterfaceContacts (protInterface, isAllContacts);
			}
		}
		/// <summary>
		/// compute the inter-atomic contacts for an interface
		/// </summary>
		/// <param name="protInterface">Protein interface</param>
		/// <param name="isAllContacts">compute all contacts or not</param>
		/// <returns></returns>
		private Contact[] ComputeInterfaceContacts (ProtInterface protInterface, bool isAllContacts)
		{
			ArrayList residuePairList = new ArrayList ();
			// get bounding box for each residue
			Hashtable residueBbHash1 = GetResidueBb (protInterface, 1);
			Hashtable residueBbHash2 = GetResidueBb (protInterface, 2);
			double contactCutoffSquare = Math.Pow (AppSettings.parameters.contactParams.cutoffAtomDist, 2);
			foreach (string seqId1 in residueBbHash1.Keys)
			{
				foreach (string seqId2 in residueBbHash2.Keys)
				{
					// bounding box overlap
					if (((BoundingBox)residueBbHash1[seqId1]).MayOverlap 
						((BoundingBox)residueBbHash2[seqId2], AppSettings.parameters.contactParams.cutoffAtomDist))
					{
						double dX2 = 0.0;
						bool contactCompDone = false;
						// inter-atomic contacts between 2 residues
						foreach (AtomInfo atom1 in protInterface.GetResidueAtoms (seqId1, 1))
						{
							if (contactCompDone && (! isAllContacts))
							{
								break;
							}
							foreach (AtomInfo atom2 in protInterface.GetResidueAtoms (seqId2, 2))
							{
								// inter-atomic distance
								dX2 = Math.Pow (atom1.xyz.X - atom2.xyz.X, 2);
								if (dX2 > contactCutoffSquare)
								{
									continue;
								}
								else
								{
									double dXY2 = Math.Pow (atom1.xyz.Y - atom2.xyz.Y, 2) + dX2;
									if (dXY2 > contactCutoffSquare)
									{
										continue;
									} // X
									else 
									{
										double dXYZ2 = Math.Pow (atom1.xyz.Z - atom2.xyz.Z, 2) + dXY2;
										if (dXYZ2 > contactCutoffSquare)
										{
											continue;
										}// Y
										else
										{
											Contact contact = new Contact ();
											contact.Residue1 = atom1.residue;
											contact.Residue2 = atom2.residue;
											contact.SeqID1 = seqId1;
											contact.SeqID2 = seqId2;
											contact.Distance = Math.Sqrt (dXYZ2);
											contact.Atom1 = atom1.atomName;
											contact.Atom2 = atom2.atomName;
											residuePairList.Add (contact);
											contactCompDone = true;
											break;
										}//Z
									}// X, Y, Z
								}// inter-atomic distance 
							}
						}// inter-atomic contacts
					}// bounding box overlap
				}
			}// loop chains 
			Contact[] contacts = new Contact [residuePairList.Count];
			residuePairList.CopyTo (contacts);
			return contacts;
		}

		/// <summary>
		/// set the bounding box for each residue
		/// </summary>
		/// <param name="protInterface"></param>
		/// <param name="dim"></param>
		/// <returns></returns>
		private Hashtable GetResidueBb (ProtInterface protInterface, int dim)
		{
			if (dim != 1 && dim != 2)
			{
				throw new Exception ("Index out of range. (Index should be 1 or 2.)");
			}
			Hashtable residueBbHash = new Hashtable ();
			ArrayList seqIdList = null;
			if (dim == 1)
			{
				seqIdList = new ArrayList (protInterface.ResidueAtoms1.Keys);
			}
			else if (dim == 2)
			{
				seqIdList = new ArrayList (protInterface.ResidueAtoms2.Keys);
			}
			foreach (string seqId in seqIdList)
			{
				AtomInfo[] atoms = protInterface.GetResidueAtoms (seqId, dim);
				BoundingBox bb = new BoundingBox (atoms);
				residueBbHash.Add (seqId, bb);
			}
			return residueBbHash;
		}
		#endregion
	}
}
