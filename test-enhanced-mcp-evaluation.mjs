#!/usr/bin/env node

/**
 * BusBuddy MCP System Evaluation using Direct Grok-4 API
 * Demonstrates structured outputs with Quick and Expert modes
 */

const API_KEY = process.env.XAI_API_KEY;
const API_URL = "https://api.x.ai/v1/chat/completions";

// Response Schemas from Enhanced MCP Server
const QUICK_RESPONSE_SCHEMA = {
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
                            benefit: { type: "string" },
                        },
                        required: ["action", "benefit"],
                    },
                    minItems: 1,
                    maxItems: 2,
                },
                summary: { type: "string" },
            },
            required: ["actionable_items", "summary"],
        },
    },
};

const EXPERT_RESPONSE_SCHEMA = {
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
                            benefit: { type: "string" },
                        },
                        required: ["title", "description", "benefit"],
                    },
                    minItems: 2,
                    maxItems: 3,
                },
                implementation_priority: { type: "string" },
            },
            required: ["recommendations", "implementation_priority"],
        },
    },
};

async function callGrokAPI(prompt, mode = "expert") {
    if (!API_KEY) {
        console.error("❌ XAI_API_KEY not configured");
        return null;
    }

    const config = {
        quick: {
            max_tokens: 400,
            temperature: 0.2,
            response_format: QUICK_RESPONSE_SCHEMA,
            system_prompt:
                "You are a concise technical assistant specializing in .NET, WPF, and BusBuddy development. Provide exactly 1-2 actionable items with clear benefits. Format as JSON.",
        },
        expert: {
            max_tokens: 1200,
            temperature: 0.6,
            response_format: EXPERT_RESPONSE_SCHEMA,
            system_prompt:
                "You are an expert software architect specializing in .NET, WPF, Syncfusion components, Azure SQL, and BusBuddy enterprise application development. Provide 2-3 specific recommendations with code examples. Format as JSON.",
        },
    };

    const settings = config[mode] || config.expert;

    try {
        const response = await fetch(API_URL, {
            method: "POST",
            headers: {
                Authorization: `Bearer ${API_KEY}`,
                "Content-Type": "application/json",
            },
            body: JSON.stringify({
                model: "grok-4-0709",
                messages: [
                    { role: "system", content: settings.system_prompt },
                    { role: "user", content: prompt },
                ],
                max_tokens: settings.max_tokens,
                temperature: settings.temperature,
                response_format: settings.response_format,
                stream: false,
            }),
        });

        if (!response.ok) {
            const errorData = await response.json().catch(() => ({}));
            throw new Error(
                `API error: ${response.status} - ${errorData.error?.message || "Unknown error"}`,
            );
        }

        const data = await response.json();
        const content = data.choices?.[0]?.message?.content;

        if (!content) {
            throw new Error("No response content");
        }

        return JSON.parse(content);
    } catch (error) {
        console.error(`❌ ${mode.toUpperCase()} mode error:`, error.message);
        return null;
    }
}

function formatQuickResponse(response) {
    if (!response?.actionable_items) return "❌ Invalid response format";

    let output = `🎯 **QUICK MODE RESPONSE**\n\n`;
    output += "**Actionable Items:**\n";

    response.actionable_items.forEach((item, index) => {
        output += `${index + 1}. **${item.action}**\n`;
        output += `   ➤ ${item.benefit}\n\n`;
    });

    output += `💡 **Summary:** ${response.summary}`;
    return output;
}

function formatExpertResponse(response) {
    if (!response?.recommendations) return "❌ Invalid response format";

    let output = `🏗️ **EXPERT MODE RESPONSE**\n\n`;
    output += "**Expert Recommendations:**\n\n";

    response.recommendations.forEach((rec, index) => {
        output += `**${index + 1}. ${rec.title}**\n`;
        output += `📝 ${rec.description}\n`;
        if (rec.code_example) {
            output += `💻 **Code Example:**\n\`\`\`csharp\n${rec.code_example}\n\`\`\`\n`;
        }
        output += `✅ **Benefit:** ${rec.benefit}\n\n`;
    });

    output += `🚀 **Implementation Priority:** ${response.implementation_priority}`;
    return output;
}

