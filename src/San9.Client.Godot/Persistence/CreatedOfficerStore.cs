using System.Text;
using San9.Application.Persistence;
using San9.Content.Authoring;

namespace San9.Client.Godot.Persistence;

public sealed class CreatedOfficerStore(string path)
{
    public string Path { get; } = System.IO.Path.GetFullPath(path);
    public CreatedOfficerLibrary Load()
    {
        try { return Read(Path); }
        catch (FileNotFoundException) { return CreatedOfficerLibrary.Empty; }
        catch (DirectoryNotFoundException) { return CreatedOfficerLibrary.Empty; }
    }
    public static CreatedOfficerLibrary Read(string path) => CreatedOfficerLibrary.Decode(File.ReadAllText(path, new UTF8Encoding(false, true)));
    public void Save(CreatedOfficerLibrary library) => Export(Path, library);
    public static void Export(string path, CreatedOfficerLibrary library) => AtomicFile.Write(path, Encoding.UTF8.GetBytes(library.Encode()));
}
