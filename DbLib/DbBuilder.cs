using System;

namespace DbLib
{
	/// <summary>
	/// Interfaces to database and to user interface, contains functions
	/// 1. create
	/// 2. update
	/// </summary>
	public class DbBuilder
	{
		public static DbConnect dbConnect = new DbConnect ();
	//	public static DbConnect dbCopyConnect = null;

		public DbBuilder()
		{
		}
	}
}
