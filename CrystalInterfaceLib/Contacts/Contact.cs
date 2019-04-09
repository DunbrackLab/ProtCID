using System;

namespace CrystalInterfaceLib.Contacts
{
	/// <summary>
	/// Summary description for ResiduePair.
	/// </summary>
	public class Contact
	{
		private string residue1 = "";
		private string seqId1 = "";
		private string residue2 = "";
		private string seqId2 = "";
	    private double distance = 0.0;
		private string atom1 = "";
		private string atom2 = "";

		public Contact()
		{
		}

        public Contact (Contact inputContact)
        {
            this.residue1 = inputContact.residue1;
            this.residue2 = inputContact.residue2;
            this.seqId1 = inputContact.seqId1;
            this.seqId2 = inputContact.seqId2;
            this.distance = inputContact.distance;
            this.atom1 = inputContact.atom1;
            this.atom2 = inputContact.atom2;
        }

		#region properties
		/// <summary>
		/// first residue
		/// </summary>
		public string Residue1
		{
			get
			{
				return residue1;
			}
			set
			{
				residue1 = value;
			}
		}

		/// <summary>
		/// second residue
		/// </summary>
		public string Residue2
		{
			get
			{
				return residue2;
			}
			set
			{
				residue2 = value;
			}
		}

		/// <summary>
		/// first sequence id
		/// </summary>
		public string SeqID1
		{
			get
			{
				return seqId1;
			}
			set
			{
				seqId1 = value;
			}
		}

		/// <summary>
		/// second sequence id
		/// </summary>
		public string SeqID2
		{
			get
			{
				return seqId2;
			}
			set
			{
				seqId2 = value;
			}
		}

		/// <summary>
		/// minimum distance between atoms of two residues
		/// </summary>
		public double Distance 
		{
			get
			{
				return distance;
			}
			set
			{
				distance = value;
			}
		}

		public string Atom1
		{
			get
			{
				return atom1;
			}
			set
			{
				atom1 = value;
			}
		}

		public string Atom2
		{
			get
			{
				return atom2;
			}
			set
			{
				atom2 = value;
			}
		}
		#endregion

		public void Reverse ()
		{
			string temp = this.residue1;
			this.residue1 = this.residue2;
			this.residue2 = temp;
			temp = this.atom1;
			this.atom1 = this.atom2;
			this.atom2 = temp;
			temp = this.seqId1;
			this.seqId1 = this.seqId2;
			this.seqId2 = temp;
		}
	}
}
