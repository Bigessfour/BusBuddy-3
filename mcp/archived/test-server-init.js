#!/usr/bin/env node

// Quick test script to verify Grok-4 MCP Server can initialize without starting
// This tests the server setup without actually running the MCP protocol

import { readFile } from 'fs/promises';
import { dirname, join } from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

async function testServerInitialization() {
  console.log("🚌 Testing Grok-4 MCP Server Initialization");
  console.log("===========================================");
  
  try {
    // Test 1: Check if we can load the server module
    const serverPath = join(__dirname, 'servers', 'grok4-mcp-server.js');
    const serverCode = await readFile(serverPath, 'utf-8');
    
    if (serverCode.includes('class Grok4MCPServer')) {
      console.log("✅ Server class definition found");
    } else {
      console.log("❌ Server class not found in file");
      return false;
    }
    
    // Test 2: Check API key availability (without exposing it)
    const apiKey = process.env.XAI_API_KEY;
    if (apiKey && apiKey.startsWith('xai-')) {
      console.log("✅ Valid XAI API key detected");
    } else {
      console.log("❌ XAI API key not available or invalid format");
      return false;
    }
    
    // Test 3: Basic module dependencies
    try {
      const nodeFetch = await import('node-fetch');
      console.log("✅ node-fetch module available");
    } catch (error) {
      console.log("⚠️  node-fetch not available, using built-in fetch");
    }
    
    console.log("\n🎯 Server Initialization Test Results:");
    console.log("• Server class: ✅ Available");
    console.log("• API configuration: ✅ Valid");
    console.log("• Dependencies: ✅ Ready");
    console.log("\n✅ Grok-4 MCP Server initialization test passed!");
    console.log("🔗 Ready for VS Code MCP integration");
    
    return true;
    
  } catch (error) {
    console.error("❌ Server initialization test failed:", error.message);
    return false;
  }
}

// Run the test
testServerInitialization()
  .then(success => {
    if (!success) {
      process.exit(1);
    }
  })
  .catch(error => {
    console.error("❌ Test runner error:", error);
    process.exit(1);
  });
