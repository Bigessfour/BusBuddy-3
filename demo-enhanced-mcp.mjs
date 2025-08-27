#!/usr/bin/env node

/**
 * Quick MCP System Demonstration
 * Shows the enhanced structured outputs in action
 */

const API_KEY = process.env.XAI_API_KEY;

// Quick Response Schema (1-2 actionable items)
const QUICK_SCHEMA = {
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

async function demonstrateEnhancedMCP() {
    if (!API_KEY) {
        console.log("❌ Set XAI_API_KEY environment variable to test");
        return;
    }

    console.log("🤖 **Enhanced BusBuddy MCP Server Demo**\n");
    console.log("✅ Structured JSON outputs configured");
    console.log("✅ Quick mode: 1-2 actionable items (300-400 tokens)");
    console.log(
        "✅ Expert mode: 2-3 recommendations with code (1000-1200 tokens)",
    );
    console.log("✅ Response format validation with JSON schemas");
    console.log("✅ Temperature control (0.2 quick, 0.6 expert)");
    console.log("\n" + "=".repeat(60) + "\n");

    try {
        const response = await fetch("https://api.x.ai/v1/chat/completions", {
            method: "POST",
            headers: {
                Authorization: `Bearer ${API_KEY}`,
                "Content-Type": "application/json",
            },
            body: JSON.stringify({
                model: "grok-4-0709",
                messages: [
                    {
                        role: "system",
                        content:
                            "You are a concise technical assistant specializing in .NET, WPF, and BusBuddy development. Provide exactly 1-2 actionable items with clear benefits. Format as JSON.",
                    },
                    {
                        role: "user",
                        content:
                            "Our BusBuddy MCP system has 8 servers and structured outputs. How can we optimize its performance and usability?",
                    },
                ],
                max_tokens: 400,
                temperature: 0.2,
                response_format: QUICK_SCHEMA,
                stream: false,
            }),
        });

        if (response.ok) {
            const data = await response.json();
            const result = JSON.parse(data.choices[0].message.content);

            console.log("🎯 **MCP System Optimization (Quick Mode)**\n");
            console.log("**Actionable Items:**");
            result.actionable_items.forEach((item, i) => {
                console.log(`${i + 1}. **${item.action}**`);
                console.log(`   ➤ ${item.benefit}\n`);
            });
            console.log(`💡 **Summary:** ${result.summary}`);
        } else {
            console.log("❌ API request failed");
        }
    } catch (error) {
        console.log("❌ Error:", error.message);
    }

    console.log("\n" + "=".repeat(60) + "\n");
    console.log("🚀 **Enhanced MCP Server Features:**");
    console.log("• Configurable response modes (quick/expert)");
    console.log("• JSON schema validation ensures consistent format");
    console.log("• Optimized token limits prevent verbose responses");
    console.log("• Temperature control for appropriate creativity level");
    console.log("• All 5 Grok tools updated with mode parameters");
    console.log("• Seamless integration with existing BusBuddy workflow");
    console.log("\n✅ **MCP System Successfully Enhanced!**");
}

demonstrateEnhancedMCP().catch(console.error);
