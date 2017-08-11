/****************************************************************************************
*	This code originates from the software development department at					*
*	swedish insurance and private loan broker Insplanet AB.								*
*	Full license available in license.txt												*
*	This text block may not be removed or altered.                                  	*
*	The list of contributors may be extended.                                           *
*																						*
*							Mikael Axblom, head of software development, Insplanet AB	*
*																						*
*	Contributors: Mikael Axblom															*
*****************************************************************************************/

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SystemHealthExternalInterface;
using HealthAndAuditShared;
using Microsoft.Azure;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using static System.Console;
using System.Collections.Concurrent;


namespace QuickAnalyzer
{
    internal class Program
    {
        public static AnalyzerEngine Engine { get; set; }
        private List<string> ScreenOutput { get; set; } = new List<string>();
        private static string Eventhubpath { get; set; }
        private static MessageAggregator<string> MessageAggregator { get; set; } = new MessageAggregator<string>();
        private static ConcurrentQueue<string> MesseageOutputQueue { get; set; } = new ConcurrentQueue<string>();
        public static Queue<string> CurrentMessages { get; set; } = new Queue<string>();
        private static bool AwaitingInput { get; set; }
        public static Dictionary<string, string> EventProcInfo { get; set; } = new Dictionary<string, string>();
        public static FileLogger Logger { get; set; } = new FileLogger();
        public static string TimeStampThis(string input)
        {
            return $"{DateTime.Now} | {input}";
        }

        private static string LeftSideDashLine { get; } = "--------------------------";

        public static void WriteLineAndLog(string line)
        {
            WriteLine(RightPadWithSpace(line));
            Logger.AddRow(line);
        }

        private static int WindowWidth { get; } = 192;
        private static int WindowHeight { get; } = 40;

