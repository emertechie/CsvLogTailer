using System;
using System.IO;

namespace CsvLogTailer.Bookmarks
{
	public class SideBySideLogFileBookmarkRepository : ILogFileBookmarkRepository
	{
		private readonly string bookmarkFileNameFormat;

		public SideBySideLogFileBookmarkRepository()
			: this (bookmarkFileNameFormat: "{0}{1}.last")
		{
		}

		public SideBySideLogFileBookmarkRepository(string bookmarkFileNameFormat)
		{
			this.bookmarkFileNameFormat = bookmarkFileNameFormat;
		}

		public void AddOrUpdate(LogFileBookmark bookmark)
		{
			string bookmarkFilePath = GetBookmarkFilePath(bookmark.FilePath);
			string contents = bookmark.LogDateTime.ToString("o");
			File.WriteAllText(bookmarkFilePath, contents);
		}

		public LogFileBookmark Get(string filePath)
		{
			string bookmarkFilePath = GetBookmarkFilePath(filePath);
			
			DateTime lastLogDateTime = DateTime.MinValue;

			if (File.Exists(bookmarkFilePath))
			{
				var contents = File.ReadAllText(bookmarkFilePath);

				if (!String.IsNullOrWhiteSpace(contents))
					lastLogDateTime = DateTime.Parse(contents.Trim());
			}

			return new LogFileBookmark(filePath, lastLogDateTime);
		}

		private string GetBookmarkFilePath(string logFilePath)
		{
			var dir = Path.GetDirectoryName(logFilePath);
			var fileName = Path.GetFileNameWithoutExtension(logFilePath);
			var ext = Path.GetExtension(logFilePath);
			return Path.Combine(dir, String.Format(bookmarkFileNameFormat, fileName, ext ?? ""));
		}
	}
}