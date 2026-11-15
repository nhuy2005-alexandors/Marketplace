import { test } from 'node:test';
import assert from 'node:assert/strict';
import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createServer } from 'vite';

async function renderOrders(prepare) {
  const vite = await createServer({ configFile: 'vite.config.ts', server: { middlewareMode: true, hmr: false }, appType: 'custom' });
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, retryOnMount: false } } });
  try {
    const { OrdersPage } = await vite.ssrLoadModule('/src/pages/OrdersPage.tsx');
    prepare(client);
    return renderToStaticMarkup(React.createElement(QueryClientProvider, { client },
      React.createElement(MemoryRouter, null, React.createElement(OrdersPage))));
  } finally { client.clear(); await vite.close(); }
}

test('customer receives an error and retry control when orders fail to load', async () => {
  const html = await renderOrders(client => {
    client.getQueryCache().build(client, { queryKey: ['orders', 1, 5] })
      .setState({ status: 'error', fetchStatus: 'idle', error: new Error('isolated load failure') });
  });
  assert.match(html, /Không thể tải đơn hàng hoặc lịch sử hoàn tiền/);
  assert.match(html, /role="alert"/);
  assert.match(html, /Thử lại/);
});

test('pending hold displays history and suppresses a second refund button', async () => {
  const html = await renderOrders(client => {
    client.setQueryData(['orders', 1, 5], { items: [{ id: 123, createdAt: '2026-01-01T00:00:00Z', status: 'Paid', shippingAddress: 'Synthetic', subtotal: 200, discountAmount: 0, total: 200, refundPending: true,
      payment: { status: 'Completed', method: 'CreditCard' }, items: [{ productId: 1, productName: 'Synthetic', quantity: 2, status: 'Pending', subtotal: 200 }] }], page: 1, totalPages: 1 });
    client.setQueryData(['refunds', 'mine', 1, 100, [123]], { items: [{ id: 1, orderId: 123, status: 'Pending', reason: 'Synthetic reason' }], page: 1, totalPages: 1 });
  });
  assert.match(html, /Đơn đang tạm dừng giao hàng/);
  assert.match(html, /Đang chờ xử lý/);
  assert.doesNotMatch(html, />Yêu cầu hoàn tiền<\/button>/);
});
