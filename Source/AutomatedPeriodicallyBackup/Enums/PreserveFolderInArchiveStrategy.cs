partial class Program
{
    public enum PreserveFolderInArchiveStrategy
    {
        //For example: c:\temp\test1\file.txt
        FullPathWithDrive,  // /c/temp/test1/file.txt
        FullPath,           // /temp/test1/file.txt
        OnlyParentFolder,   // /test1/file.txt
        None,               // /file.txt
    }
}
