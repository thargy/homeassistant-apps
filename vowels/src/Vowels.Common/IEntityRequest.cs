namespace Vowels.Common;

public interface IEntityRequest { }

public record EntityIDRequest(string EntityId) : IEntityRequest;

public record EntitiesRegexRequest(string Pattern) : IEntityRequest;
