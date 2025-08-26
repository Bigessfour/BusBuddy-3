#!/usr/bin/env node

/**
 * BusBuddy MCP Server Manager
 * Utility to start, stop, and manage MCP servers
 */

import { spawn } from 'child_process';
import fs from 'fs/promises';
import { dirname, join } from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

class MCPServerManager {
  constructor() {
    this.servers = new Map();
    this.configPath = join(__dirname, '..', 'config', 'server-configs.json');
  }

  async loadConfig() {
    try {
      const configData = await fs.readFile(this.configPath, 'utf-8');
      return JSON.parse(configData);
    } catch (error) {
      console.error('❌ Failed to load server configuration:', error.message);
      return null;
    }
  }

  async startServer(serverName) {
    const config = await this.loadConfig();
    if (!config?.servers[serverName]) {
      console.error(`❌ Server ${serverName} not found in configuration`);
      return false;
    }

    const serverPath = join(__dirname, '..', 'servers', `${serverName}.js`);
    
    try {
      console.log(`🚀 Starting ${config.servers[serverName].name}...`);
      
      const server = spawn('node', [serverPath], {
        stdio: ['pipe', 'pipe', 'pipe'],
        cwd: join(__dirname, '..', 'servers')
      });

      server.stdout.on('data', (data) => {
        console.log(`[${serverName}] ${data.toString().trim()}`);
      });

      server.stderr.on('data', (data) => {
        console.error(`[${serverName} ERROR] ${data.toString().trim()}`);
      });

      server.on('exit', (code) => {
        console.log(`🛑 ${serverName} exited with code ${code}`);
        this.servers.delete(serverName);
      });

      this.servers.set(serverName, server);
      console.log(`✅ ${serverName} started successfully`);
      return true;

    } catch (error) {
      console.error(`❌ Failed to start ${serverName}:`, error.message);
      return false;
    }
  }

  async stopServer(serverName) {
    const server = this.servers.get(serverName);
    if (!server) {
      console.log(`⚠️ Server ${serverName} is not running`);
      return;
    }

    server.kill('SIGTERM');
    this.servers.delete(serverName);
    console.log(`🛑 Stopped ${serverName}`);
  }

  async listServers() {
    const config = await this.loadConfig();
    if (!config) return;

    console.log('\n📋 **Available MCP Servers:**\n');
    
    Object.entries(config.servers).forEach(([name, info]) => {
      const status = this.servers.has(name) ? '🟢 Running' : '⚪ Stopped';
      console.log(`${status} **${name}**`);
      console.log(`   📝 ${info.description}`);
      console.log(`   🔧 Version: ${info.version}`);
      console.log(`   🛠️ Tools: ${info.tools?.length || 0} available\n`);
    });
  }

  async stopAllServers() {
    console.log('🛑 Stopping all servers...');
    for (const [name] of this.servers) {
      await this.stopServer(name);
    }
  }

  async healthCheck() {
    console.log('\n🏥 **MCP Server Health Check**\n');
    
    const config = await this.loadConfig();
    if (!config) return;

    for (const [serverName, serverInfo] of Object.entries(config.servers)) {
      const isRunning = this.servers.has(serverName);
      const status = isRunning ? '✅ Healthy' : '⚠️ Not Running';
      
      console.log(`${status} ${serverInfo.name}`);
      
      if (serverInfo.environment_variables) {
        serverInfo.environment_variables.forEach(envVar => {
          const hasEnv = process.env[envVar] ? '✅' : '❌';
          console.log(`   ${hasEnv} ${envVar}`);
        });
      }
    }
    
    console.log('\n📊 **System Status:**');
    console.log(`🔧 Active Servers: ${this.servers.size}`);
    console.log(`📁 Config File: ${this.configPath}`);
    console.log(`🌍 Environment: ${process.env.NODE_ENV || 'development'}`);
  }
}

// CLI Interface
async function main() {
  const manager = new MCPServerManager();
  const command = process.argv[2];
  const serverName = process.argv[3];

  switch (command) {
    case 'start':
      if (!serverName) {
        console.error('❌ Please specify a server name');
        process.exit(1);
      }
      await manager.startServer(serverName);
      break;

    case 'stop':
      if (!serverName) {
        console.error('❌ Please specify a server name');
        process.exit(1);
      }
      await manager.stopServer(serverName);
      break;

    case 'list':
      await manager.listServers();
      break;

    case 'health':
      await manager.healthCheck();
      break;

    case 'stop-all':
      await manager.stopAllServers();
      break;

    default:
      console.log(`
🚌 **BusBuddy MCP Server Manager**

Usage:
  node server-manager.js <command> [server-name]

Commands:
  start <server>    Start a specific MCP server
  stop <server>     Stop a specific MCP server  
  stop-all          Stop all running servers
  list              List all available servers
  health            Check server health status

Examples:
  node server-manager.js start grok4-mcp-server
  node server-manager.js stop busbuddy-mcp-server
  node server-manager.js health
      `);
  }
}

// Handle process termination
process.on('SIGINT', async () => {
  console.log('\n🛑 Shutting down MCP Server Manager...');
  const manager = new MCPServerManager();
  await manager.stopAllServers();
  process.exit(0);
});

if (import.meta.url === `file://${process.argv[1]}`) {
  main().catch(console.error);
}

export default MCPServerManager;
