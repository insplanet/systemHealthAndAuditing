using System;
using Microsoft.Azure.Documents.Client;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HealthAndAuditShared
{
    public class DocumentDBRuleStorage : IRuleStorage
    {
        private string DatabaseName { get; }
        private string CollectionName { get; }
        private DocumentClient Client { get; }
        public DocumentDBRuleStorage(string endpointUri, string primaryKey, string database, string collection)
        {
            Client = new DocumentClient(new Uri(endpointUri), primaryKey);
            DatabaseName = database;
            CollectionName = collection;
        }
        public List<AnalyseRuleset> GetAllRuleSets()
        {
            var query = Client.CreateDocumentQuery(UriFactory.CreateDocumentCollectionUri(DatabaseName, CollectionName));

            var t = query.ToList();

            foreach(var document in t)
            {
                var d = document;
            }

            return null; // GetListFromQuery(query);
        }
        public List<AnalyseRuleset> GetRuleSetsForApplication(string applicationName)
        {
            var query = Client.CreateDocumentQuery<AnalyseRuleset>(UriFactory.CreateDocumentCollectionUri(DatabaseName, CollectionName)).Where(d => d.ApplicationName == applicationName);
            return GetListFromQuery(query);
        }

        private static List<AnalyseRuleset> GetListFromQuery(IQueryable<AnalyseRuleset> query)
        {
            var returnList = new List<AnalyseRuleset>();
            
            foreach (var analyseRuleset in query)
            {
                returnList.Add(analyseRuleset);
            }
            return returnList;
        }

        public async Task UpsertRuleSetAsync(AnalyseRuleset ruleset)
        {
            await Client.UpsertDocumentAsync(UriFactory.CreateDocumentCollectionUri(DatabaseName, CollectionName), ruleset);
        }

        public void DeleteRuleSet(AnalyseRuleset ruleset)
        {
            throw new NotImplementedException();
        }
    }
}