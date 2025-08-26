#!/usr/bin/env node

/**
 * Simple Direct Grok-4 API Test
 */

async function testDirectGrokAPI() {
    const apiKey = process.env.XAI_API_KEY;
    
    if (!apiKey) {
        console.log('❌ XAI_API_KEY not found');
        return;
    }
    
    console.log('🔑 API Key found:', apiKey.substring(0, 10) + '...');
    
    try {
        const response = await fetch('https://api.x.ai/v1/chat/completions', {
            method: 'POST',
            headers: {
                'Authorization': `Bearer ${apiKey}`,
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                messages: [
                    {
                        role: 'user',
                        content: 'Hello Grok! This is a test from BusBuddy MCP. Please respond with a simple greeting.'
                    }
                ],
                model: 'grok-4-0709',
                max_tokens: 100
            })
        });
        
        console.log('📡 Response status:', response.status);
        
        if (response.ok) {
            const data = await response.json();
            console.log('\n🎉 GROK-4 RESPONDED!');
            console.log('=' .repeat(50));
            console.log(data.choices[0].message.content);
            console.log('=' .repeat(50));
        } else {
            const error = await response.text();
            console.log('❌ API Error:', error);
        }
        
    } catch (error) {
        console.log('❌ Network Error:', error.message);
    }
}

testDirectGrokAPI();
