using MeetingSim.Api.Audio;
using MeetingSim.Api.Audio.Interfaces;
using MeetingSim.Api.Events;
using MeetingSim.Api.Personas;
using MeetingSim.Api.Sessions;
using MeetingSim.Api.Transcription;
using MeetingSim.Api.Transcription.Interfaces;
using MeetingSim.Core;

const string RendererCorsPolicy = "renderer";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCoreServices();
builder.Services.AddSignalR();
builder.Services.AddSingleton<ITranscriptionService, OpenAITranscriptionService>();
builder.Services.AddSingleton<ITextToSpeechService, OpenAITextToSpeechService>();
builder.Services.AddSingleton<IAudioStore, AudioStore>();
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
app.MapSessionEndpoints();
app.MapPersonaEndpoints();
app.MapEventEndpoints();
app.MapTranscribeEndpoints();
app.MapAudioEndpoints();
app.MapHub<SessionHub>("/hubs/session");

app.Run();

public partial class Program;
