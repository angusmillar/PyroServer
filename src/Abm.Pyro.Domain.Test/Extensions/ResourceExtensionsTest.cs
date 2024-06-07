using System.Collections.Generic;
using Hl7.Fhir.Model;
using Abm.Pyro.Domain.Extensions;
using Xunit;

namespace Abm.Pyro.Domain.Test.Extensions;

public class ResourceExtensionsTest
{
    [Fact]
    public void ResourceExtensionFromBundle()
    {
        Bundle bundle = GetTestBundle();
        
        var resourceReferenceList = bundle.AllReferenceList();
        
        Assert.Equal(7 , resourceReferenceList.Count);
        
    }


    private static Bundle GetTestBundle()
    {
        var bundle = new Bundle();
        bundle.Entry = new List<Bundle.EntryComponent>();

        Patient patient = new Patient();
        patient.Id = "patient-one";
        patient.ManagingOrganization = new ResourceReference(reference: "Organization/managing-organization-org");
        patient.Extension = new List<Extension>()
        {
            new Extension(
                url: "https://testExtension.com/testing/only",
                value: new ResourceReference("CareTeam/careTeam-one")),
        };
        patient.ModifierExtension = new List<Extension>()
        {
            new Extension(
                url: "https://testModifierExtension.com/testing/only",
                value: new ResourceReference("Encounter/encounter-one")),
        }; 
        
        bundle.Entry.Add(new Bundle.EntryComponent()
        {
            FullUrl = "Patient/patient-one",
            Resource = patient
        });
        
        
        Practitioner practitioner = new Practitioner();
        practitioner.Id = "practitioner-one";
        practitioner.Qualification = new List<Practitioner.QualificationComponent>()
        {
            new Practitioner.QualificationComponent()
            {
                Issuer = new ResourceReference("Organization/Qualification-org")
            }
        }; 
        
        bundle.Entry.Add(new Bundle.EntryComponent()
        {
            FullUrl = "Practitioner/practitioner-two",
            Resource = practitioner
        });
        
        
        DiagnosticReport diagnosticReport = new DiagnosticReport();
        diagnosticReport.Id = "diagnosticReport-one";
        diagnosticReport.Status = DiagnosticReport.DiagnosticReportStatus.Final;
        diagnosticReport.Code = new CodeableConcept(system: "https://someTest.com/system", code: "Test", display: "testing", text: "Testing");
        diagnosticReport.Result = new List<ResourceReference>()
        {
            new ResourceReference("Observation/observation-one"),
            new ResourceReference("Observation/observation-two"),
            new ResourceReference("Observation/observation-three")
        };
        
        bundle.Entry.Add(new Bundle.EntryComponent()
        {
            FullUrl = "DiagnosticReport/diagnosticReport-one",
            Resource = diagnosticReport
        });

        
        return bundle;

    }
}