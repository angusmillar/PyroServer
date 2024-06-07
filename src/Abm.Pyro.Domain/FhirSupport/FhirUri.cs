using Abm.Pyro.Domain.Enums;

namespace Abm.Pyro.Domain.FhirSupport;

public class FhirUri
{
    public FhirUri(Uri primaryServiceRootServers)
    {
        PrimaryServiceRootServers = primaryServiceRootServers;
        ParseErrorMessage = string.Empty;
        ErrorInParsing = false;
        ResourceName = string.Empty;
        CompartmentalisedResourceName = string.Empty;
        ResourceName = string.Empty;
        ResourceId = string.Empty;
        VersionId = string.Empty;
        OperationName = string.Empty;
        Query = string.Empty;
        OriginalString = string.Empty;
        IsUrn = false;
        Urn = string.Empty;
        IsAbsoluteUri = false;
        IsFormDataSearch = false;
        IsRelativeToServer = false;
        IsContained = false;
        IsCompartment = false;
        IsMetaData = false;
        IsHistoryReference = false;
        CanonicalVersionId = string.Empty;
    }

    public string ParseErrorMessage { get; set; }
    public bool ErrorInParsing { get; set; }
    public string ResourceName { get; set; }
    public string CompartmentalisedResourceName { get; set; }
    public string ResourceId { get; set; }
    public string VersionId { get; set; }
    public string OperationName { get; set; }
    public string Query { get; set; }
    public string OriginalString { get; set; }
    public bool IsUrn { get; set; }
    public string Urn { get; set; }
    public UrnType? UrnType { get; set; }
    public bool IsFormDataSearch { get; set; }
    public bool IsRelativeToServer { get; set; }

    public bool IsOperation
    {
        get { return OperationType.HasValue; }
    }

    public OperationScope? OperationType { get; set; }
    public bool IsAbsoluteUri { get; set; }
    public bool IsContained { get; set; }
    public bool IsMetaData { get; set; }
    public bool IsHistoryReference { get; set; }
    public string CanonicalVersionId { get; set; }
    public bool IsCompartment { get; set; }
    public Uri? UriPrimaryServiceRoot
    {
        get
        {
            if (IsRelativeToServer)
            {
                return PrimaryServiceRootServers;
            }

            if (IsContained || (string.IsNullOrEmpty(ResourceName) && !IsHistoryReference))
            {
                //Contained references are relative to the resource and not the server or remote
                //Uri's with no Resource part are not relative to server or remote
                return null;
            }

            if (PrimaryServiceRootRemote != null)
            {
                return PrimaryServiceRootRemote;
            }

            throw new ArgumentNullException(nameof(PrimaryServiceRootServers));
        }
    }

    public Uri? PrimaryServiceRootRemote { get; set; }
    public Uri PrimaryServiceRootServers { get; set; }
    
}