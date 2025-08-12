"use client";

import WeeklyMealSelector from "@/components/WeeklyMealSelector";
import { mockRecipes } from "@/mock/recipes";
import type { Recipe } from "@/types/meal-planner";
import { Button } from "@workspace/ui/components/button";
import { useTheme } from "next-themes";

import { useEffect, useState } from "react";
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
  // Placeholder state for recipes
  const [recipes, setRecipes] = useState<Recipe[]>(mockRecipes); // Use mock data for development

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
