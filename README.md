# Easy Meals

**Easy Meals** is a modern meal planning platform that helps you discover, organize, and schedule recipes for the week ahead. Built with a clean, intuitive interface inspired by meal kit services, it simplifies the often-overwhelming task of weekly meal planning.

## The Problem

Planning meals for the week is time-consuming and stressful:

- Scrolling through endless recipe sites and blogs
- Trying to remember what you cooked last week
- Juggling dietary preferences and ingredient availability
- Creating grocery lists from multiple sources

## The Solution

Easy Meals streamlines meal planning with:

- **Smart Recipe Discovery**: Automated recipe collection and curation from across the web
- **Weekly Meal Planner**: Visual calendar interface to schedule meals for the week
- **Recipe Organization**: Save and categorize your favorite recipes in one place
- **Simplified Workflow**: Browse, select, schedule—all in one intuitive interface

## Architecture

Easy Meals is built as a modern, scalable monorepo:

```
easy-meals/
├── apps/
│   ├── web/          # Next.js frontend with shadcn/ui
│   ├── api/          # .NET Web API backend
│   └── recipe-engine/ # Background worker for recipe processing
└── packages/
    ├── ui/           # Shared React components
    └── shared/       # Shared .NET libraries
```

### Technology Stack

- **Frontend**: Next.js 15, React 19, TypeScript, Tailwind CSS, shadcn/ui
- **Backend**: .NET 8, ASP.NET Core Web API
- **Database**: MongoDB
- **Infrastructure**: Docker, Docker Compose, GitHub Actions, Coolify

## Getting Started

### Prerequisites

- Node.js 18+ and pnpm
- .NET 8 SDK
- Docker and Docker Compose
- MongoDB (or use Docker Compose)

### Quick Start

1. **Clone the repository**

   ```bash
   git clone https://github.com/yourusername/easy-meals.git
   cd easy-meals
   ```

2. **Install dependencies**

   ```bash
   pnpm install
   ```

3. **Start the development environment**

   ```bash
   docker-compose up
   ```

4. **Access the application**
   - Web UI: http://localhost:3000
   - API: http://localhost:8080
   - MongoDB: localhost:27017

## Project Status

🚧 **Early Development** - This project is actively being built. Core features are being implemented and the architecture is being refined.

## Development

### Monorepo Structure

This project uses pnpm workspaces for efficient dependency management across the monorepo:

- **apps/web**: Next.js application with App Router
- **apps/api**: .NET Web API for backend services
- **apps/recipe-engine**: Background worker for recipe scraping and processing
- **packages/ui**: Shared React components built with shadcn/ui
- **packages/shared**: Shared .NET libraries for data access and domain logic

### Key Commands

```bash
# Install dependencies
pnpm install

# Start web app in development
pnpm --filter web dev

# Build all apps
pnpm build

# Start services with Docker Compose
docker-compose up --build
```

## Contributing

Contributions are welcome! This project is still in early development, so there's plenty of opportunity to shape its direction.

## License

[MIT](LICENSE) - feel free to use this project for your own meal planning needs!

---

**Note**: Easy Meals is a personal project aimed at solving a real problem—meal planning fatigue. It's built with modern tools and best practices, but it's still evolving. Feedback and contributions are always appreciated!
