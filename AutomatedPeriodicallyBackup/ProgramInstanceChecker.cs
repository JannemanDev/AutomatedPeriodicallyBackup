using Serilog;

record MutexInfo
{
    public string MutexName { get; set; }
    public Mutex Mutex { get; set; }

    public MutexInfo(string mutexName, Mutex mutex)
    {
        MutexName = mutexName;
        Mutex = mutex;
    }
}

internal class ProgramInstanceChecker : IDisposable
{
    List<MutexInfo> mutexes = new List<MutexInfo>();

    public bool IsRunning { get; init; }

    public ProgramInstanceChecker(params string[] mutexNames)
    {
        IsRunning = false;

        foreach (string mutexName in mutexNames)
        {
            bool createdNew;
            Mutex mutex = new Mutex(true, mutexName, out createdNew);
            IsRunning = IsRunning || !createdNew;
            if (!IsRunning)
            {
                Log.Debug($"Created Mutex successfully: {mutexName}");
                mutexes.Add(new MutexInfo(mutexName, mutex));
            }
            else
            {
                Log.Debug($"Mutex already exist: {mutexName}");
                break;
            }
        }
    }

    public void Dispose()
    {
        foreach (MutexInfo mutexInfo in mutexes)
        {
            Log.Debug($"Releasing Mutex {mutexInfo.MutexName}");
            mutexInfo.Mutex.ReleaseMutex();
        }
    }
}