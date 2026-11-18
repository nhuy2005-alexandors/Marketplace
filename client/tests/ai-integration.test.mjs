import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

test('private query hooks bind account-owned caches to the current user', async () => {
  const source = await readFile(new URL('../src/api/hooks.ts', import.meta.url), 'utf8');
  assert.match(source, /queryKey:\s*\["cart", userId\]/);
  assert.match(source, /queryKey: userId === null \? \["orders", page, pageSize\] : \["orders", page, pageSize, userId\]/);
  assert.match(source, /queryKey:\s*\["wishlist", userId\]/);
  assert.match(source, /queryKey:\s*\["recommendations", userId, take\]/);
});

test('assistant requests carry conversation context and ignore late identity responses', async () => {
  const source = await readFile(new URL('../src/pages/AssistantPage.tsx', import.meta.url), 'utf8');
  assert.match(source, /body:\s*\{ query, conversation: \{ conversationId: conversationId\.current, recentTurns \} \}/);
  assert.match(source, /new AbortController\(\)/);
  assert.match(source, /requestIdentity !== identityRef\.current/);
  assert.match(source, /conversationId\.current = response\.conversationId/);
  assert.match(source, /Gemini chọn sản phẩm · dữ liệu catalog/);
});
