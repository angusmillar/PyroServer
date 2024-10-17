using System.IO.Compression;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Abm.Pyro.Api.ContentFormatters;
using Abm.Pyro.Api.DependencyInjectionFactory;
using Abm.Pyro.Api.Extensions;
using Abm.Pyro.Api.HttpContextAccess;
using Abm.Pyro.Api.Middleware;
using Abm.Pyro.Application.AssemblyMarker;
using Abm.Pyro.Application.Behavior;
using Abm.Pyro.Application.Cache;
using Abm.Pyro.Application.DependencyFactory;
using Abm.Pyro.Application.EndpointPolicy;
using Abm.Pyro.Application.FhirBundleService;
using Abm.Pyro.Application.FhirClient;
using Abm.Pyro.Application.FhirHandler;
using Abm.Pyro.Application.FhirRequest;
using Abm.Pyro.Application.FhirResolver;
using Abm.Pyro.Application.FhirResponse;
using Abm.Pyro.Application.FhirSubscriptions;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Application.Indexing;
using Abm.Pyro.Application.SearchQuery;
using Abm.Pyro.Application.SearchQueryChain;
using Abm.Pyro.Application.Validation;
using Abm.Pyro.Domain.Cache;
using Abm.Pyro.Domain.Configuration;
using Abm.Pyro.Domain.FhirQuery;
using Abm.Pyro.Domain.IndexSetters;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.SearchQuery;
using Abm.Pyro.Domain.Support;
using Abm.Pyro.Repository;
using Abm.Pyro.Repository.Predicates;
using Abm.Pyro.Repository.Query;
using Abm.Pyro.Repository.Service;
using Steeltoe.Extensions.Configuration.ConfigServer;
using Abm.Pyro.Application.HostedServiceSupport;
using Abm.Pyro.Application.Manager;
using Abm.Pyro.Application.MetaDataService;
using Abm.Pyro.Application.Notification;
using Abm.Pyro.Application.OnStartupService;
using Abm.Pyro.Domain.ServiceBaseUrlService;
using Abm.Pyro.Domain.TenantService;
using Abm.Pyro.Domain.Validation;
using Abm.Pyro.Repository.DependencyFactory;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Extensions.Http;
using Serilog.Core;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(path: "./application-start-.log", rollingInterval: RollingInterval.Day)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    
    Log.Information("Starting up application {Environment}", builder.Environment.IsDevelopment() ? "(Is Development Environment)" : string.Empty); 
    
    builder.Host
        .AddConfigServer(SteelToeSerilogExtension.GetLoggerFactory());

    Logger serilogConfiguration = new LoggerConfiguration()
        .WriteTo.Console()
        .ReadFrom.Configuration(builder.Configuration)
        .CreateLogger();
    
    builder.Services.AddSerilog(serilogConfiguration);
    
    // Configuration settings registrations -------------------------------------------------------------
    builder.Services.AddOptions<ImplementationSettings>()
        .Bind(builder.Configuration.GetSection(ImplementationSettings.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services.AddOptions<ServiceBaseUrlSettings>()
        .Bind(builder.Configuration.GetSection(ServiceBaseUrlSettings.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services.AddOptions<ServiceDefaultTimeZoneSettings>()
        .Bind(builder.Configuration.GetSection(ServiceDefaultTimeZoneSettings.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services.AddOptions<PaginationSettings>()
        .Bind(builder.Configuration.GetSection(PaginationSettings.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services.AddOptions<IncludeRevIncludeSettings>()
        .Bind(builder.Configuration.GetSection(IncludeRevIncludeSettings.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services.AddOptions<IndexingSettings>()
        .Bind(builder.Configuration.GetSection(IndexingSettings.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services.AddOptions<ResourceEndpointPolicySettings>()
        .Bind(builder.Configuration.GetSection(ResourceEndpointPolicySettings.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();
    
    builder.Services.AddOptions<TenantSettings>()
        .Bind(builder.Configuration.GetSection(TenantSettings.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();
    
    // On Application Startup Services ----------------------------------------------------------------
    builder.Services.AddAppStartUpService<DatabaseVersionValidationOnStartupService>();
    builder.Services.AddAppStartUpService<FhirServiceBaseUrlManagementOnStartupService>();
    builder.Services.AddAppStartUpService<ValidateAndPrimeResourceEndpointPoliciesOnStartupService>();
    
    //Runs a background services which processes and handles system-wide notification events, for example FHIR Subscriptions & notifications  
    builder.Services.AddTimedHostedService<NotificationManager>(opt =>
    {
        // This service continuously runs, therefore, the 1 sec is only incurred on application start-up,
        // or an uncaught exception seen cycling  
        opt.TriggersEvery = TimeSpan.FromSeconds(1); 
    });
    
    // FHIR HTTP Client registration
    builder.Services.AddScoped<IFhirHttpClientFactory, FhirHttpClientFactory>();

    
    var jitterBackoff = Backoff.DecorrelatedJitterBackoffV2(
        medianFirstRetryDelay: TimeSpan.FromSeconds(1), 
        retryCount: 12);
    
    IAsyncPolicy<HttpResponseMessage> retryPolicy = HttpPolicyExtensions
        .HandleTransientHttpError() // HttpRequestException, 5XX and 408
        .WaitAndRetryAsync(sleepDurations: jitterBackoff);
    
    builder.Services.AddHttpClient(FhirHttpClientFactory.HttpClientName)
        .AddPolicyHandler(retryPolicy);


    // Services  --------------------------------------------------------------------------------------
    builder.Services.AddSingleton<IOperationOutcomeSupport, OperationOutcomeSupport>();
    builder.Services.AddSingleton<IFhirJsonSerializersOptions, FhirJsonSerializersOptions>();
    builder.Services.AddSingleton<IFhirSerializationSupport, FhirSerializationSupport>();
    builder.Services.AddSingleton<IFhirDeSerializationSupport, FhirDeSerializationSupport>();

    builder.Services.AddSingleton<FhirResourceTypeSupport>();
    builder.Services.AddSingleton<IFhirResourceTypeSupport>(x => x.GetRequiredService<FhirResourceTypeSupport>());
    builder.Services.AddSingleton<IFhirResourceNameSupport>(x => x.GetRequiredService<FhirResourceTypeSupport>());

    builder.Services.AddScoped<IFhirBundleServiceFactory, FhirBundleServiceFactory>();
    builder.Services.AddScoped<FhirBatchService>()
        .AddScoped<IFhirBundleService, FhirBatchService>(s => s.GetRequiredService<FhirBatchService>());
    builder.Services.AddScoped<FhirTransactionService>()
        .AddScoped<IFhirBundleService, FhirTransactionService>(s => s.GetRequiredService<FhirTransactionService>());

    builder.Services.AddScoped<IFhirTransactionDeleteService, FhirTransactionDeleteService>();
    builder.Services.AddScoped<IFhirTransactionPostService, FhirTransactionPostService>();
    builder.Services.AddScoped<IFhirTransactionPutService, FhirTransactionPutService>();
    builder.Services.AddScoped<IFhirTransactionGetService, FhirTransactionGetService>();
    builder.Services.AddScoped<IFhirBundleCommonSupport, FhirBundleCommonSupport>();
    builder.Services.AddScoped<IFhirNarrativeSupport, FhirNarrativeSupport>();

    builder.Services.AddScoped<IFhirUriFactory, FhirUriFactory>();
    builder.Services.AddSingleton<IFhirResponseHttpHeaderSupport, FhirResponseHttpHeaderSupport>();
    builder.Services.AddTransient<IFhirRequestHttpHeaderSupport, FhirRequestHttpHeaderSupport>();
    builder.Services.AddSingleton<IFhirDateTimeFactory, FhirDateTimeFactory>();
    builder.Services.AddSingleton<IDateTimeIndexSupport, DateTimeIndexSupport>();
    builder.Services.AddSingleton<IFhirDateTimeSupport, FhirDateTimeSupport>();
    builder.Services.AddSingleton<IQuantityComparatorMap, QuantityComparatorMap>();
    builder.Services.AddSingleton<IPreferredReturnTypeService, PreferredReturnTypeService>();

    builder.Services.AddScoped<IPaginationSupport, PaginationSupport>();
    builder.Services.AddScoped<IFhirBundleCreationSupport, FhirBundleCreationCreationSupport>();
    builder.Services.AddScoped<IFhirPathResolve, FhirPathResolve>();
    builder.Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

    // RepositoryEvent Services-----------------------
    builder.Services.AddScoped<IRepositoryEventCollector, RepositoryEventCollector>();
    builder.Services.AddSingleton<IRepositoryEventChannel, RepositoryEventChannel>();
    
    // FHIR Subscriptions & Notification
    builder.Services.AddScoped<IFhirNotificationService, FhirNotificationService>();
    builder.Services.AddScoped<IFhirSubscriptionService, FhirSubscriptionService>();
    builder.Services.AddScoped<IFhirSubscriptionRepository, FhirSubscriptionRepository>();
    
    // Fhir Batch & Transaction bundle services ----------------------
    builder.Services.AddScoped<IMetaDataService, MetaDataService>();

    // Endpoint Policy Services -------------------------------------
    builder.Services.AddSingleton<IEndpointPolicyService, EndpointPolicyService>();

    // Validators ---------------------------------------------------
    builder.Services.AddScoped<IValidator, Validator>();
    builder.Services.AddSingleton<ICommonRequestValidation, CommonRequestValidation>();
    builder.Services.AddScoped<IValidatorBase<FhirCreateRequest>, CreateRequestValidator>();
    builder.Services.AddScoped<IValidatorBase<FhirReadRequest>, ReadRequestValidator>();
    builder.Services.AddScoped<IValidatorBase<FhirUpdateRequest>, UpdateRequestValidator>();
    builder.Services.AddScoped<IValidatorBase<FhirDeleteRequest>, DeleteRequestValidator>();
    builder.Services.AddScoped<IValidatorBase<FhirSearchRequest>, SearchRequestValidator>();
    builder.Services.AddScoped<IValidatorBase<FhirBatchOrTransactionRequest>, BatchOrTransactionRequestValidator>();
    builder.Services.AddScoped<IValidatorBase<FhirConditionalCreateRequest>, ConditionalCreateRequestValidator>();
    builder.Services.AddScoped<IValidatorBase<FhirConditionalDeleteRequest>, ConditionalDeleteRequestValidator>();
    builder.Services.AddScoped<IValidatorBase<FhirConditionalUpdateRequest>, ConditionalUpdateRequestValidator>();
    builder.Services.AddScoped<IValidatorBase<FhirHistoryInstanceLevelRequest>, HistoryInstanceLevelRequestValidator>();
    builder.Services.AddScoped<IValidatorBase<FhirHistorySystemLevelRequest>, HistorySystemLevelRequestValidator>();
    builder.Services.AddScoped<IValidatorBase<FhirHistoryTypeLevelRequest>, HistoryTypeLevelRequestValidator>();
    builder.Services.AddScoped<IValidatorBase<FhirVersionReadRequest>, VersionReadRequestValidator>();
    builder.Services.AddScoped<IValidatorBase<SearchQueryServiceOutcomeAndHeaders>, SearchQueryValidator>();
    builder.Services.AddScoped<IValidatorBase<FhirMetaDataRequest>, MetaDataRequestValidator>();

    // Caching ---------------------------
    builder.Services.AddDistributedMemoryCache();
    builder.Services.AddScoped<ISearchParameterCache, SearchParameterCache>();
    builder.Services.AddScoped<IServiceBaseUrlCache, ServiceBaseUrlCache>();
    builder.Services.AddScoped<IMetaDataCache, MetaDataCache>();
    builder.Services.AddScoped<IActiveSubscriptionCache, ActiveSubscriptionCache>();
    builder.Services.AddScoped<IPrimaryServiceBaseUrlService, PrimaryServiceBaseUrlService>();
    
    // FHIR Api Handlers ---------------------------
    builder.Services.AddScoped<IFhirDeleteHandler, FhirDeleteHandler>();
    builder.Services.AddScoped<IFhirConditionalDeleteHandler, FhirConditionalDeleteHandler>();
    builder.Services.AddScoped<IFhirCreateHandler, FhirCreateHandler>();
    builder.Services.AddScoped<IFhirConditionalUpdateHandler, FhirConditionalUpdateHandler>();
    builder.Services.AddScoped<IFhirUpdateHandler, FhirUpdateHandler>();
    builder.Services.AddScoped<IFhirReadHandler, FhirReadHandler>();
    builder.Services.AddScoped<IFhirSearchHandler, FhirSearchHandler>();

    // MediatR pipeline (Loads all MediatR Handlers and behaviors) --------------------------- 
    builder.Services.AddMediatR(config =>
    {
        config.RegisterServicesFromAssemblyContaining<IApplicationLayerAssemblyMarker>()
            .AddOpenBehavior(typeof(LoggingBehavior<,>))
            .AddOpenBehavior(typeof(CorrelationBehavior<,>))
            .AddOpenBehavior(typeof(DatabaseTransactionBehavior<,>));
    });

    //Database Transactions ----------------------------------------------------------------
    builder.Services.AddScoped<IDatabaseTransactionFactory, DatabaseTransactionFactory>();
    builder.Services.AddScoped<IDatabaseTransaction, DatabaseTransaction>();

    // Search Query ------------------------------------------------------------------------
    builder.Services.AddScoped<IDatabasePendingMigrations, DatabasePendingMigrations>();
    builder.Services.AddScoped<ISearchQueryService, SearchQueryService>();
    builder.Services.AddScoped<ISearchQueryFactory, SearchQueryFactory>();
    builder.Services.AddScoped<IChainQueryProcessingService, ChainQueryProcessingService>();
    builder.Services.AddTransient<IFhirQuery, FhirQuery>();

    // Indexing ---------------------------
    builder.Services.AddScoped<IIndexer, Indexer>();
    builder.Services.AddScoped<IReferenceSetter, ReferenceSetter>();
    builder.Services.AddScoped<IStringSetter, StringSetter>();
    builder.Services.AddScoped<IDateTimeSetter, DateTimeSetter>();
    builder.Services.AddScoped<IQuantitySetter, QuantitySetter>();
    builder.Services.AddScoped<ITokenSetter, TokenSetter>();
    builder.Services.AddScoped<INumberSetter, NumberSetter>();
    builder.Services.AddScoped<IUriSetter, UriSetter>();

    // Database Queries --------------------------------------------------------------------------------------

    // ResourceStore ----------------------
    builder.Services.AddScoped<IResourceStoreAdd, ResourceStoreAdd>();
    builder.Services.AddScoped<IResourceStoreHistoryAdd, ResourceStoreHistoryAdd>();
    builder.Services.AddScoped<IResourceStoreUpdate, ResourceStoreUpdate>();
    builder.Services.AddScoped<IResourceStoreGetByResourceId, ResourceStoreGetByResourceId>();
    builder.Services.AddScoped<IResourceStoreGetByResourceStoreId, ResourceStoreGetByResourceStoreId>();
    builder.Services.AddScoped<IResourceStoreGetByVersionId, ResourceStoreGetByVersionId>();
    builder.Services.AddScoped<IResourceStoreGetHistoryByResourceId, ResourceStoreGetHistoryByResourceId>();
    builder.Services.AddScoped<IResourceStoreGetHistory, ResourceStoreGetHistory>();
    builder.Services.AddScoped<IResourceStoreGetHistoryByResourceType, ResourceStoreGetHistoryByResourceType>();
    builder.Services.AddScoped<IResourceStoreGetForUpdateByResourceId, ResourceStoreGetForUpdateByResourceId>();
    builder.Services.AddScoped<IResourceIncludesService, ResourceIncludesService>();
    builder.Services.AddScoped<IResourceStoreSearch, ResourceStoreSearch>();

    // SearchParameterStore ----------------------
    builder.Services.AddScoped<ISearchParameterGetByBaseResourceType, SearchParameterGetByBaseResourceType>();
    builder.Services
        .AddScoped<ISearchParameterMetaDataGetByBaseResourceType, SearchParameterMetaDataGetByBaseResourceType>();

    // ServiceBaseUrl ----------------------
    builder.Services.AddScoped<IServiceBaseUrlAddByUri, ServiceBaseUrlAddByUri>();
    builder.Services.AddScoped<IServiceBaseUrlUpdate, ServiceBaseUrlUpdate>();
    builder.Services.AddScoped<IServiceBaseUrlUpdateSimultaneous, ServiceBaseUrlUpdateSimultaneous>();
    builder.Services.AddScoped<IServiceBaseUrlGetByUri, ServiceBaseUrlGetByUri>();
    builder.Services.AddScoped<IServiceBaseUrlGetPrimary, ServiceBaseUrlGetPrimary>();
    
    // Predicate Factories -------------------------------------
    builder.Services.AddScoped<ISearchPredicateFactory, SearchSearchPredicateFactory>();
    builder.Services.AddScoped<IChainedPredicateFactory, ChainedPredicateFactory>();
    builder.Services.AddScoped<IHasPredicateFactory, HasPredicateFactory>();
    builder.Services.AddScoped<IResourceStorePredicateFactory, ResourceStorePredicateFactory>();
    builder.Services.AddScoped<IIndexReferencePredicateFactory, IndexReferencePredicateFactory>();
    builder.Services.AddSingleton<IIndexStringPredicateFactory, IndexStringPredicateFactory>();
    builder.Services.AddSingleton<IIndexTokenPredicateFactory, IndexTokenPredicateFactory>();
    builder.Services.AddSingleton<IIndexNumberPredicateFactory, IndexNumberPredicateFactory>();
    builder.Services.AddSingleton<IIndexDateTimePredicateFactory, IndexDateTimePredicateFactory>();
    builder.Services.AddSingleton<IIndexQuantityPredicateFactory, IndexQuantityPredicateFactory>();
    builder.Services.AddSingleton<IIndexUriPredicateFactory, IndexUriPredicateFactory>();
    builder.Services.AddSingleton<IIndexCompositePredicateFactory, IndexCompositePredicateFactory>();
    
    // Tenant and Tenanted db queries and repositories 
    builder.Services.AddScoped<ITenantService, TenantService>();
    builder.Services.AddScoped<IGetHttpContextRequestPath, GetHttpContextRequestPath>();
    builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
    
    builder.Services.AddTransient<IPyroDbContextFactory, PyroDbContextFactory>();
    builder.Services.AddTransient<IServiceBaseUrlOnStartupRepository, ServiceBaselUrlOnStartupRepository>();
    
    

    // Database Setup ----------------------
    builder.Services.AddDbContext<PyroDbContext>((services, optionsBuilder) =>
        {
            var tenantService = services.GetRequiredService<ITenantService>();
            
            optionsBuilder
                .UseSqlServer(builder.Configuration.GetConnectionString(tenantService.GetScopedTenant().SqlConnectionStringCode))
                .EnableSensitiveDataLogging(builder.Environment.IsDevelopment());
        },
        contextLifetime: ServiceLifetime.Scoped, //Scope for the PyroDbContext.
        optionsLifetime: ServiceLifetime.Scoped //Scope for the DbContextOptions configuration.   
    );
    
    // CORS
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(
            policy => { policy.AllowAnyOrigin(); });
    });

    // Request Decompression
    builder.Services.AddRequestDecompression();

    // Response Compression
    builder.Services.AddResponseCompression(options =>
    {
        options.EnableForHttps = true;
        options.Providers.Add<BrotliCompressionProvider>();
        options.Providers.Add<GzipCompressionProvider>();
    });

    builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
    {
        options.Level = CompressionLevel.Fastest;
    });

    builder.Services.Configure<GzipCompressionProviderOptions>(options =>
    {
        options.Level = CompressionLevel.SmallestSize;
    });

    builder.Services.AddControllers();
    builder.Services.AddMvcCore(config =>
    {
        config.InputFormatters.Clear();
        //config.InputFormatters.Add(new XmlFhirInputFormatter());
        config.InputFormatters.Add(new JsonFhirInputFormatter());

        config.OutputFormatters.Clear();
        //config.OutputFormatters.Add(new XmlFhirOutputFormatter());
        config.OutputFormatters.Add(new JsonFhirOutputFormatter());


        // And include our custom content negotiator filter to handle the _format parameter
        // (from the FHIR spec:  http://hl7.org/fhir/http.html#mime-type )
        // https://docs.microsoft.com/en-us/aspnet/core/mvc/controllers/filters
        config.Filters.Add(new FhirFormatParameterFilter());
    });

    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseRequestDecompression();
    app.UseResponseCompression();

    app.UseMiddleware(typeof(ErrorHandlingMiddleware));

    app.UseHttpsRedirection();

    app.UseCors();

    app.UseAuthorization();

    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Unhandled exception");
}
finally
{
    Log.Information($"Shut down complete");
    Log.CloseAndFlush();
}