// Program.cs (Single File for Infonetica Assignment)
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;

// -----------------------------------------------------------------------------
// 1. MODELS - Defines the core concepts of the workflow engine
// -----------------------------------------------------------------------------

public record State(string Id, bool IsInitial = false, bool IsFinal = false);

public record Action(string Id, ISet<string> FromStates, string ToState);

public record WorkflowDefinition(string Id, ISet<State> States, ISet<Action> Actions);

public class WorkflowInstance
{
    public Guid Id { get; } = Guid.NewGuid();
    public string DefinitionId { get; }
    public string CurrentStateId { get; private set; }
    public List<(string ActionId, DateTime Timestamp)> History { get; } = new();

    public WorkflowInstance(string definitionId, string initialStateId)
    {
        DefinitionId = definitionId;
        CurrentStateId = initialStateId;
        History.Add(new("Instance Started", DateTime.UtcNow));
    }

    public void MoveToState(string actionId, string newStateId)
    {
        CurrentStateId = newStateId;
        History.Add(new(actionId, DateTime.UtcNow));
    }
}

// -----------------------------------------------------------------------------
// 2. IN-MEMORY PERSISTENCE & SERVICE - Manages data and logic
// -----------------------------------------------------------------------------

public class WorkflowService
{
    private readonly ConcurrentDictionary<string, WorkflowDefinition> _definitions = new();
    private readonly ConcurrentDictionary<Guid, WorkflowInstance> _instances = new();

    public IResult CreateDefinition(WorkflowDefinition definition)
    {
        if (!_definitions.TryAdd(definition.Id, definition))
        {
            return Results.Conflict($"Definition with ID '{definition.Id}' already exists.");
        }

        var initialStates = definition.States.Count(s => s.IsInitial);
        if (initialStates != 1)
        {
            _definitions.TryRemove(definition.Id, out _);
            return Results.BadRequest("Definition must have exactly one initial state.");
        }

        return Results.Created($"/definitions/{definition.Id}", definition);
    }

    public IResult GetDefinition(string id) =>
        _definitions.TryGetValue(id, out var def) ? Results.Ok(def) : Results.NotFound($"Definition with ID '{id}' not found.");

    public IResult GetAllDefinitions() => Results.Ok(_definitions.Values);

    public IResult StartInstance(string definitionId)
    {
        if (!_definitions.TryGetValue(definitionId, out var definition))
        {
            return Results.NotFound($"Definition with ID '{definitionId}' not found to start an instance.");
        }

        var initialState = definition.States.Single(s => s.IsInitial);
        var instance = new WorkflowInstance(definitionId, initialState.Id);
        _instances[instance.Id] = instance;

        return Results.Ok(new { instance.Id, instance.CurrentStateId });
    }

    public IResult GetInstance(Guid id) =>
        _instances.TryGetValue(id, out var instance)
            ? Results.Ok(new { instance.Id, instance.DefinitionId, instance.CurrentStateId, instance.History })
            : Results.NotFound($"Instance with ID '{id}' not found.");

    public IResult ExecuteAction(Guid instanceId, string actionId)
    {
        if (!_instances.TryGetValue(instanceId, out var instance))
            return Results.NotFound($"Instance with ID '{instanceId}' not found.");

        if (!_definitions.TryGetValue(instance.DefinitionId, out var definition))
            return Results.NotFound($"Definition with ID '{instance.DefinitionId}' not found for the instance.");

        var currentState = definition.States.FirstOrDefault(s => s.Id == instance.CurrentStateId);
        if (currentState == null)
            return Results.Problem("Instance is in an unknown state.");

        if (currentState.IsFinal)
            return Results.BadRequest("Cannot execute actions on an instance in a final state.");

        var actionToExecute = definition.Actions.FirstOrDefault(a => a.Id == actionId);
        if (actionToExecute == null)
            return Results.BadRequest($"Action '{actionId}' not found in workflow definition '{definition.Id}'.");

        if (!actionToExecute.FromStates.Contains(instance.CurrentStateId))
            return Results.BadRequest($"Action '{actionId}' cannot be executed from the current state '{instance.CurrentStateId}'.");

        if (definition.States.All(s => s.Id != actionToExecute.ToState))
             return Results.Problem($"Action '{actionId}' targets an unknown state '{actionToExecute.ToState}'.");

        instance.MoveToState(actionToExecute.Id, actionToExecute.ToState);
        return Results.Ok(new { instance.Id, instance.CurrentStateId });
    }
}

// -----------------------------------------------------------------------------
// 3. API ENDPOINTS - Exposes the service functionality via HTTP
// -----------------------------------------------------------------------------

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<WorkflowService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapPost("/definitions", ([FromBody] WorkflowDefinition definition, WorkflowService service) =>
    service.CreateDefinition(definition))
    .WithSummary("Create a new workflow definition.");

app.MapGet("/definitions", (WorkflowService service) =>
    service.GetAllDefinitions())
    .WithSummary("List all workflow definitions.");

app.MapGet("/definitions/{id}", (string id, WorkflowService service) =>
    service.GetDefinition(id))
    .WithSummary("Retrieve an existing definition by ID.");

app.MapPost("/instances", ([FromBody] string definitionId, WorkflowService service) =>
    service.StartInstance(definitionId))
    .WithSummary("Start a new workflow instance for a chosen definition.");

app.MapGet("/instances/{id}", (Guid id, WorkflowService service) =>
    service.GetInstance(id))
    .WithSummary("Retrieve the current state and history of an instance.");

app.MapPost("/instances/{id}/execute", (Guid id, [FromBody] string actionId, WorkflowService service) =>
    service.ExecuteAction(id, actionId))
    .WithSummary("Execute an action on a given instance.");

app.Run();
