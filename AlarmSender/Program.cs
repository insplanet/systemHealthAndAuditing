/****************************************************************************************
*	This code originates from the software development department at					*
*	swedish insurance and private loan broker Insplanet AB.								*
*	Full license available in license.txt												*
*	This text block may not be removed or altered.                                  	*
*	The list of contributors may be extended.                                           *
*																						*
*							Mikael Axblom, head of software development, Insplanet AB	*
*																						*
*	Contributors: Mikael Axblom, Fredrik Lindgren										*
*****************************************************************************************/

using System.Configuration;
using HealthAndAuditShared;
using Microsoft.Azure;
using Microsoft.Azure.WebJobs;

namespace AlarmSender
{
    // To learn more about Microsoft Azure WebJobs SDK, please see http://go.microsoft.com/fwlink/?LinkID=320976
    class Program
    {
        // Please set the following connection strings in app.config for this WebJob to run:
        // AzureWebJobsDashboard and AzureWebJobsStorage
        static void Main()
        {
        
            Functions.ChannelHolder = new AlarmChannelHolder();
            Functions.ChannelHolder.AddChannel(AlarmLevel.High, new SlackClient(CloudConfigurationManager.GetSetting("SlackHook")));
            Functions.FloodControl = new FloodControl(Functions.ChannelHolder,true);
            

            JobHostConfiguration config = new JobHostConfiguration();
            config.UseServiceBus();

            var host = new JobHost(config);

            //var ostrm = new FileStream("c:\\temp\\Redirect.txt", FileMode.OpenOrCreate, FileAccess.Write);
            //var writer = new StreamWriter(ostrm);
            //Console.SetOut(writer);

            // The following code ensures that the WebJob will be running continuously
            host.RunAndBlock();
        }
    }
}
