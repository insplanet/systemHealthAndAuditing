/****************************************************************************************
*	This code originates from the software development department at					*
*	swedish insurance and private loan broker Insplanet AB.								*
*	Full license available in license.txt												*
*	This text block may not be removed or altered.                                  	*
*	The list of contributors may be extended.                                           *
*																						*
*							Mikael Axblom, head of software development, Insplanet AB	*
*																						*
*	Contributors: Mikael Axblom, Fredrik Lindgren                                       *
*****************************************************************************************/
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Net;
using System.Text;

//A simple C# class to post messages to a Slack channel  
//Note: This class uses the Newtonsoft Json.NET serialiser available via NuGet  

namespace HealthAndAuditShared
{

    public class SlackClient : IAlarmChannel
    {
        private readonly Uri _uri;
        private readonly Encoding _encoding = new UTF8Encoding();
        public SlackClient(string urlWithAccessToken)
        {
            _uri = new Uri(urlWithAccessToken);
        }

        //Post a message using simple strings  
        public void PostMessage(string text, string username = null, string channel = null)
        {
            Payload payload = new Payload()
            {
                Channel = channel,
                Username = username,
                Text = text
            };

            PostMessage(payload);
        }

        //Post a message using a Payload object  
        public void PostMessage(Payload payload)
        {
            string payloadJson = JsonConvert.SerializeObject(payload);

            using (WebClient client = new WebClient())
            {
                NameValueCollection data = new NameValueCollection();
                data["payload"] = payloadJson;

                var response = client.UploadValues(_uri, "POST", data);

                //The response text is usually "ok"  
                string responseText = _encoding.GetString(response);
#if DEBUG
                Debug.WriteLine("Slack response: " +responseText);
#endif
            }
        }

      

        private static string GetAlarmColour(AlarmLevel level)
        {
            switch (level)
            {
                case AlarmLevel.High:
                    return "#c90906";
                case AlarmLevel.Medium:
                    return "#ff9e0c";
                case AlarmLevel.Low:
                    return "#07b220";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void SendAlarm(AlarmMessage message)
        {
            var payload = new Payload();
            var msgText = $"{message.Level} level alarm raised by {message.Origin}";
            var att =  new Payload.Attachment();
            att.Fallback = msgText;
            att.Color = GetAlarmColour(message.Level);
            att.Pretext = msgText;
            att.Title = $"{message.Message}";
            att.TitleLink = "";

            if (message.StorageID != null)
            {
                var field = new Payload.AttachmentField();
                field.Title = $"StorageID";
                field.Value = message.StorageID;
                field.Short = true;
                att.Fields.Add(field);
            }
            if (message.ExceptionMessage != null)
            {
                var field = new Payload.AttachmentField();
                field.Title = $"Caught Exception";
                field.Value = message.ExceptionMessage;
                field.Short = true;
                att.Fields.Add(field);
            }

            payload.Attachments.Add(att);
            payload.Username = message.Origin;
            PostMessage(payload);
        }

        public void SendAggregatedAlarm(AggregatedMessage<AlarmMessage> aggregated)
        {
            var payload = new Payload();
            payload.Username = aggregated.Message.Origin;
            var msgText = $"{aggregated.Message.Origin} caused an alarm flood.";
            var att = new Payload.Attachment();
            att.Fallback = msgText;
            att.Color = GetAlarmColour(aggregated.Message.Level);
            att.Pretext = msgText;
            att.Title = $"{aggregated.Message.Message}";
            att.TitleLink = "";
            var amountField = new Payload.AttachmentField();
            amountField.Title = $"Number of occurrences: {aggregated.AmountCounter}";
            var firstField = new Payload.AttachmentField();
            var lastField = new Payload.AttachmentField();
            firstField.Title = "First occurrence";
            firstField.Value = $"{aggregated.FirstOccurrence} UTC";
            lastField.Title = "Last occurrence";
            lastField.Value = $"{aggregated.LastOccurrence} UTC";
            firstField.Short = lastField.Short = true;
            att.Fields.Add(amountField);
            att.Fields.Add(firstField);
            att.Fields.Add(lastField);
            payload.Attachments.Add(att);
            PostMessage(payload);
        }
    }

    //This class serialises into the Json payload required by Slack Incoming WebHooks  
    public class Payload
    {
        [JsonProperty("channel")]
        public string Channel { get; set; }
        [JsonProperty("username")]
        public string Username { get; set; }
        [JsonProperty("text")]
        public string Text { get; set; }
        [JsonProperty("attachments")]
        public List<Attachment> Attachments { get; set; } = new List<Attachment>();
        public class Attachment
        {
            [JsonProperty("fallback")]
            public string Fallback { get; set; }
            [JsonProperty("color")]
            public string Color { get; set; }
            [JsonProperty("pretext")]
            public string Pretext { get; set; }
            [JsonProperty("title")]
            public string Title { get; set; }
            [JsonProperty("title_link")]
            public string TitleLink { get; set; }
            [JsonProperty("fields")]
            public List<AttachmentField> Fields { get; set; } = new List<AttachmentField>();
        }
        public class AttachmentField
        {
            [JsonProperty("title")]
            public string Title { get; set; }
            [JsonProperty("value")]
            public string Value { get; set; }
            [JsonProperty("short")]
            public bool Short { get; set; }
        }

    }
}