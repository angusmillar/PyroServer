using System.IO.Compression;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Abm.Pyro.Domain.Model;
namespace Abm.Pyro.Repository.Conversion;

public class ResourceStoreJsonCompressionConversion : IEntityTypeConfiguration<ResourceStore>
{
  public void Configure(EntityTypeBuilder<ResourceStore> builder)
  {
    builder.Property(e => e.Json)
           .HasConversion(
             v => Zip(v),
             v => Unzip(v));
  }
    
  public static void CopyTo(Stream src, Stream dest) {
    byte[] bytes = new byte[4096];

    int cnt;

    while ((cnt = src.Read(bytes, 0, bytes.Length)) != 0) {
      dest.Write(bytes, 0, cnt);
    }
  }

  public static byte[] Zip(string str) 
  {
    var bytes = Encoding.UTF8.GetBytes(str);

    using var msi = new MemoryStream(bytes);
    using var mso = new MemoryStream();
    using (var gs = new GZipStream(mso, CompressionMode.Compress)) 
    {
      CopyTo(msi, gs);
    }

    return mso.ToArray();
  }

  public static string Unzip(byte[] bytes)
  {
    using var msi = new MemoryStream(bytes);
    using var mso = new MemoryStream();
    using (var gs = new GZipStream(msi, CompressionMode.Decompress)) 
    {
      CopyTo(gs, mso);
    }

    return Encoding.UTF8.GetString(mso.ToArray());
  }
}
