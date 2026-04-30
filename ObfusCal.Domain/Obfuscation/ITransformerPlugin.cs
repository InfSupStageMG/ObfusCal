namespace ObfusCal.Domain.Obfuscation;

public interface IObfuscationTransformerPlugin : IObfuscationTransformer
{
    string Id { get; }
    int Order { get; }
}

public interface IBusySlotTransformerPlugin : IBusySlotTransformer
{
    string Id { get; }
    int Order { get; }
}

