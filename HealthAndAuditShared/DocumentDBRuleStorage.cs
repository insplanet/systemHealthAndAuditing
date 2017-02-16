﻿/****************************************************************************************
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
using Microsoft.Azure.Documents.Client;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;


namespace HealthAndAuditShared
{
    //todo. Automatic type detection when reading from db.

    public class DocumentDBRuleStorage : IRuleStorage
    {
        private string EndpointUri { get; }
        private string PrimaryKey { get; }
        private string DatabaseName { get; }
        private string CollectionName { get; }
        public DocumentDBRuleStorage(string endpointUri, string primaryKey, string database, string collection)
        {
            DatabaseName = database;
            CollectionName = collection;
            EndpointUri = endpointUri;
            PrimaryKey = primaryKey;
        }
        public List<AnalyseRuleset> GetAllRuleSets()
        {
            var list = GetListFromQuery(GetRuleQueryFor<MaxAmountOfFailuresRule>());
            list.AddRange(GetListFromQuery(GetRuleQueryFor<FailurePercentRule>()));
            return list;
        }
        public AnalyseRuleset GetRuleByID(string id)
        {
            using(var client = new DocumentClient(new Uri(EndpointUri), PrimaryKey))
            {
                var document = client.ReadDocumentAsync(UriFactory.CreateDocumentUri(DatabaseName, CollectionName, id)).Result;
                var type = document.Resource.GetPropertyValue<Type>("RealType");
                var deseralized = JsonConvert.DeserializeObject(document.Resource.ToString(), type);
                return deseralized as AnalyseRuleset;
            }
        }
        private IQueryable<T> GetRuleQueryFor<T>() where T : AnalyseRuleset
        {
            using(var client = new DocumentClient(new Uri(EndpointUri), PrimaryKey))
            {
                return client.CreateDocumentQuery<T>(UriFactory.CreateDocumentCollectionUri(DatabaseName, CollectionName), new FeedOptions {EnableCrossPartitionQuery = false}).Where(d => d.RealType == typeof(T));
            }
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

        public void UpsertRuleSet(AnalyseRuleset ruleset)
        {
            using(var client = new DocumentClient(new Uri(EndpointUri), PrimaryKey))
            {
                client.UpsertDocumentAsync(UriFactory.CreateDocumentCollectionUri(DatabaseName, CollectionName), ruleset).Wait(5000);
            }
        }
        public void DeleteRuleSet(AnalyseRuleset ruleset)
        {
            throw new NotImplementedException();
        }
    }
}
