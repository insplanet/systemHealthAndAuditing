using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using HealthAndAuditShared;

namespace EngineControl
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public StatusSnapShotGenerator SnapShotGenerator { get; set; }
        private MessageAggregator<string> MessageAggregator { get; } = new MessageAggregator<string>();
        public ConcurrentQueue<string> MesseageOutputQueue { get; set; } = new ConcurrentQueue<string>();
        private string CurrentSelectedAnalyzer { get; set; }
        private FileLogger Logger { get; }
        public MainWindow(FileLogger logger)
        {
            Logger = logger;
            InitializeComponent();
            ShutDownButton.IsEnabled = false;

            EventProcessor.OnUpdatedInfo += HandleEventProcessorInfo;
            App.Engine.OnStateChanged += HandleEngineStateChange;
            App.Engine.OnNewAnalyzerInfo += HandleAnalyzerInfo;


            Task.Run(() =>
            {
                while (true)
                {
                    MessageAggregator.Collection.Clear();
                    for (var i = 0; i < 50; ++i)
                    {
                        if (App.Engine.EngineMessages.TryDequeue(out var msg))
                        {
                            MessageAggregator.AddMessage(msg, msg.Message.GenerateMessageIdentifierFromString());
                        }
                    }
                    foreach (var messageTracker in MessageAggregator.Collection)
                    {
                        MesseageOutputQueue.Enqueue($"{messageTracker.Value.Message} | {messageTracker.Value.AmountCounter} times from {messageTracker.Value.FirstOccurrence} to {messageTracker.Value.LastOccurrence}");
                    }
                    for (int i = 1; i <= 14; ++i)
                    {
                        if (MesseageOutputQueue.TryDequeue(out string message))
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                MessageBox.AppendText($"{DateTime.UtcNow}\t{message}{Environment.NewLine}");
                            });
                            Logger.AddRow(message);
                            SnapShotGenerator.AddMessageToSnapShot(DateTime.UtcNow,message);
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
            StartUpdateSelectedAnalyzerTask();
        }

        private void HandleEventProcessorInfo(ConcurrentDictionary<string, string> info)
        {
            this.Dispatcher.Invoke(() =>
            {
                var newText = "";
                foreach (var entry in info)
                {
                    newText += $"{entry.Key}: {entry.Value}{Environment.NewLine}";
                }
                EventProcStatus.Text = newText;
            });
        }

        private void HandleEngineStateChange(State state)
        {
            this.Dispatcher.Invoke(() =>
            {
                EngineStatus.Content = $"AnalyzerEngine for event hub {App.EventHubName} is: {state}";
            });
        }

        private void HandleAnalyzerInfo(string name, string info)
        {
            this.Dispatcher.Invoke(() =>
            {
                AnalyzerList.Items.Clear();
                var analyzerlist = App.Engine.GetCurrentAnalyzersInfo();
                foreach (var anal in analyzerlist)
                {
                    AnalyzerList.Items.Add(new AnalyzerListItem { Name = anal.Name, Info = anal.State });
                }
            });
            UpdateSnapshotAnalyzerInfo();
        }

        private void UpdateSnapshotAnalyzerInfo()
        {
            var analyzerlist = App.Engine.GetCurrentAnalyzersInfo();
            foreach (var anal in analyzerlist)
            {
                SnapShotGenerator.AddAnalyzerInfoToSnapShot(anal);
            }
        }

        private void Restart_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                App.Engine.StopEngine();
            });
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (App.Engine.EngineIsRunning)
            {
                e.Cancel = true;
            }
            base.OnClosing(e);
        }

        private void Shutdown_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    RestartButton.IsEnabled = false;
                    ShutDownButton.IsEnabled = false;
                });
                App.RunRestartLoop = false;
                App.Engine.StopEngine();
            });
        }

        private void ActivateShutdown_Checked(object sender, RoutedEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {
                ShutDownButton.IsEnabled = ShutDownCheckbox.IsChecked.Value;
            });
        }

        private void AnalyzerList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var castSender = (ListBox)sender;
            if (castSender.Items.Count == 0)
            {
                return;
            }
            CurrentSelectedAnalyzer = ((AnalyzerListItem)castSender.SelectedItem).Name;
            this.Dispatcher.Invoke(() =>
            {
                AnalyzerReloadButton.IsEnabled = true;
                AnalyzerReloadButton.Content = " Reload rules for " + CurrentSelectedAnalyzer + " ";
                AnalyzerReloadButton.Tag = CurrentSelectedAnalyzer;
            });
            UpdateAnalyzerInfo();
        }


        private void StartUpdateSelectedAnalyzerTask()
        {
            Task.Run(() =>
            {
                while (true)
                {
                    if (string.IsNullOrWhiteSpace(CurrentSelectedAnalyzer))
                    {
                        continue;
                    }
                    UpdateAnalyzerInfo();
                    Task.Delay(3000).Wait();
                }
            });
        }

        private void UpdateAnalyzerInfo()
        {
            this.Dispatcher.Invoke(() =>
            {
                RuleList.Items.Clear();
                var rules = App.Engine.GetRulesLoadedInAnalyzer(CurrentSelectedAnalyzer);
                foreach (var rule in rules)
                {
                    RuleList.Items.Add(rule);
                }
                var analInfo = App.Engine.GetInfoForAnalyzer(CurrentSelectedAnalyzer);
                AnalyzerInfoLabel.Content = analInfo.EventsInQueue + " events in queue";

            });
        }


        private void AnalyzerReloadButton_Click(object sender, RoutedEventArgs e)
        {
            var analyzerName = ((Button)sender).Tag.ToString();
            Task.Run(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                App.Engine.ReloadRulesForAnalyzer(analyzerName);
            });
        }

        public class AnalyzerListItem
        {
            public string Name { get; set; }
            public string Info { get; set; }

            public override string ToString()
            {
                return Name + " | " + Info;
            }
        }
    }
}
