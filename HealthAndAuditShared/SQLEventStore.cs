using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SystemHealthExternalInterface;

namespace HealthAndAuditShared
{
    public class SQLEventStore : IEventStore
    {
        private string ConnectionString { get; set; }
        private string TableName { get; set; }
        public SQLEventStore(FileLogger logger, string connectionString, string tablename = null)
        {
            Logger = logger;
            TableName = tablename ?? "SystemEvents";
            ConnectionString = connectionString;
        }

        public FileLogger Logger { get; set; }

        public void StoreEvent(SystemEvent @event)
        {
            throw new NotImplementedException();
        }

        public Task<string> StoreEventsAsync(List<SystemEvent> events)
        {
            SystemEvent currentEvent = null;
            try
            {
                foreach (var @event in events)
                {
                    currentEvent = @event;
                    using (var connection = new SqlConnection(ConnectionString))
                    {
                        connection.Open();
                        var query = GenerateQuery();
                        using (SqlCommand cmd = new SqlCommand(query, connection))
                        {
                            AddParametersToCommand(cmd, @event);                     
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.AddRow("!!ERROR!! during DB save of events. " + ex);
                var backUpStorage = new FileLogger(filePrefix: "failedSQLInsert_", maxIterations: 20, maxFilesize: 1024 * 2048, async: true);
                backUpStorage.AddRow(JsonConvert.SerializeObject(currentEvent));
                return Task.Factory.StartNew(() => { return "Exception when saving to DB"; });
            }
            return Task.Factory.StartNew(() => { return "Events saved to DB"; });
        }


        private string GenerateQuery()
        {
            return $@"INSERT INTO {TableName} (TimeStampUTC,Result,OperationName,UniqueOperationID,CaughtException,OperationParameters,AppInfo,OtherInfo,RowKey,PartitionKey)
                        VALUES (@TimeStampUTC,@Result,@OperationName,@UniqueOperationID,@CaughtException,@OperationParameters,@AppInfo,@OtherInfo,@RowKey,@PartitionKey)";
        }

        private void AddParametersToCommand(SqlCommand command, SystemEvent @event)
        {
            command.Parameters.Add(new SqlParameter("@TimeStampUTC", @event.TimeStampUtc));
            command.Parameters.Add(new SqlParameter("@Result", @event.Result.ToString()));
            command.Parameters.Add(new SqlParameter("@OperationName", DbNullIfNull(@event.OperationName)));
            command.Parameters.Add(new SqlParameter("@UniqueOperationID", DbNullIfNull(@event.UniqueOperationID)));
            command.Parameters.Add(new SqlParameter("@CaughtException", DbNullIfNullElseJSON(@event.CaughtException)));
            command.Parameters.Add(new SqlParameter("@OperationParameters", DbNullIfNullElseJSON(@event.OperationParameters)));
            command.Parameters.Add(new SqlParameter("@AppInfo", DbNullIfNullElseJSON(@event.AppInfo)));
            command.Parameters.Add(new SqlParameter("@OtherInfo", DbNullIfNull(@event.OtherInfo)));
            command.Parameters.Add(new SqlParameter("@RowKey", @event.RowKey));
            command.Parameters.Add(new SqlParameter("@PartitionKey", @event.PartitionKey));
        }


        private object DbNullIfNullElseJSON(object inp)
        {
            if (inp is null)
            {
                return DBNull.Value;
            }
            return JsonConvert.SerializeObject(inp);
        }

        private object DbNullIfNull(object inp)
        {
            if (inp is null)
            {
                return DBNull.Value;
            }
            return inp;
        }


        /*        

        CREATE TABLE[dbo].[SystemEvents]
                (

           [TimeStampUTC][datetimeoffset](7) NOT NULL,

          [Result] [nvarchar] (50) NOT NULL,

           [OperationName] [nvarchar] (500) NULL,
	        [UniqueOperationID] [nvarchar] (500) NOT NULL,
 
             [CaughtException] [nvarchar]
                (max) NULL,
 
             [OperationParameters] [nvarchar]
                (max) NULL,
 
             [AppInfo] [nvarchar]
                (max) NULL,
 
             [OtherInfo] [nvarchar]
                (max) NULL
        ) ON[PRIMARY] TEXTIMAGE_ON[PRIMARY]
        GO
         *
         */


    }
}
