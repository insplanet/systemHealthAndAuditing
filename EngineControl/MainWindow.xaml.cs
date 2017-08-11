using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using HealthAndAuditShared;

namespace EngineControl
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        
        private  MessageAggregator<string> MessageAggregator { get; set; } = new MessageAggregator<string>();
        private  ConcurrentQueue<string> MesseageOutputQueue { get; set; } = new ConcurrentQueue<string>();
        private FileLogger Logger { get; set; } = new FileLogger();
        public MainWindow(FileLogger logger)
        {
            Logger = logger;
            InitializeComponent();

            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                while (true)
                {
                    MessageAggregator.Collection.Clear();
                    for (var i = 0; i < 500; ++i)
                    {
                        TimeStampedMessage<string> msg;
                        if (App.Engine.EngineMessages.TryDequeue(out msg))
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
                                MessageBox.AppendText(message + Environment.NewLine);
                            });
   
                            Logger.AddRow(message);
                        }
                    }
                   
                }
            }).Start();
        }
    }
}
