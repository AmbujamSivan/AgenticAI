using System.Collections.Concurrent;
using RedfishEmulator.Core.Models;

namespace RedfishEmulator.Core.Services;

/// <summary>Thread-safe in-memory <see cref="ISessionStore"/>.</summary>
public sealed class InMemorySessionStore : ISessionStore
{
    private const string SessionsBase = "/redfish/v1/SessionService/Sessions";

    private readonly ConcurrentDictionary<string, Session> _byId = new();
    private readonly ConcurrentDictionary<string, string> _tokenToId = new();
    private int _nextId;

    public (Session Session, string Token) Create(string userName)
    {
        var id = Interlocked.Increment(ref _nextId).ToString();
        var token = Guid.NewGuid().ToString("N");

        var session = new Session
        {
            ODataId = $"{SessionsBase}/{id}",
            ODataType = "#Session.v1_6_0.Session",
            ODataContext = "/redfish/v1/$metadata#Session.Session",
            Id = id,
            Name = $"User Session {id}",
            UserName = userName,
        };

        _byId[id] = session;
        _tokenToId[token] = id;
        return (session, token);
    }

    public Session? GetById(string sessionId) => _byId.GetValueOrDefault(sessionId);

    public Session? GetByToken(string token) =>
        _tokenToId.TryGetValue(token, out var id) ? _byId.GetValueOrDefault(id) : null;

    public bool Delete(string sessionId)
    {
        if (!_byId.TryRemove(sessionId, out _))
        {
            return false;
        }

        foreach (var (token, id) in _tokenToId)
        {
            if (id == sessionId)
            {
                _tokenToId.TryRemove(token, out _);
            }
        }

        return true;
    }

    public IReadOnlyList<Session> All() =>
        _byId.Values.OrderBy(s => int.Parse(s.Id!)).ToList();
}
