using System.Text;
using Hl7.Fhir.Model;

namespace Abm.Pyro.Domain.FhirSupport;

public class OperationOutcomeSupport : IOperationOutcomeSupport
{
    
    public OperationOutcome GetFatal(string[] messageList)
    {
        return GetOperationOutcome(informationMessageList: messageList, operationOutcomeList: new List<OperationOutcome>(),  OperationOutcome.IssueSeverity.Fatal, OperationOutcome.IssueType.Exception);
    }
    
    public OperationOutcome GetFatal(string[]? messageList, OperationOutcome? operationOutcome)
    {
        var operationOutcomeList = new List<OperationOutcome>();
        if (operationOutcome is not null)
        {
            operationOutcomeList.Add(operationOutcome);
        }
        return GetOperationOutcome(informationMessageList: messageList, operationOutcomeList: operationOutcomeList, OperationOutcome.IssueSeverity.Fatal, OperationOutcome.IssueType.Exception);
    }
    
    public OperationOutcome GetError(string[] messageList)
    {
        return GetOperationOutcome(informationMessageList: messageList, operationOutcomeList: new List<OperationOutcome>(), OperationOutcome.IssueSeverity.Error, OperationOutcome.IssueType.Processing);
    }
    
    public OperationOutcome GetError(string[]? messageList, OperationOutcome? operationOutcome)
    {
        var operationOutcomeList = new List<OperationOutcome>();
        if (operationOutcome is not null)
        {
            operationOutcomeList.Add(operationOutcome);
        }
        return GetOperationOutcome(informationMessageList: messageList, operationOutcomeList: operationOutcomeList, OperationOutcome.IssueSeverity.Error, OperationOutcome.IssueType.Processing);
    }
    
    public OperationOutcome GetWarning(string[] messageList)
    {
        return GetOperationOutcome(informationMessageList: messageList, operationOutcomeList: new List<OperationOutcome>(), OperationOutcome.IssueSeverity.Warning, OperationOutcome.IssueType.Informational);
    }
    
    public OperationOutcome GetWarning(string[]? messageList, OperationOutcome? operationOutcome)
    {
        var operationOutcomeList = new List<OperationOutcome>();
        if (operationOutcome is not null)
        {
            operationOutcomeList.Add(operationOutcome);
        }
        return GetOperationOutcome(informationMessageList: messageList, operationOutcomeList: operationOutcomeList, OperationOutcome.IssueSeverity.Warning, OperationOutcome.IssueType.Informational);
    }
    
    public OperationOutcome GetInformation(string[] messageList)
    {
        return GetOperationOutcome(informationMessageList: messageList, operationOutcomeList: new List<OperationOutcome>() , OperationOutcome.IssueSeverity.Information, OperationOutcome.IssueType.Informational);
    }
    
    public OperationOutcome GetInformation(string[]? messageList, OperationOutcome? operationOutcome)
    {
        var operationOutcomeList = new List<OperationOutcome>();
        if (operationOutcome is not null)
        {
            operationOutcomeList.Add(operationOutcome);
        }
        return GetOperationOutcome(informationMessageList: messageList, operationOutcomeList: operationOutcomeList, OperationOutcome.IssueSeverity.Information, OperationOutcome.IssueType.Informational);
    }
    
    private OperationOutcome GetOperationOutcome(
        string[]? informationMessageList, 
        List<OperationOutcome> operationOutcomeList, 
        OperationOutcome.IssueSeverity issueSeverity,
        OperationOutcome.IssueType issueType)
    {
        if ((informationMessageList is null || !informationMessageList.Any()) && !operationOutcomeList.Any())
        {
            throw new NullReferenceException($"{nameof(informationMessageList)} and {nameof(operationOutcomeList)} can not both be null or empty");
        }
        
        var result = new OperationOutcome();
        if (informationMessageList is not null && informationMessageList.Any())
        {
            result.Issue = GetIssueComponentList(messageList: informationMessageList, issueSeverity: issueSeverity, issueType: issueType);
            result.Text = BuildNarrative(result);
        }

        if (!operationOutcomeList.Any())
        {
            return result;
        }
        
        operationOutcomeList.Insert(0, result);
        
        return MergeOperationOutcomeList(operationOutcomeList);
    }

    public OperationOutcome MergeOperationOutcomeList(IEnumerable<OperationOutcome> operationOutcomeList)
    {
        var result = new OperationOutcome();
        if (!operationOutcomeList.Any())
        {
            throw new ApplicationException($"Expected a populated list for : {nameof(operationOutcomeList)}");
        }

        result.Issue = new List<OperationOutcome.IssueComponent>(); 
        foreach (var operationOutcome in operationOutcomeList)
        {
            result.Issue.AddRange(operationOutcome.Issue);
        }

        result.Text = BuildNarrative(operationOutcome: result);
        
        return result;
    }

