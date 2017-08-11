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

        void App_Startup(object sender, StartupEventArgs e)
        {                        
            var storageConnection = ConfigurationManager.AppSettings["AzureStorageConnectionString"];
            ServiceBusConnectionStringBuilder builder = new ServiceBusConnectionStringBuilder(ConfigurationManager.AppSettings["Microsoft.ServiceBus.ConnectionString.Listen"]);
            

            var eventhubConnS = ConfigurationManager.AppSettings["Microsoft.ServiceBus.ConnectionString.Listen"];            
            var alarmQueueConnS = ConfigurationManager.AppSettings["ServiceBus.Queue.Connectionstring"];
            var alarmQueueName = ConfigurationManager.AppSettings["ServiceBus.Queue.Name"];

            var alarmQueue = new ServiceBusConnection<AlarmMessage>(alarmQueueConnS, alarmQueueName);
            var alarmManger = new AlarmMessageManager(alarmQueue);
            var ruleStorage = new DocumentDBRuleStorage(ConfigurationManager.AppSettings["DocDBEndPointUrl"], ConfigurationManager.AppSettings["AuthorizationKey"], ConfigurationManager.AppSettings["RuleDatabaseId"], ConfigurationManager.AppSettings["RuleCollectionId"]);
            var engineStartCounter = 0;
            var maxEngineRestarts = 10;
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                while (true)
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
                            //MesseageOutputQueue.Enqueue(message);
                            var alarm = new AlarmMessage(AlarmLevel.High, AppDomain.CurrentDomain.FriendlyName, message);
                            alarmManger.RaiseAlarm(alarm);
                            engineStartCounter = 0;
                        }
                    }
                    var timer = new Stopwatch();
                    timer.Start();
                    while (!Engine.EngineIsRunning && timer.ElapsedMilliseconds < 30000)
                    {
                        //MesseageOutputQueue.Enqueue("Awaiting engine start. Waited " + timer.ElapsedMilliseconds + " ms");
                        Thread.Sleep(1000);
                    }
                    timer.Reset();
                }
            }).Start();


            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                var connection = new EventHubProcessor(builder.ToString(), ConfigurationManager.AppSettings["EventHubPath"]);
                var recTask = connection.StartReceiver<EventProcessor>(storageConnection);
                EventProcessor.Init(Engine, Logger, storageConnection, ConfigurationManager.AppSettings["OperationStorageTable"]);

                recTask.Wait();
            }).Start();

        

            MainWindow mainWindow = new MainWindow(Logger);


            
            mainWindow.Show();
        }
        
        


    }
}