async function testMCPSystemEvaluation() {
    console.log("🔍 **BusBuddy Enhanced MCP System Evaluation**\n");
    console.log(
        "Testing structured outputs with Quick and Expert response modes\n",
    );
    console.log("=".repeat(80) + "\n");

    // Test 1: Quick Mode - Syncfusion Performance Issue
    console.log("📊 **Test 1: Quick Mode - SfDataGrid Performance**\n");

    const quickResult = await callGrokAPI(
        `
Our BusBuddy application has performance issues with Syncfusion SfDataGrid when displaying 10,000+ student records. 
The UI freezes during loading and scrolling is laggy. Users are complaining about the system being unusable.

Current setup:
- Syncfusion WPF 30.1.42
- .NET 8 WPF application
- Entity Framework Core with Azure SQL
- Binding to ObservableCollection<Student>

What are the most important immediate fixes?
`,
        "quick",
    );

    if (quickResult) {
        console.log(formatQuickResponse(quickResult));
    }

    console.log("\n" + "=".repeat(80) + "\n");

    // Test 2: Expert Mode - Comprehensive Architecture Analysis
    console.log("🏗️ **Test 2: Expert Mode - MCP Architecture Review**\n");

    const expertResult = await callGrokAPI(
        `
Please evaluate BusBuddy's Model Context Protocol (MCP) integration architecture:

Current Setup:
- 8 MCP servers: Azure, GitHub, filesystem, Brave search, Microsoft docs, Git, project-specific, Grok-4 AI
- Enhanced Grok-4 server with structured JSON outputs (Quick/Expert modes)
- xAI API integration with response schemas and temperature control
- PowerShell automation tools (bb-* commands)
- VS Code integration via .vscode/mcp.json configuration

Architecture Components:
- BusBuddy.Core: Entity Framework, services, data models
- BusBuddy.WPF: Syncfusion UI, MVVM pattern, view models
- Grok-4 MCP Server: AI-powered analysis with structured outputs
- Supporting servers: File operations, Azure services, documentation search

Evaluate the system for scalability, maintainability, performance, and provide specific improvement recommendations.
`,
        "expert",
    );

    if (expertResult) {
        console.log(formatExpertResponse(expertResult));
    }

    console.log("\n" + "=".repeat(80) + "\n");

    // Test 3: Response Mode Comparison
    console.log("⚡ **Test 3: Response Mode Comparison - SQL Optimization**\n");

    const sqlQuery = `
Analyze this BusBuddy SQL query for optimization:

SELECT s.*, r.RouteName, d.DriverName, v.VehicleNumber
FROM Students s
LEFT JOIN Routes r ON s.RouteId = r.Id  
LEFT JOIN Drivers d ON r.DriverId = d.Id
LEFT JOIN Vehicles v ON r.VehicleId = v.Id
WHERE s.Active = 1 AND s.EnrollmentDate >= '2024-01-01'
ORDER BY s.LastName, s.FirstName

Context: Used in main dashboard, called every 30 seconds, performance is critical.
`;

    console.log("🎯 **QUICK MODE:**\n");
    const quickSqlResult = await callGrokAPI(sqlQuery, "quick");
    if (quickSqlResult) {
        console.log(formatQuickResponse(quickSqlResult));
    }

    console.log("\n" + "-".repeat(40) + "\n");

    console.log("🏗️ **EXPERT MODE:**\n");
    const expertSqlResult = await callGrokAPI(sqlQuery, "expert");
    if (expertSqlResult) {
        console.log(formatExpertResponse(expertSqlResult));
    }

    console.log("\n" + "=".repeat(80) + "\n");
    console.log("✅ **MCP System Evaluation Complete!**\n");
    console.log(
        "🎯 Quick mode provides 1-2 actionable items for immediate implementation",
    );
    console.log(
        "🏗️ Expert mode delivers comprehensive analysis with code examples",
    );
    console.log(
        "📊 Both modes use structured JSON schemas for consistent output format",
    );
    console.log(
        "⚡ Response times optimized with appropriate token limits and temperature settings",
    );
}

// Execute evaluation
testMCPSystemEvaluation().catch(console.error);
