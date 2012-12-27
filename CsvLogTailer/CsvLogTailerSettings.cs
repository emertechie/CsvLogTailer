using System;
using System.Text;
using System.Text.RegularExpressions;

namespace CsvLogTailing
{
	public class CsvLogTailerSettings
	{
		public CsvLogTailerSettings()
		{
			Encoding = Encoding.UTF8;
			BookmarkRepositoryUpdateFrequency = TimeSpan.FromSeconds(1);
		}

		public string FileOrDirectoryPath { get; set; }

		public string DirectoryFilter { get; set; }

		/// <summary>
		/// A regex used to match the file name of potential log files. Any file names matching this regex will be *excluded*.
		/// </summary>
		public Regex FileNameExcludeRegex { get; set; }

		public Func<string, string[]> ColumnNamesProvider { get; set; }

		public int DateTimeColumnIndex { get; set; }

		public TimeSpan BookmarkRepositoryUpdateFrequency { get; set; }

		public Encoding Encoding { get; set; }
	}
}