using System.IO;
using System.Linq;
namespace Abm.Pyro.CodeGeneration.Support
{
  public static class DirectorySupport
  {
    public static DirectoryInfo TryGetSolutionDirectoryInfo(string currentPath = null)
    {
      var directory = new DirectoryInfo(
        currentPath ?? Directory.GetCurrentDirectory());
      while (directory != null && !directory.GetFiles("*.sln").Any())
      {
        directory = directory.Parent;
      }
      return directory;
    }
  }
}
