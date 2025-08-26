#!/usr/bin/env node

/**
 * BusBuddy Project MCP Server
 * Provides access to BusBuddy-specific operations, database queries, and project management
 */

import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import {
    CallToolRequestSchema,
    ErrorCode,
    ListToolsRequestSchema,
    McpError,
} from "@modelcontextprotocol/sdk/types.js";
import { spawn } from "child_process";
import fs from "fs/promises";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PROJECT_ROOT = process.env.BUSBUDDY_PROJECT_ROOT || path.resolve(__dirname, "..", "..");

class BusBuddyMCPServer {
  constructor() {
    this.server = new Server(
      {
        name: "busbuddy-project",
        version: "1.0.0",
      },
      {
        capabilities: {
          tools: {},
        },
      }
    );

    this.setupToolHandlers();
    this.setupErrorHandling();
  }

  setupErrorHandling() {
    this.server.onerror = (error) => console.error("[MCP Error]", error);
    process.on("SIGINT", async () => {
      await this.server.close();
      process.exit(0);
    });
  }

  setupToolHandlers() {
    this.server.setRequestHandler(ListToolsRequestSchema, async () => ({
      tools: [
        {
          name: "bb-health",
          description: "Run BusBuddy health check to verify system status",
          inputSchema: {
            type: "object",
            properties: {},
          },
        },
        {
          name: "bb-build",
          description: "Build the BusBuddy solution",
          inputSchema: {
            type: "object",
            properties: {
              configuration: {
                type: "string",
                description: "Build configuration (Debug/Release)",
                default: "Debug"
              }
            },
          },
        },
        {
          name: "bb-run",
          description: "Run the BusBuddy WPF application",
          inputSchema: {
            type: "object",
            properties: {},
          },
        },
        {
          name: "bb-test",
          description: "Run BusBuddy unit tests",
          inputSchema: {
            type: "object",
            properties: {
              project: {
                type: "string",
                description: "Specific test project to run (optional)"
              }
            },
          },
        },
        {
          name: "busbuddy-db-query",
          description: "Execute a safe SQL query against the BusBuddy database",
          inputSchema: {
            type: "object",
            properties: {
              query: {
                type: "string",
                description: "SQL query to execute (SELECT only for safety)"
              },
              table: {
                type: "string",
                description: "Table name for simple queries (Students, Drivers, Vehicles, Routes)"
              }
            },
            required: ["query"],
          },
        },
        {
          name: "busbuddy-logs",
          description: "Get recent BusBuddy application logs",
          inputSchema: {
            type: "object",
            properties: {
              lines: {
                type: "number",
                description: "Number of recent log lines to retrieve",
                default: 50
              },
              level: {
                type: "string",
                description: "Log level filter (Debug, Information, Warning, Error)",
                default: "Information"
              }
            },
          },
        },
        {
          name: "busbuddy-project-status",
          description: "Get comprehensive BusBuddy project status and metrics",
          inputSchema: {
            type: "object",
            properties: {},
          },
        },
        {
          name: "busbuddy-syncfusion-check",
          description: "Verify Syncfusion WPF controls and licensing status",
          inputSchema: {
            type: "object",
            properties: {},
          },
        }
      ],
    }));

    this.server.setRequestHandler(CallToolRequestSchema, async (request) => {
      const { name, arguments: args } = request.params;

      try {
        switch (name) {
          case "bb-health":
            return await this.runPowerShellCommand("bb-health");
          
          case "bb-build":
            const config = args?.configuration || "Debug";
            return await this.runPowerShellCommand(`bb-build --configuration ${config}`);
          
          case "bb-run":
            return await this.runPowerShellCommand("bb-run");
          
          case "bb-test":
            const project = args?.project ? ` --project ${args.project}` : "";
            return await this.runPowerShellCommand(`bb-test${project}`);
          
          case "busbuddy-db-query":
            return await this.executeDatabaseQuery(args?.query, args?.table);
          
          case "busbuddy-logs":
            return await this.getBusBuddyLogs(args?.lines || 50, args?.level || "Information");
          
          case "busbuddy-project-status":
            return await this.getProjectStatus();
          
          case "busbuddy-syncfusion-check":
            return await this.checkSyncfusionStatus();
          
          default:
            throw new McpError(
              ErrorCode.MethodNotFound,
              `Unknown tool: ${name}`
            );
        }
      } catch (error) {
        throw new McpError(
          ErrorCode.InternalError,
          `Tool execution failed: ${error.message}`
        );
      }
    });
  }

  async runPowerShellCommand(command) {
    return new Promise((resolve, reject) => {
      const ps = spawn("pwsh", ["-Command", command], {
        cwd: PROJECT_ROOT,
        env: {
          ...process.env,
          BUSBUDDY_NO_WELCOME: "1",
          BUSBUDDY_SILENT: "1"
        }
      });

      let stdout = "";
      let stderr = "";

      ps.stdout.on("data", (data) => {
        stdout += data.toString();
      });

      ps.stderr.on("data", (data) => {
        stderr += data.toString();
      });

      ps.on("close", (code) => {
        resolve({
          content: [
            {
              type: "text",
              text: `Command: ${command}\nExit Code: ${code}\n\nOutput:\n${stdout}\n\nErrors:\n${stderr}`
            }
          ]
        });
      });

      ps.on("error", (error) => {
        reject(new Error(`Failed to execute command: ${error.message}`));
      });
    });
  }

