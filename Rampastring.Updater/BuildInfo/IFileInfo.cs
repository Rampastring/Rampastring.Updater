namespace Rampastring.Updater.BuildInfo
{
    public interface IFileInfo
    {
        string GetString();

        void Parse(string[] parts);
    }
}
