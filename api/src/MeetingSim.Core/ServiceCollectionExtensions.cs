using MeetingSim.Core.Events;
using MeetingSim.Core.Events.Interfaces;
using MeetingSim.Core.Personas;
using MeetingSim.Core.Personas.Interfaces;
using MeetingSim.Core.Sessions;
using MeetingSim.Core.Sessions.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace MeetingSim.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.AddSingleton<ISessionStore, SessionStore>();
        services.AddSingleton<ICrowdService, CrowdService>();
        services.AddSingleton<IPersonaRepository, PersonaRepository>();
        services.AddSingleton<IEventStore, EventStore>();
        return services;
    }
}
