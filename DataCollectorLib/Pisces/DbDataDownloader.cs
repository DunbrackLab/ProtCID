using System;

namespace DataCollector
{
	/// <summary>
	/// Summary description for DbDataDownloader.
	/// </summary>
	public class DbDataDownloader
	{
		public DbDataDownloader()
		{
		}


		public void DownloadDbFiles (string ftpAddress)
		{
			FtpDll myFtp = new FtpDll ();
			string ftpServer = "poulenc.fccc.edu";
			myFtp.Connect (ftpServer);
			

			myFtp.Disconnect ();
		}
	}
}
