#!/usr/bin/env node

/**
 * Grok-4 MCP Server for BusBuddy
 * Extends BusBuddy MCP infrastructure with xAI Grok-4 AI capabilities
 * 
 * Architecture: Inherits from BusBuddyMCPServer for consistency
 * Security: Uses environment variables for API keys
 * Integration: Seamlessly works with existing bb-* commands
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
import fetch from "node-fetch";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PROJECT_ROOT = process.env.BUSBUDDY_PROJECT_ROOT || path.resolve(__dirname, "..", "..");

// Import base BusBuddy server functionality
// Note: This would be refactored to import from a shared base class
class BusBuddyMCPServer {
  constructor() {
    this.server = new Server(
      {
        name: "busbuddy-grok4",
        version: "1.0.0",
      },
      {
        capabilities: {
          tools: {},
        },
      }
    );
  }

  async runPowerShellCommand(command) {
    return new Promise((resolve, reject) => {
      // Load BusBuddy PowerShell profile first, then run the command
      const profilePath = path.join(PROJECT_ROOT, "tools", "powershell", "Profiles", "Microsoft.PowerShell_profile_optimized.ps1");
      const commandWithProfile = `
        try {
          if (Test-Path "${profilePath}") {
            . "${profilePath}"
          }
          ${command}
        } catch {
          Write-Error $_.Exception.Message
        }
      `;

      const ps = spawn("pwsh", ["-Command", commandWithProfile], {
        cwd: PROJECT_ROOT,
        env: { 
          ...process.env, 
          BUSBUDDY_NO_WELCOME: "1",
          BUSBUDDY_PROJECT_ROOT: PROJECT_ROOT
        }
      });

      let stdout = "";
      let stderr = "";

      ps.stdout.on("data", (data) => stdout += data.toString());
      ps.stderr.on("data", (data) => stderr += data.toString());

      ps.on("close", (code) => {
        if (code === 0) {
          resolve({
            content: [{
              type: "text",
              text: stdout || "Command executed successfully"
            }]
          });
        } else {
          reject(new Error(`PowerShell command failed: ${stderr || stdout}`));
        }
      });
    });
  }
}

class Grok4MCPServer extends BusBuddyMCPServer {
  constructor() {
    super();
    this.server.name = "busbuddy-grok4";
    this.setupGrokTools();
    this.setupErrorHandling();
    this.validateConfiguration();
  }

  validateConfiguration() {
    // Validate xAI API key without exposing it
    const apiKey = process.env.XAI_API_KEY;
    if (!apiKey) {
      console.warn("⚠️  XAI_API_KEY not set. Grok-4 tools will be unavailable.");
      console.warn("📖 Setup instructions: https://x.ai/api");
    } else {
      console.log("✅ Grok-4 API key configured");
    }
  }

  setupErrorHandling() {
    this.server.onerror = (error) => console.error("[Grok-4 MCP Error]", error);
    process.on("SIGINT", async () => {
      await this.server.close();
      process.exit(0);
    });
  }

  setupGrokTools() {
    this.server.setRequestHandler(ListToolsRequestSchema, async () => ({
      tools: [
        // Core BusBuddy tools (inherited)
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
        // Grok-4 Enhanced Tools
        {
          name: "grok-analyze-problem",
          description: "Analyze BusBuddy code/build issues using Grok-4's advanced reasoning capabilities",
          inputSchema: {
            type: "object",
            properties: {
              query: {
                type: "string",
                description: "Problem description or error message to analyze"
              },
              context: {
                type: "string",
                description: "Additional context (file paths, build logs, etc.)",
                default: ""
              },
              mode: {
                type: "string",
                description: "Response mode: 'quick' (1-2 actionable items) or 'expert' (detailed analysis)",
                enum: ["quick", "expert"],
                default: "expert"
              },
              includeProjectStatus: {
                type: "boolean",
                description: "Include current BusBuddy project status in analysis",
                default: true
              }
            },
            required: ["query"],
          },
        },
        {
          name: "grok-code-review",
          description: "Get Grok-4 code review and optimization suggestions for BusBuddy components",
          inputSchema: {
            type: "object",
            properties: {
              filePath: {
                type: "string",
                description: "Path to C# file relative to project root"
              },
              mode: {
                type: "string", 
                description: "Response mode: 'quick' (1-2 improvements) or 'expert' (detailed review)",
                enum: ["quick", "expert"],
                default: "expert"
              },
              focusArea: {
                type: "string",
                description: "Specific area to focus on (performance, security, MVVM, Syncfusion, etc.)",
                default: "general"
              }
            },
            required: ["filePath"],
          },
        },
        {
          name: "grok-syncfusion-guidance",
          description: "Get Grok-4 guidance on Syncfusion WPF implementation and best practices",
          inputSchema: {
            type: "object",
            properties: {
              component: {
                type: "string",
                description: "Syncfusion component name (SfDataGrid, DockingManager, etc.)"
              },
              issue: {
                type: "string",
                description: "Specific issue or question about the component"
              },
              mode: {
                type: "string",
                description: "Response mode: 'quick' (1-2 actionable fixes) or 'expert' (comprehensive guidance)",
                enum: ["quick", "expert"],
                default: "expert"
              },
              xamlSnippet: {
                type: "string",
                description: "XAML code snippet for analysis (optional)",
                default: ""
              }
            },
            required: ["component", "issue"],
          },
        },
        {
          name: "grok-azure-sql-optimize",
          description: "Get Grok-4 suggestions for Azure SQL query optimization and database design",
          inputSchema: {
            type: "object",
            properties: {
              query: {
                type: "string",
                description: "SQL query to analyze and optimize"
              },
              context: {
                type: "string",
                description: "Context about the query purpose and performance issues",
                default: ""
              },
              mode: {
                type: "string",
                description: "Response mode: 'quick' (1-2 optimizations) or 'expert' (comprehensive analysis)",
                enum: ["quick", "expert"], 
                default: "expert"
              }
            },
            required: ["query"],
          },
        },
        {
          name: "grok-architecture-review",
          description: "Get Grok-4 architectural analysis and recommendations for BusBuddy",
          inputSchema: {
            type: "object",
            properties: {
              component: {
                type: "string",
                description: "Component or module to analyze (Core, WPF, Services, etc.)",
                default: "overall"
              },
              concern: {
                type: "string", 
                description: "Specific architectural concern (scalability, maintainability, performance, etc.)",
                default: "general"
              },
              mode: {
                type: "string",
                description: "Response mode: 'quick' (1-2 key recommendations) or 'expert' (comprehensive analysis)",
                enum: ["quick", "expert"],
                default: "expert"
              }
            },
            required: [],
          },
        }
      ],
    }));

    this.server.setRequestHandler(CallToolRequestSchema, async (request) => {
      const { name, arguments: args } = request.params;

      try {
        switch (name) {
          // Inherited BusBuddy tools
          case "bb-health":
            return await this.runPowerShellCommand("bb-health");
          
          case "bb-build":
            const config = args?.configuration || "Debug";
            return await this.runPowerShellCommand(`bb-build --configuration ${config}`);
          
          // Grok-4 enhanced tools
          case "grok-analyze-problem":
            return await this.analyzeWithGrok(args.query, args.context, args.includeProjectStatus, args.mode);
          
          case "grok-code-review":
            return await this.reviewCodeWithGrok(args.filePath, args.focusArea, args.mode);
          
          case "grok-syncfusion-guidance":
            return await this.getSyncfusionGuidance(args.component, args.issue, args.xamlSnippet, args.mode);
          
          case "grok-azure-sql-optimize":
            return await this.optimizeSqlWithGrok(args.query, args.context, args.mode);
          
          case "grok-architecture-review":
            return await this.reviewArchitectureWithGrok(args.component, args.concern, args.mode);
          
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

  async analyzeWithGrok(query, context = "", includeProjectStatus = true, mode = 'expert') {
    let fullContext = query;
    
    if (includeProjectStatus) {
      try {
        // Get current project status to provide context to Grok-4
        const statusResult = await this.runPowerShellCommand("bb-health");
        fullContext += `\n\nCurrent BusBuddy Project Status:\n${statusResult.content[0].text}`;
      } catch (error) {
        console.warn("Could not get project status for context");
      }
    }
    
    if (context) {
      fullContext += `\n\nAdditional Context:\n${context}`;
    }

    const prompt = `
As an expert in .NET WPF development, Syncfusion components, and Azure SQL Database, please analyze this BusBuddy project issue:

${fullContext}

Provide ${mode === 'quick' ? '1-2 immediate actionable steps to resolve this issue' : 'comprehensive analysis including root cause, solution steps, prevention strategies, and code examples'}.

Focus on BusBuddy's technology stack: .NET 8/9, WPF, Syncfusion WPF 30.1.42, Entity Framework Core, Azure SQL, Serilog logging.
`;

    return await this.callGrokAPI(prompt, mode);
  }

  async reviewCodeWithGrok(filePath, focusArea = "general", mode = 'expert') {
    try {
      const fullPath = path.resolve(PROJECT_ROOT, filePath);
      const code = await fs.readFile(fullPath, 'utf-8');
      
      const prompt = `
Please review this BusBuddy C# code file for ${focusArea} improvements:

File: ${filePath}
Focus Area: ${focusArea}

\`\`\`csharp
${code}
\`\`\`

Provide ${mode === 'quick' ? '1-2 specific improvements' : 'comprehensive code review including quality assessment, improvement suggestions, best practices, and Syncfusion integration tips'}.

Context: This is part of BusBuddy, a school transportation management system using .NET 8/9, WPF, Syncfusion components, and Azure SQL.
`;

      return await this.callGrokAPI(prompt, mode);
    } catch (error) {
      throw new McpError(
        ErrorCode.InvalidArgument,
        `Could not read file ${filePath}: ${error.message}`
      );
    }
  }

  async getSyncfusionGuidance(component, issue, xamlSnippet = "", mode = 'expert') {
    const prompt = `
As a Syncfusion WPF expert, please help with this BusBuddy implementation:

Component: ${component}
Issue: ${issue}
${xamlSnippet ? `\nXAML Code:\n\`\`\`xml\n${xamlSnippet}\n\`\`\`` : ''}

Provide ${mode === 'quick' ? '1-2 actionable fixes' : 'comprehensive guidance including solution, best practices, performance tips, and integration examples'}.

Reference the official Syncfusion documentation: https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf
`;

    return await this.callGrokAPI(prompt, mode);
  }

  async optimizeSqlWithGrok(query, context = "", mode = 'expert') {
    const prompt = `
As an Azure SQL Database expert, please analyze and optimize this query for BusBuddy:

SQL Query:
\`\`\`sql
${query}
\`\`\`

${context ? `Context: ${context}` : ''}

Provide ${mode === 'quick' ? '1-2 optimization recommendations' : 'comprehensive analysis including performance review, index recommendations, Azure SQL optimizations, and EF Core considerations'}.

Consider BusBuddy's data model: Students, Drivers, Vehicles, Routes, Maintenance records, and related entities.
`;

    return await this.callGrokAPI(prompt, mode);
  }

  async reviewArchitectureWithGrok(component = "overall", concern = "general", mode = 'expert') {
    const prompt = `
Please review BusBuddy's architecture for ${component} focusing on ${concern}:

BusBuddy is a school transportation management system with:
- .NET 8/9 WPF application with MVVM pattern
- Syncfusion WPF 30.1.42 for UI components
- Entity Framework Core with Azure SQL Database
- Serilog for structured logging
- Model Context Protocol (MCP) for AI integration
- PowerShell automation tools (bb-* commands)

Component Focus: ${component}
Concern: ${concern}

Provide ${mode === 'quick' ? '1-2 key architectural recommendations' : 'comprehensive analysis including strengths/weaknesses, scalability, maintainability, performance, security, and specific implementation guidance'}.

Focus on concrete, actionable advice following Microsoft and industry best practices.
`;

    return await this.callGrokAPI(prompt, mode);
  }

  async callGrokAPI(prompt, mode = 'expert') {
    const apiKey = process.env.XAI_API_KEY;
    
    if (!apiKey) {
      return {
        content: [{
          type: "text",
          text: "❌ Grok-4 API key not configured. Please set XAI_API_KEY environment variable.\n\n📖 Setup instructions: https://x.ai/api"
        }]
      };
    }

    // Define response modes with structured outputs
    const RESPONSE_MODES = {
      quick: {
        max_tokens: 400,
        temperature: 0.2,
        response_format: {
          type: "json_schema",
          json_schema: {
            name: "quick_response",
            schema: {
              type: "object",
              properties: {
                actionable_items: {
                  type: "array",
                  items: {
                    type: "object",
                    properties: {
                      action: { type: "string" },
                      benefit: { type: "string" }
                    },
                    required: ["action", "benefit"]
                  },
                  minItems: 1,
                  maxItems: 2
                },
                summary: { type: "string" }
              },
              required: ["actionable_items", "summary"]
            }
          }
        },
        system_prompt: "You are a concise technical assistant specializing in .NET, WPF, and BusBuddy development. Provide exactly 1-2 actionable items with clear benefits. Format as JSON."
      },
      expert: {
        max_tokens: 1200,
        temperature: 0.6,
        response_format: {
          type: "json_schema",
          json_schema: {
            name: "expert_response", 
            schema: {
              type: "object",
              properties: {
                recommendations: {
                  type: "array",
                  items: {
                    type: "object",
                    properties: {
                      title: { type: "string" },
                      description: { type: "string" },
                      code_example: { type: "string" },
                      benefit: { type: "string" }
                    },
                    required: ["title", "description", "benefit"]
                  },
                  minItems: 2,
                  maxItems: 3
                },
                implementation_priority: { type: "string" }
              },
              required: ["recommendations", "implementation_priority"]
            }
          }
        },
        system_prompt: "You are an expert software architect specializing in .NET, WPF, Syncfusion components, Azure SQL, and BusBuddy enterprise application development. Provide 2-3 specific recommendations with code examples. Format as JSON."
      }
    };

    const config = RESPONSE_MODES[mode] || RESPONSE_MODES.expert;

    try {
      const requestBody = {
        model: "grok-4-0709",
        messages: [
          {
            role: "system",
            content: config.system_prompt
          },
          {
            role: "user", 
            content: prompt
          }
        ],
        max_tokens: config.max_tokens,
        temperature: config.temperature,
        stream: false
      };

      // Add structured output format
      if (config.response_format) {
        requestBody.response_format = config.response_format;
      }

      const response = await fetch("https://api.x.ai/v1/chat/completions", {
        method: "POST",
        headers: {
          "Authorization": `Bearer ${apiKey}`,
          "Content-Type": "application/json"
        },
        body: JSON.stringify(requestBody)
      });

      if (!response.ok) {
        const errorData = await response.json().catch(() => ({}));
        throw new Error(`xAI API error: ${response.status} ${response.statusText} - ${errorData.error?.message || 'Unknown error'}`);
      }

      const data = await response.json();
      const analysis = data.choices?.[0]?.message?.content || "No response from Grok-4";

      // Format structured output for display
      let formattedOutput = `🤖 **Grok-4 Analysis (${mode.toUpperCase()} mode)**\n\n`;
      
      try {
        const parsedResponse = JSON.parse(analysis);
        
        if (mode === 'quick' && parsedResponse.actionable_items) {
          formattedOutput += "🎯 **Actionable Items:**\n";
          parsedResponse.actionable_items.forEach((item, index) => {
            formattedOutput += `${index + 1}. **${item.action}**\n`;
            formattedOutput += `   ➤ ${item.benefit}\n\n`;
          });
          formattedOutput += `💡 **Summary:** ${parsedResponse.summary}`;
        } else if (mode === 'expert' && parsedResponse.recommendations) {
          formattedOutput += "🏗️ **Expert Recommendations:**\n\n";
          parsedResponse.recommendations.forEach((rec, index) => {
            formattedOutput += `**${index + 1}. ${rec.title}**\n`;
            formattedOutput += `📝 ${rec.description}\n`;
            if (rec.code_example) {
              formattedOutput += `💻 **Code Example:**\n\`\`\`csharp\n${rec.code_example}\n\`\`\`\n`;
            }
            formattedOutput += `✅ **Benefit:** ${rec.benefit}\n\n`;
          });
          formattedOutput += `🚀 **Implementation Priority:** ${parsedResponse.implementation_priority}`;
        }
      } catch (e) {
        // Fallback to raw response if JSON parsing fails
        formattedOutput += analysis;
      }

      formattedOutput += "\n\n---\n*Powered by xAI Grok-4 via BusBuddy MCP Server*";

      return {
        content: [{
          type: "text",
          text: formattedOutput
        }]
      };

    } catch (error) {
      console.error("Grok-4 API Error:", error);
      return {
        content: [{
          type: "text",
          text: `❌ **Grok-4 API Error**\n\n${error.message}\n\nPlease check:\n1. XAI_API_KEY is valid\n2. Internet connectivity\n3. API quota/billing status\n\n📖 Support: https://x.ai/api`
        }]
      };
    }
  }

  async run() {
    const transport = new StdioServerTransport();
    await this.server.connect(transport);
    console.error("🚌 BusBuddy Grok-4 MCP server running on stdio");
    console.error("🤖 Enhanced with xAI Grok-4 AI capabilities");
  }
}

// Run the server
const server = new Grok4MCPServer();
await server.run();
