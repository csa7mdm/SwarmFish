using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Scalar.AspNetCore;
using SwarmFish.Api.Gateway.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

// --- Simulations ---

var simulations = app.MapGroup("/api/simulations")
    .WithTags("Simulations");

simulations.MapPost("/", () => Results.Ok(new { simulationId = Guid.NewGuid() }))
    .WithName("StartSimulation")
    .WithSummary("Start a new simulation")
    .WithDescription("Initializes and starts a new simulation run based on the provided configuration.")
    .Produces(StatusCodes.Status200OK);

simulations.MapGet("/{id}", (Guid id) => Results.Ok(new { id, status = "Running", progress = 0.45 }))
    .WithName("GetSimulationStatus")
    .WithSummary("Get simulation status")
    .WithDescription("Returns the current status and progress of the specified simulation.")
    .Produces(StatusCodes.Status200OK);

simulations.MapPost("/{id}/pause", (Guid id) => Results.Accepted())
    .WithName("PauseSimulation")
    .WithSummary("Pause a simulation")
    .Produces(StatusCodes.Status202Accepted);

simulations.MapPost("/{id}/resume", (Guid id) => Results.Accepted())
    .WithName("ResumeSimulation")
    .WithSummary("Resume a simulation")
    .Produces(StatusCodes.Status202Accepted);

simulations.MapDelete("/{id}", (Guid id) => Results.NoContent())
    .WithName("StopSimulation")
    .WithSummary("Stop and cleanup a simulation")
    .Produces(StatusCodes.Status204NoContent);

// --- Seeds ---

var seeds = app.MapGroup("/api/seeds")
    .WithTags("Seeds");

seeds.MapPost("/", () => Results.Ok(new { seedId = Guid.NewGuid() }))
    .WithName("UploadSeed")
    .WithSummary("Upload a seed document")
    .WithDescription("Uploads a new seed document and triggers the ingestion pipeline.")
    .Produces(StatusCodes.Status200OK);

seeds.MapGet("/{id}/status", (Guid id) => Results.Ok(new { id, progress = 1.0, status = "Completed" }))
    .WithName("GetSeedStatus")
    .WithSummary("Get ingestion status")
    .Produces(StatusCodes.Status200OK);

// --- Reports ---

var reports = app.MapGroup("/api/reports")
    .WithTags("Reports");

reports.MapGet("/{simulationId}", (Guid simulationId) => Results.Ok(new { 
        simulationId, 
        summary = "Mock summary", 
        findings = new[] { new { text = "Finding 1", confidence = 0.9 } },
        overallConfidence = 0.85 
    }))
    .WithName("GetReport")
    .WithSummary("Get generated report")
    .Produces(StatusCodes.Status200OK);

reports.MapPost("/{simulationId}/chat", (Guid simulationId, [FromBody] string message) => Results.Ok(new { response = "Mock follow-up response" }))
    .WithName("ChatWithReport")
    .WithSummary("Follow-up question to ReportAgent")
    .Produces(StatusCodes.Status200OK);

// --- Internal ---

var internalApi = app.MapGroup("/internal")
    .WithTags("Internal");

internalApi.MapPost("/graph/bulk", () => Results.Ok())
    .WithName("BulkWriteGraph")
    .WithSummary("Bulk write to KuzuDB")
    .WithDescription("Internal endpoint for the Python ingestion service to perform bulk writes.")
    .Produces(StatusCodes.Status200OK);

// --- SignalR ---

app.MapHub<SimulationHub>("/hubs/simulation");

app.Run();
