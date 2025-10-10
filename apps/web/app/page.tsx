"use client";

import WeeklyMealSelector from "@/components/WeeklyMealSelector";
import type { Recipe } from "@/types/meal-planner";
import { Button } from "@workspace/ui/components/button";
import { useTheme } from "next-themes";

import { useEffect, useState } from "react";

// Dynamic import for mock data - only in development
const loadMockRecipes = async (): Promise<Recipe[]> => {
  if (process.env.NODE_ENV === "development") {
    try {
      const { mockRecipes } = await import("@/mock/recipes");
      return mockRecipes;
    } catch (error) {
      console.warn(
        "Mock recipes not found. Create apps/web/mock/recipes.ts from recipes.example.ts"
      );
      return [];
    }
  }
  return [];
};

function ThemeToggle() {
  const { theme, setTheme } = useTheme();
  const [mounted, setMounted] = useState(false);
  useEffect(() => {
    setMounted(true);
  }, []);
  if (!mounted) return null;
  return (
    <Button
      variant="outline"
      size="icon"
      aria-label="Toggle theme"
      onClick={() => setTheme(theme === "dark" ? "light" : "dark")}
      className="ml-2"
    >
      {theme === "dark" ? "🌙" : "☀️"}
    </Button>
  );
}

export default function Page() {
  const [recipes, setRecipes] = useState<Recipe[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    const initRecipes = async () => {
      // In development, try to load mock data
      if (process.env.NODE_ENV === "development") {
        const mockData = await loadMockRecipes();
        setRecipes(mockData);
        setIsLoading(false);
      } else {
        // In production, fetch from API
        try {
          const response = await fetch("/api/recipes");
          const data = await response.json();
          setRecipes(data);
        } catch (error) {
          console.error("Failed to fetch recipes:", error);
        } finally {
          setIsLoading(false);
        }
      }
    };

    initRecipes();
  }, []);

  if (isLoading) {
    return (
      <main className="min-h-screen flex items-center justify-center">
        <div className="text-center">
          <p className="text-lg text-muted-foreground">Loading recipes...</p>
        </div>
      </main>
    );
  }

  return (
    <main className="min-h-screen flex flex-col items-center px-4 py-8">
      <div className="w-full max-w-6xl">
        <header className="text-center mb-12">
          <h1 className="text-4xl font-bold mb-4">Easy Meals</h1>
          <p className="text-lg text-muted-foreground">
            Plan your weekly meals with ease
          </p>
        </header>

        {/* Weekly Meal Selector */}
        <WeeklyMealSelector recipes={recipes} />
      </div>
    </main>
  );
}
