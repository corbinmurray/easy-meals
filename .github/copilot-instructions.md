# Copilot Instructions: Easy Meals Monorepo

## Architecture at a glance

- Monorepo with TypeScript/Next frontend (`apps/web`), .NET Web API (`apps/api`), and .NET recipe-engine worker (`apps/recipe-engine`).
- Shared UI and configuration packages live in `packages/` (e.g., `packages/ui`, `packages/typescript-config`).
- The recipe-engine follows Clean Architecture / DDD and stores data in MongoDB via shared persistence packages.

## Quick commands (use these)
- pnpm install                        # install workspace deps from repo root
- pnpm --filter web dev               # run frontend dev server
- pnpm --filter web typecheck         # TS type checks
- pnpm --filter web lint              # lint frontend code
- dotnet run --project apps/api/src/EasyMeals.Api       # run API locally
- dotnet run --project apps/recipe-engine/src/EasyMeals.RecipeEngine # run recipe worker
- docker-compose up --build           # run full stack (for local integration + Mongo)

## What you should read first (order matters)
1. README.md — repo quickstart and tech stack
2. apps/recipe-engine/README.md — design and provider model
3. .github/workflows/deploy.yml — CI detection, build and deploy flow
4. Directory.Packages.props — .NET package/version centralization
5. .specify/memory/constitution.md — project governance and TDD/test-first requirements

## CI / delivery details (important)
- CI (deploy.yml) detects which apps changed (api, web, recipe-engine), builds Docker images per-app and pushes to GHCR.
- Deploy steps call a Coolify webhook — merges to main will typically trigger a production redeploy.
- Dockerfile locations you will edit: apps/api/src/EasyMeals.Api/Dockerfile, apps/web/Dockerfile, apps/recipe-engine/src/EasyMeals.RecipeEngine/Dockerfile

## Testing and quality gates (concrete)
- This repo requires tests as part of feature work: unit tests + integration/contract tests for cross-service pieces.
- Add frontend tests near components (`apps/web/components/__tests__` or `packages/ui` for shared components).
- For .NET: add test projects alongside affected projects and use `dotnet test` at solution-level (e.g., apps/recipe-engine/EasyMeals.RecipeEngine.sln).
- The repo includes Testcontainers references (Testcontainers.MongoDb) for integration testing against real MongoDB instances in CI.

## Project conventions and gotchas (examples)
- Next.js App Router; prefer server components unless interactivity/state requires a client component.
- Styling: Tailwind v4 + shadcn conventions.
- Animations: use motion/react (`motion`) and AnimatePresence patterns.
- Recipe-engine: provider plugin model — inspect ServiceCollectionExtensions.cs in the Infrastructure project to see how providers are registered.
- DO NOT run `npm install` at root — use pnpm. DO NOT hard-code secrets.

## AI-Agent tips (do / don’t)
- Do: Read the plan/spec/templates before implementing. Tests should be added first and must fail before the implementation (TDD).
- Do: Keep changes small and scoped to a feature branch, add migrations/plans if changing persistent schemas or public APIs.
- Don’t: Make large cross-cutting infra changes without updating CI and the deployment plan.

If you want an exact CI job snippet, a working test command, or the path of a specific Dockerfile for the app you're editing, ask for the path and I will extract the exact snippet.
