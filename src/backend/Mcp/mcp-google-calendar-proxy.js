/**
 * MCP Proxy Wrapper for mcp-google-calendar
 * 
 * WHY WE HAVE THIS:
 * The original @modelcontextprotocol/server-google-calendar package generates JSON 
 * tool schemas that include properties (like `$schema`) that cause the rigorous 
 * .NET MCP SDK parser in the backend to throw an ArgumentException and crash.
 * Furthermore, some of its parameters for Google API tools (like `list_calendar_events`) 
 * were poorly mapped or strictly enforced.
 * 
 * WHAT THIS SCRIPT DOES:
 * This script acts as a "man-in-the-middle" between the C# Backend and the Node.js MCP server.
 * It spawns the real MCP server as a child process and intercepts all JSON-RPC 
 * communication over the standard input/output pipes (stdio). It parses the outbound 
 * messages from the server, forcibly overwrites and cleans up the tool schemas so 
 * they are valid for the C# backend, and then passes them along.
 */
const { spawn } = require('child_process');
const fs = require('fs');
const path = require('path');

// The backend root directory is one level up from this script (Mcp/mcp-google-calendar-proxy.js)
const backendDir = path.join(__dirname, '..');

// If credentials or tokens are passed via environment variables (e.g., in Cloud Run), write them to disk
if (process.env.GOOGLE_CREDENTIALS_JSON) {
  const credPath = path.join(backendDir, 'credentials.json');
  fs.writeFileSync(credPath, process.env.GOOGLE_CREDENTIALS_JSON);
  console.error('[PROXY] Wrote credentials.json from environment variable.');
}

if (process.env.GOOGLE_TOKEN_JSON) {
  const tokenPath = path.join(backendDir, 'mcp-google-calendar-token.json');
  fs.writeFileSync(tokenPath, process.env.GOOGLE_TOKEN_JSON);
  console.error('[PROXY] Wrote mcp-google-calendar-token.json from environment variable.');
}


const child = spawn('npx', ['-y', 'mcp-google-calendar'], {
  cwd: backendDir, // Run where the credentials.json is located
  env: process.env,
  stdio: ['pipe', 'pipe', 'pipe'] // Intercept all stdio
});

child.stderr.on('data', (chunk) => {
  const text = chunk.toString();
  fs.appendFileSync('/tmp/mcp-traffic.log', `[STDERR] ${text}\n`);
});

// Pass incoming JSON-RPC requests from the C# Backend to the MCP server
process.stdin.on('data', (data) => {
  fs.appendFileSync('/tmp/mcp-traffic.log', `[IN ] ${data.toString()}`);
  child.stdin.write(data);
});

// Intercept outbound JSON-RPC responses from the MCP server to the C# Backend
let buffer = '';
child.stdout.on('data', (chunk) => {
  fs.appendFileSync('/tmp/mcp-traffic-raw.log', `[RAW OUT] ${chunk.toString()}`);
  fs.appendFileSync('/tmp/mcp-traffic.log', `[OUT] ${chunk.toString()}`);
  buffer += chunk.toString();
  
  let newlineIdx;
  while ((newlineIdx = buffer.indexOf('\n')) !== -1) {
    const line = buffer.slice(0, newlineIdx);
    buffer = buffer.slice(newlineIdx + 1);
    
    if (line.trim()) {
      try {
        const msg = JSON.parse(line);
        fixOutboundMessage(msg);
        process.stdout.write(JSON.stringify(msg) + '\n');
      } catch (e) {
        fs.appendFileSync('/tmp/mcp-traffic.log', `[ERR] ${e.stack}\n`);
        process.stdout.write(line + '\n');
      }
    }
  }
});

