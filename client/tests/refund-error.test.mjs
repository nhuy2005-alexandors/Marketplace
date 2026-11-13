import { test } from 'node:test';
import assert from 'node:assert/strict';
import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createServer } from 'vite';

test('admin refund list reports a failed load instead of a blank page', async () => {
  const vite = await createServer({ configFile: 'vite.config.ts', server: { middlewareMode: true }, appType: 'custom' });
  try {
    const { AdminRefundsPage } = await vite.ssrLoadModule('/src/pages/AdminRefundsPage.tsx');
    const client = new QueryClient({ defaultOptions: { queries: { retry: false, retryOnMount: false } } });
    const query = client.getQueryCache().build(client, { queryKey: ['admin-refunds', 'Pending', 1, 20] });
    query.setState({ status: 'error', fetchStatus: 'idle', error: new Error('isolated test failure') });
    const html = renderToStaticMarkup(React.createElement(QueryClientProvider, { client }, React.createElement(AdminRefundsPage)));
    assert.match(html, /Không thể tải danh sách hoàn tiền/);
    assert.match(html, /role="alert"/);
    client.clear();
  } finally { await vite.close(); }
});
