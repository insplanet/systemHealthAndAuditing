using System;
using Microsoft.Azure.Documents.Client;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace HealthAndAuditShared
{
    //todo. Automatic type detection when reading from db.

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
            var list = GetListFromQuery(GetRuleQueryFor<MaxAmountOfFailuresRule>());
            list.AddRange(GetListFromQuery(GetRuleQueryFor<FailurePercentRule>()));
            return list;
        }
        private IQueryable<T> GetRuleQueryFor<T>() where T : AnalyseRuleset
        {
            return Client.CreateDocumentQuery<T>(UriFactory.CreateDocumentCollectionUri(DatabaseName, CollectionName)).Where(d => d.RealType == typeof(T));
        }
        public List<AnalyseRuleset> GetRuleSetsForApplication(string applicationName)
        {
            var list = GetListFromQuery(GetRuleQueryFor<MaxAmountOfFailuresRule>().Where(d => d.ApplicationName == applicationName));
            list.AddRange(GetListFromQuery(GetRuleQueryFor<FailurePercentRule>()).Where(d => d.ApplicationName == applicationName));
            return list;
        }
        private static List<AnalyseRuleset> GetListFromQuery(IEnumerable<AnalyseRuleset> query)
        {
            return query.ToList();
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
