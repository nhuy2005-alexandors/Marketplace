# Product Catalog — Owner Business/Database Contract

**Status:** Implementation contract for the Marketplace Owner lane  
**Updated:** 2026-11-14  
**Persistence:** SQL Server / EF Core

## Currency contract

`Product.Price` and all Product API monetary values are denominated in USD, the canonical Marketplace catalog currency. Product prices are stored and returned without FX conversion. The known DbSeeder and synthetic Dataset V2 values are USD. Historical values are not rewritten as part of this policy.

## 1. Product identity and ownership

A Product contains Name, Sku, optional BrandId, Description, Price, Stock, ImageUrl, CategoryId, and SellerId.

- SKU is trimmed, normalized to uppercase and globally unique in the catalog. If omitted on creation, the backend generates a unique SKU.
- Brand is optional. A referenced Brand must exist.
- SellerId is always the authenticated creator's user ID. Only Admin or an Approved Seller may create products.
- Public catalog queries hide products owned by Sellers that are not Approved.
- Product deletion removes structured ProductAttributeValues first and still respects historical OrderItem and CartItem foreign keys.
- Product CategoryId is immutable through the application API because it participates in the composite principal key (Product.Id, Product.CategoryId) used by typed attribute values. Reclassification requires creating a new Product.

## 2. Brand

Brand has a unique Slug, Name, and optional Description.

- GET /api/brands is public.
- POST /api/brands, PUT /api/brands/{id}, and DELETE /api/brands/{id} require Admin.
- Slugs are normalized from the supplied slug or name. A Brand referenced by any Product cannot be deleted.

## 3. Category attribute definitions

AttributeDefinition belongs to one Category and contains Code (unique within Category), DisplayName, DataType, optional Unit, IsRequired, IsFilterable, IsComparable, and IsActive.

DataType is one of Text, Number, or Boolean.

- GET /api/catalogattributes?categoryId=... returns active definitions. Admin may request includeInactive=true.
- Admin owns create/update/delete.
- CategoryId and DataType are immutable because they participate in the composite key (Id, CategoryId, DataType). Create a new definition to change either.
- A definition referenced by ProductAttributeValues cannot be deleted; deactivate it instead.

## 4. Typed ProductAttributeValue contract

Each ProductAttributeValue references a Product and AttributeDefinition and must match both the Product category and definition category/type.

Exactly one value column is populated:
- Text → ValueText only, trimmed, max 2,000 characters.
- Number → ValueNumber only, max 4 decimal places and decimal(18,4) range.
- Boolean → ValueBoolean only; false is a valid supplied value.

The database enforces typed-value exclusivity and category/type matching through check constraints and composite foreign keys. Application validation additionally enforces:
- no duplicate AttributeDefinitionId in one product request;
- definition must be active and belong to the product category;
- every active required definition is supplied when creating a product or explicitly replacing its attribute set;
- at most 100 attributes per product request.

On Product update, omitted Attributes means preserve the current set. A supplied list replaces the active attribute set while retaining values tied to inactive definitions; those values remain hidden until the definition is reactivated. An empty list removes all active optional attributes. Existing attribute rows are updated in place where possible to avoid unique-key delete/insert races.

## 5. Product API contract

ProductDto exposes Sku, BrandId, BrandName, and active typed Attributes in addition to the existing product fields.

CreateProductRequest accepts optional Sku, BrandId, and Attributes.

UpdateProductRequest:
- omitted Sku and BrandId preserve existing values;
- ClearBrand=true explicitly clears Brand;
- omitted Attributes preserves the existing attribute set;
- supplied Attributes replaces the set;
- CategoryId must remain unchanged.

## 6. Search and filtering

GET /api/products supports:
- keyword, CategoryId, SellerId, BrandId;
- MinPrice/MaxPrice and InStockOnly;
- sort and pagination;
- up to 20 structured attribute filters, AND-combined.

Each attribute filter specifies exactly one of:
- ValueText for exact text matching;
- MinValue and/or MaxValue for numeric range matching;
- ValueBoolean for exact boolean matching.

Only active, filterable AttributeDefinitions participate in filtering. Product search is performed against the complete server-side catalog, not a browser-supplied candidate page.

## 7. Database performance and integrity

- Unique indexes: Product SKU, Brand Slug, Category+Attribute Code, Product+AttributeDefinition.
- Typed attribute indexes: AttributeDefinitionId+ValueNumber and AttributeDefinitionId+ValueBoolean, filtered to non-null values. Text values use the AttributeDefinitionId predicate and exact ValueText comparison; ValueText is not an index key because nvarchar(2000) exceeds SQL Server's nonclustered index key limit.
- OrderItems.SellerId references Users.
- TR_OrderItems_ProtectSnapshot validates ProductName/UnitPrice/SellerId against Product at insertion and prevents later changes to checkout snapshot fields.
- The trigger does not prevent legitimate fulfillment-status changes.

## 8. Frontend catalog vertical slice

- Admin product form supports SKU, optional Brand and category-defined typed attributes.
- Seller product form supports SKU, optional Brand and typed attributes for its selected category; sellers cannot manage Brand or AttributeDefinition definitions.
- Admin catalog settings supports Brand create/update/delete and AttributeDefinition create/update/deactivate/delete. Deletion is rejected when referenced; deactivation is the safe path for definitions already used by products.
- Public product cards/details show Brand/SKU/typed attributes; product list supports server-side Brand filtering and category-scoped typed attribute filters (exact text, numeric range, and boolean).
- Product category is read-only while editing because it participates in the typed attribute composite key. Required attribute definitions are enforced by the backend; the UI marks them explicitly. Inactive definition values are preserved during product edits and become visible again if the definition is reactivated.

## 9. Explicit non-goals

No vector database, recommendation scoring, RAG logic, new database, dataset regeneration, product-image enrichment, currency conversion, or speculative ML infrastructure is part of this contract. USD is the accepted canonical catalog currency. MoMo FX remains a separate provider-boundary configuration and must be explicitly approved/set before real initiation.
