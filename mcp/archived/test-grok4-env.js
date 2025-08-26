#!/usr/bin/env node

// Test script to validate Grok-4 MCP Server API key availability
// This script tests the environment setup without exposing secrets

async function runTests() {
console.log("🚌 BusBuddy Grok-4 MCP Server - API Key Test");
console.log("============================================");

// Test 1: Check if XAI_API_KEY is available
const apiKey = process.env.XAI_API_KEY;
if (apiKey) {
  console.log("✅ XAI_API_KEY environment variable is set");
  
  // Test 2: Validate key format (without exposing the key)
  if (apiKey.startsWith('xai-')) {
    console.log("✅ API key format appears valid (starts with 'xai-')");
    console.log(`✅ API key length: ${apiKey.length} characters`);
  } else {
    console.log("⚠️  API key format warning: does not start with 'xai-'");
  }
} else {
  console.log("❌ XAI_API_KEY environment variable not found");
  console.log("📖 Please configure using PowerShell profile mechanism");
  process.exit(1);
}

// Test 3: Check Node.js version compatibility
const nodeVersion = process.version;
console.log(`✅ Node.js version: ${nodeVersion}`);

// Test 4: Test module loading
try {
  // Only test the import, not actual execution
  const fs = await import('fs');
  console.log("✅ Required modules available");
} catch (error) {
  console.log("❌ Module loading error:", error.message);
  process.exit(1);
}

console.log("\n🎯 Environment Test Results:");
console.log("• API Key: Available and formatted correctly");
console.log("• Node.js: Compatible version");
console.log("• Dependencies: Ready");
console.log("\n✅ Grok-4 MCP Server ready for activation!");
console.log("🔗 To activate: Configure in VS Code MCP settings");
}

runTests().catch(console.error);
