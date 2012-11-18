using System;
using System.Text;

namespace CsvTailer
{
	public class CsvTailerSettings
	{
		public CsvTailerSettings()
		{
			Encoding = Encoding.UTF8;
			BookmarkRepositoryUpdateFrequency = TimeSpan.FromSeconds(1);
		}

		public string FileOrDirectoryPath { get; set; }

		public Encoding Encoding { get; set; }

		public string DirectoryFilter { get; set; }

		public Func<string, string[]> ColumnNamesProvider { get; set; }

		public int DateTimeColumnIndex { get; set; }

		public TimeSpan BookmarkRepositoryUpdateFrequency { get; set; }
	}
}