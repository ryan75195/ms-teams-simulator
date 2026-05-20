using System.Text;
using MeetingSim.Core.Personas;

namespace MeetingSim.Etl.Moderator;

internal static class SchemaBuilder
{
    public static string BuildModeratorDecisionSchema(IReadOnlyList<Persona> roster)
    {
        var personaIds = roster
            .Where(p => p.Archetype != Archetype.User)
            .Select(p => "\"" + p.Id + "\"")
            .ToList();

        var enumLiterals = new StringBuilder();
        for (var i = 0; i < personaIds.Count; i++)
        {
            enumLiterals.Append(personaIds[i]);
            enumLiterals.Append(", ");
        }
        enumLiterals.Append("null");

        return $$"""
        {
          "type": "object",
          "properties": {
            "action":     { "type": "string", "enum": ["speak", "chat", "hand-raise", "none"] },
            "personaId":  { "type": ["string", "null"], "enum": [{{enumLiterals}}] },
            "text":       { "type": ["string", "null"] },
            "raised":     { "type": ["boolean", "null"] },
            "reasoning":  { "type": "string" }
          },
          "required": ["action", "personaId", "text", "raised", "reasoning"],
          "additionalProperties": false
        }
        """;
    }
}
