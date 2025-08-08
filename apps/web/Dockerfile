FROM node:20-alpine AS base

# Enable pnpm via Corepack and add common alpine dependency
ENV PNPM_HOME="/pnpm"
ENV PATH="$PNPM_HOME:$PATH"
RUN corepack enable pnpm

FROM base AS builder
RUN apk add --no-cache libc6-compat
# Set working directory
WORKDIR /app

COPY . .

# Use pnpm to run turbo without installing globally
# Generate a partial monorepo with a pruned lockfile for the target workspace
# Assuming "web" is the name entered in the project's package.json: { name: "web" }
RUN pnpm dlx turbo@^2 prune web --docker

# Add lockfile and package.json's of isolated subworkspace
FROM base AS installer
RUN apk add --no-cache libc6-compat
WORKDIR /app

# First install the dependencies (as they change less often)
COPY --from=builder /app/out/json/ .
# Copy the pruned lockfile for pnpm
COPY --from=builder /app/out/pnpm-lock.yaml ./pnpm-lock.yaml
# Install with a frozen lockfile to ensure reproducible builds
RUN pnpm install --frozen-lockfile

# Build the project
COPY --from=builder /app/out/full/ .
# Use the repo's build script (runs `turbo build` in pruned workspace)
RUN pnpm run build

FROM base AS runner
WORKDIR /app

# Don't run production as root
RUN addgroup --system --gid 1001 nodejs \
	&& adduser --system --uid 1001 nextjs
USER nextjs

# Automatically leverage output traces to reduce image size
# https://nextjs.org/docs/advanced-features/output-file-tracing
COPY --from=installer --chown=nextjs:nodejs /app/apps/web/.next/standalone ./
COPY --from=installer --chown=nextjs:nodejs /app/apps/web/.next/static ./apps/web/.next/static
# COPY --from=installer --chown=nextjs:nodejs /app/apps/web/public ./apps/web/public

ENV NODE_ENV=production
ENV PORT=3000
EXPOSE 3000

CMD node apps/web/server.js