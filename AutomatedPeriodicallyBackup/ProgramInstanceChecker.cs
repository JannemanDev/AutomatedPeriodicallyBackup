internal class ProgramInstanceChecker : IDisposable
{
    List<Mutex> mutexes = new List<Mutex>();

    public bool IsRunning { get; init; }

    public ProgramInstanceChecker(params string[] mutexNames)
    {
        IsRunning = false;

        foreach (string mutexName in mutexNames)
        {
            bool createdNew;
            mutexes.Add(new Mutex(true, mutexName, out createdNew));
            IsRunning = IsRunning || !createdNew;
        }
    }

    public void Dispose()
    {
        foreach (Mutex mutex in mutexes)
        {
            mutex.ReleaseMutex();
        }
    }
}