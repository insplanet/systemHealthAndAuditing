using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using HealthAndAuditShared;
using Microsoft.ServiceBus;

namespace EngineControl
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {

        public static FileLogger Logger { get; set; } = new FileLogger();
        public static AnalyzerEngine Engine { get; set; }= new AnalyzerEngine();
        public static bool RunRestartLoop { get; set; } = true;
        public static string EventHubName { get; private set; }

        

        void App_Startup(object sender, StartupEventArgs e)
        {
            MainWindow mainWindow = new MainWindow(Logger);


            mainWindow.SnapShotGenerator = new StatusSnapShotGenerator(ConfigurationManager.AppSettings["jsonPath"],Logger);

            var storageConnection = ConfigurationManager.AppSettings["AzureStorageConnectionString"];
            ServiceBusConnectionStringBuilder builder = new ServiceBusConnectionStringBuilder(ConfigurationManager.AppSettings["Microsoft.ServiceBus.ConnectionString.Listen"]);
            

            var eventhubConnS = ConfigurationManager.AppSettings["Microsoft.ServiceBus.ConnectionString.Listen"];            
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
                            mainWindow.MesseageOutputQueue.Enqueue(message);
                            var alarm = new AlarmMessage(AlarmLevel.High, AppDomain.CurrentDomain.FriendlyName, message);
                            alarmManger.RaiseAlarm(alarm);
                            engineStartCounter = 0;
                        }
                        var timer = new Stopwatch();
                        timer.Start();
                        while (!Engine.EngineIsRunning && timer.ElapsedMilliseconds < 20000)
                        {
                            mainWindow.MesseageOutputQueue.Enqueue("Awaiting engine start. Waited " + timer.ElapsedMilliseconds + " ms");
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

            mainWindow.SnapShotGenerator.StartGenerator();
            mainWindow.Show();
        }

       

    }
}
