using RedfishEmulator.Core.Models;

namespace RedfishEmulator.Core.Services;

/// <summary>Stores authenticated sessions and their opaque auth tokens.</summary>
public interface ISessionStore
{
    /// <summary>Creates a session for a user, returning the session and its auth token.</summary>
    (Session Session, string Token) Create(string userName);

    Session? GetById(string sessionId);

    /// <summary>Resolves the session for an <c>X-Auth-Token</c> value, or null if unknown.</summary>
    Session? GetByToken(string token);

    /// <summary>Deletes a session (logout); false if it did not exist.</summary>
    bool Delete(string sessionId);

    IReadOnlyList<Session> All();
}
