using System;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json;

namespace Abm.Pyro.CodeGeneration.Support
{
  public class ZipFileJsonLoader 
  {
    public JsonReader Load(byte[] zipFileBytes, string fileNameRequired)
    {
      Stream fileStream = new MemoryStream(zipFileBytes);
      try
      {
        using (var archive = new ZipArchive(fileStream))
        {
          foreach (var entry in archive.Entries)
          {
            if (entry.FullName.Equals(fileNameRequired, StringComparison.OrdinalIgnoreCase))
            {
              var streamItem = entry.Open();
              using (streamItem)
              {
                try
                {
                  var buffer = new MemoryStream();
                  streamItem.CopyTo(buffer);
                  buffer.Seek(0, SeekOrigin.Begin);
                  return Hl7.Fhir.Utility.SerializationUtil.JsonReaderFromStream(buffer);
                }
                catch (Exception exec)
                {
                  throw new Exception($"Exception thrown when de-serializing to json the file named {fileNameRequired}. See inner exception for more info.", exec);
                }
              }
            }
          }
          throw new Exception($"Unable to locate the file named {fileNameRequired} with in the provided zip file byte array. Check the file is found in the zip file being targeted.");
        }
      }
      catch (Exception exec)
      {
        string errorMessage = $"Exception thrown when attempting to unzip the zip file byte array in order to find the file named : {fileNameRequired}. See inner exception for more info.";
        throw new Exception(errorMessage, exec);
      }
    }
  }
}
