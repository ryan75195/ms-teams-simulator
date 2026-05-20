namespace MeetingSim.Core.Personas.Interfaces;

public interface ICrowdService
{
    Persona Generate(int seed, int index);
    bool TryParseCrowdId(string personaId, out int seed, out int index);
}
