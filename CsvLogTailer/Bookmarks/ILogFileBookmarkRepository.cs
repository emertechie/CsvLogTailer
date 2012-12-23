namespace CsvLogTailing.Bookmarks
{
	public interface ILogFileBookmarkRepository
	{
		void AddOrUpdate(LogFileBookmark bookmark);

		LogFileBookmark Get(string filePath);
	}
}