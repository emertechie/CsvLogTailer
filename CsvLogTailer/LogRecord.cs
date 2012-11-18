using System;

namespace CsvLogTailer
{
	public class LogRecord
	{
		private readonly string filePath;
		private readonly DateTime logDateTime;
		private readonly string[] logFields;
		private readonly string[] columnNames;

		public LogRecord(string filePath, DateTime logDateTime, string[] logFields, string[] possiblyNullColumnNames)
		{
			this.filePath = filePath;
			this.logDateTime = logDateTime;
			this.logFields = logFields;
			this.columnNames = possiblyNullColumnNames;
		}

		public string FilePath
		{
			get { return filePath; }
		}

		public DateTime LogDateTime
		{
			get { return logDateTime; }
		}

		public string[] LogFields
		{
			get { return logFields; }
		}

		public string[] ColumnNames
		{
			get { return columnNames; }
		}
	}
}