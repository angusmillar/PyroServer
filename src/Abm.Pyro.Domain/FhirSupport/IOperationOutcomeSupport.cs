using Hl7.Fhir.Model;

namespace Abm.Pyro.Domain.FhirSupport;

public interface IOperationOutcomeSupport
{
  OperationOutcome GetFatal(string[] messageList);
  OperationOutcome GetFatal(string[]? messageList, OperationOutcome? operationOutcome);
  OperationOutcome GetError(string[] messageList);
  OperationOutcome GetError(string[]? messageList, OperationOutcome? operationOutcome);
  OperationOutcome GetWarning(string[] messageList);
  OperationOutcome GetWarning(string[]? messageList, OperationOutcome? operationOutcome);
  OperationOutcome GetInformation(string[] messageList);
  OperationOutcome GetInformation(string[]? messageList, OperationOutcome? operationOutcome);
  public OperationOutcome MergeOperationOutcomeList(IEnumerable<OperationOutcome> operationOutcomeList);
}
