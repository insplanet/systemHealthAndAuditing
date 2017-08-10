using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HealthAndAuditShared
{
    /// <summary>
    /// Helps to do simple text file logging.
    /// </summary>
    public class FileLogger
    {
        /// <summary>
        /// Number of iterations in file cycle. Oldest will be deleted when cycle starts over and a new one will be created.
        /// </summary>
        private uint MaxIterations { get; }

        /// <summary>
        /// Maximum file size in bytes, when reached a new file will be created.
        /// </summary>
        private uint MaxFileSize { get; }

        /// <summary>
        /// Number of days a file will be used before a new will be created.
        /// </summary>
        private uint DaysBeforeNewFile { get; }

        /// <summary>
        /// Gets the log file prefix.
        /// </summary>
        internal string LogFilePrefix { get; }

        /// <summary>
        /// Gets the log file folder.
        /// </summary>
        /// <value>
        /// The log file folder.
        /// </value>
        private DirectoryInfo LogFileFolder { get; }

        /// <summary>
        /// Gets the current log file.
        /// </summary>
        /// <value>
        /// The current log file.
        /// </value>
        public string CurrentLogFile { get; private set; }

        /// <summary>
        /// Gets the active queue.
        /// </summary>
        /// <value>
        /// The active queue.
        /// </value>
        private ConcurrentQueue<string> ActiveQueue { get; } = new ConcurrentQueue<string>();
        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="FileLogger"/> is writing.
        /// </summary>
        /// <value>
        ///   <c>true</c> if writing; otherwise, <c>false</c>.
        /// </value>
        private bool Writing { get; set; }
        /// <summary>
        /// Gets a value indicating whether this <see cref="FileLogger"/> is asynchronous.
        /// </summary>
        /// <value>
        ///   <c>true</c> if asynchronous; otherwise, <c>false</c>.
        /// </value>
        private bool Async { get; }
        /// <summary>
        /// The file switch lock for async logging.
        /// </summary>
        private object FileSwitchLock = new object();
        /// <summary>
        /// The start lock for async logging.
        /// </summary>
        private object StartLock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="FileLogger"/> class.
        /// </summary>
        /// <param name="logFileFolderPath">The log file folder path. If left emtpy, the programs executing folder will be used.</param>
        /// <param name="filePrefix">The file prefix. Text before datestamp on file</param>
        /// <param name="async">if set to <c>true</c> [asynchronous].</param>
        /// <param name="maxIterations">The maximum iterations before file recycle.</param>
        /// <param name="maxFilesize">The maximum filesize in bytes before new file iteration.</param>
        /// <param name="daysBeforeNewFile">The maximum days before new file iteration</param>
        /// <exception cref="System.IO.DirectoryNotFoundException">Can not find  + logFileFolderPath</exception>
        public FileLogger(string logFileFolderPath = "", string filePrefix = "LogFile_", bool async = false, uint maxIterations = 4, uint maxFilesize = 1024 * 10000, uint daysBeforeNewFile = 7)
        {
            LogFilePrefix = filePrefix;
            MaxIterations = maxIterations;
            const uint minFileSize = 1024 * 1024;
            MaxFileSize = maxFilesize;
            if (MaxFileSize < minFileSize)
            {
                MaxFileSize = minFileSize;
            }
            DaysBeforeNewFile = daysBeforeNewFile;
            Async = async;

            if (logFileFolderPath == "")
            {
                logFileFolderPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            }
            Debug.Assert(logFileFolderPath != null, "Kunde ej hämta programets exekveringsmapp");
            if (!logFileFolderPath.EndsWith(@"\"))
            {
                logFileFolderPath += @"\";
            }

            LogFileFolder = new DirectoryInfo(logFileFolderPath);
            if (!LogFileFolder.Exists)
            {
                throw new DirectoryNotFoundException("Can not find " + logFileFolderPath);
            }
            ChooseFile();
        }

        /// <summary>
        /// Checks if a new file should be created or we a have an old one to write to. Deletes oldest file if <see cref="MaxIterations"/> is reached.
        /// </summary>
        private void ChooseFile()
        {
            var oldFiles = LogFileFolder.GetFiles().Where(x => x.Name.StartsWith(LogFilePrefix)).ToList();
            bool createNewFile = true;
            if (oldFiles.Count > 0)
            {
                createNewFile = false;
                var latestFile = oldFiles.First(x => x.CreationTime == oldFiles.Max(c => c.CreationTime));
                if (((DateTime.Now - latestFile.CreationTime).TotalDays > DaysBeforeNewFile) || latestFile.Length >= MaxFileSize)
                {
                    createNewFile = true;
                }
                else
                {
                    CurrentLogFile = latestFile.FullName;
                }
            }
            if (createNewFile)
            {
                PrepareNewFile();
            }
        }
        /// <summary>
        /// Prepares a new file to write to.
        /// </summary>
        private void PrepareNewFile()
        {
            lock (FileSwitchLock)
            {
                string fileBaseName = LogFileFolder.FullName + LogFilePrefix + DateTime.Now.ToString("yyyy-MM-dd");
                const string fileSuffix = ".txt";
                CurrentLogFile = fileBaseName + fileSuffix;

                var oldFiles = LogFileFolder.GetFiles().Where(x => x.Name.StartsWith(LogFilePrefix)).ToList();
                if (oldFiles.Exists(x => x.FullName == CurrentLogFile))
                {
                    CurrentLogFile = fileBaseName + "_" + DateTime.Now.Hour + DateTime.Now.Minute + DateTime.Now.Second + fileSuffix;
                }

                if (oldFiles.Count >= MaxIterations)
                {
                    var earliesttFile = oldFiles.First(x => x.CreationTime == oldFiles.Min(c => c.CreationTime));
                    try
                    {
                        earliesttFile.Delete();
                    }
                    catch (IOException)
                    {

                    }
                    catch (UnauthorizedAccessException) when (!File.Exists(earliesttFile.FullName))
                    {
                        //Eat if file does not exist.
                    }
                }
            }
        }
        /// <summary>
        /// Checks if new file needed because <see cref="MaxFileSize"/> is reached.
        /// </summary>
        private void CheckIfNewFileNeeded()
        {
            var currentFile = new FileInfo(CurrentLogFile);
            if (currentFile.Exists && currentFile.Length >= MaxFileSize)
            {
                PrepareNewFile();
            }
        }

        /// <summary>
        /// Adds a row to current Log file.
        /// </summary>
        /// <param name="row">The row to add.</param>
        public void AddRow(string row)
        {
            AddRow(new[] { row });
        }

        /// <summary>
        /// Adds a row to current Log file.
        /// </summary>
        /// <param name="columns">Columns to write on row.</param>
        /// <param name="separator">Column separator.</param>
        public void AddRow(string[] columns, string separator = "   ")
        {
            var row = new StringBuilder();
            row.Append(DateTime.Now);
            foreach (string column in columns)
            {
                row.Append(separator);
                row.Append(column);
            }

            if (Async)
            {
                ActiveQueue.Enqueue(row.ToString());
                if (!Writing)
                {
                    StartTask();
                }
            }
            else
            {
                CheckIfNewFileNeeded();
                TryWrite(CurrentLogFile, row.ToString());
            }
        }
        /// <summary>
        /// Callback method for async write task
        /// </summary>
        /// <param name="result">The result.</param>
        private void WriteCallBack(IAsyncResult result)
        {
            Writing = false;
            if (!ActiveQueue.IsEmpty)
            {
                StartTask();
            }
        }
        /// <summary>
        /// Starts the task for async writing.
        /// </summary>
        private void StartTask()
        {
            lock (StartLock)
            {
                if (Writing)
                {
                    return;
                }
                Writing = true;
                Task t = WriteAsync(ActiveQueue);
                t.ContinueWith(WriteCallBack);
            }
        }
        /// <summary>
        /// Writes asynchronous.
        /// </summary>
        /// <param name="queue">The queue of rows to write.</param>
        /// <returns></returns>
        private async Task WriteAsync(ConcurrentQueue<string> queue)
        {
            await Task.Run(() =>
            {
                while (queue.Count > 0)
                {
                    CheckIfNewFileNeeded();
                    string row;
                    if (queue.TryDequeue(out row))
                    {
                        TryWrite(CurrentLogFile, row);
                    }
                }
            });
        }

        /// <summary>
        /// Tries the write a row to current log file.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="row">The row to write.</param>
        /// <exception cref="System.IO.IOException"></exception>
        private static void TryWrite(string path, string row)
        {
            var writeFailed = true;
            var writeAttempts = 10;
            for (int i = 0; i < writeAttempts; i++)
            {
                if (WriteRowToFile(path, row))
                {
                    writeFailed = false;
                    break;
                }
            }
            if (writeFailed)
            {
                throw new IOException($"Could not write row: \"{row}\". to file: \"{path}\". Number of tries: {writeAttempts}.");
            }
        }
        /// <summary>
        /// Writes the row to file.
        /// </summary>
        /// <param name="file">The file path.</param>
        /// <param name="row">The row to write.</param>
        /// <returns>true if row was written.</returns>
        private static bool WriteRowToFile(string file, string row)
        {
            try
            {
                using (StreamWriter sw = new StreamWriter(file, true, Encoding.UTF8))
                {
                    sw.WriteLine(row);
                }
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }
    }
}
