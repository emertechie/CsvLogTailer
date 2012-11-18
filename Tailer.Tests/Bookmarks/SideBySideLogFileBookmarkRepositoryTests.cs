using System;
using System.IO;
using Tailer.Bookmarks;
using Xunit;

namespace Tailer.Tests.Bookmarks
{
	public class SideBySideLogFileBookmarkRepositoryTests
	{
		[Fact]
		public void CanStoreAndRetrieveLogBookmark()
		{
			string bookmarkFileNameFormat = "{0}{1}.last";
			var sut = new SideBySideLogFileBookmarkRepository(bookmarkFileNameFormat);

			var dummyLogFilePath = Path.GetTempFileName();
			string bookmarkFilePath = null;

			try
			{
				var now = DateTime.UtcNow;

				var originalBookmark = new LogFileBookmark(filePath: dummyLogFilePath, logDateTime: now);

				sut.AddOrUpdate(originalBookmark);

				bookmarkFilePath = Path.Combine(
					Path.GetDirectoryName(dummyLogFilePath),
					String.Format(bookmarkFileNameFormat, Path.GetFileNameWithoutExtension(dummyLogFilePath),Path.GetExtension(dummyLogFilePath)));
				Assert.True(File.Exists(bookmarkFilePath));

				var reloadedBookmark = sut.Get(dummyLogFilePath);
				Assert.Equal(originalBookmark.FilePath, reloadedBookmark.FilePath); ;
				Assert.Equal(originalBookmark.LogDateTime, reloadedBookmark.LogDateTime);
			}
			finally
			{
				File.Delete(dummyLogFilePath);

				if (bookmarkFilePath != null)
					File.Delete(bookmarkFilePath);
			}
		}
	}
}