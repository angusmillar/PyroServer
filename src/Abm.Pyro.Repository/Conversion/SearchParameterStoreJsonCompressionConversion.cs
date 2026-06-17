using System.IO.Compression;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Abm.Pyro.Domain.Model;
namespace Abm.Pyro.Repository.Conversion;

public class SearchParameterStoreJsonCompressionConversion : IEntityTypeConfiguration<SearchParameterStore>
{
  public void Configure(EntityTypeBuilder<SearchParameterStore> builder)
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

    var compressed = mso.ToArray();

    // Normalize the GZip header OS byte (index 9) so compression output is identical across operating
    // systems. .NET writes the host OS here (10 on Windows, 3 on Unix); because these compressed bytes
    // are embedded as HasData seed values in the EF migration snapshot, a snapshot generated on one OS
    // would otherwise mismatch the model rebuilt on another (e.g. the Linux CI runner), raising a
    // spurious EF Core 9 PendingModelChangesWarning. The value 10 matches the existing committed
    // snapshot, so no seed rows need rewriting. The deflate body is already deterministic across
    // platforms, and this header byte is ignored when decompressing.
    if (compressed.Length > 9)
    {
      compressed[9] = 10;
    }

    return compressed;
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
