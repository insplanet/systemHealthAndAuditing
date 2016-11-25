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
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HealthAndAuditShared;
using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json;
using static System.Console;

namespace Eventpump
{
    /// <summary>
    /// Test program
    /// Pump events to the hub
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {

            try
            {
                var eventhubclient = EventHubClient.CreateFromConnectionString(ConfigurationManager.AppSettings["Microsoft.ServiceBus.ConnectionString.Send"]);
                var t = 0;
                var innerCounter = 0;
                var edatalist = new List<EventData>();

                WriteLine("Generating first eventbatch...");
                while(true)
                {
                    try
                    {
                        if(innerCounter++ == 20)
                        {
                            Clear();
                            WriteLine($"Enter för att skicka {innerCounter - 1} events.");
                            //WriteLine($"Skickar {innerCounter - 1} events.");
                            innerCounter = 0;
                            ReadLine();
                            eventhubclient.SendBatch(edatalist);
                            edatalist.Clear();
                            WriteLine("Sent.");
                            WriteLine("Generating new eventbatch...");
                        }
                        Write(".");
                        //WriteLine(innerCounter);



                        var opres = new SystemEvent(SystemEvent.OperationResult.Failure);
                        opres.OtherInfo = $"{t++}";

                        if(t%2 == 0)
                        {
                            opres.OperationName = "operation1";
                        }
                        else
                        {
                            opres.OperationName = "operation2";
                        }
                        opres.OperationParameters = new Dictionary<string, object>();
                        opres.OperationParameters.Add(nameof(innerCounter), innerCounter);
                        opres.OperationParameters.Add("test", 56);

                        if(t%8 == 0)
                        {
                            opres.AppInfo.ApplicationName = opres.PartitionKey = "Annan app";
                        }

                        if(t%6 == 0)
                        {
                            opres.Result = SystemEvent.OperationResult.Success;
                        }
                        else
                        {
                            opres.Result = SystemEvent.OperationResult.Failure;
                            opres.CaughtException = new NullReferenceException();
                            //string error = null;
                            //try
                            //{
                            //    if(t%7 == 0)
                            //    {
                            //        throw new ArgumentException("arg fail");
                            //    }
                            //    if(error.Length == 9)
                            //    {
                            //        error = "9";
                            //    }
                            //}
                            //catch(Exception ex)
                            //{
                            //    opres.Result = SystemEvent.OperationResult.Failure;
                            //    opres.CaughtException = ex;
                            //}
                        }



                        var data = JsonConvert.SerializeObject(opres);
                        //WriteLine(data);
                        var ed = new EventData();
                        edatalist.Add(new EventData(Encoding.UTF8.GetBytes(data)));
                        //eventhubclient.Send(new EventData(Encoding.UTF8.GetBytes(data)));
                        //Thread.Sleep(100);
                    }
                    catch  { }
                }
            }
            catch {}

        }
    }
}
