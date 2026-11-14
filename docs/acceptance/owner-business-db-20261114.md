# Owner Business + Database Lane — Implementation Receipt

**Date:** 2026-11-14  
**Repository:** nhuy2005-alexandors/Marketplace  
**Base:** PR #3 head 442d55922fa88390975fef88148b6b20eca33292  
**Branch:** codex/marketplace-owner-catalog-integrity-20261114

## Scope

This work covers the Owner-owned Product/Catalog business contract and database integrity. It does not coordinate or depend on Khang's RAG lane or Khánh's Recommendation lane.

## Implemented in this branch

- Product API/DTO exposes SKU, optional Brand and typed structured attributes.
- Admin Brand CRUD and AttributeDefinition CRUD; public read endpoints.
- Product create/update validates brand/category/type/required attributes and typed value exclusivity.
- Product search filters by Brand and typed text/numeric/boolean attributes.
- Product category and AttributeDefinition category/type are immutable through the application API where they participate in composite keys.
- Product deletion removes dependent structured values first and still respects historical order/cart FKs.
- Public catalog hides products owned by unapproved Sellers.
- Review eligibility requires a Paid/Shipped/Delivered order, Completed non-demo payment, no reconciliation hold, and a non-cancelled matching OrderItem.
- Seller fulfillment validator matches service behavior: Seller can only set Shipped or Delivered.
- Coupon expiration is exclusive at ExpiresAt <= now.
- Dashboard revenue and top-product aggregation excludes cancelled OrderItems.
- SQL migration adds OrderItems.SellerId FK, enum check constraints, typed numeric/boolean attribute indexes and immutable checkout-snapshot trigger.
- SRS and domain specs updated to the current COD, seller approval, review, catalog and payment behavior.

## Database migration

Migration: 20261114120000_OwnerCatalogIntegrity

Expected migration count after PR #3's 15 migrations: 16.

The migration is guarded to the project's LocalDB ECommerceDb_Dev and checks orphan Seller IDs and invalid enum values before adding constraints. It does not seed, import, reset, restore or mutate the V2 dataset.

## Verification status

This document is an implementation receipt, not a claim that tests have passed. Final status depends on:
- GitHub CI for unit/integration tests and frontend/backend builds;
- opt-in Windows LocalDB test for HasPendingModelChanges, FK migration and trigger behavior;
- Owner review of the migration against the disposable development database.

Currency remains OPEN. No production currency or FX conversion is inferred from synthetic V2 data.
