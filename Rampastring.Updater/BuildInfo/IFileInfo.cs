namespace Rampastring.Updater.BuildInfo
{
    public interface IFileInfo
    {
        string FilePath { get; }

        string GetString();

        void Parse(string[] parts);
    }
}
