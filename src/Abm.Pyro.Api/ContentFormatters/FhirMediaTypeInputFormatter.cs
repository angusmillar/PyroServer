using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace Abm.Pyro.Api.ContentFormatters
{
  public abstract class FhirMediaTypeInputFormatter : TextInputFormatter
  {
    protected FhirMediaTypeInputFormatter() : base()
    {
      this.SupportedEncodings.Clear();
      this.SupportedEncodings.Add(UTF8EncodingWithoutBOM); // Encoding.UTF8);
    }
   
    protected override bool CanReadType(Type type)
    {
      return typeof(Resource).IsAssignableFrom(type);
    }

  }
}
