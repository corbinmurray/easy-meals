"use client";

import WeeklyMealSelector from "@/components/WeeklyMealSelector";
import type { Recipe } from "@/types/meal-planner";

import { useEffect, useState } from "react";

export default function Page() {
  const [recipes, setRecipes] = useState<Recipe[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchRecipes = async () => {
      try {
        const response = await fetch("/api/recipes");
        if (!response.ok) {
          throw new Error(`Failed to fetch recipes: ${response.statusText}`);
        }
        const data = await response.json();
        setRecipes(data);
      } catch (error) {
        console.error("Failed to fetch recipes:", error);
        setError(
          error instanceof Error ? error.message : "Failed to load recipes"
        );
      } finally {
        setIsLoading(false);
      }
    };

    fetchRecipes();
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

  if (error) {
    return (
      <main className="min-h-screen flex items-center justify-center">
        <div className="text-center">
          <p className="text-lg text-destructive mb-2">Error loading recipes</p>
          <p className="text-sm text-muted-foreground">{error}</p>
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
