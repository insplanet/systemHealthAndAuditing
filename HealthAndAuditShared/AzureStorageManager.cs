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
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace HealthAndAuditShared
{
    public class AzureStorageManager
    {
        private CloudStorageAccount StorageAccount { get;}
        public AzureStorageManager(string connectionString)
        {
            StorageAccount = CloudStorageAccount.Parse(connectionString);
        }

        public CloudTable GetTableReference(string tableName)
        {
            CloudTableClient tableClient = StorageAccount.CreateCloudTableClient();
            return tableClient.GetTableReference(tableName);
        }

    }
}
