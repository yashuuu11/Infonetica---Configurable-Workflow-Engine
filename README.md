# Infonetica - Configurable Workflow Engine

This project is a take-home exercise for the Software Engineer Intern role at Infonetica. It's a minimal backend service built with .NET 8 that implements a configurable state-machine API.

## Core Features

* Define dynamic workflows with states and actions.
* Create and manage instances of those workflows.
* Execute actions to transition instances between states, with full validation.
* Inspect all definitions and running instances via a RESTful API.

## Quick Start

### Prerequisites

* [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Running the Application

1.  Clone the repository.
2.  Navigate to the project directory.
3.  Run the following command to start the service:
    ```bash
    dotnet run
    ```
4.  The API will be available at `http://localhost:5000` (or a similar port). You can view and interact with the API via the Swagger UI at `http://localhost:5000/swagger`.

## API Usage Examples

You can use a tool like `curl` or the Swagger UI to interact with the API.

#### 1. Create a Workflow Definition

```bash
curl -X POST "http://localhost:5000/definitions" -H "Content-Type: application/json" -d \
'{
  "id": "document-review",
  "states": [
    { "id": "draft", "isInitial": true },
    { "id": "in-review" },
    { "id": "approved", "isFinal": true },
    { "id": "rejected", "isFinal": true }
  ],
  "actions": [
    { "id": "submit-for-review", "fromStates": ["draft"], "toState": "in-review" },
    { "id": "approve", "fromStates": ["in-review"], "toState": "approved" },
    { "id": "reject", "fromStates": ["in-review"], "toState": "rejected" }
  ]
}'
```

#### 2. Start a New Instance

```bash
curl -X POST "http://localhost:5000/instances" -H "Content-Type: application/json" -d '"document-review"'
```
*(This will return an instance ID, e.g., `{"id":"a1b2c3d4-...","currentStateId":"draft"}`)*

#### 3. Execute an Action

*(Use the instance ID from the previous step)*

```bash
curl -X POST "http://localhost:5000/instances/{INSTANCE_ID}/execute" -H "Content-Type: application/json" -d '"submit-for-review"'
```

#### 4. Get Instance Status

```bash
curl -X GET "http://localhost:5000/instances/{INSTANCE_ID}"
```

## Assumptions & Shortcuts

In the interest of meeting the ~2 hour time-box, the following assumptions and shortcuts were made:

* **Single-File Project**: The entire application (models, service, and API endpoints) is in `Program.cs` for simplicity. In a larger project, I would separate these into different files and projects (e.g., `Domain`, `Application`, `Infrastructure`).
* **Simple Persistence**: Persistence is entirely in-memory using thread-safe `ConcurrentDictionary` collections, as suggested. Data is lost on restart.
* **Basic Validation**: Implemented all required validation rules. More robust validation (e.g., ensuring all `toState` and `fromStates` IDs correspond to actual defined states) would be added with more time.
* **Enabled/Disabled Flags**: The `enabled` flags on `State` and `Action` are part of the models but are not currently used in the logic for validation. This would be a straightforward extension.
* **Error Handling**: Error handling is basic, returning standard HTTP status codes and messages. A production system would have more structured error responses.
