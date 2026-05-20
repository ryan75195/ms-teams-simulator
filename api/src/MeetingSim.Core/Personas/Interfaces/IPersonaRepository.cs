namespace MeetingSim.Core.Personas.Interfaces;

public interface IPersonaRepository
{
    IReadOnlyList<Persona> Roster { get; }

    Persona? Resolve(string personaId);

    IReadOnlyList<Persona> Crowd(int seed, int skip, int count);
}
