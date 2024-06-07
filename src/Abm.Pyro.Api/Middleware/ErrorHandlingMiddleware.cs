using System.Net;
using Hl7.Fhir.Serialization;
using System.Diagnostics;
using Hl7.Fhir.Model;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Exceptions;
using Abm.Pyro.Domain.FhirSupport;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Api.Middleware;

public class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger, IOperationOutcomeSupport operationOutcomeSupport)
{
  public async Task Invoke(HttpContext context /* other dependencies */)
  {
    try
    {
      await next(context);
    }
    catch (FhirException fhirException)
    {
      await HandleFhirException(fhirException, context);
    }
    catch (Exception exception) 
    {
      await HandleExceptionAsync(context, exception, logger);
    }
  }

  private Task HandleFhirException(FhirException fhirException, HttpContext context)
  {
    logger.LogError(fhirException, "FhirException has been thrown");

    var acceptHeader = context.Request.Headers.SingleOrDefault(x =>
      x.Key.ToLower(System.Globalization.CultureInfo.CurrentCulture) == "accept");
    
    FhirFormatType acceptFormatType = ContentFormatters.FhirMediaType.GetFhirFormatTypeFromAcceptHeader(
      acceptHeader.Value.First());

    OperationOutcome? operationOutcomeResult = fhirException switch {
      FhirFatalException fatalExec => operationOutcomeSupport.GetFatal(fatalExec.MessageList, fatalExec.OperationOutcome),
      FhirErrorException errorExec => operationOutcomeSupport.GetError(errorExec.MessageList, errorExec.OperationOutcome),
      FhirWarnException warnExec => operationOutcomeSupport.GetWarning(warnExec.MessageList, warnExec.OperationOutcome),
      FhirInfoException infoExec => operationOutcomeSupport.GetInformation(infoExec.MessageList, infoExec.OperationOutcome),
      _ => operationOutcomeSupport.GetFatal(new string[] { $"Unexpected FhirException type encountered of : {fhirException.GetType().FullName}" })
    };

    context.Response.StatusCode = (int)fhirException.HttpStatusCode;
    context.Response.ContentType = ContentFormatters.FhirMediaType.GetMediaTypeHeaderValue(operationOutcomeResult.GetType(), acceptFormatType).Value;
    
    switch (acceptFormatType)
    {
      case FhirFormatType.Xml:
      {
        FhirXmlSerializer fhirXmlSerializer = new FhirXmlSerializer(new SerializerSettings() { Pretty = true });
        return context.Response.WriteAsync(fhirXmlSerializer.SerializeToString(operationOutcomeResult));
      }
      case FhirFormatType.Json:
      {
        FhirJsonSerializer fhirJsonSerializer = new FhirJsonSerializer(new SerializerSettings() { Pretty = true });
        return context.Response.WriteAsync(fhirJsonSerializer.SerializeToString(operationOutcomeResult));
      }
      default:
        logger.LogError("Unexpected FhirFormatType type encountered of : {AcceptFormatType}",
                        acceptFormatType.GetType().FullName);
        throw new ApplicationException(
          $"Unexpected FhirFormatType type encountered of : {acceptFormatType.GetType().FullName}");
    }
  }

  private Task HandleExceptionAsync(HttpContext context, Exception exec, ILogger<ErrorHandlingMiddleware> logger)
  {
    string errorGuid = GuidSupport.NewFhirGuid();
    string usersErrorMessage = $"An unhanded exception has been thrown. To protect data privacy the exception information has been written to the application log with the error log identifier: {errorGuid}";
    if (Debugger.IsAttached)
    {
      usersErrorMessage =  $"{System.Text.Encodings.Web.HtmlEncoder.Default.Encode(exec.ToString())} ->  Server Error log identifier: {errorGuid}";
    }
    
    logger.LogError(exec, "Error log identifier: {ErrorGuid}", errorGuid);
    OperationOutcome operationOutcomeResult = operationOutcomeSupport.GetFatal(new string[] { usersErrorMessage });
    context.Response.ContentType = ContentFormatters.FhirMediaType.GetMediaTypeHeaderValue(operationOutcomeResult.GetType(), FhirFormatType.Xml).Value;
    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
    FhirXmlSerializer fhirXmlSerializer = new FhirXmlSerializer(new SerializerSettings() { Pretty = true });
    return context.Response.WriteAsync(fhirXmlSerializer.SerializeToString(operationOutcomeResult));
  }
  
}
