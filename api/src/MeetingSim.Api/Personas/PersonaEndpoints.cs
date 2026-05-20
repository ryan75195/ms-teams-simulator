using MeetingSim.Api.Contracts;
using MeetingSim.Core.Personas.Interfaces;
using MeetingSim.Core.Sessions.Interfaces;

namespace MeetingSim.Api.Personas;

public static class PersonaEndpoints
{
    private const int DefaultCrowdPageSize = 50;
    private const int MaxCrowdPageSize = 500;

    public static IEndpointRouteBuilder MapPersonaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/sessions/{id:guid}/personas");
        group.MapGet("/", ListPersonas);
        group.MapGet("/{personaId}", GetPersona);
        return app;
    }

    private static IResult ListPersonas(
        Guid id,
        int? skip,
        int? count,
        ISessionStore sessions,
        IPersonaRepository catalog)
    {
        if (sessions.TryGet(id) is not { } session)
        {
            return Results.NotFound();
        }

        var pageSize = Math.Clamp(count ?? DefaultCrowdPageSize, 0, MaxCrowdPageSize);
        var pageSkip = Math.Max(0, skip ?? 0);
        var crowdSize = Math.Max(0, session.AudienceSize - catalog.Roster.Count);
        var available = Math.Max(0, crowdSize - pageSkip);
        var actualCount = Math.Min(pageSize, available);

        var crowd = actualCount > 0
            ? catalog.Crowd(session.Seed, pageSkip, actualCount)
            : Array.Empty<Core.Personas.Persona>();

        return Results.Ok(new PersonasResponse(catalog.Roster, crowd, crowdSize));
    }

    private static IResult GetPersona(
        Guid id,
        string personaId,
        ISessionStore sessions,
        IPersonaRepository catalog)
    {
        if (sessions.TryGet(id) is null)
        {
            return Results.NotFound();
        }

        return catalog.Resolve(personaId) is { } persona
            ? Results.Ok(persona)
            : Results.NotFound();
    }
}
