using System.Collections.Generic;
using System.Linq;
using System.Net;
using Hl7.Fhir.Model;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Exceptions;
using Abm.Pyro.Domain.FhirSupport;
using Xunit;

namespace Abm.Pyro.Domain.Test.FhirSupport;

public class OperationOutcomeSupportTest
{
    //Setup
    protected OperationOutcomeSupportTest()
    {
    }

    public class GetError : OperationOutcomeSupportTest
    {
        [Fact]
        public void UusStringArray_ReturnsOperationOutcome()
        {
            //Arrange
            var target = new OperationOutcomeSupport();

            string[] messageList = { "Line one", "Line two" };

            //Act
            OperationOutcome result = target.GetError(messageList);

            //Assert
            Assert.Equal(OperationOutcome.IssueSeverity.Error, result.Issue.First().Severity);
            Assert.Equal(OperationOutcome.IssueType.Processing, result.Issue.First().Code);
            Assert.Equal(messageList.First(), result.Issue.First().Details.Text);
            
            Assert.Equal(OperationOutcome.IssueSeverity.Error, result.Issue.Last().Severity);
            Assert.Equal(OperationOutcome.IssueType.Processing, result.Issue.Last().Code);
            Assert.Equal(messageList.Last(), result.Issue.Last().Details.Text);
            
        }
        
        [Fact]
        public void MergeMessageListAndOtherOperationOutcome_ReturnsOperationOutcome()
        {
            //Arrange
            var target = new OperationOutcomeSupport();

            
            string[] messageList = { "Line one A", "Line two A" };
            var operationOutcome = new OperationOutcome()
            {
                Id = "100",
                Issue = new List<OperationOutcome.IssueComponent>()
                {
                    new OperationOutcome.IssueComponent()
                    {
                        Severity = OperationOutcome.IssueSeverity.Fatal,
                        Code = OperationOutcome.IssueType.Conflict,
                        Details = new CodeableConcept()
                        {
                            Text = "Line One B"
                        }
                    }
                }
            };

            //Act
            OperationOutcome result = target.GetError(messageList: messageList, operationOutcome: operationOutcome);

            //Assert
            Assert.Equal(OperationOutcome.IssueSeverity.Error, result.Issue.First().Severity);
            Assert.Equal(OperationOutcome.IssueType.Processing, result.Issue.First().Code);
            Assert.Equal(messageList.First(), result.Issue.First().Details.Text);
            
            Assert.Equal(OperationOutcome.IssueSeverity.Error, result.Issue[1].Severity);
            Assert.Equal(OperationOutcome.IssueType.Processing, result.Issue[1].Code);
            Assert.Equal(messageList[1], result.Issue[1].Details.Text);
            
            Assert.Equal(OperationOutcome.IssueSeverity.Fatal, result.Issue.Last().Severity);
            Assert.Equal(OperationOutcome.IssueType.Conflict, result.Issue.Last().Code);
            Assert.Equal(operationOutcome.Issue.Last().Details.Text, result.Issue.Last().Details.Text);
            
        }
        
        
    }
    
    public class GetFatal : OperationOutcomeSupportTest
    {
        [Fact]
        public void UusStringArray_ReturnsOperationOutcome()
        {
            //Arrange
            var target = new OperationOutcomeSupport();

            string[] messageList = { "Line one", "Line two" };

            //Act
            OperationOutcome result = target.GetFatal(messageList);

            //Assert
            Assert.Equal(OperationOutcome.IssueSeverity.Fatal, result.Issue.First().Severity);
            Assert.Equal(OperationOutcome.IssueType.Exception, result.Issue.First().Code);
            Assert.Equal(messageList.First(), result.Issue.First().Details.Text);
            
            Assert.Equal(OperationOutcome.IssueSeverity.Fatal, result.Issue.Last().Severity);
            Assert.Equal(OperationOutcome.IssueType.Exception, result.Issue.Last().Code);
            Assert.Equal(messageList.Last(), result.Issue.Last().Details.Text);
            
        }
        
        [Fact]
        public void MergeMessageListAndOtherOperationOutcome_ReturnsOperationOutcome()
        {
            //Arrange
            var target = new OperationOutcomeSupport();

            
            string[] messageList = { "Line one A", "Line two A" };
            var operationOutcome = new OperationOutcome()
            {
                Id = "100",
                Issue = new List<OperationOutcome.IssueComponent>()
                {
                    new OperationOutcome.IssueComponent()
                    {
                        Severity = OperationOutcome.IssueSeverity.Warning,
                        Code = OperationOutcome.IssueType.Deleted,
                        Details = new CodeableConcept()
                        {
                            Text = "Line One B"
                        }
                    }
                }
            };

            //Act
            OperationOutcome result = target.GetFatal(messageList: messageList, operationOutcome: operationOutcome);

            //Assert
            Assert.Equal(OperationOutcome.IssueSeverity.Fatal, result.Issue.First().Severity);
            Assert.Equal(OperationOutcome.IssueType.Exception, result.Issue.First().Code);
            Assert.Equal(messageList.First(), result.Issue.First().Details.Text);
            
            Assert.Equal(OperationOutcome.IssueSeverity.Fatal, result.Issue[1].Severity);
            Assert.Equal(OperationOutcome.IssueType.Exception, result.Issue[1].Code);
            Assert.Equal(messageList[1], result.Issue[1].Details.Text);
            
            Assert.Equal(operationOutcome.Issue.First().Severity, result.Issue.Last().Severity);
            Assert.Equal(operationOutcome.Issue.First().Code, result.Issue.Last().Code);
            Assert.Equal(operationOutcome.Issue.Last().Details.Text, result.Issue.Last().Details.Text);
            
        }
        
        
    }
    
