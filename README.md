CsvLogTailer
============

CsvLogTailer provides an IObservable interface over CSV log files. It can tail individual files, or all files in a particular folder. Tailing a folder supports wildcard filtering and detecting file modifications to pick up new and deleted files.

An optional bookmark mechanism means you can continue tailing from where you last left off - handy for implementing a Store-and-Forward type scheme. The default implementation provided stores a ".last" file beside each log file being tailed. This file contains the last DateTime processed. You are free to provide your own implementation.

Installation
------------

Available on NuGet:
```
Install-Package CsvLogTailer
```

Usage
-----

### Tailing an individual log file

```C#

var tailer = new CsvLogTailer();
tailer
    .Tail("C:\\MyApp.log")
    .Subscribe(logRecord => DoSomethingWith(logRecord));
```

### Tailing a directory

```C#

var tailer = new CsvLogTailer();
tailer
    .Tail("C:\\Logs", "*.log")
    .Subscribe(logRecord => DoSomethingWith(logRecord));
```

### Using bookmarks to restart tailing from last position

Given a directory C:\Logs with log files a.log and b.log, tailing the directory with the configuration below will create files a.log.last and b.log.last. Each '.last' file will contain the last date and time processed from the corresponding log files.

**Note**: On restart, the tailer will only process logs where the log DateTime is greater than or equal to the last log time read from the '.last' file. The reason for this is that there could have been another log written just at the moment the tailer was stopping with the same DateTime as the last log processed. So, be prepared for the potential to have duplicate logs. But, it should be extremely rare and is better than missing logs anyway.

```C#

ILogFileBookmarkRepository bookMarkRepository = new SideBySideLogFileBookmarkRepository();

var tailer = new CsvLogTailer();
tailer
    .Tail("C:\\Logs", "*.log", bookMarkRepository )
    .Subscribe(logRecord => DoSomethingWith(logRecord));
```

Configuration
-------------

The return value from the Tail method shown above is an IObservable<LogRecord>. LogRecord has the following properties:

```C#
public class LogRecord
{
    public string FilePath { get; }
    public DateTime LogDateTime { get; }
    public string[] LogFields { get; }
    public string[] ColumnNames { get; }
}
```

In order to populate the LogDateTime, the tailer needs to know which column contains the Log DateTime. By default it uses the first column. If you need to configure this, you will need to use one of the overloads for the Tail method which takes a CsvLogTailerSettings instance.

You can also optionally provide the column names via one of the Tail method overloads The CsvLogTailer class does not support reading columns names from file (From the first row, for example)
