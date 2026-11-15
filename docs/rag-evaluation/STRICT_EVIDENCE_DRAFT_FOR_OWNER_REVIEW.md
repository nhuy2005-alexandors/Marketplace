# Strict evidence domain draft — owner review required

Status: drafted from the local schema report files in `_reports/attr-defs.txt`, `_reports/attr-ranges.txt`, and `_reports/brands.txt`. These are snapshots, not a fresh database read. No database was queried or changed for this draft. The authored query fixtures are synthetic test material and have not been owner-reviewed.

## Proposed supported typed attributes

The parser should use active `AttributeDefinitions` and current `ProductAttributeValues` at runtime. Configuration aliases only provide user vocabulary; they do not define schema fields. Current snapshot:

| Category | Code | Type/unit | Snapshot range or values |
|---|---|---|---|
| Audio & Wearables | anc | Boolean | both true/false values represented |
| Audio & Wearables | battery | Number / hours | combined code range 10–6000; confirm category-specific unit/value distribution |
| Audio & Wearables | codec, color, connection | Text | 3 codec values, 5 colors, 3 connection values |
| Audio & Wearables | weight | Number / g | 100–2000 across all categories |
| Books & Learning | author, format, language, publisher | Text | author 3, format 3, language 2, publisher 3 values |
| Books & Learning | edition, pages | Number | edition 1–4, pages 120–400 |
| Computers & Laptops | cpu, gpu, os | Text | 3 CPU, 3 GPU, 2 OS values |
| Computers & Laptops | screen | Number / inches | 6.1–17.3 |
| Computers & Laptops | ram, storage | Number / GB | RAM 8–64; storage 128–2048 |
| Fashion & Accessories | care, fit, material, size, color | Text | care 3, fit 3, material 4, size 4, color shared 5 values |
| Fashion & Accessories | weight | Number / g | see shared range above |
| Gaming & Accessories | compatibility, connection, platform, resolution, color | Text | 3/3/2/3/5 values respectively |
| Gaming & Accessories | weight | Number / g | see shared range above |
| Home & Living | capacity | Number / L | 1–10 |
| Home & Living | depth, height, width | Number / mm | 50–250, 100–400, 120–450 |
| Home & Living | material | Text | 4 values |
| Home & Living | power | Number / W | 10–150 |
| Smartphones & Tablets | battery | Number / mAh | merged code range 10–6000; confirm category-specific values |
| Smartphones & Tablets | camera | Number / MP | 12–108 |
| Smartphones & Tablets | chipset | Text | 3 values |
| Smartphones & Tablets | screen | Number / inches | 6.1–17.3 |
| Smartphones & Tablets | ram, storage | Number / GB | RAM 8–64; storage 128–2048 |
| Sports & Outdoors | activity, material, skill, color | Text | activity 4, material 4, skill 3, color shared 5 values |
| Sports & Outdoors | length | Number / mm | 500–2000 |
| Sports & Outdoors | weight | Number / g | see shared range above |

The code names `battery`, `color`, `connection`, `material`, `ram`, `screen`, `storage`, and `weight` repeat across categories. The implementation groups equivalent parsed conditions and ORs matching active definition IDs, then requires each distinct condition group. Numeric comparison units must match the definition unit; TB is converted to 1024 GB. Currency is not inferred. Request price filters retain the existing SQL behavior.

## Brand snapshot

The report contains 35 demo brands, `FictionBrand 01` through `FictionBrand 35`. Runtime brand resolution is exact after normalization against the live `Brands` table. An explicit but unknown brand condition is represented as impossible (`BrandId=-1`) and therefore yields no products.

## Owner decisions to confirm later

- Are RAM/storage, screen, battery, ANC, color and connection the only attributes for strict conversational parsing in this demo?
- Should text values besides colors and connection be parsed (e.g. CPU/GPU/OS, book language/format, material, compatibility/platform)?
- Confirm TB=1024 GB and comparison wording/behavior; confirm that mixed currency labels are never inferred from a numeric price.
- Confirm the `AssistantDomain` Vietnamese/English aliases, especially boolean negation (“không ANC”, “không dây”).
- Confirm whether missing typed fields (warranty/origin/waterproof in the initial draft) should return product cards with an explicit information gap, or no product cards.

## Fixture inventory

- `RAG_AUTHOR_WRITTEN_CONSTRAINTS.jsonl`: 60 author-written scenarios; includes satisfiable, impossible, boundary, multi-condition, unit-conversion, English, accented and unaccented cases. Expected constraints describe intended parsing, not human-verified product labels.
- `RAG_AUTHOR_WRITTEN_CONTEXT.jsonl`: 30 short prior-turn/query pairs for follow-up merge, new-topic reset and missing-attribute behavior.

Neither fixture is for threshold tuning or a quality claim. Owner-reviewed evaluation remains the separate `RAG_OWNER_REVIEWED_QUERIES.jsonl` set.
