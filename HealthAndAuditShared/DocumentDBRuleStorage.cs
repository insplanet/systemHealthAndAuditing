using System;
using Microsoft.Azure.Documents.Client;
using System.Collections.Generic;

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
            var all = Client.ReadDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(DatabaseName, CollectionName));
            throw new System.NotImplementedException();
        }
        public List<AnalyseRuleset> GetRuleSetsForApplication(string applicationName)
        {
            throw new System.NotImplementedException();
        }
        public void SaveRuleSet(AnalyseRuleset ruleset)
        {
            Client.UpsertDocumentAsync(UriFactory.CreateDocumentCollectionUri(DatabaseName, CollectionName), ruleset);
        }

        public void DeleteRuleSet(AnalyseRuleset ruleset)
        {
            throw new NotImplementedException();
        }
    }
}