        private static void Main(string[] args)
        {
            SetWindowSize(WindowWidth, WindowHeight);
            Clear();
            var storageConnection = ConfigurationManager.AppSettings["AzureStorageConnectionString"];
            var eventhubConnS = ConfigurationManager.AppSettings["Microsoft.ServiceBus.ConnectionString.Listen"];
            Eventhubpath = ConfigurationManager.AppSettings["EventHubPath"];
            var alarmQueueConnS = ConfigurationManager.AppSettings["ServiceBus.Queue.Connectionstring"];
            var alarmQueueName = ConfigurationManager.AppSettings["ServiceBus.Queue.Name"];

            WriteLineAndLog("Starting analyzer for hub: " + Eventhubpath);

            var alarmQueue = new ServiceBusConnection<AlarmMessage>(alarmQueueConnS, alarmQueueName);
            var alarmManger = new AlarmMessageManager(alarmQueue);
            var ruleStorage = new DocumentDBRuleStorage(ConfigurationManager.AppSettings["DocDBEndPointUrl"], ConfigurationManager.AppSettings["AuthorizationKey"], ConfigurationManager.AppSettings["RuleDatabaseId"], ConfigurationManager.AppSettings["RuleCollectionId"]);
            Engine = new AnalyzerEngine();
            ServiceBusConnectionStringBuilder builder = new ServiceBusConnectionStringBuilder(eventhubConnS);
            builder.TransportType = TransportType.Amqp;
            var connection = new EventHubProcessor(builder.ToString(), Eventhubpath);
            WriteLineAndLog("Starting event receiver.");
            var recTask = connection.StartReceiver<EventProc>(storageConnection);
            recTask.Wait();            

            WriteLineAndLog("Receiver waiting.");
            var engineStartCounter = 0;
            var maxEngineRestarts = 10;
            try
            {

                new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    while (true)
                    {
                        if (!AwaitingInput)
                        {                            
                            Render();
                            Thread.Sleep(500);
                        }
                    }
                }).Start();
                new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    while (true)
                    {
                        ParseInput();
                    }
                }).Start();


                while (true)
                {
                    MessageAggregator.Collection.Clear();
                    for (var i = 0; i < 500; ++i)
                    {
                        TimeStampedMessage<string> msg;
                        if (Engine.EngineMessages.TryDequeue(out msg))
                        {
                            MessageAggregator.AddMessage(msg, msg.Message.GenerateMessageIdentifierFromString());
                        }
                    }
                    foreach (var messageTracker in MessageAggregator.Collection)
                    {
                        MesseageOutputQueue.Enqueue($"{messageTracker.Value.Message} | {messageTracker.Value.AmountCounter} times from {messageTracker.Value.FirstOccurrence} to {messageTracker.Value.LastOccurrence}");
                    }
                    if (Engine.State == State.ShuttingDown)
                    {
                        continue;
                    }
                    if (Engine.State == State.Stopped)
                    {
                        Engine.StartEngine(ruleStorage, alarmManger);
                        if (maxEngineRestarts <= engineStartCounter++)
                        {
                            var message = $"AnalyserEngine main task has been restared {engineStartCounter - 1} times. Engine is down and can not recover! Resetting start counter.";
                            Logger.AddRow(message);
                            MesseageOutputQueue.Enqueue(message);
                            var alarm = new AlarmMessage(AlarmLevel.High, AppDomain.CurrentDomain.FriendlyName, message);
                            alarmManger.RaiseAlarm(alarm);
                            engineStartCounter = 0;
                        }
                    }
                    var timer = new Stopwatch();
                    timer.Start();
                    while (!Engine.EngineIsRunning && timer.ElapsedMilliseconds < 30000)
                    {
                        MesseageOutputQueue.Enqueue("Awaiting engine start. Waited " + timer.ElapsedMilliseconds + " ms");
                        Thread.Sleep(1000);
                    }
                    timer.Reset();
                }
            }
            catch (Exception ex)
            {
                var alarm = new AlarmMessage(AlarmLevel.High, AppDomain.CurrentDomain.FriendlyName, $"Exception in main loop.", ex.Message);
                alarmManger.RaiseAlarm(alarm);
                WriteLineAndLog($"Exception in main loop.");
                WriteLineAndLog(ex.ToString());
            }

            WriteLineAndLog("End of program.");
        }


        private static void Render()
        {
            SetCursorPosition(0, 0);
            WriteLine(RightPadWithSpace("Press any key to start command input."));
            WriteLine(RightPadWithSpace($"AnalyzerEngine for event hub {Eventhubpath} is: {(Engine.EngineIsRunning ? "running" : "not running")}"));
            WriteLine(RightPadWithDash(LeftSideDashLine + " Eventprocessor status "));
            foreach (var processor in EventProcInfo)
            {
                WriteLine(RightPadWithSpace($"{processor.Key}: {processor.Value}"));
            }
            RenderAnalyzerInfo();
            RenderMessages();
            WriteLine(RightPadWithSpace(DateTime.Now.ToString()));
        }
        private static void RenderAnalyzerInfo()
        {
            WriteLine(RightPadWithDash(LeftSideDashLine +" Analyzing events from "));
            var currentRowIndex = 0;
            var currentColumnIndex = 0;
            var numberofColumns = 4;
            var columnWidth = WindowWidth / numberofColumns;
            var analyzerInfoOutput = new List<string[]>
            {
                new string[numberofColumns]
            };
            foreach (var anal in Engine.GetCurrentAnalyzersInfo())
            {
                if (currentColumnIndex >= numberofColumns)
                {
                    analyzerInfoOutput.Add(new string[numberofColumns]);
                    currentRowIndex++;
                }
                analyzerInfoOutput[currentRowIndex][currentColumnIndex] = anal.ToString();
                while (analyzerInfoOutput[currentRowIndex][currentColumnIndex].Length < (currentColumnIndex + 1) * columnWidth)
                {
                    analyzerInfoOutput[currentRowIndex][currentColumnIndex] += " ";
                }
                currentColumnIndex++;
            }         
            foreach(var array in analyzerInfoOutput)
            {
                var line = "";
                foreach(var column in array)
                {
                    line += column;
                }
                WriteLine(line);
            }            
        }
        private static void RenderMessages()
        {
            WriteLine(RightPadWithDash(LeftSideDashLine + " Messages "));
            WriteLine(RightPadWithSpace($"{MesseageOutputQueue.Count} messages in queue."));
            WriteLine("________________________________________________");
            var messagesToPrint = 16;
            for (int i = 1; i <= 14; ++i)
            {
                if (MesseageOutputQueue.TryDequeue(out string message))
                {
                    if (CurrentMessages.Count > messagesToPrint)
                    {
                        CurrentMessages.Dequeue();
                    }
                    CurrentMessages.Enqueue(TimeStampThis(message));
                    Logger.AddRow(message);
                }
            }
            var printedMessages = 0;
            foreach (var message in CurrentMessages)
            {
                WriteLine(RightPadWithSpace(message));
                printedMessages++;
            }
            while(printedMessages++ < messagesToPrint)
            {
                WriteLine(RightPadWithSpace("--"));
            }
            WriteLine(RightPadWithDash(""));
        }

        private static void ClearCurrentConsoleLine()
        {
            int currentLineCursor = Console.CursorTop;
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write(new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, currentLineCursor);
        }

        private static string RightPadWithDash(string org)
        {
            while(org.Length < WindowWidth-1)
            {
                org += "-";
            }
            return org;
        }

        private static string RightPadWithSpace(string org)
        {
            while (org.Length < WindowWidth - 1)
            {
                org += " ";
            }
            return org;
        }


        private static void ParseInput()
        {
            ReadKey();
            void invalidCommand()
            {
                Write("Invalid command. Press any key.");
                ReadKey();
                ClearCurrentConsoleLine();
            }
            AwaitingInput = true;
            Thread.Sleep(500);
            var commandAccepted = false;
            while (!commandAccepted)
            {
                SetCursorPosition(0, 0);
                WriteLine("====================== Rendering paused. Awaiting command. The program is still running. ===========================");
                SetCursorPosition(0, WindowHeight - 7);
                WriteLine("====================================================================================================================");
                WriteLine("Rendering paused. The program is still running. Available commands: ");
                var values = Enum.GetValues(typeof(Commands));
                foreach (var value in values)
                {
                    Write($"{value} ");
                }
                WriteLine();
                ClearCurrentConsoleLine();
                Write("Enter command: ");
                var input = ReadLine();
                var inputArray = input.Split(' ');
                if (Enum.TryParse(inputArray[0], true, out Commands command))
                {
                    commandAccepted = ExcecuteCommand(command, inputArray);
                    if (!commandAccepted)
                    {
                        invalidCommand();
                    }
                }
                else
                {
                    invalidCommand();
                }
            }
            AwaitingInput = false;
            Clear();
        }

        private static bool ExcecuteCommand(Commands command, string[] fullInput)
        {
            if (command == Commands.exit)
            {
                Clear();
                WriteLineAndLog("Exiting by user command. Please wait while the engine is shutting down...");
                new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    while (true)
                    {
                        Thread.Sleep(500);
                        Write(".");
                    }
                }).Start();
                new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    while (true)
                    {
                        if (MesseageOutputQueue.TryDequeue(out string message))
                        {
                            Logger.AddRow(message);
                        }
                    }
                }
                ).Start();
                Engine.StopEngine();
                Environment.Exit(0);
                return true;
            }
            if (command == Commands.resume)
            {
                return true;
            }
            if (command == Commands.restartEng)
            {
                Engine.StopEngine();
                //The main loop will start the engine if it detects that it's stopped
                return true;
            }
            if (command == Commands.reloadRules)
            {
                if (fullInput.Length < 2 || string.IsNullOrWhiteSpace(fullInput[1]))
                {
                    return false;
                }
                var analyzerName = fullInput[1];
                if (Engine.GetCurrentAnalyzersInfo().Any(a => a.name == analyzerName))
                {
                    Engine.ReloadRulesForAnalyzer(analyzerName);
                    return true;
                }
                else
                {
                    WriteLine($"Analyzer for program named {analyzerName} is not running. (Name is case sensitive.)");
                    return false;
                }

            }
            return false;
        }

        public enum Commands
        {
            exit,
            resume,
            restartEng,
            reloadRules
        }

        public class EventProc : IEventProcessor
        {

            private string id;

            public Task OpenAsync(PartitionContext context)
            {
                id = Guid.NewGuid().ToString();
                EventProcInfo.Add(id, TimeStampThis("Open"));
                return Task.FromResult<object>(null);
            }

            public async Task ProcessEventsAsync(PartitionContext context, IEnumerable<EventData> messages)
            {
                EventProcInfo[id] = TimeStampThis("Processing");
                var parsedData = messages.Select(eventData => Encoding.UTF8.GetString(eventData.GetBytes())).Select(JsonConvert.DeserializeObject<SystemEvent>).ToList();
                if (Engine.EngineIsRunning)
                {
                    await Engine.AddToMainQueue(parsedData);
                    EventProcInfo[id] = TimeStampThis(parsedData.Count + " events added to engine");
                }
                else
                {
                    EventProcInfo[id] = TimeStampThis("Engine is not running. Cannot add events. Aborting.");
                    return;
                }
                try
                {
                    var storageMan = new AzureStorageManager(CloudConfigurationManager.GetSetting("AzureStorageConnectionString"));
                    CloudTable Table = storageMan.GetTableReference(ConfigurationManager.AppSettings["OperationStorageTable"]);

                    var batches = new Dictionary<string, TableBatchOperation>();
                    var batchNames = new Dictionary<string, string>();
                    const int maxOps = Microsoft.WindowsAzure.Storage.Table.Protocol.TableConstants.TableServiceBatchMaximumOperations;
                    foreach (var operationResult in parsedData)
                    {
                        string batchName;
                        if (!batchNames.TryGetValue(operationResult.PartitionKey, out batchName))
                        {
                            batchName = operationResult.PartitionKey;
                        }
                        TableBatchOperation batchOperation;
                        if (!batches.ContainsKey(batchName))
                        {
                            batchOperation = new TableBatchOperation();
                            batches.Add(batchName, batchOperation);
                        }
                        else
                        {
                            batches.TryGetValue(batchName, out batchOperation);
                        }
                        Debug.Assert(batchOperation != null, "Could not find batchOperation in Dictionary.");

                        if (batchOperation.Count == maxOps)
                        {
                            batchOperation = new TableBatchOperation();
                            batches.Add(GetNewBatchName(operationResult.PartitionKey, batchNames), batchOperation);
                        }
                        batchOperation.Insert(operationResult);
                    }
                    EventProcInfo[id] = TimeStampThis("Running batches");
                    foreach (var batch in batches)
                    {
                        await Table.ExecuteBatchAsync(batch.Value);
                    }
                }
                catch (Exception ex) when (ex.Message.Contains("The specified entity already exists"))
                {
                    Logger.AddRow("Duplicate entry tried to be added to table storage.");
                }
                catch (Exception ex)
                {
                    EventProcInfo[id] = TimeStampThis("!ERROR! " + ex.Message);
                    Logger.AddRow("!ERROR! In event processor");
                    Logger.AddRow(ex.ToString());
                }
                finally
                {
                    EventProcInfo[id] = TimeStampThis("Setting Checkpoint");
                    await context.CheckpointAsync();
                    EventProcInfo[id] = TimeStampThis("Checkpoint set");
                }
            }

            public string GetNewBatchName(string orginalName, Dictionary<string, string> names)
            {
                const string addon = "X";
                string currentAlias;
                if (names.TryGetValue(orginalName, out currentAlias))
                {
                    names[orginalName] = currentAlias + addon;
                }
                else
                {
                    currentAlias = orginalName + addon;
                    names.Add(orginalName, currentAlias);
                }
                return currentAlias;
            }

            public async Task CloseAsync(PartitionContext context, CloseReason reason)
            {
                EventProcInfo[id] = TimeStampThis("Closing");
                if (reason == CloseReason.Shutdown)
                {
                    await context.CheckpointAsync();
                    EventProcInfo[id] = TimeStampThis("Closing, checkpoint set.");
                }
            }
        }
    }
}