    public class GetInformation : OperationOutcomeSupportTest
    {
        [Fact]
        public void UusStringArray_ReturnsOperationOutcome()
        {
            //Arrange
            var target = new OperationOutcomeSupport();

            string[] messageList = { "Line one", "Line two" };

            //Act
            OperationOutcome result = target.GetInformation(messageList);

            //Assert
            Assert.Equal(OperationOutcome.IssueSeverity.Information, result.Issue.First().Severity);
            Assert.Equal(OperationOutcome.IssueType.Informational, result.Issue.First().Code);
            Assert.Equal(messageList.First(), result.Issue.First().Details.Text);
            
            Assert.Equal(OperationOutcome.IssueSeverity.Information, result.Issue.Last().Severity);
            Assert.Equal(OperationOutcome.IssueType.Informational, result.Issue.Last().Code);
            Assert.Equal(messageList.Last(), result.Issue.Last().Details.Text);
            
        }
        
        [Fact]
        public void MergeMessageListAndOtherOperationOutcome_ReturnsOperationOutcome()
        {
            //Arrange
            var target = new OperationOutcomeSupport();

            
            string[] messageList = { "Line one A", "Line two A" };
            var operationOutcome = new OperationOutcome()
            {
                Id = "100",
                Issue = new List<OperationOutcome.IssueComponent>()
                {
                    new OperationOutcome.IssueComponent()
                    {
                        Severity = OperationOutcome.IssueSeverity.Warning,
                        Code = OperationOutcome.IssueType.Deleted,
                        Details = new CodeableConcept()
                        {
                            Text = "Line One B"
                        }
                    }
                }
            };

            //Act
            OperationOutcome result = target.GetInformation(messageList: messageList, operationOutcome: operationOutcome);

            //Assert
            Assert.Equal(OperationOutcome.IssueSeverity.Information, result.Issue.First().Severity);
            Assert.Equal(OperationOutcome.IssueType.Informational, result.Issue.First().Code);
            Assert.Equal(messageList.First(), result.Issue.First().Details.Text);
            
            Assert.Equal(OperationOutcome.IssueSeverity.Information, result.Issue[1].Severity);
            Assert.Equal(OperationOutcome.IssueType.Informational, result.Issue[1].Code);
            Assert.Equal(messageList[1], result.Issue[1].Details.Text);
            
            Assert.Equal(operationOutcome.Issue.First().Severity, result.Issue.Last().Severity);
            Assert.Equal(operationOutcome.Issue.First().Code, result.Issue.Last().Code);
            Assert.Equal(operationOutcome.Issue.Last().Details.Text, result.Issue.Last().Details.Text);
            
        }
        
        
    }
    
    public class GetWarning : OperationOutcomeSupportTest
    {
        [Fact]
        public void UusStringArray_ReturnsOperationOutcome()
        {
            //Arrange
            var target = new OperationOutcomeSupport();

            string[] messageList = { "Line one", "Line two" };

            //Act
            OperationOutcome result = target.GetWarning(messageList);

            //Assert
            Assert.Equal(OperationOutcome.IssueSeverity.Warning, result.Issue.First().Severity);
            Assert.Equal(OperationOutcome.IssueType.Informational, result.Issue.First().Code);
            Assert.Equal(messageList.First(), result.Issue.First().Details.Text);
            
            Assert.Equal(OperationOutcome.IssueSeverity.Warning, result.Issue.Last().Severity);
            Assert.Equal(OperationOutcome.IssueType.Informational, result.Issue.Last().Code);
            Assert.Equal(messageList.Last(), result.Issue.Last().Details.Text);
            
        }
        
        [Fact]
        public void MergeMessageListAndOtherOperationOutcome_ReturnsOperationOutcome()
        {
            //Arrange
            var target = new OperationOutcomeSupport();

            
            string[] messageList = { "Line one A", "Line two A" };
            var operationOutcome = new OperationOutcome()
            {
                Id = "100",
                Issue = new List<OperationOutcome.IssueComponent>()
                {
                    new OperationOutcome.IssueComponent()
                    {
                        Severity = OperationOutcome.IssueSeverity.Fatal,
                        Code = OperationOutcome.IssueType.Deleted,
                        Details = new CodeableConcept()
                        {
                            Text = "Line One B"
                        }
                    }
                }
            };

            //Act
            OperationOutcome result = target.GetWarning(messageList: messageList, operationOutcome: operationOutcome);

            //Assert
            Assert.Equal(OperationOutcome.IssueSeverity.Warning, result.Issue.First().Severity);
            Assert.Equal(OperationOutcome.IssueType.Informational, result.Issue.First().Code);
            Assert.Equal(messageList.First(), result.Issue.First().Details.Text);
            
            Assert.Equal(OperationOutcome.IssueSeverity.Warning, result.Issue[1].Severity);
            Assert.Equal(OperationOutcome.IssueType.Informational, result.Issue[1].Code);
            Assert.Equal(messageList[1], result.Issue[1].Details.Text);
            
            Assert.Equal(operationOutcome.Issue.First().Severity, result.Issue.Last().Severity);
            Assert.Equal(operationOutcome.Issue.First().Code, result.Issue.Last().Code);
            Assert.Equal(operationOutcome.Issue.Last().Details.Text, result.Issue.Last().Details.Text);
            
        }
        
    }
}