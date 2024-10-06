using System;
using System.Collections.Generic;
using Hl7.Fhir.Model;
using Date = Hl7.Fhir.Model.Date;
using Quantity = Hl7.Fhir.Model.Quantity;

namespace Abm.Pyro.Domain.Test.Factories;

public static class TestResourceFactory
{
    public static class PatientResource
    {
        public static Patient GetDonaldDuck(ResourceReference? managingOrganizationResourceReference = null)
        {
            return new Patient()
            {
                Id = "donald-duck-patent-id",
                Meta = new Meta()
                {
                    LastUpdated = DateTimeOffset.Now,
                    VersionId = "1"
                },
                Active = true, 
                Identifier = new List<Identifier>()
                {
                    new Identifier()
                    {
                        Type = new CodeableConcept("http://terminology.hl7.org/CodeSystem/v2-0203", "MC", "Medicare Number"),
                        System = "http://ns.electronichealth.net.au/id/medicare-number",
                        Value = "21560052752"
                    },
                    new Identifier()
                    {
                    Type = new CodeableConcept("http://terminology.hl7.org/CodeSystem/v2-0203", "NI", "IHI"),
                    System = "http://ns.electronichealth.net.au/id/hi/ihi/1.0",
                    Value = "8003608808447304"
                }
                },
                Name = new List<HumanName>()
                {
                    new HumanName()
                    {
                        Family = "Duck",
                        Given = new []{ "Donald"}
                    }
                },
                Gender = AdministrativeGender.Male,
                BirthDateElement = new Date(1973, 09, 30),
                Address = new List<Address>()
                {
                    new Address()
                    {
                        Use = Address.AddressUse.Home,
                        Text = "20 Gabrielle Place, Manly West 4179, QLD, Australia",
                        Line = new []{"19 Gabrielle Place"},
                        City = "Manly West",
                        State = "QLD",
                        PostalCode = "4179",
                        Country = "AUS" 
                    }
                },
                Telecom = new List<ContactPoint>()
                {
                    new ContactPoint()
                    {
                        System = ContactPoint.ContactPointSystem.Phone,
                        Value = "+61481059999",
                        Use = ContactPoint.ContactPointUse.Home,
                    },
                    new ContactPoint()
                    {
                        System = ContactPoint.ContactPointSystem.Email,
                        Value = "angusmillar@acmehealth.com.au",
                        Use = ContactPoint.ContactPointUse.Work,
                    }
                },
                ManagingOrganization = managingOrganizationResourceReference
            };
        }
    }
    
    public static class OrganizationResource
    {
        public static Organization GetAcmeOrganization()
        {
            var acmeOrganization = new Organization()
            {
                Id = "org-acme",
                Meta = new Meta()
                {
                    VersionId = "1"
                },
                Name = "Acme Healthcare"
            };
            return acmeOrganization;
        }
    }
    
    public static class ObservationResource
    {
        public static Observation GetHemoglobinObservation(ResourceReference? patientResourceReference = null)
        {
            var observationResource = new Observation()
            {
                Id = "obs-hb-id",
                Meta = new Meta()
                {
                    VersionId = "1"
                },
                Status = ObservationStatus.Final, 
                Issued = new DateTimeOffset(2023, 10, 23, 10, 30, 25, TimeSpan.FromHours(10)),
                Subject = patientResourceReference,
                Effective = new Hl7.Fhir.Model.FhirDateTime(new DateTimeOffset(2023, 10, 23, 10, 00, 00, TimeSpan.FromHours(10))),
                Code = new CodeableConcept("http://loinc.org", "718-7", "Hemoglobin [Mass/volume] in Blood", "Hemoglobin [Mass/volume] in Blood"),
                Value = new Quantity(7.2m, "g/dl", system: "http://unitsofmeasure.org") { Code = "g/dL"},
                ReferenceRange = new List<Observation.ReferenceRangeComponent>()
                {
                    new Observation.ReferenceRangeComponent()
                    {
                        Low = new Quantity(7.5m, "g/dl", system: "http://unitsofmeasure.org") { Code = "g/dL"},
                        High = new Quantity(10m, "g/dl", system: "http://unitsofmeasure.org") { Code = "g/dL"},
                    }
                },
                Interpretation = new List<CodeableConcept>()
                {
                    new CodeableConcept(system: "http://terminology.hl7.org/CodeSystem/v3-ObservationInterpretation", code: "L", display: "Low", text: "Low")
                }
            };
            return observationResource;
        }
    }
    
    public static class SubscriptionResource
    {
        public static Subscription GetSubscription()
        {
            var observationResource = new Subscription()
            {
                Id = "obs-hb-id",
                Meta = new Meta()
                {
                    VersionId = "1"
                },
                Status = Subscription.SubscriptionStatus.Requested, 
                Criteria = "Patient?family=testing",
                
                Channel = new Subscription.ChannelComponent()
                {
                    Type = Subscription.SubscriptionChannelType.RestHook, 
                    Endpoint = "https://test-notification-endpoint.com.au/notifiy"
                }
            };
            return observationResource;
        }
    }
    
    public static class ChargeItemResource
    {
        public static ChargeItem GetChargeItem()
        {
            var observationResource = new ChargeItem()
            {
                Id = "charge-item-id",
                Meta = new Meta()
                {
                    VersionId = "1"
                },
                Status = ChargeItem.ChargeItemStatus.Planned,
                Code = new CodeableConcept(system: "https:/someSystem", code: "ABC"),
                Subject = new ResourceReference("Patient/dummy"),
                FactorOverride = 10
            };
            return observationResource;
        }
    }
}