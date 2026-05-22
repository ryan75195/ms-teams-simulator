using MeetingSim.Api.Audio;
using MeetingSim.Api.Audio.Interfaces;
using MeetingSim.Api.Decisions;
using MeetingSim.Api.Events;
using MeetingSim.Api.Personas;
using MeetingSim.Api.Realtime;
using MeetingSim.Api.Realtime.Interfaces;
using MeetingSim.Api.Sessions;
using MeetingSim.Api.Sessions.Interfaces;
using MeetingSim.Api.Transcription;
using MeetingSim.Api.Transcription.Interfaces;
using MeetingSim.Core;

const string RendererCorsPolicy = "renderer";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCoreServices();
builder.Services.AddSignalR();
builder.Services.AddSingleton<ITranscriptionService, OpenAITranscriptionService>();
builder.Services.AddSingleton<IRealtimeTranscriptionClient, OpenAIRealtimeTranscriptionClient>();
builder.Services.AddSingleton<ITextToSpeechService, OpenAITextToSpeechService>();
builder.Services.AddSingleton<IAudioStore, AudioStore>();
builder.Services.AddSingleton<ISessionArchive, FileSystemSessionArchive>();
builder.Services.AddCors(options => options.AddPolicy(RendererCorsPolicy, policy => policy
    .WithOrigins(
        "http://127.0.0.1:5173",
        "http://localhost:5173",
        "app://./")
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

app.UseCors(RendererCorsPolicy);
app.UseWebSockets();
app.MapSessionEndpoints();
app.MapPersonaEndpoints();
app.MapEventEndpoints();
app.MapTranscribeEndpoints();
app.MapRealtimeEndpoints();
app.MapAudioEndpoints();
app.MapDecisionEndpoints();
app.MapArchiveEndpoints();
app.MapHub<SessionHub>("/hubs/session");

app.Run();

public partial class Program;
