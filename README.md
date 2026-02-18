# Mommey

Mommey is an AI-powered personal assistant designed to help manage family life by orchestrating Notion (notes/journaling) and Google (calendar/reminders) via the Model Context Protocol (MCP).

## Project Structure

- `docs/`: Architecture and design documentation.
- `src/backend/`: C# .NET 9 BFF (Backend for Frontend) Orchestrator.
- `src/frontend/`: React + TypeScript embeddable Chat Widget and development harness.

## Getting Started

### Prerequisites

- .NET 9 SDK
- Node.js (v20+)
- MCP Servers (Notion, Google)

### Running Locally

1. **Backend**:
   ```bash
   cd src/backend
   dotnet run
   ```
   The backend runs at `http://localhost:5235`.

2. **Frontend**:
   ```bash
   cd src/frontend
   npm install
   npm run dev
   ```
   The development harness runs at `http://localhost:5173`.

## Architecture

The system follows a BFF pattern where the React Chat Widget communicates with the C# Backend. The Backend acts as an Orchestrator, using an LLM to decide which MCP tools (Notion or Google) to invoke based on user messages.
