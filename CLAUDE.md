# CLAUDE.md — MiniShop

## Subagent Orchestration (token economy)

Khi model chính = Opus: Opus = orchestrator + reviewer, Sonnet subagent = executor.
Delegate qua `Agent` tool `model: sonnet` cho việc tốn token: recon/map codebase, code cơ học (CRUD/boilerplate/test/rename), chạy build+test tóm kết quả. Việc độc lập → nhiều sub song song (1 message nhiều Agent call). Việc tuần tự/phụ thuộc hoặc sửa 1-2 dòng → tự làm. Prompt sub phải tự chứa (path, dòng, spec). LUÔN model sonnet, không haiku. Trust but verify — đọc diff thật trước khi báo xong.

## Spec & Checkpoint Workflow (BẮT BUỘC)

- **ĐẦU mỗi phiên**: đọc `task.md` (checkpoint hiện tại) để nắm trạng thái.
- **TRƯỚC khi build feature mới**: đọc spec liên quan trong `docs/tech_specs/`. Chưa có spec → dừng, bàn user viết spec trước, KHÔNG code chay.
- **CUỐI mỗi task lớn**: update `task.md` — mẻ mới, task, trạng thái, ghi chú (path file). Giữ format bảng hiện có.
- Feature phức tạp → viết spec `docs/tech_specs/<ngày>-<tên>.md` trước.
- Quyết định kiến trúc → ghi vào spec/task.md, không giữ trong đầu.

## Stack notes
- Backend: .NET 9, EF Core, SQL Server LocalDB, JWT+BCrypt, xUnit. Build: `dotnet build ECommerce.sln`, test: `dotnet test`.
- Frontend: `client/` React 19 + Vite + Tailwind + React Query + Zustand. Lint: oxlint.
- Clean Architecture: Domain (entities) → Application (services/DTO) → Infrastructure (auth/payments/persistence) → API (controllers).
