namespace Abm.Pyro.Api.Test.Support;

public static class RiskAssessmentBuilder
{
    public static Hl7.Fhir.Model.RiskAssessment Build(decimal? probability = null, string? subjectPatientId = null)
    {
        var riskAssessment = new Hl7.Fhir.Model.RiskAssessment
        {
            Status = Hl7.Fhir.Model.ObservationStatus.Final,
            Subject = new Hl7.Fhir.Model.ResourceReference($"Patient/{subjectPatientId}")
        };

        if (probability.HasValue)
        {
            riskAssessment.Prediction =
            [
                new Hl7.Fhir.Model.RiskAssessment.PredictionComponent
                {
                    Probability = new Hl7.Fhir.Model.FhirDecimal(probability.Value)
                }
            ];
        }

        return riskAssessment;
    }
}
