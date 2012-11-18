using System;
using System.Text;

namespace CsvTailer
{
	public class CsvTailerSettings
	{
		private readonly string fileOrDirectoryPath;

		public CsvTailerSettings(string fileOrDirectoryPath)
		{
			this.fileOrDirectoryPath = fileOrDirectoryPath;
			Encoding = Encoding.UTF8;

			BookmarkRepositoryUpdateFrequency = TimeSpan.FromSeconds(1);
		}

		public string FileOrDirectoryPath
		{
			get { return fileOrDirectoryPath; }
		}

		public Encoding Encoding { get; set; }

		public string DirectoryFilter { get; set; }

		public Func<string, string[]> ColumnsProvider { get; set; }

		public int DateTimeColumnIndex { get; set; }

		public TimeSpan BookmarkRepositoryUpdateFrequency { get; set; }
	}
}