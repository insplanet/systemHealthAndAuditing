using System;
using System.Collections.Concurrent;
using System.Configuration;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HealthAndAuditShared;
using Microsoft.ServiceBus;
using static System.Console;

namespace AnalyzerEngineConsole
{
    internal class Program
    {
        public static FileLogger Logger { get; set; } = new FileLogger(maxIterations:10);
        public static AnalyzerEngine Engine { get; set; } = new AnalyzerEngine();
        public static bool RunRestartLoop { get; set; } = true;
        public static string EventHubName { get; private set; }
        private const int MF_BYCOMMAND = 0x00000000;
        public const int SC_CLOSE = 0xF060;

        [DllImport("user32.dll")]
        public static extern int DeleteMenu(IntPtr hMenu, int nPosition, int wFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern IntPtr GetConsoleWindow();

        static void Main()
        {
            DeleteMenu(GetSystemMenu(GetConsoleWindow(), false), SC_CLOSE, MF_BYCOMMAND);
            AnalyzerEngingeProgram engingeProgram = new AnalyzerEngingeProgram(Engine,Logger);

            engingeProgram.SnapShotGenerator = new StatusSnapShotGenerator(ConfigurationManager.AppSettings["jsonPath"], Logger);

            var storageConnection = ConfigurationManager.AppSettings["AzureStorageConnectionString"];
            ServiceBusConnectionStringBuilder builder = new ServiceBusConnectionStringBuilder(ConfigurationManager.AppSettings["Microsoft.ServiceBus.ConnectionString.Listen"]);
            
            var alarmQueueConnS = ConfigurationManager.AppSettings["ServiceBus.Queue.Connectionstring"];
            var alarmQueueName = ConfigurationManager.AppSettings["ServiceBus.Queue.Name"];

            var alarmQueue = new ServiceBusConnection<AlarmMessage>(alarmQueueConnS, alarmQueueName);
            var alarmManger = new AlarmMessageManager(alarmQueue);
            var ruleStorage = new DocumentDBRuleStorage(ConfigurationManager.AppSettings["DocDBEndPointUrl"], ConfigurationManager.AppSettings["AuthorizationKey"], ConfigurationManager.AppSettings["RuleDatabaseId"], ConfigurationManager.AppSettings["RuleCollectionId"]);
            EventHubName = ConfigurationManager.AppSettings["EventHubName"];
            var engineStartCounter = 0;
            var maxEngineRestarts = 10;
            Task.Run(() =>
            {
                while (RunRestartLoop)
                {
                    if (Engine.State == State.ShuttingDown)
                    {
                        continue;
                    }
                    if (Engine.State == State.Stopped)
                    {
                        Engine.StartEngine(ruleStorage, alarmManger);
                        if (maxEngineRestarts <= engineStartCounter++)
                        {
                            var message = $"AnalyzerEngine main task has been restared {engineStartCounter - 1} times. Engine is down and can not recover! Resetting start counter.";
                            Logger.AddRow(message);
                            engingeProgram.MesseageOutputQueue.Enqueue(message);
                            var alarm = new AlarmMessage(AlarmLevel.High, AppDomain.CurrentDomain.FriendlyName, message);
                            alarmManger.RaiseAlarm(alarm);
                            engineStartCounter = 0;
                        }
                        var timer = new Stopwatch();
                        timer.Start();
                        while (!Engine.EngineIsRunning && timer.ElapsedMilliseconds < 20000)
                        {
                            engingeProgram.MesseageOutputQueue.Enqueue("Awaiting engine start. Waited " + timer.ElapsedMilliseconds + " ms");
                            Task.Delay(1000).Wait();
                        }
                        timer.Reset();
                    }
                }
            });

            Task.Run(() =>
            {
                var connection = new EventHubProcessor(builder.ToString(), EventHubName);
                var recTask = connection.StartReceiver<EventProcessor>(storageConnection);
                EventProcessor.Init(Engine, Logger, storageConnection, ConfigurationManager.AppSettings["OperationStorageTable"]);
                recTask.Wait();
            });
            var gui = new GuiHandler(engingeProgram);
            var guiThread = new Thread(gui.Run);
            guiThread.IsBackground = true;
            guiThread.Start();
        }
    }


    public class AnalyzerEngingeProgram
    {
        public class State
        {
            public long NumberDequeuedSinceStart { get; set; }
            public long ErrorCount { get; set; }
            public string EngineState { get; set; }
            public DateTime StartTimeUtc { get; set; } = DateTime.UtcNow;
            public string EventHubState { get; set; }

            public override string ToString()
            {
                var ret = new StringBuilder();
                foreach (var propertyInfo in GetType().GetProperties())
                {
                    ret.AppendLine(propertyInfo.Name + ": " + propertyInfo.GetValue(this));
                }
                return ret.ToString();
            }
        }