  async executeDatabaseQuery(query, table) {
    // Safety check - only allow SELECT queries
    if (!query.toLowerCase().trim().startsWith("select")) {
      throw new Error("Only SELECT queries are allowed for safety");
    }

    // If table specified, create a simple query
    if (table && !query.includes("from")) {
      query = `SELECT * FROM ${table} LIMIT 10`;
    }

    try {
      const result = await this.runPowerShellCommand(`bb-sql-query "${query}"`);
      return result;
    } catch (error) {
      return {
        content: [
          {
            type: "text",
            text: `Database query failed: ${error.message}\nQuery: ${query}`
          }
        ]
      };
    }
  }

  async getBusBuddyLogs(lines, level) {
    try {
      const logsPath = path.join(PROJECT_ROOT, "logs", "collected");
      const files = await fs.readdir(logsPath).catch(() => []);
      
      if (files.length === 0) {
        return {
          content: [
            {
              type: "text",
              text: "No log files found in logs/collected directory"
            }
          ]
        };
      }

      // Get the most recent log file
      const logFile = files.sort().reverse()[0];
      const logPath = path.join(logsPath, logFile);
      const logContent = await fs.readFile(logPath, "utf-8");
      
      // Filter by level and get recent lines
      const logLines = logContent.split('\n')
        .filter(line => line.includes(level) || level === "Debug")
        .slice(-lines)
        .join('\n');

      return {
        content: [
          {
            type: "text",
            text: `Recent BusBuddy logs (${lines} lines, level: ${level}):\n\n${logLines}`
          }
        ]
      };
    } catch (error) {
      return {
        content: [
          {
            type: "text",
            text: `Failed to retrieve logs: ${error.message}`
          }
        ]
      };
    }
  }

  async getProjectStatus() {
    try {
      const packageJson = await fs.readFile(path.join(PROJECT_ROOT, "package.json"), "utf-8");
      const pkg = JSON.parse(packageJson);
      
      const solutionFile = await fs.readFile(path.join(PROJECT_ROOT, "BusBuddy.sln"), "utf-8");
      const projectCount = (solutionFile.match(/Project\(/g) || []).length;

      const status = {
        projectName: "BusBuddy",
        version: pkg.version || "1.0.0",
        projectCount: projectCount,
        structure: {
          core: await fs.access(path.join(PROJECT_ROOT, "BusBuddy.Core")).then(() => "✅").catch(() => "❌"),
          wpf: await fs.access(path.join(PROJECT_ROOT, "BusBuddy.WPF")).then(() => "✅").catch(() => "❌"),
          tests: await fs.access(path.join(PROJECT_ROOT, "BusBuddy.Tests")).then(() => "✅").catch(() => "❌"),
          mcp: await fs.access(path.join(PROJECT_ROOT, "mcp")).then(() => "✅").catch(() => "❌"),
          docs: await fs.access(path.join(PROJECT_ROOT, "Documentation")).then(() => "✅").catch(() => "❌")
        }
      };

      return {
        content: [
          {
            type: "text",
            text: `BusBuddy Project Status:
            
📊 **Overview**
- Project: ${status.projectName}
- Version: ${status.version}
- Solution Projects: ${status.projectCount}

🏗️ **Structure**
- Core Library: ${status.structure.core}
- WPF Application: ${status.structure.wpf}
- Unit Tests: ${status.structure.tests}
- MCP Integration: ${status.structure.mcp}
- Documentation: ${status.structure.docs}

🔧 **Quick Actions**
- Run Health Check: bb-health
- Build Solution: bb-build
- Run Application: bb-run
- Run Tests: bb-test`
          }
        ]
      };
    } catch (error) {
      return {
        content: [
          {
            type: "text",
            text: `Failed to get project status: ${error.message}`
          }
        ]
      };
    }
  }

  async checkSyncfusionStatus() {
    try {
      const result = await this.runPowerShellCommand("if ($env:SYNCFUSION_LICENSE_KEY) { 'License: ✅ Configured' } else { 'License: ❌ Not set' }");
      
      // Check for Syncfusion references in csproj files
      const wpfProject = await fs.readFile(path.join(PROJECT_ROOT, "BusBuddy.WPF", "BusBuddy.WPF.csproj"), "utf-8");
      const syncfusionRefs = (wpfProject.match(/Syncfusion\./g) || []).length;

      return {
        content: [
          {
            type: "text",
            text: `Syncfusion WPF Status:

🔑 **License Status**
${result.content[0].text.split('\n').find(line => line.includes('License:')) || 'License: Unknown'}

📦 **Package References**
- Syncfusion references found: ${syncfusionRefs}
- Expected version: 30.2.6 (per upgrade notes)

🎨 **Controls Used**
- SfDataGrid: Primary data display
- DockingManager: Layout management  
- NavigationDrawer: Side navigation
- Theme: FluentDark/FluentLight

🔧 **Quick Actions**
- Check license: bbLicense
- Validate XAML: bb-xaml-validate
- Build verification: bb-build`
          }
        ]
      };
    } catch (error) {
      return {
        content: [
          {
            type: "text",
            text: `Failed to check Syncfusion status: ${error.message}`
          }
        ]
      };
    }
  }

  async run() {
    const transport = new StdioServerTransport();
    await this.server.connect(transport);
    console.error("BusBuddy MCP Server running on stdio");
  }
}

const server = new BusBuddyMCPServer();
server.run().catch(console.error);
