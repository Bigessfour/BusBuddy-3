#!/usr/bin/env node

/**
 * Test script for Grok-4 MCP Server
 * Validates basic functionality without requiring XAI API key
 */

import { spawn } from "child_process";
import fs from "fs/promises";
import path from "path";

const PROJECT_ROOT = process.env.BUSBUDDY_PROJECT_ROOT || path.resolve(process.cwd(), "..");

async function testGrok4Server() {
    console.log("🧪 Testing Grok-4 MCP Server...\n");

    // Test 1: Server starts without API key
    console.log("1️⃣ Testing server startup (no API key)...");
    
    try {
        // Start server without API key to test graceful degradation
        const serverProcess = spawn("node", ["servers/grok4-mcp-server.js"], {
            cwd: process.cwd(),
            env: { ...process.env, XAI_API_KEY: "" }, // Explicitly remove API key
            stdio: ["pipe", "pipe", "pipe"]
        });

        let stdout = "";
        let stderr = "";

        serverProcess.stdout.on("data", (data) => {
            stdout += data.toString();
        });

        serverProcess.stderr.on("data", (data) => {
            stderr += data.toString();
        });

        // Give server time to start and show warning
        setTimeout(() => {
            serverProcess.kill("SIGTERM");
        }, 2000);

        await new Promise((resolve) => {
            serverProcess.on("close", () => resolve());
        });

        if (stderr.includes("XAI_API_KEY not set") && stderr.includes("Grok-4 MCP server running")) {
            console.log("✅ Server starts gracefully without API key");
            console.log("✅ Shows appropriate warning message");
        } else {
            console.log("❌ Server startup test failed");
            console.log("stderr:", stderr);
        }

    } catch (error) {
        console.log("❌ Server startup test failed:", error.message);
    }

    console.log();

    // Test 2: Validate PowerShell command integration
    console.log("2️⃣ Testing PowerShell integration...");
    
    try {
        const testCommand = spawn("pwsh", ["-Command", "Get-Location"], {
            cwd: PROJECT_ROOT
        });

        let output = "";
        testCommand.stdout.on("data", (data) => {
            output += data.toString();
        });

        await new Promise((resolve) => {
            testCommand.on("close", (code) => {
                if (code === 0 && output.includes("BusBuddy")) {
                    console.log("✅ PowerShell integration working");
                    console.log("✅ Project root detected correctly");
                } else {
                    console.log("❌ PowerShell integration test failed");
                }
                resolve();
            });
        });

    } catch (error) {
        console.log("❌ PowerShell test failed:", error.message);
    }

    console.log();

    // Test 3: Validate file access
    console.log("3️⃣ Testing file system access...");
    
    try {
        const testFile = path.join(PROJECT_ROOT, "BusBuddy.sln");
        const stats = await fs.stat(testFile);
        
        if (stats.isFile()) {
            console.log("✅ Can access BusBuddy project files");
        } else {
            console.log("❌ Cannot access project files");
        }

        // Test reading a sample C# file
        const sampleCsFile = path.join(PROJECT_ROOT, "BusBuddy.WPF", "App.xaml.cs");
        const content = await fs.readFile(sampleCsFile, 'utf-8');
        
        if (content.includes("namespace") || content.includes("class")) {
            console.log("✅ Can read C# source files for analysis");
        } else {
            console.log("❌ Cannot properly read C# files");
        }

    } catch (error) {
        console.log("❌ File system test failed:", error.message);
    }

    console.log();

    // Test 4: Validate MCP configuration
    console.log("4️⃣ Testing MCP configuration...");
    
    try {
        const mcpConfigPath = path.join(PROJECT_ROOT, ".vscode", "mcp.json");
        const mcpConfig = await fs.readFile(mcpConfigPath, 'utf-8');
        const config = JSON.parse(mcpConfig);
        
        if (config.servers && config.servers["busbuddy-grok4-mcp"]) {
            console.log("✅ MCP configuration includes Grok-4 server");
            console.log("✅ VS Code integration configured");
        } else {
            console.log("❌ MCP configuration missing Grok-4 server");
        }

    } catch (error) {
        console.log("❌ MCP configuration test failed:", error.message);
    }

    console.log();

    // Test Summary
    console.log("🎯 Test Summary:");
    console.log("- Server graceful startup: Tested");
    console.log("- PowerShell integration: Tested");
    console.log("- File system access: Tested");
    console.log("- MCP configuration: Tested");
    console.log();
    console.log("🚀 Next Steps:");
    console.log("1. Set XAI_API_KEY environment variable");
    console.log("2. Test with actual Grok-4 API calls");
    console.log("3. Integration test with VS Code MCP extension");
    console.log();
    console.log("📖 Setup Guide: mcp/GROK4-INTEGRATION.md");
}

// Run tests
testGrok4Server().catch(console.error);
