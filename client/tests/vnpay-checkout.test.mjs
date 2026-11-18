import { test } from 'node:test';
import assert from 'node:assert/strict';
import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createServer } from 'vite';

test('checkout offers VNPAY sandbox and disables MoMo while its gateway is unavailable', async () => {
  const vite = await createServer({ configFile: 'vite.config.ts', server: { middlewareMode: true, hmr: false }, appType: 'custom' });
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  try {
    const { CheckoutPage } = await vite.ssrLoadModule('/src/pages/CheckoutPage.tsx');
    const html = renderToStaticMarkup(React.createElement(QueryClientProvider, { client },
      React.createElement(MemoryRouter, null, React.createElement(CheckoutPage))));
    assert.match(html, /value="vnpay"[^>]*>VNPAY Sandbox/);
    assert.match(html, /value="momo"[^>]*disabled/);
  } finally { client.clear(); await vite.close(); }
});
