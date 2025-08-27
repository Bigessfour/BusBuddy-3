#!/usr/bin/env node

/**
 * Git MCP Server for BusBuddy
 * Provides Git operations via Model Context Protocol
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
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PROJECT_ROOT =
    process.env.BUSBUDDY_PROJECT_ROOT || path.resolve(__dirname, "..", "..");

class GitMCPServer {
    constructor() {
        this.server = new Server(
            {
                name: "busbuddy-git",
                version: "1.0.0",
            },
            {
                capabilities: {
                    tools: {},
                },
            },
        );

        this.setupToolHandlers();
        this.setupErrorHandling();
    }

    setupErrorHandling() {
        this.server.onerror = (error) =>
            console.error("[Git MCP Error]", error);
        process.on("SIGINT", async () => {
            await this.server.close();
            process.exit(0);
        });
    }

    setupToolHandlers() {
        this.server.setRequestHandler(ListToolsRequestSchema, async () => ({
            tools: [
                {
                    name: "git-status",
                    description: "Get Git repository status",
                    inputSchema: {
                        type: "object",
                        properties: {},
                    },
                },
                {
                    name: "git-log",
                    description: "Get Git commit history",
                    inputSchema: {
                        type: "object",
                        properties: {
                            count: {
                                type: "number",
                                description: "Number of commits to show",
                                default: 10,
                            },
                        },
                    },
                },
                {
                    name: "git-diff",
                    description: "Show Git diff for unstaged changes",
                    inputSchema: {
                        type: "object",
                        properties: {
                            file: {
                                type: "string",
                                description: "Specific file to diff (optional)",
                            },
                        },
                    },
                },
                {
                    name: "git-branch",
                    description: "List Git branches",
                    inputSchema: {
                        type: "object",
                        properties: {
                            remote: {
                                type: "boolean",
                                description: "Include remote branches",
                                default: false,
                            },
                        },
                    },
                },
                {
                    name: "git-add",
                    description: "Stage files for commit",
                    inputSchema: {
                        type: "object",
                        properties: {
                            files: {
                                type: "array",
                                items: { type: "string" },
                                description: "Files to stage (use '.' for all)",
                            },
                        },
                        required: ["files"],
                    },
                },
                {
                    name: "git-commit",
                    description: "Commit staged changes",
                    inputSchema: {
                        type: "object",
                        properties: {
                            message: {
                                type: "string",
                                description: "Commit message",
                            },
                        },
                        required: ["message"],
                    },
                },
            ],
        }));

        this.server.setRequestHandler(
            CallToolRequestSchema,
            async (request) => {
                const { name, arguments: args } = request.params;

                try {
                    switch (name) {
                        case "git-status":
                            return await this.runGitCommand([
                                "status",
                                "--porcelain",
                            ]);

                        case "git-log":
                            const count = args?.count || 10;
                            return await this.runGitCommand([
                                "log",
                                "--oneline",
                                `-${count}`,
                            ]);

                        case "git-diff":
                            const diffArgs = args?.file
                                ? ["diff", args.file]
                                : ["diff"];
                            return await this.runGitCommand(diffArgs);

                        case "git-branch":
                            const branchArgs = args?.remote
                                ? ["branch", "-a"]
                                : ["branch"];
                            return await this.runGitCommand(branchArgs);

                        case "git-add":
                            return await this.runGitCommand([
                                "add",
                                ...args.files,
                            ]);

                        case "git-commit":
                            return await this.runGitCommand([
                                "commit",
                                "-m",
                                args.message,
                            ]);

                        default:
                            throw new McpError(
                                ErrorCode.MethodNotFound,
                                `Unknown tool: ${name}`,
                            );
                    }
                } catch (error) {
                    throw new McpError(
                        ErrorCode.InternalError,
                        `Git command failed: ${error.message}`,
                    );
                }
            },
        );
    }

    async runGitCommand(args) {
        return new Promise((resolve, reject) => {
            const git = spawn("git", args, {
                cwd: PROJECT_ROOT,
                stdio: ["pipe", "pipe", "pipe"],
            });

            let stdout = "";
            let stderr = "";

            git.stdout.on("data", (data) => {
                stdout += data.toString();
            });

            git.stderr.on("data", (data) => {
                stderr += data.toString();
            });

            git.on("close", (code) => {
                const output = stdout || stderr;
                resolve({
                    content: [
                        {
                            type: "text",
                            text: `Git command: git ${args.join(" ")}\nExit code: ${code}\n\n${output}`,
                        },
                    ],
                });
            });

            git.on("error", (error) => {
                reject(
                    new Error(
                        `Failed to execute git command: ${error.message}`,
                    ),
                );
            });
        });
    }

    async run() {
        const transport = new StdioServerTransport();
        await this.server.connect(transport);
        console.error("Git MCP Server running on stdio");
    }
}

const server = new GitMCPServer();
server.run().catch(console.error);
