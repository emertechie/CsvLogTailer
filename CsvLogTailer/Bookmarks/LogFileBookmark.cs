using System;

namespace CsvLogTailer.Bookmarks
{
	public class LogFileBookmark
	{
		private readonly string filePath;
		private readonly DateTime logDateTime;

		public LogFileBookmark(string filePath, DateTime logDateTime)
		{
			this.filePath = filePath;
			this.logDateTime = logDateTime;
		}

		public string FilePath
		{
			get { return filePath; }
		}

		public DateTime LogDateTime
		{
			get { return logDateTime; }
		}
	}
}