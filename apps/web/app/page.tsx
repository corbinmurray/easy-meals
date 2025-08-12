"use client";

import RecipeGallery from "@/components/RecipeGallery";
import SelectedMealsSummary from "@/components/SelectedMealsSummary";
import WeeklyCalendar from "@/components/WeeklyCalendar";
import { mockRecipes } from "@/mock/recipes";
import type { Recipe, ScheduledMeal } from "@/types/meal-planner";
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
  // Placeholder state for selected recipes and schedule
  const [recipes, setRecipes] = useState<Recipe[]>(mockRecipes); // Use mock data for development
  const [scheduledMeals, setScheduledMeals] = useState<ScheduledMeal[]>([]); // { day, recipeId }

  return (
    <main className="min-h-screen flex flex-col items-center">
      <header className="mb-8 text-center flex flex-col items-center">
        <div className="flex items-center gap-2">
          <h1 className="text-4xl font-bold mb-2">Easy Meals Planner</h1>
          <ThemeToggle />
        </div>
      </header>

      {/* Recipe Gallery */}
      <section className="w-full max-w-5xl mb-8">
        <RecipeGallery
          recipes={recipes}
          setRecipes={setRecipes}
          scheduledMeals={scheduledMeals}
          setScheduledMeals={setScheduledMeals}
        />
      </section>

      {/* Weekly Calendar */}
      <section className="w-full max-w-3xl mb-8">
        <WeeklyCalendar
          scheduledMeals={scheduledMeals}
          setScheduledMeals={setScheduledMeals}
          recipes={recipes}
        />
      </section>

      {/* Selected Meals Summary */}
      <section className="mb-4">
        <SelectedMealsSummary
          scheduledMeals={scheduledMeals}
          recipes={recipes}
        />
      </section>
    </main>
  );
}
