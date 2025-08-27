#!/usr/bin/env node

/**
 * Simple MCP Client Test for BusBuddy Grok-4 Server
 * Tests the MCP server functionality directly
 */

import { spawn } from "child_process";

async function testMCPServer() {
    console.log("🧪 Starting MCP Server Test...");

    try {
        // Start the MCP server
        const serverProcess = spawn("node", ["servers/grok4-mcp-server.js"], {
            stdio: ["pipe", "pipe", "pipe"],
            cwd: process.cwd(),
        });

        let responseData = "";

        // Set up response handler
        serverProcess.stdout.on("data", (data) => {
            responseData += data.toString();
            console.log("📤 Server response:", data.toString().trim());
        });

        serverProcess.stderr.on("data", (data) => {
            console.log("⚠️ Server error:", data.toString().trim());
        });

        // Wait for server to initialize
        await new Promise((resolve) => setTimeout(resolve, 2000));

        // Send tools/list request
        console.log("📨 Sending tools/list request...");
        const listRequest =
            JSON.stringify({
                jsonrpc: "2.0",
                id: 1,
                method: "tools/list",
                params: {},
            }) + "\n";

        serverProcess.stdin.write(listRequest);

        // Wait for response
        await new Promise((resolve) => setTimeout(resolve, 3000));

        // Send a simple tool call
        console.log("📨 Sending bb-health tool call...");
        const toolRequest =
            JSON.stringify({
                jsonrpc: "2.0",
                id: 2,
                method: "tools/call",
                params: {
                    name: "bb-health",
                    arguments: {},
                },
            }) + "\n";

        serverProcess.stdin.write(toolRequest);

        // Wait for response
        await new Promise((resolve) => setTimeout(resolve, 5000));

        // Clean up
        serverProcess.kill();
        console.log("✅ Test completed");
    } catch (error) {
        console.error("❌ Test failed:", error);
    }
}

testMCPServer();
