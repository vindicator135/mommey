# Architecture Overview

## Components

### 1. Chat Widget (Frontend)
- **Tech**: React, TypeScript, Tailwind CSS.
- **Role**: Provides the user interface for chatting with the assistant. Designed to be built as a single-bundle JS file for CDN distribution and embedding on any website.

### 2. BFF Orchestrator (Backend)
- **Tech**: ASP.NET Core (.NET 9).
- **Role**: Receives chat messages, manages session/context, and interacts with LLMs to determine intent.
- **Logging**: Serilog for structured logging.

### 3. Model Context Protocol (MCP) Integration
- **Role**: Standardized communication layer between the AI and external tools.
- **Servers**:
    - **Notion**: Note-taking and journaling.
    - **Google**: Calendar and reminders.

## Data Flow

1. User sends message via Chat Widget.
2. Widget forwards request to `/api/chat` on the BFF.
3. BFF passes context to LLM.
4. LLM decides if it needs to call a tool (e.g., "Add calendar event").
5. BFF executes the Tool via the corresponding MCP server.
6. Result is returned to LLM, then back to the User.
