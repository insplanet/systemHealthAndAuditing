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

using System.Net;
using System.Net.Mail;
using HealthAndAuditShared;
using HealthAndAuditShared.AlarmChannels;
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

            NetworkCredential creds = new NetworkCredential(CloudConfigurationManager.GetSetting("smtpuser"), CloudConfigurationManager.GetSetting("smtppwd"));
            var smtp = new SmtpClient(CloudConfigurationManager.GetSetting("smtpadress"), int.Parse(CloudConfigurationManager.GetSetting("smtpport"))) { Credentials = creds, EnableSsl = true };
            
            var lowMailChannel = new EmailAlarmChannel(CloudConfigurationManager.GetSetting("sendLowmailto"), CloudConfigurationManager.GetSetting("sendMailFrom"), smtp);
            var mediumMailChannel = new EmailAlarmChannel(CloudConfigurationManager.GetSetting("sendMediummailto"), CloudConfigurationManager.GetSetting("sendMailFrom"), smtp);
            var highMailChannel = new EmailAlarmChannel(CloudConfigurationManager.GetSetting("sendHighmailto"), CloudConfigurationManager.GetSetting("sendMailFrom"), smtp);

            Functions.ChannelHolder.AddChannel(AlarmLevel.Low, lowMailChannel);
            Functions.ChannelHolder.AddChannel(AlarmLevel.Medium, mediumMailChannel);
            Functions.ChannelHolder.AddChannel(AlarmLevel.High, highMailChannel);

            Functions.ChannelHolder.AddChannel(AlarmLevel.High, new SlackClient(CloudConfigurationManager.GetSetting("SlackHook")));

            Functions.FloodControl = new FloodControl(Functions.ChannelHolder, true);
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
