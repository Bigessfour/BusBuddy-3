#!/usr/bin/env node

/**
 * BusBuddy MCP System Evaluation
 * Tests the enhanced Grok-4 MCP server with structured outputs
 */

import { spawn } from "child_process";
import { dirname, join } from "path";
import { fileURLToPath } from "url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

// Configuration
const MCP_SERVER_PATH = join(
    __dirname,
    "mcp",
    "servers",
    "grok4-mcp-server.js",
);

class MCPTester {
    constructor() {
        this.server = null;
        this.requestId = 1;
    }

    async startServer() {
        console.log("🚀 Starting BusBuddy Grok-4 MCP Server...\n");

        this.server = spawn("node", [MCP_SERVER_PATH], {
            stdio: ["pipe", "pipe", "pipe"],
            cwd: join(__dirname, "mcp", "servers"),
        });

        this.server.stderr.on("data", (data) => {
            console.error("Server Error:", data.toString());
        });

        // Wait for server startup
        await new Promise((resolve) => setTimeout(resolve, 2000));
    }

    async sendRequest(method, params = {}) {
        const request = {
            jsonrpc: "2.0",
            id: this.requestId++,
            method,
            params,
        };

        return new Promise((resolve, reject) => {
            let response = "";

            const timeout = setTimeout(() => {
                reject(new Error("Request timeout"));
            }, 30000);

            const dataHandler = (data) => {
                response += data.toString();

                try {
                    const lines = response
                        .split("\n")
                        .filter((line) => line.trim());
                    for (const line of lines) {
                        const parsed = JSON.parse(line);
                        if (parsed.id === request.id) {
                            clearTimeout(timeout);
                            this.server.stdout.off("data", dataHandler);
                            resolve(parsed);
                            return;
                        }
                    }
                } catch (e) {
                    // Continue collecting data
                }
            };

            this.server.stdout.on("data", dataHandler);
            this.server.stdin.write(JSON.stringify(request) + "\n");
        });
    }

    async testQuickMode() {
        console.log("🎯 Testing QUICK Mode (1-2 actionable items)...\n");

        const response = await this.sendRequest("tools/call", {
            name: "grok-analyze-problem",
            arguments: {
                query: "Our Syncfusion SfDataGrid is loading slowly with large datasets. Students complain about UI freezing.",
                mode: "quick",
                context:
                    "BusBuddy student management screen with 10,000+ student records",
                includeProjectStatus: false,
            },
        });

        if (response.result?.content?.[0]?.text) {
            console.log("✅ Quick Mode Response:");
            console.log(response.result.content[0].text);
            console.log("\n" + "=".repeat(80) + "\n");
        }
    }

    async testExpertMode() {
        console.log("🏗️ Testing EXPERT Mode (comprehensive analysis)...\n");

        const response = await this.sendRequest("tools/call", {
            name: "grok-syncfusion-guidance",
            arguments: {
                component: "SfDataGrid",
                issue: "Need to implement virtualization and pagination for large datasets with complex filtering",
                mode: "expert",
                xamlSnippet: `<syncfusion:SfDataGrid x:Name="StudentsGrid" 
                         ItemsSource="{Binding Students}"
                         AutoGenerateColumns="False"
                         AllowFiltering="True" />`,
            },
        });

        if (response.result?.content?.[0]?.text) {
            console.log("✅ Expert Mode Response:");
            console.log(response.result.content[0].text);
            console.log("\n" + "=".repeat(80) + "\n");
        }
    }

    async evaluateMCPSystem() {
        console.log("🔍 Evaluating Overall MCP System...\n");

        const response = await this.sendRequest("tools/call", {
            name: "grok-architecture-review",
            arguments: {
                component: "MCP Integration",
                concern: "system evaluation and optimization",
                mode: "expert",
            },
        });

        if (response.result?.content?.[0]?.text) {
            console.log("✅ MCP System Evaluation:");
            console.log(response.result.content[0].text);
            console.log("\n" + "=".repeat(80) + "\n");
        }
    }

    async testResponseModes() {
        console.log("⚡ Testing Response Mode Comparison...\n");

        const testQuery =
            "How can we optimize BusBuddy's Entity Framework Core queries for better performance?";

        // Quick mode
        const quickResponse = await this.sendRequest("tools/call", {
            name: "grok-azure-sql-optimize",
            arguments: {
                query: "SELECT * FROM Students s JOIN Routes r ON s.RouteId = r.Id WHERE s.Active = 1",
                context: "Used in main dashboard, called frequently",
                mode: "quick",
            },
        });

        // Expert mode
        const expertResponse = await this.sendRequest("tools/call", {
            name: "grok-azure-sql-optimize",
            arguments: {
                query: "SELECT * FROM Students s JOIN Routes r ON s.RouteId = r.Id WHERE s.Active = 1",
                context: "Used in main dashboard, called frequently",
                mode: "expert",
            },
        });

        console.log("🎯 QUICK Mode SQL Optimization:");
        if (quickResponse.result?.content?.[0]?.text) {
            console.log(quickResponse.result.content[0].text);
        }
        console.log("\n" + "-".repeat(40) + "\n");

        console.log("🏗️ EXPERT Mode SQL Optimization:");
        if (expertResponse.result?.content?.[0]?.text) {
            console.log(expertResponse.result.content[0].text);
        }
        console.log("\n" + "=".repeat(80) + "\n");
    }

    async cleanup() {
        if (this.server) {
            this.server.kill();
            console.log("🛑 Server stopped");
        }
    }
}

async function main() {
    const tester = new MCPTester();

    try {
        await tester.startServer();

        // Test sequence
        await tester.testQuickMode();
        await tester.testExpertMode();
        await tester.testResponseModes();
        await tester.evaluateMCPSystem();

        console.log("✅ All tests completed successfully!");
    } catch (error) {
        console.error("❌ Test failed:", error.message);
    } finally {
        await tester.cleanup();
    }
}

// Handle process termination
process.on("SIGINT", async () => {
    console.log("\n🛑 Test interrupted");
    process.exit(0);
});

process.on("SIGTERM", async () => {
    console.log("\n🛑 Test terminated");
    process.exit(0);
});

main().catch(console.error);
