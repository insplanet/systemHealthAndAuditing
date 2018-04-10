using System.Net.Mail;

namespace HealthAndAuditShared.AlarmChannels
{
    public class EmailAlarmChannel : IAlarmChannel
    {
        private readonly string _sendToAdress;
        private readonly string _sendFromAdress;
        private readonly SmtpClient _smtpClient;
        public EmailAlarmChannel(string sendToAdress, string sendFromAdress, SmtpClient smtpClient)
        {
            _sendToAdress = sendToAdress;
            _sendFromAdress = sendFromAdress;
            _smtpClient = smtpClient;
        }

        public void SendAlarm(AlarmMessage message)
        {
            var mail = new MailMessage();
            mail.To.Add(_sendToAdress);
            mail.From = new MailAddress(_sendFromAdress);
            mail.IsBodyHtml = true;
            mail.Subject = $"{message.Level} level alarm raised by {message.Origin}";
            mail.Body = $@"EventID: {message.StorageID}
                        <br>Message: {message.Message}
                        <br>ExceptionMessage: {message.ExceptionMessage}";

            _smtpClient.Send(mail);
        }

        public void SendAggregatedAlarm(AggregatedMessage<AlarmMessage> aggregated)
        {
            var mail = new MailMessage();
            mail.To.Add(_sendToAdress);
            mail.From = new MailAddress(_sendFromAdress);
            mail.IsBodyHtml = true;
            mail.Subject = $"{aggregated.Message.Origin} caused an alarm flood.";
            mail.Body = $@"{aggregated.Message.Message}
                        <br>Number of occurrences: {aggregated.AmountCounter}
                        <br>First occurrence {aggregated.FirstOccurrence} UTC
                        <br>Last occurrence { aggregated.LastOccurrence} UTC";

            _smtpClient.Send(mail);
        }                                                                  
    }
}