function fixOutboundMessage(msg) {
  // Check if this message is a response to the "tools/list" request.
  if (msg && msg.result && msg.result.tools) {
    for (const tool of msg.result.tools) {
      
      // FIX 1: The C# backend strict JSON parser throws an error if it sees a "$schema" key.
      // We create a fresh, empty schema object to gracefully reset and drop any invalid schema keys.
      tool.inputSchema = { type: "object", properties: {} };
      
      // FIX 2: We re-inject all the parameter definitions manually.
      // The original package sometimes exported arguments that were confusing or missing descriptions,
      // and in the case of `list_calendar_events`, the name of the arguments it was validating
      // against internally (like `timeMin` and `timeMax`) were very specific.
      if (tool.name === 'list_calendar_events') {
        tool.inputSchema.properties = {
          calendarId: { type: "string" },
          timeMin: { type: "string", description: "The start date of the events. Format: YYYY-MM-DDT00:00:00Z" },
          timeMax: { type: "string", description: "The end date of the events. Format: YYYY-MM-DDT00:00:00Z" },
          pageToken: { type: "string", description: "The next page token" }
        };
        tool.inputSchema.required = ["calendarId", "timeMin", "timeMax"];
      } else if (tool.name === 'list_calendars') {
        tool.inputSchema.properties = {
          pageToken: { type: "string", description: "The next page token" }
        };
        tool.inputSchema.required = [];
      } else if (tool.name === 'create_calendar_event') {
        tool.inputSchema.properties = {
          calendarId: { type: "string", description: "The calendar ID" },
          event: { 
            type: "object",
            properties: {
              summary: { type: "string", description: "The summary of the event" },
              description: { type: "string", description: "The description of the event" },
              start: { type: "string", description: "The start date of the event. Format: YYYY-MM-DD" },
              end: { type: "string", description: "The end date of the event. Format: YYYY-MM-DD" },
              anyoneCanAddSelf: { type: "boolean", description: "Whether anyone can add themselves to the event" },
              colorId: { type: "string", description: "The color of the event" }
            },
            required: ["summary", "description", "start", "end"]
          }
        };
        tool.inputSchema.required = ["calendarId", "event"];
      } else if (tool.name === 'get_calendar_event') {
        tool.inputSchema.properties = { calendarId: { type: "string" }, eventId: { type: "string" } };
        tool.inputSchema.required = ["calendarId", "eventId"];
      } else if (tool.name === 'edit_calendar_event') {
         tool.inputSchema.properties = {
          calendarId: { type: "string" },
          eventId: { type: "string" },
          event: { 
            type: "object",
            properties: {
              summary: { type: "string", description: "The summary of the event" },
              description: { type: "string", description: "The description of the event" },
              start: { type: "string", description: "The start date of the event. Format: YYYY-MM-DD" },
              end: { type: "string", description: "The end date of the event. Format: YYYY-MM-DD" },
              anyoneCanAddSelf: { type: "boolean", description: "Whether anyone can add themselves to the event" },
              colorId: { type: "string", description: "The color of the event" }
            },
            required: ["summary", "description", "start", "end"]
          }
        };
        tool.inputSchema.required = ["calendarId", "eventId", "event"];
      } else if (tool.name === 'delete_calendar_event') {
        tool.inputSchema.properties = { calendarId: { type: "string" }, eventId: { type: "string" } };
        tool.inputSchema.required = ["calendarId", "eventId"];
      }
    }
  } else {
    // Just in case, the generic fixTypes logic:
    fixTypes(msg);
  }
}

function fixTypes(obj) {
  if (Array.isArray(obj)) {
    obj.forEach(fixTypes);
  } else if (obj !== null && typeof obj === 'object') {
    if (obj.inputSchema || obj.properties || obj.items) {
      if (Array.isArray(obj.type)) {
        const mainType = obj.type.find(t => t !== 'null') || 'string';
        obj.type = mainType;
      }
    }
    for (const key in obj) {
      if (key === 'inputSchema') {
        const schema = obj[key];
        if (typeof schema === 'object' && schema !== null) {
          if (!schema.type) schema.type = 'object';
          if (!schema.properties) schema.properties = {};
        }
      }
      if (key === 'type' && Array.isArray(obj[key])) {
         const mainType = obj[key].find(t => t !== 'null') || 'string';
         obj[key] = mainType;
      }
      fixTypes(obj[key]);
    }
  }
}

child.on('exit', (code) => {
  process.exit(code || 0);
});
