# Marketplace Seller and Fulfillment Contract

**Updated:** 2026-11-14

## Seller lifecycle

- POST /api/auth/register-seller creates a Seller in Pending status.
- Admin approval moves the Seller to Approved.
- Only Approved Sellers can create products and appear in public seller/product discovery.
- Admin-created products remain supported; their SellerId is the Admin user ID.

## Product ownership

- Seller can create, update and delete only products whose SellerId matches the authenticated user.
- Admin can manage products across sellers.
- SellerId is assigned by the backend; it is never accepted from the client as an ownership override.

## Per-item fulfillment

- A multi-seller Order contains OrderItems with immutable SellerId snapshots.
- Seller can update only their own OrderItems.
- Seller fulfillment transitions are Pending → Shipped → Delivered.
- Seller cannot mark an item Cancelled. Cancellation is performed only by the whole-order cancellation/refund workflow while fulfillment has not started.
- Whole Order status and Payment status remain separate from per-item fulfillment.
- OrderItems.SellerId has a Users FK. A SQL trigger verifies the inserted checkout snapshot matches the current Product and prevents later edits to OrderId, ProductId, SellerId, ProductName, UnitPrice or Quantity.
