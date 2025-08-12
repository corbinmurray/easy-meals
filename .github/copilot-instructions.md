# Copilot Instructions: Easy Meals Monorepo

## Architecture Overview

- **Monorepo** managed with pnpm workspaces. Key folders:
  - `apps/web`: Next.js (App Router, TypeScript, Tailwind, shadcn, Motion for React)
  - `apps/api`: .NET 8 Web API (C#)
  - `packages/ui`: Shared UI components (React, Tailwind, shadcn)
  - `packages/typescript-config`, `packages/eslint-config`: Shared config
- **Data flow**: API (C#) provides backend, web app fetches data via API routes or direct fetch.
- **UI**: All React UI uses Tailwind, and shadcn patterns. Animations use Motion for React (`motion/react`).

## Developer Workflows

- **Install dependencies**: Use `pnpm install` (never `npm install`)
- **Add package**: Use `pnpm add <pkg>` in the correct subfolder
- **Run web app**: `pnpm dev` from `apps/web`
- **Run API**: Use `dotnet run` from `apps/api/src/EasyMeals.Api`
- **Build all**: `pnpm build` (runs builds for all packages/apps)
- **Test**: (Add test instructions here if/when tests exist)

## Project-Specific Patterns

- **Next.js App Router**: Use server components by default, client components only for interactivity/state.
- **UI components**: Co-locate feature components in `apps/web/components`, shared in `packages/ui/src/components`.
- **Styling**: Use Tailwind utility classes theme tokens, and shadcn conventions. See `globals.css` for theme.
- **lucide-react**: Use `lucide-react` for icons.
- **Motion for React**: Use `<motion.* />` components for animation. Animate with `animate`, `initial`, `whileHover`, `whileTap`, `whileInView`, `exit`, and `layout` props. Use `AnimatePresence` for exit animations. Example:
  ```tsx
  import { motion } from "motion/react";
  <motion.div
    initial={{ opacity: 0 }}
    animate={{ opacity: 1 }}
    exit={{ opacity: 0 }}
  />;
  ```
- **Grid layouts**: Use CSS grid with `auto-fit`/`minmax` for responsive card galleries (see `RecipeGallery.tsx`).
- **Icons**: Use `lucide-react` for icons.

## Integration & Cross-Component Patterns

- **Shared types**: Place in `apps/web/types` or `packages/ui` as needed.
- **API integration**: Web app fetches from API project or Next.js API routes.
- **Motion**: For advanced animation, refer to https://motion.dev/docs/react for patterns like gestures, scroll, layout, and exit animations.

## Examples

- **RecipeCard**: Animated card using shadcn, DaisyUI theme, and Motion for React.
- **RecipeGallery**: Responsive grid using `grid-cols-[repeat(auto-fit,minmax(340px,1fr))]`.

## Conventions

- Use pnpm for all JS/TS package management
- Use Tailwind, DaisyUI, and shadcn for all styling
- Use Motion for React for all animation
- Use C# for backend API

---

For expert-level Motion for React usage, see https://motion.dev/docs/react and use props like `animate`, `initial`, `whileHover`, `whileTap`, `whileInView`, `exit`, `layout`, and `transition` for all animation needs.
