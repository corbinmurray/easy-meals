<!--
  SYNC IMPACT REPORT
  Version: 0.1.0 | Type: Initial Constitution (MINOR - new principles established)
  Modified Principles: None (initial creation)
  Added Principles: I. Code Quality, II. Testing Standards, III. User Experience Consistency, IV. Performance Requirements
  Added Sections: Development Standards, Deployment & Release Policy
  Removed Sections: None
  Template Updates: Cascading impact across spec-template.md, tasks-template.md, plan-template.md
  Follow-up Checklist: ✅ Pending - verify dependent templates are reviewed
-->

# Easy Meals Constitution

<!-- Governance document for the Easy Meals meal planning platform monorepo -->

## Core Principles

### I. Code Quality

All production code MUST maintain high quality standards that enable maintainability, safety, and scalability:

- Every code change MUST follow established linting, formatting, and style guidelines per language (TypeScript, C#, etc.)
- All code MUST be self-documenting; comments explain WHY, not WHAT (refer to self-explanatory-code.md)
- Security-first mindset: OWASP Top 10 principles apply to all code (refer to security-and-owasp.md)
- Code reviews MUST verify code quality; no exceptions for urgent features
- Complexity MUST be justified; YAGNI principle enforced—start simple, refactor as patterns emerge
- Dependencies MUST be minimized; each package/library must have clear, singular purpose
- Type safety is non-negotiable: TypeScript (strict mode), C# (nullable reference types enabled)

### II. Testing Standards

Quality assurance through comprehensive, well-designed test coverage is mandatory:

- Test-First discipline: Tests written BEFORE implementation; Red-Green-Refactor cycle enforced
- All user-facing features MUST have contract tests (define API/UI behavior) and integration tests
- Unit tests MUST focus on business logic and edge cases, not implementation details
- Test organization: contract/ → integration/ → unit/ (by visibility/scope)
- Tests MUST be independent; no shared state or test-order dependencies
- Test failure MUST block PR merge; all tests MUST pass in CI/CD before deployment
- Acceptance scenarios from spec MUST map 1:1 to test cases; traceability required
- Coverage goals: 80%+ for critical paths; gaps require justification

### III. User Experience Consistency

All user-facing interfaces (web, API, CLI) MUST provide intuitive, predictable, accessible experiences:

- UI components MUST adhere to shadcn/ui patterns and Tailwind CSS conventions (v4 syntax)
- All interactive elements MUST provide visual feedback (hover states, loading states, error states)
- Animations MUST use Motion for React (motion/react) with purposeful transitions (no gratuitous effects)
- Error messages MUST be user-friendly, actionable, and never expose stack traces
- Accessibility MUST meet WCAG 2.1 Level AA minimum; semantic HTML, keyboard navigation, screen reader support
- API responses MUST follow consistent schema design; versioning required for breaking changes
- Loading states MUST be explicit; skeleton loaders, spinners, or progress indicators MUST appear within 200ms
- Mobile responsiveness MUST be tested on actual devices; Tailwind responsive classes required

### IV. Performance Requirements

System MUST deliver responsive, efficient operations under realistic load:

- Frontend: Time to Interactive (TTI) MUST be <3 seconds on 4G networks; First Contentful Paint <1.5s
- Backend API: P95 response latency MUST be <500ms for typical queries; P99 < 1 second
- Database: Indexes MUST be created for all foreign keys, sort fields, and filter predicates; no full table scans
- Memory usage: Single service MUST not exceed 512MB at rest; memory leaks MUST be detected in CI/CD
- Asset optimization: Images MUST be WebP format; JavaScript bundles MUST be code-split by route
- Caching: Query results MUST be cached when appropriate (refer to infrastructure documentation)
- Monitoring: Response times, error rates, and resource usage MUST be logged and visible (refer to logging standards)
- Performance regressions MUST be caught before merge; baseline metrics required in PR description

## Development Standards

All development work MUST adhere to these project-specific standards:

### Monorepo Conventions

- Use pnpm for all package management (never npm)
- Workspace dependencies explicitly defined in package.json
- Build order: shared dependencies → packages (ui, shared) → applications (api, recipe-engine, web)
- All internal dependencies use workspace protocol: `"@easy-meals/*": "workspace:*"`

### Language & Framework Standards

- **TypeScript**: App Router (Next.js), strict mode, ESLint + Prettier, no `any` types (use `unknown` with proper narrowing)
- **C#**: .NET 8+, nullable reference types enabled, LINQ for queries, DDD patterns (Entities, Value Objects, Services)
- **Database**: MongoDB for app data; refer to EasyMeals.Shared.Data documentation for data access patterns
- **Testing**: TypeScript (Vitest or Jest), C# (xUnit); 80%+ coverage on critical paths

### Security & OWASP Compliance

- Environment variables: NO hardcoded secrets; .env files for development only
- Input validation: Parameterized queries (MongoDB), type validation (TypeScript/C#)
- Authentication: Session management with HttpOnly + Secure + SameSite cookies; rate limiting on auth endpoints
- Refer to security-and-owasp.md for complete requirements

## Deployment & Release Policy

Production deployments MUST satisfy these quality gates:

- **CI/CD**: All tests MUST pass; code coverage MUST meet thresholds; security scans MUST have no critical issues
- **Versioning**: Semantic versioning (MAJOR.MINOR.PATCH); breaking changes MUST increment MAJOR version
- **Rollback**: All deployments MUST be reversible; data migrations MUST include rollback scripts
- **Monitoring**: Error rates, response times, and resource usage MUST be visible post-deployment
- **Documentation**: Breaking changes, new features, and deprecations MUST be documented in CHANGELOG.md

## Governance

**Constitution Supersedes All Other Practices**: This document is the source of truth for development standards. Individual preferences, shortcuts, or "expedient" solutions that violate these principles MUST be challenged and justified through formal amendment.

**Amendments**:

- New principles require design document (rationale, examples, migration plan) and team consensus
- Principle modifications require changelog entry and impact assessment on existing code
- Version increments follow semantic versioning: MAJOR (breaking principles), MINOR (add principle), PATCH (clarify)
- All amendments recorded with date and rationale in version history

**Compliance Review**:

- Code reviews MUST verify compliance against applicable principles; see checklist in code-review-template.md
- PR descriptions MUST reference which principles apply to changes
- Violations MUST be resolved before merge or explicitly documented as deferred technical debt
- Performance regressions, security issues, or accessibility failures MUST be treated as blocking

**Runtime Guidance**:

- Refer to `.github/instructions/` for language/framework-specific implementation patterns
- Refer to `.github/copilot-instructions.md` for project conventions and architecture details
- Ambiguities resolved in order: Constitution → Instructions → Code Review

---

**Version**: 1.0.0 | **Ratified**: 2025-11-02 | **Last Amended**: 2025-11-02
