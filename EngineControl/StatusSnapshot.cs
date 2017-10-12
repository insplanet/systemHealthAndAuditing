using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HealthAndAuditShared;
using Newtonsoft.Json;

namespace EngineControl
{

    public class StatusSnapShotGenerator
    {
        private ConcurrentDictionary<string, AnalyzerEngine.AnalyzerInstanceInfo> AnalyzerInfo { get; } = new ConcurrentDictionary<string, AnalyzerEngine.AnalyzerInstanceInfo>();
        private FileLogger Logger { get; }
        private bool IsRunning { get; set; }
        private DirectoryInfo LogFileFolder { get; }
        private int FileGenerationIntervalSeconds { get; }
        public StatusSnapShotGenerator(string fileSavePath, FileLogger logger, int fileGenerationIntervalSeconds = 5)
        {
            Logger = logger;
            try
            {
                if (!fileSavePath.EndsWith(@"\"))
                {
                    fileSavePath += @"\";
                }
                LogFileFolder = new DirectoryInfo(fileSavePath);
                if (!LogFileFolder.Exists)
                {
                    throw new DirectoryNotFoundException("Can not find " + fileSavePath);
                }
                FileGenerationIntervalSeconds = fileGenerationIntervalSeconds;
            }
            catch (Exception e)
            {
                Logger.AddRow($"Exception in {nameof(StatusSnapShotGenerator)} constructor. {e}");
            }
        }

        public void Reset()
        {
            AnalyzerInfo.Clear();
        }

        public void AddMessageToSnapShot(string message)
        {
           CurrentList.Push(message);
        }

        public void AddAnalyzerInfoToSnapShot(AnalyzerEngine.AnalyzerInstanceInfo info)
        {
            AnalyzerInfo.AddOrUpdate(info.Name, info, (key, oldValue) =>  info);
        }

        private ConcurrentStack<string> MessageList1 { get; } = new ConcurrentStack<string>();
        private ConcurrentStack<string> MessageList2 { get; } = new ConcurrentStack<string>();

        private ConcurrentStack<string> CurrentList
        {
            get
            {
                if (List1IsCurrent)
                {
                    return MessageList1;
                }
                return MessageList2;
            }
        }

        private ConcurrentStack<string> NotCurrentList
        {
            get
            {
                if (!List1IsCurrent)
                {
                    return MessageList1;
                }
                return MessageList2;
            }
        }

        private bool List1IsCurrent { get; set; } = true;
        

        private void SwitchList()
        {
            List1IsCurrent = !List1IsCurrent;
        }

        private Queue<string> ListToExport { get; } = new Queue<string>();

        private (List<string> list,int overflow ) GetListToExport()
        {
            var listSize = 20;
            var enqueuedCounter = 0;
            while (NotCurrentList.TryPop(out string message))
            {
                if (ListToExport.Count > listSize)
                {
                    ListToExport.Dequeue();
                }
                ListToExport.Enqueue(message);
                enqueuedCounter++;
            }
            var overflow = 0;
            if (enqueuedCounter > listSize)
            {
                overflow = enqueuedCounter - listSize;
            }
            return (ListToExport.ToList(),overflow);
        }


        public void StartGenerator()
        {
            if (!IsRunning)
            {
                IsRunning = true;
                Run();
            }
        }
        private void Run()
        {
            try
            {
                Task.Run(() =>
                {
                    while (IsRunning)
                    {
                        SwitchList();
                        var snapToSave= new StatusSnapshot();
                        snapToSave.GeneratedUTC = DateTime.UtcNow;
                        foreach (var info in AnalyzerInfo)
                        {
                            snapToSave.AnalyzerList.Add(info.Value);
                        }
                        var messageExport = GetListToExport();
                        foreach (var message in messageExport.list)
                        {
                            snapToSave.Messages.Add(message);
                        }
                        snapToSave.OverFlowMessages = messageExport.overflow;
                        var fileContent = JsonConvert.SerializeObject(snapToSave);
                        using (var sw = new StreamWriter(LogFileFolder.FullName + "enginestatussnapshot.json", false,Encoding.UTF8))
                        {
                            sw.Write(fileContent);
                        }
                        
                        Task.Delay(FileGenerationIntervalSeconds * 1000).Wait();
                    }
                });
            }
            catch (Exception e)
            {
                IsRunning = false;
                Logger.AddRow($"Exception in {nameof(StatusSnapShotGenerator)}.{nameof(Run)} method. {e}");
            }
            StartGenerator();
        }

        private class StatusSnapshot
        {
            public DateTime GeneratedUTC { get; set; }
            public List<string> Messages { get; } = new List<string>();
            public int OverFlowMessages { get; set; }
            public List<AnalyzerEngine.AnalyzerInstanceInfo> AnalyzerList { get; } = new List<AnalyzerEngine.AnalyzerInstanceInfo>();

            public void Reset()
            {
                Messages.Clear();
                AnalyzerList.Clear();
            }
        }
    }



}