        public StatusSnapShotGenerator SnapShotGenerator { get; set; }
        private MessageAggregator<string> MessageAggregator { get; } = new MessageAggregator<string>();
        public ConcurrentQueue<string> MesseageOutputQueue { get; set; } = new ConcurrentQueue<string>();
        private FileLogger Logger { get; }
        private AnalyzerEngine Engine { get;}
        private State CurrentState { get; } = new State();
        public bool Running { get; private set; } = true;

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public enum Commands
        {
            help,
            reloadrulesforanal,
            exit,
            state,
            showloadedanal,
            showrulesforanal,
            restart
        }
        public ConcurrentQueue<string> OutputQueue { get; } = new ConcurrentQueue<string>();
        public void InputCommand(string command)
        {
            var divided = command.Split(' ');
            if (Enum.TryParse(divided[0], true, out Commands com))
            {
                HandleCommand(com, divided);
            }
            else
            {
                OutputQueue.Enqueue("unknown command " + divided[0]);
            }
        }
        private void HandleCommand(Commands command, string[] args)
        {
            OutputQueue.Enqueue("Command " + command + " recieved.");
            switch (command)
            {
                case Commands.help:

                    OutputQueue.Enqueue("Available commands:");
                    foreach (var avCom in Enum.GetValues(typeof(Commands)))
                    {
                        OutputQueue.Enqueue(avCom.ToString());
                    }
                    break;
                case Commands.exit:
                    Program.RunRestartLoop = false;
                    Engine.StopEngine();
                    Running = false;
                    OutputQueue.Enqueue("Command sent to engine. See log for info.");
                    break;
                case Commands.reloadrulesforanal:
                    if (args.Length < 2 || string.IsNullOrEmpty(args[1]))
                    {
                        OutputQueue.Enqueue("missing analyzer name");
                        break;
                    }
                    Engine.ReloadRulesForAnalyzer(args[1]);
                    OutputQueue.Enqueue("Command sent to engine. See log for info.");
                    break;
                case Commands.showrulesforanal:
                    if (args.Length < 2 || string.IsNullOrEmpty(args[1]))
                    {
                        OutputQueue.Enqueue("missing analyzer name");
                        break;
                    }
                    var rules = Engine.GetRulesLoadedInAnalyzer(args[1]);
                    var loaded = rules.Aggregate("", (current, rule) => current + (rule + Environment.NewLine));
                    OutputQueue.Enqueue(loaded);
                    break;
                case Commands.state:
                    OutputQueue.Enqueue(CurrentState.ToString());
                    break;
                case Commands.showloadedanal:
                    OutputQueue.Enqueue(string.Join(Environment.NewLine, Engine.GetCurrentAnalyzersInfo().Select(item=> $"{item.Name} {item.State}: inQ: {item.EventsInQueue}. Loaded rules: {item.NumberOfRulesLoaded}.")));
                    break;
                case Commands.restart:
                    Program.RunRestartLoop = false;
                    Engine.StopEngine();
                    Running = false;
                    OutputQueue.Enqueue("Command sent to engine. See log for info.");
                    break;
            }
        }

        public AnalyzerEngingeProgram(AnalyzerEngine engine ,FileLogger logger)
        {
            Engine = engine;
            Logger = logger;
            Engine.OnStateChanged += HandleEngineStateChange;
            EventProcessor.OnUpdatedInfo += HandleEventProcessorInfo;

            Task.Run(() =>
            {
                while (true)
                {
                    MessageAggregator.Collection.Clear();
                    for (var i = 0; i < 50; ++i)
                    {
                        if (Engine.EngineMessages.TryDequeue(out var msg))
                        {
                            MessageAggregator.AddMessage(msg, msg.Message.GenerateMessageIdentifierFromString());
                        }
                    }
                    foreach (var messageTracker in MessageAggregator.Collection)
                    {
                        MesseageOutputQueue.Enqueue($"{messageTracker.Value.Message} | {messageTracker.Value.AmountCounter} times from {messageTracker.Value.FirstOccurrence} to {messageTracker.Value.LastOccurrence}");
                    }
                    for (int i = 1; i <= 140; ++i)
                    {
                        if (MesseageOutputQueue.TryDequeue(out string message))
                        {
                            Logger.AddRow(message);
                            SnapShotGenerator.AddMessageToSnapShot(DateTime.UtcNow, message);
                        }
                    }
                }
            });

            Task.Run(() =>
            {
                while (true)
                {
                    UpdateSnapshotAnalyzerInfo();
                    Task.Delay(4000).Wait();
                }
            });
        }

      

        private void HandleEngineStateChange(HealthAndAuditShared.State state)
        {
            CurrentState.EngineState = $"AnalyzerEngine is: {state}";
        }

        private void HandleEventProcessorInfo(ConcurrentDictionary<string, string> info)
        {
            var newText = "";
            foreach (var entry in info)
            {
                newText += $"{entry.Key}: {entry.Value}{Environment.NewLine}";
            }
            CurrentState.EventHubState = newText;
        }

        private void UpdateSnapshotAnalyzerInfo()
        {
            var analyzerlist = Engine.GetCurrentAnalyzersInfo();
            foreach (var anal in analyzerlist)
            {
                SnapShotGenerator.AddAnalyzerInfoToSnapShot(anal);
            }
        }

    }
    public class GuiHandler
    {
        public GuiHandler(AnalyzerEngingeProgram realProgram)
        {
            RealProgram = realProgram;
        }
        private FileLogger OutputLog { get; } = new FileLogger(filePrefix: "OutputLog_");
        private AnalyzerEngingeProgram RealProgram { get; }

        public void Run()
        {
            Thread.Sleep(1000);
            while (true)
            {
                while (RealProgram.OutputQueue.TryDequeue(out var fromQueue))
                {
                    OutputLog.AddRow(fromQueue);
                    WriteLine(fromQueue);
                }
                if (RealProgram.Running)
                {
                    WriteLine("Input command (help to see available commands):");
                    RealProgram.InputCommand(ReadLine());
                }
            }
        }
    }
}