    public string[] ExtractErrorMessages(OperationOutcome operationOutcome)
    {
        List<string> messages = new List<string>();

        if (operationOutcome.Issue is null)
        {
            return messages.ToArray();
        }
        foreach (var issue in operationOutcome.Issue)
        {
            if (string.IsNullOrWhiteSpace(issue.Details.Text))
            {
                messages.Add(issue.Details.Text);    
            }
        }

        return messages.ToArray();
        
    }
    
    private List<OperationOutcome.IssueComponent> GetIssueComponentList(string[] messageList,
        OperationOutcome.IssueSeverity issueSeverity,
        OperationOutcome.IssueType issueType)
    {
        var result = new List<OperationOutcome.IssueComponent>();

        foreach (var message in messageList)
        {
            result.Add(GetIssueComponent(issueSeverity, issueType, message));
        }

        return result;
    }

    private static OperationOutcome.IssueComponent GetIssueComponent(OperationOutcome.IssueSeverity issueSeverity,
        OperationOutcome.IssueType issueType,
        string errorMsg)
    {
        return new OperationOutcome.IssueComponent
        {
            Severity = issueSeverity,
            Code = issueType,
            Details = new CodeableConcept
            {
                Text = errorMsg
            }
        };
    }

    private Narrative BuildNarrative(OperationOutcome operationOutcome)
    {
        if (operationOutcome.Issue is null)
        {
            return BuildEmptyNarrative();
        }

        if (operationOutcome.Issue.Count == 1)
        {
            return BuildSingleIssueNarrative();
        }
        
        return BuildMultiIssueNarrative();


        Narrative BuildSingleIssueNarrative()
        {
            var stringBuilder = new StringBuilder();
            if (operationOutcome.Issue is null)
            {
                throw new NullReferenceException(nameof(operationOutcome.Issue));
            }
            
            StartRootDiv(stringBuilder);
            if (operationOutcome.Issue.First().Details?.Text is not null)
            {
                AddParagraph(stringBuilder, operationOutcome.Issue.First().Details?.Text);
            }

            EndRootDiv(stringBuilder);
            
            var narrative = GetNarrativeAsGenerated();
            narrative.Div = stringBuilder.ToString();
            return narrative;
        }

        Narrative BuildMultiIssueNarrative()
        {
            var stringBuilder = new StringBuilder();
            StartRootDiv(stringBuilder);
            StartOrderedList(stringBuilder);
            foreach (OperationOutcome.IssueComponent issue in operationOutcome.Issue)
            {
                if (issue.Details.Text is not null)
                {
                    AddListItem(stringBuilder, issue.Details.Text);
                }
            }

            EndOrderedList(stringBuilder);
            EndRootDiv(stringBuilder);
            
            var narrative = GetNarrativeAsGenerated();
            narrative.Div = stringBuilder.ToString();
            return narrative;
        }
    }

    private Narrative BuildEmptyNarrative()
    {
        
        var stringBuilder = new StringBuilder();
        StartRootDiv(stringBuilder);
        EndRootDiv(stringBuilder);
        
        var narrative = GetNarrativeAsGenerated();
        narrative.Div = stringBuilder.ToString();
        return narrative;
    }

    private static Narrative GetNarrativeAsGenerated()
    {
        var narrative = new Narrative();
        narrative.Status = Narrative.NarrativeStatus.Generated;
        return narrative;
    }

    private static void StartOrderedList(StringBuilder narrative)
    {
        const string content = "<ol>";
        narrative.Append(content);
    }

    private static void EndOrderedList(StringBuilder narrative)
    {
        const string content = "</ol>";
        narrative.Append(content);
    }

    private static void EndRootDiv(StringBuilder narrative)
    {
        const string content = "</div>";
        narrative.Append(content);
    }

    private static void StartRootDiv(StringBuilder narrative)
    {
        const string content = "<div xmlns=\"http://www.w3.org/1999/xhtml\">";
        narrative.Append(content);
    }

    private static void AddListItem(StringBuilder narrative,
        string errorMsg)
    {
        const string open = "<li>";
        const string close = "</li>";
        AddElement(narrative: narrative, open: open, close: close, content: errorMsg);
    }

    private static void AddParagraph(StringBuilder narrative,
        string? errorMsg)
    {
        const string open = "<p>";
        const string close = "</p>";
        AddElement(narrative: narrative, open: open, close: close, content: errorMsg ?? string.Empty);
    }
    
    private static void AddElement(StringBuilder narrative, string open, string close, string content)
    {
        narrative.Append($"{open}{System.Web.HttpUtility.HtmlEncode(content)}{close}");
    }
}