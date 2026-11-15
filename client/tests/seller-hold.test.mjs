import { test } from 'node:test';
import assert from 'node:assert/strict';
import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createServer } from 'vite';

async function renderSellerOrder(overrides) {
  const vite = await createServer({ configFile: 'vite.config.ts', server: { middlewareMode: true, hmr: false }, appType: 'custom' });
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  try {
    const { SellerOrdersPage } = await vite.ssrLoadModule('/src/pages/SellerOrdersPage.tsx');
    client.setQueryData(['seller-orders', 1, 5], { items: [{
      id: 1, status: 'Paid', createdAt: '2026-11-15T00:00:00Z', shippingAddress: 'Audit', subtotal: 20, discountAmount: 0, total: 20,
      payment: { status: 'Completed', method: 'EWallet' }, refundPending: false,
      items: [{ id: 1, productId: 1, productName: 'Audit', quantity: 1, subtotal: 20, status: 'Pending' }], ...overrides,
    }], page: 1, totalPages: 1 });
    return renderToStaticMarkup(React.createElement(QueryClientProvider, { client }, React.createElement(SellerOrdersPage)));
  } finally { client.clear(); await vite.close(); }
}

test('seller cannot ship an order with a pending refund hold', async () => {
  const html = await renderSellerOrder({ refundPending: true });
  assert.doesNotMatch(html, /Đánh dấu đã gửi|Đánh dấu đã giao/);
  assert.match(html, /tạm dừng giao hàng/);
});

test('seller cannot ship unpaid or reconciliation-held orders', async () => {
  for (const overrides of [
    { status: 'Pending', payment: { status: 'Pending', method: 'EWallet' } },
    { payment: { status: 'Completed', method: 'EWallet', requiresReconciliation: true } },
  ]) assert.doesNotMatch(await renderSellerOrder(overrides), /Đánh dấu đã gửi|Đánh dấu đã giao/);
});

test('accepted uncollected COD remains fulfillable', async () => {
  const html = await renderSellerOrder({ status: 'Accepted', payment: { status: 'Pending', method: 'CashOnDelivery' } });
  assert.match(html, /Đánh dấu đã gửi/);
});
