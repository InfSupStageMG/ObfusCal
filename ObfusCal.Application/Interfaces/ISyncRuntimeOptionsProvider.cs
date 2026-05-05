using ObfusCal.Application.Configuration;

namespace ObfusCal.Application.Interfaces;

public interface ISyncRuntimeOptionsProvider
{
    SyncOptions Get();
}

