using System.ComponentModel.DataAnnotations;

namespace Abm.Pyro.Domain.Configuration;

public class IndexingSettings
{
    public const string SectionName = "Indexing";
    /// <summary>
    /// If set to True the FHIR search indexes associated with a history instance are removed as the resource is updated or deleted. 
    /// If set to False then all FHIR search indexes are persisted.
    /// When set to True the database will grow at a faster rate as all indexes are kept across all resource updates and deletes.
    /// However, when True, version aware resource references are respected in search queries and chained search queries.
    ///
    /// Though do understand that FHIR _includes and _revincludes do not support or respect version aware resource references as FHIR search Bundles
    /// do not allow the same resource to appear twice in a Bundle of type 'searchset'. Therefore, _includes and _revincludes only consider and return
    /// the most current resource instance regardless of this setting. 
    ///
    /// When set to False, version aware resource references can't be supported beyond the most recent current version, as these are wil be no historic indexes for the server to search against.
    /// In this case a version aware resource references that targets a historic resource instance will never find a match when used is a chained search queries.
    /// 
    /// For example 'GET [Base]/Patient?organization.name=ACME Health' will find no match if the reference found in the Patent resource to the Organization resource is version
    /// aware (e.g Organization/10/_history/1) and the Organization resource has subsequently been update to version 2, even though the Organization's name still remains as 'ACME Health'.
    /// The key point is that a version aware resource reference is exactly that, Version Aware! 
    /// </summary>
    public bool RemoveHistoricResourceIndexesOnUpdateOrDelete { get; init; } = true;
    
}