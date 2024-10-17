using Abm.Pyro.Application.FhirResolver;
using Hl7.Fhir.ElementModel;
using Microsoft.Extensions.Logging;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.FhirPath;
using Abm.Pyro.Domain.Cache;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Exceptions;
using Abm.Pyro.Domain.IndexSetters;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Projection;
using SearchParamType = Abm.Pyro.Domain.Enums.SearchParamType;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Application.Indexing
{
    public class Indexer(
        IDateTimeSetter dateTimeSetter,
        INumberSetter numberSetter,
        IReferenceSetter referenceSetterSupport,
        IStringSetter stringSetter,
        ITokenSetter tokenSetter,
        IQuantitySetter quantitySetter,
        IUriSetter uriSetter,
        ILogger<Indexer> logger,
        ISearchParameterCache searchParameterCache,
        IFhirPathResolve fhirPathResolve)
        : IIndexer
    {
        private IndexerOutcome? IndexerOutcome;

        public async Task<IndexerOutcome> Process(Resource fhirResource,
            FhirResourceTypeId resourceType)
        {
            IndexerOutcome = new IndexerOutcome(
                new List<IndexString>(),
                new List<IndexReference>(),
                new List<IndexDateTime>(),
                new List<IndexQuantity>(),
                new List<IndexToken>(),
                new List<IndexUri>());

            IEnumerable<SearchParameterProjection> baseResourceSearchParameterList = await searchParameterCache.GetListByResourceType(FhirResourceTypeId.Resource);
            IEnumerable<SearchParameterProjection> searchParameterList = baseResourceSearchParameterList.Concat(await searchParameterCache.GetListByResourceType(resourceType));

            foreach (SearchParameterProjection searchParameter in searchParameterList)
            {
                await GetSearchParameterIndexes(fhirResource, resourceType, searchParameter);
            }

            return IndexerOutcome;
        }

        private async Task GetSearchParameterIndexes(Resource fhirResource,
            FhirResourceTypeId resourceType,
            SearchParameterProjection searchParameter)
        {
            //Composite searchParameters do not require populating as they are a Composite of another SearchParameter Type
            //searchParameters with an empty or null FHIRPath can not be indexed, this is true for _query and _content which 
            //in my view should not be searchParameters, just as _sort or _count are not.
            if (searchParameter.Type != SearchParamType.Composite)
            {
                //FhirPathCompiler.DefaultSymbolTable.AddFhirExtensions();
                ElementNavFhirExtensions.PrepareFhirSymbolTableFunctions();
                IEnumerable<ITypedElement> typedElementList = GetTypedElementList(fhirResource, searchParameter);
                foreach (ITypedElement typedElement in typedElementList)
                {
                    await GetIndexListBySearchParameterType(resourceType, searchParameter, typedElement);
                }
            }
        }

        private IEnumerable<ITypedElement> GetTypedElementList(Resource fhirResource, SearchParameterProjection searchParameter)
        {
            if (string.IsNullOrWhiteSpace(searchParameter.Expression))
            {
                return Enumerable.Empty<ITypedElement>();
            }

            try
            {
                ScopedNode resourceModel = new ScopedNode(fhirResource.ToTypedElement());

                return resourceModel.Select(
                    expression: searchParameter.Expression,
                    ctx: new FhirEvaluationContext(resourceModel)
                    {
                        ElementResolver = fhirPathResolve.Resolver //Add our custom resolver to handle fhirpath Resolve() functions 
                    });
            }
            catch (Exception exception)
            {
                throw new FhirFatalException(System.Net.HttpStatusCode.InternalServerError,
                    $"Unable to evaluate the FhirPath select expression for the SearchParameter code : {searchParameter.Code} " +
                    $"for the resource type of : {fhirResource.TypeName} with the SearchParameter database primary key of {searchParameter.SearchParameterStoreId.ToString()}. " +
                    $"The FhirPath expression was : {searchParameter.Expression}. See inner exception for more info.", exception);
            }
        }

        private async Task GetIndexListBySearchParameterType(FhirResourceTypeId resourceType,
            SearchParameterProjection searchParameter,
            ITypedElement typedElement)
        {
            switch (searchParameter.Type)
            {
                case SearchParamType.Number:
                    GetNumberIndexList(resourceType, searchParameter, typedElement);
                    break;
                case SearchParamType.Date:
                    GetDateIndexList(resourceType, searchParameter, typedElement);
                    break;
                case SearchParamType.String:
                    GetStringIndexList(resourceType, searchParameter, typedElement);
                    break;
                case SearchParamType.Token:
                    GetTokenIndexList(resourceType, searchParameter, typedElement);
                    break;
                case SearchParamType.Reference:
                    await GetReferenceIndexList(resourceType, searchParameter, typedElement);
                    break;
                case SearchParamType.Composite:
                    //Composite searchParameters do not require populating as they are a Composite of other SearchParameter Types
                    break;
                case SearchParamType.Quantity:
                    GetQuantityIndexList(resourceType, searchParameter, typedElement);
                    break;
                case SearchParamType.Uri:
                    GetUriIndexList(resourceType, searchParameter, typedElement);
                    break;
                case SearchParamType.Special:
                    logger.LogWarning("Encountered a search parameter of type: {SearchParamType} which is not supported by the server. The search parameter " +
                                      "had the code of : {SearchParameterCode} with a SearchParameterStore database primary key of {SearchParameterStoreId}. " +
                                      "The resource type being processed was of type : {ResourceType}",
                        SearchParamType.Special.ToString(),
                        searchParameter.Code,
                        searchParameter.SearchParameterStoreId.ToString(),
                        resourceType.ToString());
                    break;
                default:
                    throw new FhirFatalException(System.Net.HttpStatusCode.InternalServerError, $"Encountered an unknown SearchParamType of type {searchParameter.Type.GetCode()}");
            }
        }

        private void GetUriIndexList(FhirResourceTypeId resourceType,
            SearchParameterProjection searchParameter,
            ITypedElement typedElement)
        {
            if (IndexerOutcome is null)
            {
                throw new NullReferenceException(nameof(IndexerOutcome));
            }

            if (!searchParameter.SearchParameterStoreId.HasValue)
            {
                throw new NullReferenceException(nameof(searchParameter.SearchParameterStoreId));
            }

            IList<IndexUri> uriIndexList = uriSetter.Set(typedElement, resourceType, searchParameter.SearchParameterStoreId.Value, searchParameter.Code);
            IndexerOutcome.UriIndexList.AddRange(uriIndexList);
        }

        private void GetQuantityIndexList(FhirResourceTypeId resourceType,
            SearchParameterProjection searchParameter,
            ITypedElement typedElement)
        {
            if (IndexerOutcome is null)
            {
                throw new NullReferenceException(nameof(IndexerOutcome));
            }

            if (!searchParameter.SearchParameterStoreId.HasValue)
            {
                throw new NullReferenceException(nameof(searchParameter.SearchParameterStoreId));
            }

            IList<IndexQuantity> quantityIndexList = quantitySetter.Set(typedElement, resourceType, searchParameter.SearchParameterStoreId.Value, searchParameter.Code);
            IndexerOutcome.QuantityIndexList.AddRange(quantityIndexList);
        }

        private async Task GetReferenceIndexList(FhirResourceTypeId resourceType,
            SearchParameterProjection searchParameter,
            ITypedElement typedElement)
        {
            if (IndexerOutcome is null)
            {
                throw new NullReferenceException(nameof(IndexerOutcome));
            }

            if (!searchParameter.SearchParameterStoreId.HasValue)
            {
                throw new NullReferenceException(nameof(searchParameter.SearchParameterStoreId));
            }

            IList<IndexReference> referenceIndexList = await referenceSetterSupport.SetAsync(typedElement, resourceType, searchParameter.SearchParameterStoreId.Value, searchParameter.Code); 
            IndexerOutcome.ReferenceIndexList.AddRange(referenceIndexList);
        }

        private void GetTokenIndexList(FhirResourceTypeId resourceType,
            SearchParameterProjection searchParameter,
            ITypedElement typedElement)
        {
            if (IndexerOutcome is null)
            {
                throw new NullReferenceException(nameof(IndexerOutcome));
            }

            if (!searchParameter.SearchParameterStoreId.HasValue)
            {
                throw new NullReferenceException(nameof(searchParameter.SearchParameterStoreId));
            }

            IList<IndexToken> tokenIndexList = tokenSetter.Set(typedElement, resourceType, searchParameter.SearchParameterStoreId.Value, searchParameter.Code);
            IndexerOutcome.TokenIndexList.AddRange(tokenIndexList );
        }

        private void GetStringIndexList(FhirResourceTypeId resourceType,
            SearchParameterProjection searchParameter,
            ITypedElement typedElement)
        {
            if (IndexerOutcome is null)
            {
                throw new NullReferenceException(nameof(IndexerOutcome));
            }

            if (!searchParameter.SearchParameterStoreId.HasValue)
            {
                throw new NullReferenceException(nameof(searchParameter.SearchParameterStoreId));
            }

            IList<IndexString> stringIndexList = stringSetter.Set(typedElement, resourceType, searchParameter.SearchParameterStoreId.Value, searchParameter.Code);
            IndexerOutcome.StringIndexList.AddRange(stringIndexList);
        }

        private void GetDateIndexList(FhirResourceTypeId resourceType,
            SearchParameterProjection searchParameter,
            ITypedElement typedElement)
        {
            if (IndexerOutcome is null)
            {
                throw new NullReferenceException(nameof(IndexerOutcome));
            }

            if (!searchParameter.SearchParameterStoreId.HasValue)
            {
                throw new NullReferenceException(nameof(searchParameter.SearchParameterStoreId));
            }

            IList<IndexDateTime> dateIndexList = dateTimeSetter.Set(typedElement, resourceType, searchParameter.SearchParameterStoreId.Value, searchParameter.Code);
            IndexerOutcome.DateTimeIndexList.AddRange(dateIndexList);
        }

        private void GetNumberIndexList(FhirResourceTypeId resourceType,
            SearchParameterProjection searchParameter,
            ITypedElement typedElement)
        {
            if (IndexerOutcome is null)
            {
                throw new NullReferenceException(nameof(IndexerOutcome));
            }

            if (!searchParameter.SearchParameterStoreId.HasValue)
            {
                throw new NullReferenceException(nameof(searchParameter.SearchParameterStoreId));
            }
            
            IList<IndexQuantity> quantityIndexList = numberSetter.Set(typedElement, resourceType, searchParameter.SearchParameterStoreId.Value, searchParameter.Code);
            IndexerOutcome.QuantityIndexList.AddRange(quantityIndexList);
        }
    }
}