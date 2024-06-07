using BenchmarkDotNet.Attributes;
using Hl7.Fhir.Model;
using Abm.Pyro.Domain.Extensions;

namespace Abm.Pyro.Domain.Benchmark.Extensions;


[MemoryDiagnoser]
public class ResourceExtensionsBenchmark
{
    
    // | Method                   | Mean     | Error     | StdDev    | Ratio | Gen0   | Allocated | Alloc Ratio |
    // |------------------------- |---------:|----------:|----------:|------:|-------:|----------:|------------:|
    // | FhirUriFactoryRemoteBase | 7.807 us | 0.6421 us | 0.6006 us |  1.00 | 0.8240 |   5.06 KB |        1.00 |

    
    private Bundle? Bundle;
    

    [GlobalSetup]
    public void SetupData()
    {
        Bundle = GetTestBundle();
    }
    
    [Benchmark(Baseline = true)]
    public void FhirUriFactoryRemoteBase()
    {
        var resourceReferenceList = Bundle.AllReferenceList();
    }
    
    private static Bundle GetTestBundle()
    {
        var bundle = new Bundle();
        bundle.Entry = new List<Bundle.EntryComponent>();

        Patient patient = new Patient();
        patient.Id = "patient-one";
        patient.ManagingOrganization = new ResourceReference(reference: "Organization/org-one");
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
            FullUrl = "Practitioner/practitioner-one",
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
